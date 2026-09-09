var policy = new DisableAccountPolicy();
var validator = new AcknowledgmentValidator();
var executor = new RecordingExecutor();

WorkflowScenario[] scenarios =
[
    new(
        Name: "Valid acknowledgment",
        Context: CreateContext("user-100"),
        ResponseMode: ResponseMode.Accept,
        ProtectResourceAfterAcknowledgment: false,
        ExpectedFinalState: "Executed",
        ExpectedLastDecision: GovernanceDecisionOutcome.Allowed,
        ExpectedExecutorInvocations: 1,
        ExpectedStages:
        [
            "decision",
            "challenge-issued",
            "acknowledgment-accepted",
            "re-evaluation",
            "execution-completed"
        ]),
    new(
        Name: "Rejected acknowledgment",
        Context: CreateContext("user-200"),
        ResponseMode: ResponseMode.Reject,
        ProtectResourceAfterAcknowledgment: false,
        ExpectedFinalState: "AcknowledgmentRejected",
        ExpectedLastDecision: GovernanceDecisionOutcome.AcknowledgmentRequired,
        ExpectedExecutorInvocations: 0,
        ExpectedStages:
        [
            "decision",
            "challenge-issued",
            "acknowledgment-rejected"
        ]),
    new(
        Name: "Wrong actor",
        Context: CreateContext("user-300"),
        ResponseMode: ResponseMode.WrongActor,
        ProtectResourceAfterAcknowledgment: false,
        ExpectedFinalState: "AcknowledgmentInvalid",
        ExpectedLastDecision: GovernanceDecisionOutcome.AcknowledgmentRequired,
        ExpectedExecutorInvocations: 0,
        ExpectedStages:
        [
            "decision",
            "challenge-issued",
            "acknowledgment-invalid"
        ]),
    new(
        Name: "Expired challenge",
        Context: CreateContext("user-400"),
        ResponseMode: ResponseMode.Expired,
        ProtectResourceAfterAcknowledgment: false,
        ExpectedFinalState: "AcknowledgmentInvalid",
        ExpectedLastDecision: GovernanceDecisionOutcome.AcknowledgmentRequired,
        ExpectedExecutorInvocations: 0,
        ExpectedStages:
        [
            "decision",
            "challenge-issued",
            "acknowledgment-invalid"
        ]),
    new(
        Name: "Context drift after acknowledgment",
        Context: CreateContext("service-500"),
        ResponseMode: ResponseMode.Accept,
        ProtectResourceAfterAcknowledgment: true,
        ExpectedFinalState: "BlockedAfterReevaluation",
        ExpectedLastDecision: GovernanceDecisionOutcome.EscalationRecommended,
        ExpectedExecutorInvocations: 0,
        ExpectedStages:
        [
            "decision",
            "challenge-issued",
            "acknowledgment-accepted",
            "re-evaluation"
        ])
];

Console.WriteLine("Acknowledgment and Audit Residue");
Console.WriteLine(new string('=', 34));
Console.WriteLine();

foreach (WorkflowScenario scenario in scenarios)
{
    executor.Reset();

    WorkflowResult result = RunScenario(
        scenario,
        policy,
        validator,
        executor);

    VerifyScenario(
        scenario,
        result,
        executor.InvocationCount);

    Console.WriteLine($"Scenario: {scenario.Name}");
    Console.WriteLine($"Final state: {result.FinalState}");
    Console.WriteLine($"Last decision: {result.LastDecision.Outcome}");
    Console.WriteLine($"Executor invocations: {executor.InvocationCount}");
    Console.WriteLine("Audit timeline:");

    foreach (AuditResidue residue in result.AuditTrail)
    {
        Console.WriteLine(
            $"  {residue.Sequence,2}. {residue.Stage,-25} " +
            $"outcome={residue.Outcome,-28} " +
            $"reasons={FormatReasons(residue.ReasonCodes)}");
    }

    Console.WriteLine(
        "Correlation preserved: " +
        result.AuditTrail.All(
            residue => residue.CorrelationId ==
                scenario.Context.CorrelationId));
    Console.WriteLine();
}

Console.WriteLine("Architectural invariants preserved:");
Console.WriteLine("- Acknowledgment is validated before continuation.");
Console.WriteLine("- Rejected, mismatched, and expired responses never reach execution.");
Console.WriteLine("- Valid acknowledgment satisfies one requirement but does not bypass re-evaluation.");
Console.WriteLine("- Decision, acknowledgment, and execution remain distinct evidence events.");

