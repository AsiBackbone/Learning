DateTimeOffset nowUtc =
    new(2026, 8, 14, 15, 0, 0, TimeSpan.Zero);

SampleHost host = SampleComposition.Create();

Console.WriteLine("Governed AI Tool Gateway");
Console.WriteLine(new string('=', 25));
Console.WriteLine();
Console.WriteLine("The model may propose. The host retains execution authority.");
Console.WriteLine();

AiToolProposal unknownTool = new(
    ProposalId: "proposal-unknown-tool",
    ModelId: "simulated-model-v1",
    ToolName: "finance.transfer_unlimited",
    Arguments: new Dictionary<string, string>(),
    ModelRationale: "A simulated model proposed an unregistered tool.");

GatewayResult unknownToolResult = await host.Gateway.ExecuteAsync(
    unknownTool,
    new HostActor("operator-7", "tenant-a"),
    nowUtc,
    acknowledgmentResponse: null,
    CancellationToken.None);

PrintResult("Unknown tool", unknownToolResult, host.Handler.InvocationCount);

host.Handler.Reset();

AiToolProposal externalProposal = new(
    ProposalId: "proposal-external-notification",
    ModelId: "simulated-model-v1",
    ToolName: "notification.send",
    Arguments: new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["recipient"] = "partner@example.net",
        ["template"] = "case-update",
        ["classification"] = "internal"
    },
    ModelRationale:
        "The model claims the destination is internal, but the host will rebuild that fact.");

GatewayResult awaitingAcknowledgment = await host.Gateway.ExecuteAsync(
    externalProposal,
    new HostActor("operator-7", "tenant-a"),
    nowUtc,
    acknowledgmentResponse: null,
    CancellationToken.None);

PrintResult(
    "Model-supplied classification ignored",
    awaitingAcknowledgment,
    host.Handler.InvocationCount);

if (awaitingAcknowledgment.AcknowledgmentChallenge is null)
{
    throw new InvalidOperationException(
        "The external destination should require host-owned acknowledgment.");
}

host.Handler.Reset();

AcknowledgmentResponse acceptedAcknowledgment = new(
    ChallengeId: awaitingAcknowledgment.AcknowledgmentChallenge.ChallengeId,
    ActorId: "operator-7",
    Accepted: true,
    RespondedUtc: nowUtc.AddSeconds(10));

GatewayResult acknowledgedResult = await host.Gateway.ExecuteAsync(
    externalProposal,
    new HostActor("operator-7", "tenant-a"),
    nowUtc.AddSeconds(10),
    acceptedAcknowledgment,
    CancellationToken.None);

PrintResult(
    "External destination after valid acknowledgment",
    acknowledgedResult,
    host.Handler.InvocationCount);

host.Handler.Reset();

AiToolProposal internalProposal = new(
    ProposalId: "proposal-internal-notification",
    ModelId: "simulated-model-v1",
    ToolName: "notification.send",
    Arguments: new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["recipient"] = "employee@example.internal",
        ["template"] = "case-update"
    },
    ModelRationale: "A simulated model proposed a narrow semantic operation.");

GatewayResult internalResult = await host.Gateway.ExecuteAsync(
    internalProposal,
    new HostActor("operator-7", "tenant-a"),
    nowUtc.AddMinutes(1),
    acknowledgmentResponse: null,
    CancellationToken.None);

PrintResult(
    "Internal notification",
    internalResult,
    host.Handler.InvocationCount);

Console.WriteLine("Observed audit stages for the acknowledged proposal:");

foreach (DecisionReceipt receipt in host.AuditSink.Entries.Where(
             entry => string.Equals(
                 entry.CorrelationId,
                 externalProposal.ProposalId,
                 StringComparison.Ordinal)))
{
    Console.WriteLine(
        $"- {receipt.Stage}: {receipt.Outcome} ({receipt.ReasonCode})");
}

Console.WriteLine();
Console.WriteLine("Architectural invariants demonstrated:");
Console.WriteLine("- Unknown model-proposed tools are rejected before execution.");
Console.WriteLine("- Model-supplied classification is not authoritative host context.");
Console.WriteLine("- External destinations pause for host-owned acknowledgment.");
Console.WriteLine("- A valid acknowledgment is re-evaluated rather than treated as an override.");
Console.WriteLine("- Execution authority is represented by a short-lived, single-use capability.");
Console.WriteLine("- Capability validation occurs immediately before the dry-run tool handler.");
Console.WriteLine("- The model never receives infrastructure credentials or invokes the handler directly.");
Console.WriteLine("- The sample performs no real external side effect; it only reports WouldExecute.");

