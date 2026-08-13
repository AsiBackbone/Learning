var policy = new DisableAccountPolicy();
var executor = new RecordingDisableAccountExecutor();
var workflow = new DisableAccountWorkflow(policy, executor);

DisableAccountContext[] scenarios =
[
    CreateContext(
        accountId: "user-100",
        requesterIsAdministrator: true,
        isProtectedAccount: false,
        maintenanceHoldActive: false,
        reason: "Security investigation"),
    CreateContext(
        accountId: "user-200",
        requesterIsAdministrator: false,
        isProtectedAccount: false,
        maintenanceHoldActive: false,
        reason: "Security investigation"),
    CreateContext(
        accountId: "service-protected",
        requesterIsAdministrator: true,
        isProtectedAccount: true,
        maintenanceHoldActive: false,
        reason: "Rotation request"),
    CreateContext(
        accountId: "user-300",
        requesterIsAdministrator: true,
        isProtectedAccount: false,
        maintenanceHoldActive: true,
        reason: "Administrative cleanup"),
    CreateContext(
        accountId: "user-400",
        requesterIsAdministrator: true,
        isProtectedAccount: false,
        maintenanceHoldActive: false,
        reason: string.Empty)
];

Console.WriteLine("Decision Before Execution");
Console.WriteLine(new string('=', 25));
Console.WriteLine();

foreach (DisableAccountContext context in scenarios)
{
    int before = executor.InvocationCount;

    GovernanceDecision decision = await workflow.ExecuteAsync(
        context,
        CancellationToken.None);

    int invocationsForScenario = executor.InvocationCount - before;

    Console.WriteLine($"Account: {context.Intent.AccountId}");
    Console.WriteLine($"Outcome: {decision.Outcome}");
    Console.WriteLine($"Reason: {decision.ReasonCode}");
    Console.WriteLine($"Executor invoked: {invocationsForScenario == 1}");
    Console.WriteLine();
}

if (executor.InvocationCount != 1)
{
    throw new InvalidOperationException(
        $"Expected exactly one allowed operation to reach execution, but observed {executor.InvocationCount}.");
}

Console.WriteLine("Invariant preserved: blocked decisions never reached the executor.");
Console.WriteLine($"Total simulated executions: {executor.InvocationCount}");

static DisableAccountContext CreateContext(
    string accountId,
    bool requesterIsAdministrator,
    bool isProtectedAccount,
    bool maintenanceHoldActive,
    string reason)
{
    var intent = new DisableAccountIntent(
        AccountId: accountId,
        RequestedBy: "operator-7",
        Reason: reason);

    return new DisableAccountContext(
        Intent: intent,
        RequesterIsAdministrator: requesterIsAdministrator,
        IsProtectedAccount: isProtectedAccount,
        MaintenanceHoldActive: maintenanceHoldActive,
        CorrelationId: $"sample-{accountId}",
        PolicyVersion: "1.0");
}

public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public sealed record GovernanceDecision(
    DecisionOutcome Outcome,
    string ReasonCode,
    string Reason)
{
    public bool CanExecute => Outcome == DecisionOutcome.Allowed;

    public static GovernanceDecision Allow() =>
        new(
            DecisionOutcome.Allowed,
            "decision.allowed",
            "The operation may proceed.");

    public static GovernanceDecision Deny(
        string code,
        string reason) =>
        new(DecisionOutcome.Denied, code, reason);

    public static GovernanceDecision Defer(
        string code,
        string reason) =>
        new(DecisionOutcome.Deferred, code, reason);

    public static GovernanceDecision RequireAcknowledgment(
        string code,
        string reason) =>
        new(DecisionOutcome.AcknowledgmentRequired, code, reason);

    public static GovernanceDecision Escalate(
        string code,
        string reason) =>
        new(DecisionOutcome.EscalationRecommended, code, reason);
}

public sealed record DisableAccountIntent(
    string AccountId,
    string RequestedBy,
    string Reason);

public sealed record DisableAccountContext(
    DisableAccountIntent Intent,
    bool RequesterIsAdministrator,
    bool IsProtectedAccount,
    bool MaintenanceHoldActive,
    string CorrelationId,
    string PolicyVersion);

public sealed class DisableAccountPolicy
{
    public GovernanceDecision Evaluate(DisableAccountContext context)
    {
        if (!context.RequesterIsAdministrator)
        {
            return GovernanceDecision.Deny(
                "account.disable.not-administrator",
                "Only administrators may disable accounts.");
        }

        if (context.IsProtectedAccount)
        {
            return GovernanceDecision.Escalate(
                "account.disable.protected-account",
                "Protected accounts require escalation.");
        }

        if (context.MaintenanceHoldActive)
        {
            return GovernanceDecision.Defer(
                "account.disable.maintenance-hold",
                "Account changes are temporarily deferred.");
        }

        if (string.IsNullOrWhiteSpace(context.Intent.Reason))
        {
            return GovernanceDecision.RequireAcknowledgment(
                "account.disable.reason-required",
                "A reason must be acknowledged before this operation proceeds.");
        }

        return GovernanceDecision.Allow();
    }
}

public interface IDisableAccountExecutor
{
    Task ExecuteAsync(
        DisableAccountIntent intent,
        CancellationToken cancellationToken);
}

public sealed class RecordingDisableAccountExecutor
    : IDisableAccountExecutor
{
    public int InvocationCount { get; private set; }

    public Task ExecuteAsync(
        DisableAccountIntent intent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        InvocationCount++;

        Console.WriteLine(
            $"SIMULATED HOST EXECUTION: would disable {intent.AccountId}.");

        return Task.CompletedTask;
    }
}

public sealed class DisableAccountWorkflow(
    DisableAccountPolicy policy,
    IDisableAccountExecutor executor)
{
    public async Task<GovernanceDecision> ExecuteAsync(
        DisableAccountContext context,
        CancellationToken cancellationToken)
    {
        GovernanceDecision decision = policy.Evaluate(context);

        if (!decision.CanExecute)
        {
            return decision;
        }

        await executor.ExecuteAsync(
            context.Intent,
            cancellationToken);

        return decision;
    }
}