static WorkflowResult RunScenario(
    WorkflowScenario scenario,
    DisableAccountPolicy policy,
    AcknowledgmentValidator validator,
    RecordingExecutor executor)
{
    DateTimeOffset nowUtc =
        new(2026, 8, 13, 19, 0, 0, TimeSpan.Zero);

    var audit = new List<AuditResidue>();
    DisableAccountPolicyContext context = scenario.Context;
    GovernanceDecision decision = policy.Evaluate(context);

    AddDecisionResidue(
        audit,
        nowUtc,
        context,
        decision,
        "decision");

    if (decision.Outcome !=
        GovernanceDecisionOutcome.AcknowledgmentRequired)
    {
        return new WorkflowResult(
            "BlockedByInitialDecision",
            decision,
            audit);
    }

    AcknowledgmentChallenge challenge =
        CreateChallenge(
            context,
            decision,
            nowUtc);

    nowUtc = nowUtc.AddSeconds(1);
    AddResidue(
        audit,
        nowUtc,
        context,
        outcome: "ChallengeIssued",
        reasonCodes:
            decision.Reasons
                .Select(reason => reason.Code)
                .ToArray(),
        stage: "challenge-issued");

    DateTimeOffset responseUtc =
        scenario.ResponseMode == ResponseMode.Expired
            ? challenge.ExpiresUtc.AddSeconds(1)
            : nowUtc.AddSeconds(1);

    AcknowledgmentResponse response =
        CreateResponse(
            challenge,
            scenario.ResponseMode,
            responseUtc);

    AcknowledgmentValidation validation =
        validator.Validate(
            challenge,
            response,
            responseUtc);

    if (!response.Accepted)
    {
        AddResidue(
            audit,
            responseUtc,
            context,
            outcome: "AcknowledgmentRejected",
            reasonCodes: [validation.ReasonCode],
            stage: "acknowledgment-rejected",
            actorId: response.ActorId);

        return new WorkflowResult(
            "AcknowledgmentRejected",
            decision,
            audit);
    }

    if (!validation.IsValid)
    {
        AddResidue(
            audit,
            responseUtc,
            context,
            outcome: "AcknowledgmentInvalid",
            reasonCodes: [validation.ReasonCode],
            stage: "acknowledgment-invalid",
            actorId: response.ActorId);

        return new WorkflowResult(
            "AcknowledgmentInvalid",
            decision,
            audit);
    }

    AddResidue(
        audit,
        responseUtc,
        context,
        outcome: "AcknowledgmentAccepted",
        reasonCodes:
        [
            challenge.ReasonCode,
            validation.ReasonCode
        ],
        stage: "acknowledgment-accepted",
        actorId: response.ActorId);

    context = context with
    {
        RequiredAcknowledgmentSatisfied = true,
        Account = scenario.ProtectResourceAfterAcknowledgment
            ? context.Account with { IsProtected = true }
            : context.Account
    };

    decision = policy.Evaluate(context);
    nowUtc = responseUtc.AddSeconds(1);

    AddDecisionResidue(
        audit,
        nowUtc,
        context,
        decision,
        "re-evaluation");

    if (!decision.CanProceed)
    {
        return new WorkflowResult(
            "BlockedAfterReevaluation",
            decision,
            audit);
    }

    executor.Execute(context.Intent);

    AddResidue(
        audit,
        nowUtc.AddSeconds(1),
        context,
        outcome: "Executed",
        reasonCodes: [],
        stage: "execution-completed");

    return new WorkflowResult(
        "Executed",
        decision,
        audit);
}

static DisableAccountPolicyContext CreateContext(
    string accountId)
{
    return new DisableAccountPolicyContext(
        Intent: new DisableAccountIntent(
            accountId,
            RequestedBy: "operator-7",
            Reason: string.Empty),
        Actor: new ActorContext(
            ActorId: "operator-7",
            TenantId: "tenant-a",
            IsAdministrator: true),
        Account: new AccountContext(
            AccountId: accountId,
            TenantId: "tenant-a",
            IsProtected: false),
        RequiredAcknowledgmentSatisfied: false,
        CorrelationId: $"sample-{accountId}",
        PolicyVersion: "3.2");
}