await GovernanceObservabilityDemo.RunAsync();

static void PrintResult(
    string scenario,
    GatewayResult result,
    int handlerInvocations)
{
    Console.WriteLine($"Scenario: {scenario}");
    Console.WriteLine($"Status: {result.Status}");
    Console.WriteLine($"Reason: {result.ReasonCode}");
    Console.WriteLine($"WouldExecute: {result.WouldExecute}");
    Console.WriteLine($"Handler invocations: {handlerInvocations}");

    if (result.AcknowledgmentChallenge is not null)
    {
        Console.WriteLine(
            $"Acknowledgment challenge: {result.AcknowledgmentChallenge.ChallengeId}");
    }

    Console.WriteLine();
}

public static class SampleComposition
{
    public static SampleHost Create()
    {
        ToolDescriptor notificationTool = new(
            Name: "notification.send",
            RequiredArguments: new HashSet<string>(
                ["recipient", "template"],
                StringComparer.Ordinal),
            AllowedArguments: new HashSet<string>(
                ["recipient", "template", "classification"],
                StringComparer.Ordinal),
            GovernanceOperation: "notification.send",
            RequiredScope: "notification.send",
            Audience: "notification-gateway");

        var toolRegistry = new ToolRegistry([notificationTool]);
        var contextFactory = new HostPolicyContextFactory();
        var policy = new NotificationPolicy();
        var acknowledgmentService = new AcknowledgmentService();
        var acknowledgmentChallengeStore =
            new InMemoryAcknowledgmentChallengeStore();
        var capabilityIssuer = new ExecutionCapabilityIssuer();
        var capabilityValidator = new ExecutionCapabilityValidator();
        var useStore = new InMemoryCapabilityUseStore();
        var handler = new RecordingNotificationHandler(
            credentialReference: "host-owned-notification-credential");
        var auditSink = new InMemoryAuditSink();

        var gateway = new GovernedAiToolGateway(
            toolRegistry,
            acknowledgmentChallengeStore,
            useStore,
            handler,
            auditSink);

        return new SampleHost(
            Gateway: gateway,
            ToolRegistry: toolRegistry,
            ContextFactory: contextFactory,
            Policy: policy,
            AcknowledgmentService: acknowledgmentService,
            AcknowledgmentChallengeStore: acknowledgmentChallengeStore,
            CapabilityIssuer: capabilityIssuer,
            CapabilityValidator: capabilityValidator,
            CapabilityUseStore: useStore,
            Handler: handler,
            AuditSink: auditSink);
    }
}

public sealed record SampleHost(
    GovernedAiToolGateway Gateway,
    ToolRegistry ToolRegistry,
    HostPolicyContextFactory ContextFactory,
    NotificationPolicy Policy,
    AcknowledgmentService AcknowledgmentService,
    InMemoryAcknowledgmentChallengeStore AcknowledgmentChallengeStore,
    ExecutionCapabilityIssuer CapabilityIssuer,
    ExecutionCapabilityValidator CapabilityValidator,
    InMemoryCapabilityUseStore CapabilityUseStore,
    RecordingNotificationHandler Handler,
    InMemoryAuditSink AuditSink);

public sealed record HostActor(
    string ActorId,
    string TenantId);

public sealed record AiToolProposal(
    string ProposalId,
    string ModelId,
    string ToolName,
    IReadOnlyDictionary<string, string> Arguments,
    string? ModelRationale);

public sealed record ToolDescriptor(
    string Name,
    IReadOnlySet<string> RequiredArguments,
    IReadOnlySet<string> AllowedArguments,
    string GovernanceOperation,
    string RequiredScope,
    string Audience);

public sealed class ToolRegistry(IEnumerable<ToolDescriptor> tools)
{
    private readonly IReadOnlyDictionary<string, ToolDescriptor> _tools = tools.ToDictionary(
            tool => tool.Name,
            StringComparer.Ordinal);

    public ToolDescriptor? Find(string toolName)
    {
        return _tools.TryGetValue(toolName, out ToolDescriptor? descriptor)
            ? descriptor
            : null;
    }
}

