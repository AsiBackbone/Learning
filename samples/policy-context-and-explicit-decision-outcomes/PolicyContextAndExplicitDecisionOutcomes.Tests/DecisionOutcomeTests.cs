using Xunit;

namespace PolicyContextAndExplicitDecisionOutcomes.Tests;

public sealed class DecisionOutcomeTests
{
    [Fact]
    public void DeniedOutcomeIsExplicitAndCannotProceed()
    {
        GovernanceDecision decision = new DisableAccountPolicy().Evaluate(
            CreateContext(isAdministrator: false));

        Assert.Equal(GovernanceDecisionOutcome.Denied, decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.not-administrator",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void DeferredOutcomeIsExplicitAndCannotProceed()
    {
        GovernanceDecision decision = new DisableAccountPolicy().Evaluate(
            CreateContext(maintenanceHoldActive: true));

        Assert.Equal(GovernanceDecisionOutcome.Deferred, decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.maintenance-hold",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void AcknowledgmentRequiredOutcomeIsExplicitAndCannotProceed()
    {
        GovernanceDecision decision = new DisableAccountPolicy().Evaluate(
            CreateContext(reason: string.Empty));

        Assert.Equal(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.reason-required",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void AllowedOutcomeCanProceedWithoutReasonCodes()
    {
        GovernanceDecision decision = new DisableAccountPolicy().Evaluate(
            CreateContext());

        Assert.Equal(GovernanceDecisionOutcome.Allowed, decision.Outcome);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.Reasons);
    }

    private static DisableAccountPolicyContext CreateContext(
        bool isAdministrator = true,
        bool maintenanceHoldActive = false,
        string reason = "Security investigation")
    {
        return new DisableAccountPolicyContext(
            Intent: new DisableAccountIntent(
                AccountId: "user-100",
                RequestedBy: "operator-7",
                Reason: reason),
            Actor: new ActorContext(
                ActorId: "operator-7",
                TenantId: "tenant-a",
                IsAdministrator: isAdministrator),
            Account: new AccountContext(
                AccountId: "user-100",
                TenantId: "tenant-a",
                IsProtected: false,
                IsAlreadyDisabled: false),
            Environment: new EnvironmentContext(
                MaintenanceHoldActive: maintenanceHoldActive,
                Region: "us-central"),
            CorrelationId: "test-user-100",
            PolicyVersion: "2.0");
    }
}