static AcknowledgmentChallenge CreateChallenge(
    DisableAccountPolicyContext context,
    GovernanceDecision decision,
    DateTimeOffset nowUtc)
{
    DecisionReason reason = decision.Reasons.Single();

    return new AcknowledgmentChallenge(
        ChallengeId: $"{context.CorrelationId}-challenge",
        ActorId: context.Actor.ActorId,
        OperationName: "account.disable",
        ResourceId: context.Account.AccountId,
        ReasonCode: reason.Code,
        RequiredAcknowledgmentCode:
            "account.disable.accept-responsibility",
        CorrelationId: context.CorrelationId,
        PolicyVersion: context.PolicyVersion,
        ExpiresUtc: nowUtc.AddMinutes(5));
}

static AcknowledgmentResponse CreateResponse(
    AcknowledgmentChallenge challenge,
    ResponseMode mode,
    DateTimeOffset occurredUtc)
{
    bool accepted = mode != ResponseMode.Reject;
    string actorId = mode == ResponseMode.WrongActor
        ? "operator-99"
        : challenge.ActorId;

    return new AcknowledgmentResponse(
        AcknowledgmentId:
            $"{challenge.CorrelationId}-acknowledgment",
        ChallengeId: challenge.ChallengeId,
        ActorId: actorId,
        AcknowledgmentCode:
            challenge.RequiredAcknowledgmentCode,
        Accepted: accepted,
        OccurredUtc: occurredUtc,
        CorrelationId: challenge.CorrelationId);
}

static void AddDecisionResidue(
    List<AuditResidue> audit,
    DateTimeOffset occurredUtc,
    DisableAccountPolicyContext context,
    GovernanceDecision decision,
    string stage)
{
    AddResidue(
        audit,
        occurredUtc,
        context,
        outcome: decision.Outcome.ToString(),
        reasonCodes:
            decision.Reasons
                .Select(reason => reason.Code)
                .ToArray(),
        stage: stage);
}

static void AddResidue(
    List<AuditResidue> audit,
    DateTimeOffset occurredUtc,
    DisableAccountPolicyContext context,
    string outcome,
    IReadOnlyList<string> reasonCodes,
    string stage,
    string? actorId = null)
{
    int sequence = audit.Count + 1;

    audit.Add(
        new AuditResidue(
            Sequence: sequence,
            EventId:
                $"{context.CorrelationId}-event-{sequence:00}",
            OccurredUtc: occurredUtc,
            ActorId: actorId ?? context.Actor.ActorId,
            OperationName: "account.disable",
            Outcome: outcome,
            ReasonCodes: reasonCodes,
            CorrelationId: context.CorrelationId,
            PolicyVersion: context.PolicyVersion,
            Stage: stage));
}

static void VerifyScenario(
    WorkflowScenario scenario,
    WorkflowResult result,
    int executorInvocations)
{
    if (result.FinalState != scenario.ExpectedFinalState)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected final state " +
            $"{scenario.ExpectedFinalState} but received {result.FinalState}.");
    }

    if (result.LastDecision.Outcome !=
        scenario.ExpectedLastDecision)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected last decision " +
            $"{scenario.ExpectedLastDecision} but received " +
            $"{result.LastDecision.Outcome}.");
    }

    if (executorInvocations !=
        scenario.ExpectedExecutorInvocations)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected " +
            $"{scenario.ExpectedExecutorInvocations} executor invocation(s) " +
            $"but observed {executorInvocations}.");
    }

    string[] stages = result.AuditTrail
        .Select(residue => residue.Stage)
        .ToArray();

    if (!stages.SequenceEqual(
            scenario.ExpectedStages,
            StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' produced an unexpected audit timeline.");
    }

    if (result.AuditTrail.Any(
            residue => residue.CorrelationId !=
                scenario.Context.CorrelationId))
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' lost correlation across its audit timeline.");
    }
}

static string FormatReasons(
    IReadOnlyList<string> reasonCodes)
{
    return reasonCodes.Count == 0
        ? "<none>"
        : string.Join(",", reasonCodes);
}

public sealed record WorkflowScenario(
    string Name,
    DisableAccountPolicyContext Context,
    ResponseMode ResponseMode,
    bool ProtectResourceAfterAcknowledgment,
    string ExpectedFinalState,
    GovernanceDecisionOutcome ExpectedLastDecision,
    int ExpectedExecutorInvocations,
    IReadOnlyList<string> ExpectedStages);

public sealed record WorkflowResult(
    string FinalState,
    GovernanceDecision LastDecision,
    IReadOnlyList<AuditResidue> AuditTrail);

public enum ResponseMode
{
    Accept,
    Reject,
    WrongActor,
    Expired
}

