namespace GovernedFailureInjectionTrace;

public sealed record PolicyDecision(
    PolicyOutcome Outcome,
    string ReasonCode);

public sealed record VerificationResult(
    StageStatus Status,
    TraceOutcome? StopOutcome,
    string ReasonCode);

/// <summary>
/// Loads the facts policy depends on. The caller supplies identifiers, never these facts.
/// </summary>
public sealed class AuthoritativeContextStore(AuthoritativeContext context, bool available)
{
    public AuthoritativeContext? Load(ExportIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);

        return available ? context : null;
    }
}

/// <summary>
/// A pure function of the loaded context. It returns a decision and never acts on it.
/// </summary>
public static class PolicyEvaluator
{
    public static PolicyDecision Evaluate(AuthoritativeContext? context)
    {
        if (context is null)
        {
            // Inability to decide is an explicit outcome, not an exception and not a denial.
            return new(PolicyOutcome.Deferred, "context.unavailable");
        }

        if (context.Risk == RiskLevel.High)
        {
            return new(PolicyOutcome.Denied, "export.risk-high");
        }

        if (string.Equals(context.Region, "EU", StringComparison.Ordinal))
        {
            return new(PolicyOutcome.AcknowledgmentRequired, "export.region-eu-acknowledgment");
        }

        return new(PolicyOutcome.Allowed, "export.allowed");
    }
}

/// <summary>
/// Verifies the decision evidence presented with a continuation before any authority is
/// issued: integrity, binding to the current intent, freshness, and any acknowledgment.
/// </summary>
public static class ContinuationVerifier
{
    public static VerificationResult Verify(
        DecisionReceipt presented,
        ExportIntent intent,
        AcknowledgmentRecord? acknowledgment,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(presented);
        ArgumentNullException.ThrowIfNull(intent);

        if (!SimulatedReceiptSeal.Matches(presented))
        {
            return Failed("receipt.seal-mismatch");
        }

        if (!string.Equals(presented.IntentId, intent.IntentId, StringComparison.Ordinal) ||
            !string.Equals(presented.ResourceId, intent.ResourceId, StringComparison.Ordinal) ||
            !string.Equals(presented.Operation, intent.Operation, StringComparison.Ordinal))
        {
            return Failed("receipt.intent-mismatch");
        }

        if (nowUtc >= presented.ExpiresAtUtc)
        {
            return Failed("receipt.expired");
        }

        switch (presented.Outcome)
        {
            case PolicyOutcome.Allowed:
                return new(StageStatus.Completed, null, "continuation.not-required");

            case PolicyOutcome.AcknowledgmentRequired:
                bool acknowledged =
                    acknowledgment is not null &&
                    string.Equals(acknowledgment.ReceiptId, presented.ReceiptId, StringComparison.Ordinal) &&
                    string.Equals(acknowledgment.ActorId, intent.ActorId, StringComparison.Ordinal);

                return acknowledged
                    ? new(StageStatus.Completed, null, "continuation.acknowledged")
                    : new(
                        StageStatus.AwaitingContinuation,
                        TraceOutcome.ContinuationRequired,
                        "continuation.acknowledgment-missing");

            case PolicyOutcome.Denied:
            case PolicyOutcome.Deferred:
            default:
                return Failed("receipt.outcome-not-executable");
        }
    }

    private static VerificationResult Failed(string reasonCode)
    {
        return new(StageStatus.Refused, TraceOutcome.VerificationFailed, reasonCode);
    }
}

/// <summary>
/// SIMULATED trust anchor. It stands in for validating an issuer's signature on the
/// authority; here the host simply asks whether the issuer recorded the identifier.
/// </summary>
public sealed class IssuedAuthorityRegistry
{
    private readonly HashSet<string> _issued = new(StringComparer.Ordinal);

    public void Register(string authorityId)
    {
        _issued.Add(authorityId);
    }

    public bool WasIssued(string authorityId)
    {
        return _issued.Contains(authorityId);
    }
}

/// <summary>
/// Single-process bounded-use state. A production store must make the consume step
/// atomic and durable across every host instance.
/// </summary>
public sealed class AuthorityUseStore
{
    private readonly HashSet<string> _consumed = new(StringComparer.Ordinal);

    public bool TryConsume(string authorityId)
    {
        return _consumed.Add(authorityId);
    }
}

public sealed class AuthorityIssuer(IssuedAuthorityRegistry registry)
{
    public ScopedAuthority Issue(
        DecisionReceipt verifiedReceipt,
        string audience,
        string operation,
        DateTimeOffset expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(verifiedReceipt);

        ScopedAuthority authority = new(
            AuthorityId: $"auth-{verifiedReceipt.ReceiptId}",
            ReceiptId: verifiedReceipt.ReceiptId,
            Audience: audience,
            Operation: operation,
            ResourceId: verifiedReceipt.ResourceId,
            ExpiresAtUtc: expiresAtUtc);

        registry.Register(authority.AuthorityId);
        return authority;
    }
}

public interface IProtectedExecutor
{
    ExecutionEvidence Execute(ScopedAuthority authority, DateTimeOffset nowUtc);
}

/// <summary>
/// Stands in for the real side effect. It performs nothing and counts its invocations.
/// </summary>
public sealed class CountingExecutor : IProtectedExecutor
{
    public int Invocations { get; private set; }

    public ExecutionEvidence Execute(ScopedAuthority authority, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(authority);

        Invocations++;

        return new(
            ExecutionId: $"exec-{authority.AuthorityId}",
            AuthorityId: authority.AuthorityId,
            ReceiptId: authority.ReceiptId,
            ExecutedAtUtc: nowUtc);
    }
}

/// <summary>
/// The host-owned boundary. It is the only caller of the executor, and it validates
/// whatever authority it is handed, however the caller arrived here.
/// </summary>
public sealed class ProtectedHost(
    IssuedAuthorityRegistry registry,
    AuthorityUseStore useStore,
    IProtectedExecutor executor)
{
    public HostEnforcementResult Execute(
        ScopedAuthority? authority,
        ExportIntent intent,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(intent);

        string? refusal = authority switch
        {
            null => "authority.missing",
            _ when !registry.WasIssued(authority.AuthorityId) => "authority.not-issued",
            _ when !string.Equals(authority.Audience, ExportContract.HostAudience, StringComparison.Ordinal) =>
                "authority.audience-mismatch",
            _ when !string.Equals(authority.Operation, intent.Operation, StringComparison.Ordinal) =>
                "authority.operation-mismatch",
            _ when !string.Equals(authority.ResourceId, intent.ResourceId, StringComparison.Ordinal) =>
                "authority.resource-mismatch",
            _ when nowUtc >= authority.ExpiresAtUtc => "authority.expired",
            _ => null
        };

        if (refusal is not null)
        {
            return new(false, refusal, null);
        }

        // Consume only after every other check passes, so a refused attempt does not
        // burn authority that a valid attempt could still use.
        if (!useStore.TryConsume(authority!.AuthorityId))
        {
            return new(false, "authority.already-used", null);
        }

        ExecutionEvidence evidence = executor.Execute(authority, nowUtc);
        return new(true, "execution.completed", evidence);
    }
}