public sealed record ProposalValidationResult(
    bool IsValid,
    string ReasonCode,
    IReadOnlyList<string> Errors)
{
    public static ProposalValidationResult Valid()
    {
        return new(true, "proposal.valid", []);
    }

    public static ProposalValidationResult Invalid(
        string reasonCode,
        IReadOnlyList<string> errors)
    {
        return new(false, reasonCode, errors);
    }
}

public sealed class ProposalValidator
{
    public static ProposalValidationResult Validate(
        AiToolProposal proposal,
        ToolDescriptor descriptor)
    {
        List<string> errors = [];

        foreach (string requiredArgument in descriptor.RequiredArguments)
        {
            if (!proposal.Arguments.TryGetValue(
                    requiredArgument,
                    out string? value) ||
                string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Missing required argument: {requiredArgument}");
            }
        }

        foreach (string argumentName in proposal.Arguments.Keys)
        {
            if (!descriptor.AllowedArguments.Contains(argumentName))
            {
                errors.Add($"Unexpected argument: {argumentName}");
            }
        }

        return errors.Count == 0
            ? ProposalValidationResult.Valid()
            : ProposalValidationResult.Invalid(
                "proposal.arguments-invalid",
                errors);
    }
}

public enum DestinationClassification
{
    Unknown,
    Internal,
    External
}

public sealed class RecipientDirectory
{
    public static DestinationClassification Classify(string recipient)
    {
        return recipient.EndsWith(
                "@example.internal",
                StringComparison.OrdinalIgnoreCase)
            ? DestinationClassification.Internal
            : recipient.EndsWith(
                "@example.net",
                StringComparison.OrdinalIgnoreCase) ||
            recipient.EndsWith(
                "@blocked.example",
                StringComparison.OrdinalIgnoreCase)
            ? DestinationClassification.External
            : DestinationClassification.Unknown;
    }
}

public sealed record AiToolPolicyContext(
    AiToolProposal Proposal,
    string ActorId,
    string TenantId,
    string OperationName,
    string Recipient,
    string Template,
    DestinationClassification DestinationClassification,
    string CorrelationId,
    string PolicyVersion,
    string? SatisfiedAcknowledgmentId);

public sealed class HostPolicyContextFactory
{
    public static AiToolPolicyContext Create(
        AiToolProposal proposal,
        ToolDescriptor descriptor,
        HostActor actor,
        string? hostCorrelationId = null)
    {
        string recipient = proposal.Arguments["recipient"];
        string template = proposal.Arguments["template"];

        DestinationClassification classification =
            RecipientDirectory.Classify(recipient);

        return new AiToolPolicyContext(
            Proposal: proposal,
            ActorId: actor.ActorId,
            TenantId: actor.TenantId,
            OperationName: descriptor.GovernanceOperation,
            Recipient: recipient,
            Template: template,
            DestinationClassification: classification,
            CorrelationId: hostCorrelationId ?? proposal.ProposalId,
            PolicyVersion: "5.0",
            SatisfiedAcknowledgmentId: null);
    }
}

public enum GovernanceDecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired
}

public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    string ReasonCode,
    string Reason)
{
    public bool CanProceed => Outcome == GovernanceDecisionOutcome.Allowed;

    public static GovernanceDecision Allow()
    {
        return new(
            GovernanceDecisionOutcome.Allowed,
            "notification.allowed",
            "Current host policy allows the notification.");
    }

    public static GovernanceDecision Deny(
        string reasonCode,
        string reason)
    {
        return new(GovernanceDecisionOutcome.Denied, reasonCode, reason);
    }

    public static GovernanceDecision Defer(
        string reasonCode,
        string reason)
    {
        return new(GovernanceDecisionOutcome.Deferred, reasonCode, reason);
    }

    public static GovernanceDecision RequireAcknowledgment(
        string reasonCode,
        string reason)
    {
        return new(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            reasonCode,
            reason);
    }
}

public sealed class NotificationPolicy
{
    public static GovernanceDecision Evaluate(AiToolPolicyContext context)
    {
        if (context.Recipient.EndsWith(
                "@blocked.example",
                StringComparison.OrdinalIgnoreCase))
        {
            return GovernanceDecision.Deny(
                "notification.destination-blocked",
                "The host blocks this destination domain.");
        }

        return context.DestinationClassification ==
            DestinationClassification.Unknown
            ? GovernanceDecision.Defer(
                "notification.destination-unknown",
                "The host cannot classify the destination.")
            : context.DestinationClassification ==
                DestinationClassification.External &&
            string.IsNullOrWhiteSpace(
                context.SatisfiedAcknowledgmentId)
            ? GovernanceDecision.RequireAcknowledgment(
                "notification.external-acknowledgment-required",
                "External notifications require acknowledgment.")
            : GovernanceDecision.Allow();
    }
}