public sealed record DisableAccountIntent(
    string AccountId,
    string RequestedBy,
    string Reason);

public sealed record ActorContext(
    string ActorId,
    string TenantId,
    bool IsAdministrator);

public sealed record AccountContext(
    string AccountId,
    string TenantId,
    bool IsProtected);

public sealed record DisableAccountPolicyContext(
    DisableAccountIntent Intent,
    ActorContext Actor,
    AccountContext Account,
    bool RequiredAcknowledgmentSatisfied,
    string CorrelationId,
    string PolicyVersion);

public enum GovernanceDecisionOutcome
{
    Allowed,
    Denied,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record DecisionReason(
    string Code,
    string Message);

public sealed record GovernanceDecision(
    GovernanceDecisionOutcome Outcome,
    IReadOnlyList<DecisionReason> Reasons)
{
    public bool CanProceed =>
        Outcome == GovernanceDecisionOutcome.Allowed;

    public static GovernanceDecision Allow() =>
        new(GovernanceDecisionOutcome.Allowed, []);

    public static GovernanceDecision Deny(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Denied,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision RequireAcknowledgment(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Escalate(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.EscalationRecommended,
            [new DecisionReason(code, message)]);
}

public sealed class DisableAccountPolicy
{
    public GovernanceDecision Evaluate(
        DisableAccountPolicyContext context)
    {
        if (!context.Actor.IsAdministrator)
        {
            return GovernanceDecision.Deny(
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (!string.Equals(
                context.Actor.TenantId,
                context.Account.TenantId,
                StringComparison.Ordinal))
        {
            return GovernanceDecision.Deny(
                "account.disable.cross-tenant",
                "The actor and account belong to different tenants.");
        }

        if (context.Account.IsProtected)
        {
            return GovernanceDecision.Escalate(
                "account.disable.protected-account",
                "Protected accounts require escalation.");
        }

        if (string.IsNullOrWhiteSpace(context.Intent.Reason) &&
            !context.RequiredAcknowledgmentSatisfied)
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.reason-required",
                "The missing administrative reason requires explicit acknowledgment.");
        }

        return GovernanceDecision.Allow();
    }
}

public sealed record AcknowledgmentChallenge(
    string ChallengeId,
    string ActorId,
    string OperationName,
    string ResourceId,
    string ReasonCode,
    string RequiredAcknowledgmentCode,
    string CorrelationId,
    string PolicyVersion,
    DateTimeOffset ExpiresUtc);

public sealed record AcknowledgmentResponse(
    string AcknowledgmentId,
    string ChallengeId,
    string ActorId,
    string AcknowledgmentCode,
    bool Accepted,
    DateTimeOffset OccurredUtc,
    string CorrelationId);

public sealed record AcknowledgmentValidation(
    bool IsValid,
    string ReasonCode);

public sealed class AcknowledgmentValidator
{
    public AcknowledgmentValidation Validate(
        AcknowledgmentChallenge challenge,
        AcknowledgmentResponse response,
        DateTimeOffset nowUtc)
    {
        if (!response.Accepted)
        {
            return new(false, "acknowledgment.rejected");
        }

        if (challenge.ChallengeId != response.ChallengeId)
        {
            return new(false, "acknowledgment.challenge-mismatch");
        }

        if (challenge.ActorId != response.ActorId)
        {
            return new(false, "acknowledgment.actor-mismatch");
        }

        if (challenge.RequiredAcknowledgmentCode !=
            response.AcknowledgmentCode)
        {
            return new(false, "acknowledgment.code-mismatch");
        }

        if (challenge.CorrelationId != response.CorrelationId)
        {
            return new(false, "acknowledgment.correlation-mismatch");
        }

        if (nowUtc > challenge.ExpiresUtc)
        {
            return new(false, "acknowledgment.expired");
        }

        return new(true, "acknowledgment.accepted");
    }
}

public sealed record AuditResidue(
    int Sequence,
    string EventId,
    DateTimeOffset OccurredUtc,
    string ActorId,
    string OperationName,
    string Outcome,
    IReadOnlyList<string> ReasonCodes,
    string CorrelationId,
    string PolicyVersion,
    string Stage);

public sealed class RecordingExecutor
{
    public int InvocationCount { get; private set; }

    public void Execute(DisableAccountIntent intent)
    {
        InvocationCount++;
        _ = intent;
    }

    public void Reset()
    {
        InvocationCount = 0;
    }
}
