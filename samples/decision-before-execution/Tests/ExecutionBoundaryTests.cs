using Xunit;

namespace DecisionBeforeExecution.Tests;

public sealed class ExecutionBoundaryTests
{
    [Fact]
    public async Task DeniedDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(requesterIsAdministrator: false),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal("account.disable.not-administrator", decision.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task DeferredDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(maintenanceHoldActive: true),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Deferred, decision.Outcome);
        Assert.Equal("account.disable.maintenance-hold", decision.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task AcknowledgmentRequiredDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(reason: string.Empty),
            CancellationToken.None);

        Assert.Equal(
            DecisionOutcome.AcknowledgmentRequired,
            decision.Outcome);
        Assert.Equal("account.disable.reason-required", decision.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task EscalationRecommendedDecisionDoesNotReachExecutor()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(isProtectedAccount: true),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.EscalationRecommended, decision.Outcome);
        Assert.Equal("account.disable.protected-account", decision.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task WhitespaceReasonRequiresAcknowledgmentWithoutExecution()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(reason: "   "),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.AcknowledgmentRequired, decision.Outcome);
        Assert.Equal("account.disable.reason-required", decision.ReasonCode);
        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task AllowedDecisionCrossesExecutionBoundaryExactlyOnce()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(
            executor);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(),
            CancellationToken.None);

        Assert.Equal(DecisionOutcome.Allowed, decision.Outcome);
        Assert.Equal("decision.allowed", decision.ReasonCode);
        Assert.Equal(1, executor.InvocationCount);
    }

    [Fact]
    public async Task CancellationPreventsAllowedExecution()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(executor);
        var cancellationToken = new CancellationToken(canceled: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => workflow.ExecuteAsync(CreateContext(), cancellationToken));

        Assert.Equal(0, executor.InvocationCount);
    }

    [Fact]
    public async Task BlockedDecisionDoesNotNeedToCrossCanceledExecutionBoundary()
    {
        var executor = new RecordingDisableAccountExecutor();
        var workflow = new DisableAccountWorkflow(executor);
        var cancellationToken = new CancellationToken(canceled: true);

        GovernanceDecision decision = await workflow.ExecuteAsync(
            CreateContext(requesterIsAdministrator: false),
            cancellationToken);

        Assert.Equal(DecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(0, executor.InvocationCount);
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