public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string ActorId,
    string TenantId,
    string CorrelationId,
    string OperationName,
    string Recipient,
    string ReasonCode,
    string Text,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc);

public sealed record AcknowledgmentResponse(
    string ChallengeId,
    string ActorId,
    bool Accepted,
    DateTimeOffset RespondedUtc);

public enum AcknowledgmentChallengeLookupStatus
{
    Unknown,
    Issued,
    Consumed,
    Expired
}

public sealed record AcknowledgmentChallengeLookup(
    AcknowledgmentChallengeLookupStatus Status,
    AcknowledgmentChallenge? Challenge);

public sealed class InMemoryAcknowledgmentChallengeStore
{
    private static readonly TimeSpan _terminalStateRetention =
        TimeSpan.FromMinutes(5);
    private readonly Lock _sync = new();
    private readonly Dictionary<string, AcknowledgmentChallenge> _issued =
        new(StringComparer.Ordinal);
    private readonly Dictionary<string, TerminalAcknowledgmentChallenge> _terminal =
        new(StringComparer.Ordinal);

    public void Issue(
        AcknowledgmentChallenge challenge,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            Prune(nowUtc);

            if (_terminal.ContainsKey(challenge.ChallengeId) ||
                !_issued.TryAdd(challenge.ChallengeId, challenge))
            {
                throw new InvalidOperationException(
                    "The acknowledgment challenge identifier was already issued.");
            }
        }
    }

    public AcknowledgmentChallengeLookup Find(
        string challengeId,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            Prune(nowUtc);

            if (_issued.TryGetValue(
                    challengeId,
                    out AcknowledgmentChallenge? challenge))
            {
                return new(
                    AcknowledgmentChallengeLookupStatus.Issued,
                    challenge);
            }

            return _terminal.TryGetValue(
                    challengeId,
                    out TerminalAcknowledgmentChallenge? terminal)
                ? new(terminal.Status, null)
                : new(AcknowledgmentChallengeLookupStatus.Unknown, null);
        }
    }

    public bool TryConsume(
        string challengeId,
        DateTimeOffset nowUtc)
    {
        lock (_sync)
        {
            Prune(nowUtc);

            if (!_issued.Remove(challengeId))
            {
                return false;
            }

            _terminal[challengeId] = new(
                AcknowledgmentChallengeLookupStatus.Consumed,
                nowUtc.Add(_terminalStateRetention));
            return true;
        }
    }

    private void Prune(DateTimeOffset nowUtc)
    {
        KeyValuePair<string, AcknowledgmentChallenge>[] expiredChallenges =
        [
            .. _issued
                .Where(pair => nowUtc >= pair.Value.ExpiresUtc)
        ];

        foreach ((string challengeId, AcknowledgmentChallenge challenge) in
            expiredChallenges)
        {
            _issued.Remove(challengeId);
            _terminal[challengeId] = new(
                AcknowledgmentChallengeLookupStatus.Expired,
                challenge.ExpiresUtc.Add(_terminalStateRetention));
        }

        string[] removableTerminalIds =
        [
            .. _terminal
                .Where(pair => nowUtc >= pair.Value.RemoveAfterUtc)
                .Select(pair => pair.Key)
        ];

        foreach (string challengeId in removableTerminalIds)
        {
            _terminal.Remove(challengeId);
        }
    }

    private sealed record TerminalAcknowledgmentChallenge(
        AcknowledgmentChallengeLookupStatus Status,
        DateTimeOffset RemoveAfterUtc);
}

public sealed record AcknowledgmentValidationResult(
    bool Accepted,
    string ReasonCode,
    string? AcknowledgmentId)
{
    public static AcknowledgmentValidationResult Success(
        string acknowledgmentId)
    {
        return new(true, "acknowledgment.accepted", acknowledgmentId);
    }

    public static AcknowledgmentValidationResult Failure(
        string reasonCode)
    {
        return new(false, reasonCode, null);
    }
}

