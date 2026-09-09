var policy = new DisableAccountPolicy();

PolicyScenario[] scenarios =
[
    new(
        "Normal request",
        CreateContext(
            accountId: "user-100",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: true,
            isProtected: false,
            isAlreadyDisabled: false,
            maintenanceHoldActive: false,
            reason: "Security investigation"),
        GovernanceDecisionOutcome.Allowed,
        null),
    new(
        "Non-administrator",
        CreateContext(
            accountId: "user-200",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: false,
            isProtected: false,
            isAlreadyDisabled: false,
            maintenanceHoldActive: false,
            reason: "Security investigation"),
        GovernanceDecisionOutcome.Denied,
        "account.disable.not-administrator"),
    new(
        "Cross-tenant account",
        CreateContext(
            accountId: "user-300",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-b",
            isAdministrator: true,
            isProtected: false,
            isAlreadyDisabled: false,
            maintenanceHoldActive: false,
            reason: "Security investigation"),
        GovernanceDecisionOutcome.Denied,
        "account.disable.cross-tenant"),
    new(
        "Already disabled",
        CreateContext(
            accountId: "user-400",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: true,
            isProtected: false,
            isAlreadyDisabled: true,
            maintenanceHoldActive: false,
            reason: "Administrative cleanup"),
        GovernanceDecisionOutcome.Warning,
        "account.disable.already-disabled"),
    new(
        "Protected account",
        CreateContext(
            accountId: "service-protected",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: true,
            isProtected: true,
            isAlreadyDisabled: false,
            maintenanceHoldActive: false,
            reason: "Rotation request"),
        GovernanceDecisionOutcome.EscalationRecommended,
        "account.disable.protected-account"),
    new(
        "Maintenance hold",
        CreateContext(
            accountId: "user-500",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: true,
            isProtected: false,
            isAlreadyDisabled: false,
            maintenanceHoldActive: true,
            reason: "Administrative cleanup"),
        GovernanceDecisionOutcome.Deferred,
        "account.disable.maintenance-hold"),
    new(
        "Missing reason",
        CreateContext(
            accountId: "user-600",
            actorTenantId: "tenant-a",
            accountTenantId: "tenant-a",
            isAdministrator: true,
            isProtected: false,
            isAlreadyDisabled: false,
            maintenanceHoldActive: false,
            reason: string.Empty),
        GovernanceDecisionOutcome.AcknowledgmentRequired,
        "account.disable.reason-required")
];

Console.WriteLine("Policy Context and Explicit Decision Outcomes");
Console.WriteLine(new string('=', 45));
Console.WriteLine();

foreach (PolicyScenario scenario in scenarios)
{
    GovernanceDecision decision = policy.Evaluate(scenario.Context);

    VerifyScenario(scenario, decision);

    Console.WriteLine($"Scenario: {scenario.Name}");
    Console.WriteLine(
        $"Context: actor={scenario.Context.Actor.ActorId}/{scenario.Context.Actor.TenantId}, " +
        $"resource={scenario.Context.Account.AccountId}/{scenario.Context.Account.TenantId}, " +
        $"region={scenario.Context.Environment.Region}, " +
        $"policy={scenario.Context.PolicyVersion}");
    Console.WriteLine($"Outcome: {decision.Outcome}");
    Console.WriteLine($"Can proceed: {decision.CanProceed}");
    Console.WriteLine($"Reason codes: {FormatReasonCodes(decision)}");
    Console.WriteLine();
}

Console.WriteLine("Invariant preserved: every explicit context produced the expected structured outcome.");
Console.WriteLine($"Scenarios verified: {scenarios.Length}");

static DisableAccountPolicyContext CreateContext(
    string accountId,
    string actorTenantId,
    string accountTenantId,
    bool isAdministrator,
    bool isProtected,
    bool isAlreadyDisabled,
    bool maintenanceHoldActive,
    string reason)
{
    return new DisableAccountPolicyContext(
        Intent: new DisableAccountIntent(
            AccountId: accountId,
            RequestedBy: "operator-7",
            Reason: reason),
        Actor: new ActorContext(
            ActorId: "operator-7",
            TenantId: actorTenantId,
            IsAdministrator: isAdministrator),
        Account: new AccountContext(
            AccountId: accountId,
            TenantId: accountTenantId,
            IsProtected: isProtected,
            IsAlreadyDisabled: isAlreadyDisabled),
        Environment: new EnvironmentContext(
            MaintenanceHoldActive: maintenanceHoldActive,
            Region: "us-central"),
        CorrelationId: $"sample-{accountId}",
        PolicyVersion: "2.0");
}

static void VerifyScenario(
    PolicyScenario scenario,
    GovernanceDecision decision)
{
    if (decision.Outcome != scenario.ExpectedOutcome)
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected {scenario.ExpectedOutcome} but received {decision.Outcome}.");
    }

    string[] reasonCodes = decision.Reasons
        .Select(reason => reason.Code)
        .ToArray();

    if (scenario.ExpectedReasonCode is null)
    {
        if (reasonCodes.Length != 0)
        {
            throw new InvalidOperationException(
                $"Scenario '{scenario.Name}' expected no reason codes.");
        }

        return;
    }

    if (!reasonCodes.Contains(
            scenario.ExpectedReasonCode,
            StringComparer.Ordinal))
    {
        throw new InvalidOperationException(
            $"Scenario '{scenario.Name}' expected reason code '{scenario.ExpectedReasonCode}'.");
    }
}

static string FormatReasonCodes(GovernanceDecision decision)
{
    return decision.Reasons.Count == 0
        ? "<none>"
        : string.Join(
            ", ",
            decision.Reasons.Select(reason => reason.Code));
}

public sealed record PolicyScenario(
    string Name,
    DisableAccountPolicyContext Context,
    GovernanceDecisionOutcome ExpectedOutcome,
    string? ExpectedReasonCode);

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
    bool IsProtected,
    bool IsAlreadyDisabled);

public sealed record EnvironmentContext(
    bool MaintenanceHoldActive,
    string Region);

public sealed record DisableAccountPolicyContext(
    DisableAccountIntent Intent,
    ActorContext Actor,
    AccountContext Account,
    EnvironmentContext Environment,
    string CorrelationId,
    string PolicyVersion);

public enum GovernanceDecisionOutcome
{
    Allowed,
    Warning,
    Denied,
    Deferred,
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
        Outcome is GovernanceDecisionOutcome.Allowed
            or GovernanceDecisionOutcome.Warning;

    public static GovernanceDecision Allow() =>
        new(
            GovernanceDecisionOutcome.Allowed,
            []);

    public static GovernanceDecision Warning(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Warning,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Deny(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Denied,
            [new DecisionReason(code, message)]);

    public static GovernanceDecision Defer(
        string code,
        string message) =>
        new(
            GovernanceDecisionOutcome.Deferred,
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

        if (context.Account.IsAlreadyDisabled)
        {
            return GovernanceDecision.Warning(
                "account.disable.already-disabled",
                "The account is already disabled.");
        }

        if (context.Account.IsProtected)
        {
            return GovernanceDecision.Escalate(
                "account.disable.protected-account",
                "Protected accounts require escalation.");
        }

        if (context.Environment.MaintenanceHoldActive)
        {
            return GovernanceDecision.Defer(
                "account.disable.maintenance-hold",
                "Account changes are temporarily deferred.");
        }

        if (string.IsNullOrWhiteSpace(
                context.Intent.Reason))
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.reason-required",
                "A reason must be supplied and acknowledged.");
        }

        return GovernanceDecision.Allow();
    }
}
