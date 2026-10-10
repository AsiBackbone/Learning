namespace GovernedFailureInjectionTrace;

/// <summary>
/// The trust boundaries a proposed operation must pass, in order.
/// </summary>
public enum TraceStage
{
    Intent,
    AuthoritativeContext,
    PolicyDecision,
    ContinuationVerification,
    AuthorityIssuance,
    HostEnforcement,
    ProtectedExecutor
}

public enum StageStatus
{
    Completed,
    Refused,
    Deferred,
    Unavailable,
    AwaitingContinuation,
    NotReached,
    SkippedByCaller
}

/// <summary>
/// What the policy concluded. These are decision facts, not execution results.
/// </summary>
public enum PolicyOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired
}

/// <summary>
/// How one run ended. Each non-executed value names a different kind of refusal,
/// so a policy denial is never reported as a verification or enforcement failure.
/// </summary>
public enum TraceOutcome
{
    Executed,
    Denied,
    Deferred,
    ContinuationRequired,
    VerificationFailed,
    EnforcementRefused
}

public enum RiskLevel
{
    Low,
    High
}

/// <summary>
/// One failure injected into an otherwise ordinary run. <see cref="None"/> runs the
/// unmodified path.
/// </summary>
public enum FailureInjection
{
    None,
    ContextUnavailable,
    OmitAcknowledgment,
    TamperReceipt,
    ExpireReceipt,
    ReceiptForDifferentIntent,
    ExpireAuthority,
    ReplayAuthority,
    WrongAudience,
    WrongOperation,
    BypassEntryPoint
}

public static class Components
{
    public const string EntryPoint = "GovernedEntryPoint";
    public const string ContextStore = "AuthoritativeContextStore";
    public const string PolicyEvaluator = "PolicyEvaluator";
    public const string ContinuationVerifier = "ContinuationVerifier";
    public const string AuthorityIssuer = "AuthorityIssuer";
    public const string ProtectedHost = "ProtectedHost";
    public const string ProtectedExecutor = "ProtectedExecutor";

    public static string For(TraceStage stage)
    {
        return stage switch
        {
            TraceStage.Intent => EntryPoint,
            TraceStage.AuthoritativeContext => ContextStore,
            TraceStage.PolicyDecision => PolicyEvaluator,
            TraceStage.ContinuationVerification => ContinuationVerifier,
            TraceStage.AuthorityIssuance => AuthorityIssuer,
            TraceStage.HostEnforcement => ProtectedHost,
            TraceStage.ProtectedExecutor => ProtectedExecutor,
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown stage.")
        };
    }
}

public static class ExportContract
{
    public const string Operation = "customer.export";
    public const string HostAudience = "customer-export-host";
    public const string PolicyId = "customer-export";
    public const string PolicyVersion = "1.0";
}

public sealed record ExportIntent(
    string IntentId,
    string ActorId,
    string ResourceId,
    string Operation);

public sealed record AuthoritativeContext(
    string TenantId,
    string Region,
    RiskLevel Risk);

/// <summary>
/// Decision evidence: what the policy concluded about one intent, sealed so later
/// stages can detect that it was altered.
/// </summary>
public sealed record DecisionReceipt(
    string ReceiptId,
    string IntentId,
    string ActorId,
    string ResourceId,
    string Operation,
    PolicyOutcome Outcome,
    string ReasonCode,
    string PolicyId,
    string PolicyVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    string Seal);

public sealed record AcknowledgmentRecord(
    string ReceiptId,
    string ActorId,
    DateTimeOffset AcknowledgedAtUtc);

/// <summary>
/// Narrow execution authority for one operation on one resource at one host.
/// </summary>
public sealed record ScopedAuthority(
    string AuthorityId,
    string ReceiptId,
    string Audience,
    string Operation,
    string ResourceId,
    DateTimeOffset ExpiresAtUtc);

public sealed record StageRecord(
    TraceStage Stage,
    string Component,
    StageStatus Status,
    string? ReasonCode);

public sealed record DecisionEvidence(
    string ReceiptId,
    PolicyOutcome Outcome,
    string ReasonCode,
    string PolicyId,
    string PolicyVersion);

/// <summary>
/// Execution evidence: what the host actually did. It exists only when the protected
/// executor ran, and it is recorded separately from the decision that preceded it.
/// </summary>
public sealed record ExecutionEvidence(
    string ExecutionId,
    string AuthorityId,
    string ReceiptId,
    DateTimeOffset ExecutedAtUtc);

public sealed record TraceResult(
    string ScenarioId,
    FailureInjection Injection,
    TraceOutcome Outcome,
    string ReasonCode,
    TraceStage? FirstStoppingStage,
    string? StoppingComponent,
    bool AuthorityIssued,
    int ExecutorInvocations,
    IReadOnlyList<StageRecord> Stages,
    DecisionEvidence? Decision,
    ExecutionEvidence? Execution);

public sealed record HostEnforcementResult(
    bool Executed,
    string ReasonCode,
    ExecutionEvidence? Execution);