public sealed class AcknowledgmentService
{
    public static AcknowledgmentChallenge CreateChallenge(
        AiToolPolicyContext context,
        GovernanceDecision decision,
        DateTimeOffset nowUtc)
    {
        return decision.Outcome !=
            GovernanceDecisionOutcome.AcknowledgmentRequired
            ? throw new InvalidOperationException(
                "Acknowledgment challenges may only satisfy an acknowledgment-required decision.")
            : new AcknowledgmentChallenge(
            ChallengeId: Convert.ToHexString(
                    System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .ToLowerInvariant(),
            ActorId: context.ActorId,
            TenantId: context.TenantId,
            CorrelationId: context.CorrelationId,
            OperationName: context.OperationName,
            Recipient: context.Recipient,
            ReasonCode: decision.ReasonCode,
            Text:
                $"I acknowledge that notification.send will target external recipient {context.Recipient}.",
            IssuedUtc: nowUtc,
            ExpiresUtc: nowUtc.AddMinutes(5));
    }

    public static AcknowledgmentValidationResult Validate(
        AcknowledgmentChallenge challenge,
        AcknowledgmentResponse response,
        AiToolPolicyContext context,
        DateTimeOffset nowUtc)
    {
        if (!string.Equals(
                challenge.ChallengeId,
                response.ChallengeId,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.challenge-mismatch");
        }

        if (!string.Equals(
                challenge.ActorId,
                response.ActorId,
                StringComparison.Ordinal) ||
            !string.Equals(
                challenge.ActorId,
                context.ActorId,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.actor-mismatch");
        }

        if (!string.Equals(
                challenge.TenantId,
                context.TenantId,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.tenant-mismatch");
        }

        if (!string.Equals(
                challenge.CorrelationId,
                context.CorrelationId,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.correlation-mismatch");
        }

        if (!string.Equals(
                challenge.OperationName,
                context.OperationName,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.operation-mismatch");
        }

        if (!string.Equals(
                challenge.Recipient,
                context.Recipient,
                StringComparison.Ordinal))
        {
            return AcknowledgmentValidationResult.Failure(
                "acknowledgment.resource-mismatch");
        }

        bool outsideIssuedWindow =
            response.RespondedUtc < challenge.IssuedUtc ||
            response.RespondedUtc >= challenge.ExpiresUtc ||
            nowUtc < challenge.IssuedUtc ||
            nowUtc >= challenge.ExpiresUtc;

        return outsideIssuedWindow
            ? AcknowledgmentValidationResult.Failure(
                "acknowledgment.expired")
            : !response.Accepted
            ? AcknowledgmentValidationResult.Failure(
                "acknowledgment.rejected")
            : AcknowledgmentValidationResult.Success(
                $"{challenge.ChallengeId}-accepted");
    }
}

public sealed record ExecutionCapability(
    string CapabilityId,
    string Issuer,
    string Audience,
    string SubjectId,
    string OperationName,
    string ResourceId,
    IReadOnlySet<string> Scopes,
    DateTimeOffset IssuedUtc,
    DateTimeOffset ExpiresUtc,
    string PolicyVersion,
    string? AcknowledgmentId,
    int MaximumUses);

public sealed class ExecutionCapabilityIssuer
{
    public static ExecutionCapability Issue(
        AiToolPolicyContext context,
        GovernanceDecision decision,
        ToolDescriptor descriptor,
        DateTimeOffset nowUtc)
    {
        return !decision.CanProceed
            ? throw new InvalidOperationException(
                "A blocked decision cannot produce execution authority.")
            : new ExecutionCapability(
            CapabilityId: $"{context.CorrelationId}-capability",
            Issuer: "learning-governance-host",
            Audience: descriptor.Audience,
            SubjectId: context.ActorId,
            OperationName: descriptor.GovernanceOperation,
            ResourceId: context.Recipient,
            Scopes: new HashSet<string>(
                [descriptor.RequiredScope],
                StringComparer.Ordinal),
            IssuedUtc: nowUtc,
            ExpiresUtc: nowUtc.AddMinutes(2),
            PolicyVersion: context.PolicyVersion,
            AcknowledgmentId: context.SatisfiedAcknowledgmentId,
            MaximumUses: 1);
    }
}

public sealed record CapabilityValidationResult(
    bool IsValid,
    string ReasonCode)
{
    public static CapabilityValidationResult Valid()
    {
        return new(true, "capability.valid");
    }

    public static CapabilityValidationResult Invalid(
        string reasonCode)
    {
        return new(false, reasonCode);
    }
}

public sealed class ExecutionCapabilityValidator
{
    public static CapabilityValidationResult Validate(
        ExecutionCapability capability,
        AiToolPolicyContext context,
        ToolDescriptor descriptor,
        DateTimeOffset nowUtc)
    {
        if (!string.Equals(
                capability.Issuer,
                "learning-governance-host",
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.issuer-mismatch");
        }

        if (!string.Equals(
                capability.Audience,
                descriptor.Audience,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.audience-mismatch");
        }

        if (!string.Equals(
                capability.SubjectId,
                context.ActorId,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.subject-mismatch");
        }

        if (!string.Equals(
                capability.OperationName,
                context.OperationName,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.operation-mismatch");
        }

        if (!string.Equals(
                capability.ResourceId,
                context.Recipient,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.resource-mismatch");
        }

        if (!capability.Scopes.Contains(descriptor.RequiredScope))
        {
            return CapabilityValidationResult.Invalid(
                "capability.scope-missing");
        }

        if (!string.Equals(
                capability.PolicyVersion,
                context.PolicyVersion,
                StringComparison.Ordinal))
        {
            return CapabilityValidationResult.Invalid(
                "capability.policy-mismatch");
        }

        if (nowUtc < capability.IssuedUtc)
        {
            return CapabilityValidationResult.Invalid(
                "capability.not-yet-valid");
        }

        if (nowUtc >= capability.ExpiresUtc)
        {
            return CapabilityValidationResult.Invalid(
                "capability.expired");
        }

        return !string.Equals(
                capability.AcknowledgmentId,
                context.SatisfiedAcknowledgmentId,
                StringComparison.Ordinal)
            ? CapabilityValidationResult.Invalid(
                "capability.acknowledgment-mismatch")
            : capability.MaximumUses != 1
            ? CapabilityValidationResult.Invalid(
                "capability.use-limit-invalid")
            : CapabilityValidationResult.Valid();
    }
}

public sealed class InMemoryCapabilityUseStore
{
    private readonly HashSet<string> _consumedCapabilityIds =
        new(StringComparer.Ordinal);

    public bool TryConsume(string capabilityId)
    {
        return _consumedCapabilityIds.Add(capabilityId);
    }
}

public sealed record ToolExecutionResult(
    bool WouldExecute,
    string Recipient,
    string Template);

public sealed class RecordingNotificationHandler(string credentialReference)
{
    public int InvocationCount { get; private set; }

    public string? LastRecipient { get; private set; }

    public string CredentialOwner
    {
        get =>
        string.IsNullOrWhiteSpace(field)
            ? "none"
            : "host";
    } = credentialReference;

    public Task<ToolExecutionResult> ExecuteDryRunAsync(
        AiToolPolicyContext context,
        CancellationToken cancellationToken)
    {
        using System.Diagnostics.Activity? activity =
            GovernanceObservabilityInstrumentation.StartStage(
                "executor.invoke",
                context.CorrelationId,
                context.Proposal.ProposalId);

        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;
        LastRecipient = context.Recipient;

        activity?.SetTag("execution.invoked", true);
        activity?.SetTag("execution.result", "would-execute");

        return Task.FromResult(
            new ToolExecutionResult(
                WouldExecute: true,
                Recipient: context.Recipient,
                Template: context.Template));
    }

    public void Reset()
    {
        InvocationCount = 0;
        LastRecipient = null;
    }
}

public sealed record DecisionReceipt(
    string CorrelationId,
    string Stage,
    string Outcome,
    string ReasonCode,
    string? PolicyVersion = null);

public sealed class InMemoryAuditSink
{
    private readonly List<DecisionReceipt> _entries = [];

    public IReadOnlyList<DecisionReceipt> Entries => _entries;

    public void Write(
        string correlationId,
        string stage,
        string outcome,
        string reasonCode,
        string? policyVersion = null)
    {
        var receipt = new DecisionReceipt(
            correlationId,
            stage,
            outcome,
            reasonCode,
            policyVersion);

        _entries.Add(receipt);
        GovernanceObservabilityInstrumentation.RecordAuditEvent(receipt);
    }
}

public enum GatewayStatus
{
    Rejected,
    Blocked,
    AwaitingAcknowledgment,
    WouldExecute
}

public sealed record GatewayResult(
    GatewayStatus Status,
    string ReasonCode,
    bool WouldExecute,
    string CorrelationId,
    GovernanceDecisionOutcome? DecisionOutcome,
    AcknowledgmentChallenge? AcknowledgmentChallenge,
    string? CapabilityId)
{
    public static GatewayResult Rejected(
        string correlationId,
        string reasonCode)
    {
        return new(
            GatewayStatus.Rejected,
            reasonCode,
            false,
            correlationId,
            null,
            null,
            null);
    }

    public static GatewayResult Blocked(
        string correlationId,
        GovernanceDecision decision)
    {
        return new(
            GatewayStatus.Blocked,
            decision.ReasonCode,
            false,
            correlationId,
            decision.Outcome,
            null,
            null);
    }

    public static GatewayResult Awaiting(
        string correlationId,
        GovernanceDecision decision,
        AcknowledgmentChallenge challenge)
    {
        return new(
            GatewayStatus.AwaitingAcknowledgment,
            decision.ReasonCode,
            false,
            correlationId,
            decision.Outcome,
            challenge,
            null);
    }

    public static GatewayResult Executable(
        string correlationId,
        GovernanceDecision decision,
        string capabilityId)
    {
        return new(
            GatewayStatus.WouldExecute,
            "execution.would-execute",
            true,
            correlationId,
            decision.Outcome,
            null,
            capabilityId);
    }
}

public sealed class GovernedAiToolGateway(
    ToolRegistry toolRegistry,
    InMemoryAcknowledgmentChallengeStore acknowledgmentChallengeStore,
    InMemoryCapabilityUseStore capabilityUseStore,
    RecordingNotificationHandler handler,
    InMemoryAuditSink auditSink)
{
    public async Task<GatewayResult> ExecuteAsync(
        AiToolProposal proposal,
        HostActor actor,
        DateTimeOffset nowUtc,
        AcknowledgmentResponse? acknowledgmentResponse,
        CancellationToken cancellationToken,
        string? hostCorrelationId = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string correlationId =
            hostCorrelationId ?? proposal.ProposalId;

        ToolDescriptor? descriptor =
            toolRegistry.Find(proposal.ToolName);

        if (descriptor is null)
        {
            auditSink.Write(
                correlationId,
                "proposal-validation",
                "rejected",
                "tool.unknown");

            return GatewayResult.Rejected(
                correlationId,
                "tool.unknown");
        }

        ProposalValidationResult proposalValidation =
            ProposalValidator.Validate(proposal, descriptor);

        if (!proposalValidation.IsValid)
        {
            auditSink.Write(
                correlationId,
                "proposal-validation",
                "rejected",
                proposalValidation.ReasonCode);

            return GatewayResult.Rejected(
                correlationId,
                proposalValidation.ReasonCode);
        }

        auditSink.Write(
            correlationId,
            "proposal-validation",
            "valid",
            proposalValidation.ReasonCode);

        AiToolPolicyContext context =
            HostPolicyContextFactory.Create(
                proposal,
                descriptor,
                actor,
                correlationId);

        auditSink.Write(
            context.CorrelationId,
            "context",
            context.DestinationClassification.ToString(),
            "context.host-authoritative",
            context.PolicyVersion);

        GovernanceDecision decision =
            NotificationPolicy.Evaluate(context);

        auditSink.Write(
            context.CorrelationId,
            "decision",
            decision.Outcome.ToString(),
            decision.ReasonCode,
            context.PolicyVersion);

        if (decision.Outcome is
            GovernanceDecisionOutcome.Denied or
            GovernanceDecisionOutcome.Deferred)
        {
            return GatewayResult.Blocked(
                context.CorrelationId,
                decision);
        }

        if (decision.Outcome ==
            GovernanceDecisionOutcome.AcknowledgmentRequired)
        {
            AcknowledgmentChallenge challenge;

            if (acknowledgmentResponse is null)
            {
                challenge =
                    AcknowledgmentService.CreateChallenge(
                        context,
                        decision,
                        nowUtc);

                acknowledgmentChallengeStore.Issue(
                    challenge,
                    nowUtc);

                auditSink.Write(
                    context.CorrelationId,
                    "acknowledgment",
                    "required",
                    decision.ReasonCode);

                return GatewayResult.Awaiting(
                    context.CorrelationId,
                    decision,
                    challenge);
            }

            AcknowledgmentChallengeLookup lookup =
                acknowledgmentChallengeStore.Find(
                    acknowledgmentResponse.ChallengeId,
                    nowUtc);

            if (lookup.Status !=
                    AcknowledgmentChallengeLookupStatus.Issued ||
                lookup.Challenge is null)
            {
                string reasonCode = lookup.Status switch
                {
                    AcknowledgmentChallengeLookupStatus.Unknown =>
                        "acknowledgment.challenge-unknown",
                    AcknowledgmentChallengeLookupStatus.Issued =>
                        "acknowledgment.challenge-unknown",
                    AcknowledgmentChallengeLookupStatus.Consumed =>
                        "acknowledgment.challenge-consumed",
                    AcknowledgmentChallengeLookupStatus.Expired =>
                        "acknowledgment.expired",
                    _ => "acknowledgment.challenge-unknown"
                };

                auditSink.Write(
                    context.CorrelationId,
                    "acknowledgment",
                    "rejected",
                    reasonCode);

                return GatewayResult.Rejected(
                    context.CorrelationId,
                    reasonCode);
            }

            challenge = lookup.Challenge;

            AcknowledgmentValidationResult acknowledgment =
                AcknowledgmentService.Validate(
                    challenge,
                    acknowledgmentResponse,
                    context,
                    nowUtc);

            if (!acknowledgment.Accepted ||
                acknowledgment.AcknowledgmentId is null)
            {
                auditSink.Write(
                    context.CorrelationId,
                    "acknowledgment",
                    "rejected",
                    acknowledgment.ReasonCode);

                return GatewayResult.Rejected(
                    context.CorrelationId,
                    acknowledgment.ReasonCode);
            }

            if (!acknowledgmentChallengeStore.TryConsume(
                    challenge.ChallengeId,
                    nowUtc))
            {
                auditSink.Write(
                    context.CorrelationId,
                    "acknowledgment",
                    "rejected",
                    "acknowledgment.challenge-consumed");

                return GatewayResult.Rejected(
                    context.CorrelationId,
                    "acknowledgment.challenge-consumed");
            }

            auditSink.Write(
                context.CorrelationId,
                "acknowledgment",
                "accepted",
                acknowledgment.ReasonCode);

            context = context with
            {
                SatisfiedAcknowledgmentId =
                    acknowledgment.AcknowledgmentId
            };

            decision = NotificationPolicy.Evaluate(context);

            auditSink.Write(
                context.CorrelationId,
                "re-evaluation",
                decision.Outcome.ToString(),
                decision.ReasonCode,
                context.PolicyVersion);

            if (!decision.CanProceed)
            {
                return GatewayResult.Blocked(
                    context.CorrelationId,
                    decision);
            }
        }

        ExecutionCapability capability =
            ExecutionCapabilityIssuer.Issue(
                context,
                decision,
                descriptor,
                nowUtc);

        auditSink.Write(
            context.CorrelationId,
            "capability-issued",
            "issued",
            "capability.issued");

        CapabilityValidationResult capabilityValidation =
            ExecutionCapabilityValidator.Validate(
                capability,
                context,
                descriptor,
                nowUtc);

        auditSink.Write(
            context.CorrelationId,
            "capability-validation",
            capabilityValidation.IsValid ? "valid" : "invalid",
            capabilityValidation.ReasonCode);

        if (!capabilityValidation.IsValid)
        {
            return GatewayResult.Rejected(
                context.CorrelationId,
                capabilityValidation.ReasonCode);
        }

        if (!capabilityUseStore.TryConsume(capability.CapabilityId))
        {
            auditSink.Write(
                context.CorrelationId,
                "capability-consumption",
                "rejected",
                "capability.already-consumed");

            return GatewayResult.Rejected(
                context.CorrelationId,
                "capability.already-consumed");
        }

        auditSink.Write(
            context.CorrelationId,
            "capability-consumption",
            "consumed",
            "capability.consumed");

        ToolExecutionResult execution =
            await handler.ExecuteDryRunAsync(
                context,
                cancellationToken);

        auditSink.Write(
            context.CorrelationId,
            "execution",
            execution.WouldExecute ? "would-execute" : "blocked",
            execution.WouldExecute
                ? "execution.would-execute"
                : "execution.blocked");

        return GatewayResult.Executable(
            context.CorrelationId,
            decision,
            capability.CapabilityId);
    }
}
