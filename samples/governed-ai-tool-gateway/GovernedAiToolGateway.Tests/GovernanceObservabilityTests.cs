using Xunit;

namespace GovernedAiToolGateway.Tests;

public sealed class GovernanceObservabilityTests
{
    [Fact]
    public async Task AllowedProposalTraceReachesExecutor()
    {
        GovernanceObservabilityRun run =
            await GovernanceObservabilityRunner.RunAsync(
                GovernanceObservabilityScenario.Allowed,
                "corr-test-allowed");

        Assert.Equal(GatewayStatus.WouldExecute, run.Result.Status);
        Assert.Equal("corr-test-allowed", run.Result.CorrelationId);
        Assert.Equal(1, run.ExecutorInvocationCount);
        Assert.Contains(
            run.Activities,
            activity => activity.Name == "model.inference");
        Assert.Contains(
            run.Activities,
            activity => activity.Name == "host.governance-gateway");
        Assert.Contains(
            run.Activities,
            activity => activity.Name == "executor.invoke");
        Assert.Contains(
            run.AuditEntries,
            entry =>
                entry.Stage == "proposal-validation" &&
                entry.Outcome == "valid");
        Assert.Contains(
            run.AuditEntries,
            entry =>
                entry.Stage == "decision" &&
                entry.Outcome == "Allowed" &&
                entry.PolicyVersion == "5.0");
        Assert.Contains(
            run.AuditEntries,
            entry => entry.Stage == "execution");

        string traceId = Assert.Single(
            run.Activities,
            activity => activity.Name == "ai.governance.workflow").TraceId;

        Assert.All(
            run.Activities,
            activity => Assert.Equal(traceId, activity.TraceId));
    }

    [Fact]
    public async Task DeniedProposalTraceProvesZeroExecutorCalls()
    {
        GovernanceObservabilityRun run =
            await GovernanceObservabilityRunner.RunAsync(
                GovernanceObservabilityScenario.Denied,
                "corr-test-denied");

        Assert.Equal(GatewayStatus.Blocked, run.Result.Status);
        Assert.Equal(
            "notification.destination-blocked",
            run.Result.ReasonCode);
        Assert.Equal(0, run.ExecutorInvocationCount);
        Assert.DoesNotContain(
            run.Activities,
            activity => activity.Name == "executor.invoke");
        Assert.Contains(
            run.AuditEntries,
            entry =>
                entry.Stage == "decision" &&
                entry.Outcome == "Denied" &&
                entry.ReasonCode ==
                    "notification.destination-blocked");
        Assert.DoesNotContain(
            run.AuditEntries,
            entry => entry.Stage == "capability-issued");
        Assert.DoesNotContain(
            run.AuditEntries,
            entry => entry.Stage == "execution");
    }

    [Fact]
    public async Task AcknowledgmentTracePrecedesCapabilityAndExecution()
    {
        GovernanceObservabilityRun run =
            await GovernanceObservabilityRunner.RunAsync(
                GovernanceObservabilityScenario.AcknowledgmentRequired,
                "corr-test-ack");

        Assert.Equal(GatewayStatus.WouldExecute, run.Result.Status);
        Assert.Equal(1, run.ExecutorInvocationCount);
        Assert.NotNull(run.Result.CapabilityId);
        Assert.Contains(
            run.Activities,
            activity => activity.Name == "acknowledgment.respond");
        Assert.Contains(
            run.Activities,
            activity => activity.Name == "executor.invoke");

        int acceptedAcknowledgment = Array.FindIndex(
            run.AuditEntries.ToArray(),
            entry =>
                entry.Stage == "acknowledgment" &&
                entry.Outcome == "accepted");
        int capabilityIssued = Array.FindIndex(
            run.AuditEntries.ToArray(),
            entry => entry.Stage == "capability-issued");
        int execution = Array.FindIndex(
            run.AuditEntries.ToArray(),
            entry => entry.Stage == "execution");

        Assert.True(acceptedAcknowledgment >= 0);
        Assert.True(capabilityIssued > acceptedAcknowledgment);
        Assert.True(execution > capabilityIssued);
        Assert.Contains(
            run.AuditEntries,
            entry =>
                entry.Stage == "re-evaluation" &&
                entry.Outcome == "Allowed" &&
                entry.PolicyVersion == "5.0");
    }
}
