using Xunit;

namespace DecisionBeforeExecution.Tests;

public sealed class ExecutionBoundaryTests
{
    [Fact]
    public async Task DeniedDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            new DisableAccountPolicy(),
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(requesterIsAdministrator: false),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DeferredDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            new DisableAccountPolicy(),
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(maintenanceHoldActive: true),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task AcknowledgmentRequiredDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            new DisableAccountPolicy(),
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(reason: string.Empty),
            CancellationToken.None);

        Assert.Equal(
            DecisionOutcome.AcknowledgmentRequired,
            decision.Outcome);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task AllowedDecisionCrossesExecutionBoundaryExactlyOnce()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            new DisableAccountPolicy(),
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
        Assert.Equal(1, executor.InvocationCount);
    }

    private static DisableAccountContext CreateContext(
        bool requesterIsAdministrator = true,
        bool isProtectedAccount = false,
        bool maintenanceHoldActive = false,
        string reason = "Security investigation")
    {
        var intent = new DisableAccountIntent(
            AccountId: "user-100",
            RequestedBy: "operator-7",
            Reason: reason);

        return new DisableAccountContext(
            Intent: intent,
            RequesterIsAdministrator: requesterIsAdministrator,
            IsProtectedAccount: isProtectedAccount,
            MaintenanceHoldActive: maintenanceHoldActive,
            CorrelationId: "test-user-100",
            PolicyVersion: "1.0");
    }
}
