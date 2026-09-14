using Xunit;

namespace PolicyContextAndExplicitDecisionOutcomes.Tests;

public sealed class DecisionOutcomeTests
{
    [Fact]
    public void DeniedOutcomeIsExplicitAndCannotProceed()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
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
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(maintenanceHoldActive: true));

        Assert.Equal(GovernanceDecisionOutcome.Deferred, decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.maintenance-hold",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void CrossTenantOutcomeIsDeniedWithStableReasonCode()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(accountTenantId: "tenant-b"));

        Assert.Equal(GovernanceDecisionOutcome.Denied, decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.cross-tenant",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void AlreadyDisabledOutcomeIsWarningAndCanProceed()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(isAlreadyDisabled: true));

        Assert.Equal(GovernanceDecisionOutcome.Warning, decision.Outcome);
        Assert.True(decision.CanProceed);
        Assert.Equal(
            "account.disable.already-disabled",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void ProtectedAccountOutcomeRecommendsEscalationAndCannotProceed()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(isProtected: true));

        Assert.Equal(
            GovernanceDecisionOutcome.EscalationRecommended,
            decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.protected-account",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void AcknowledgmentRequiredOutcomeIsExplicitAndCannotProceed()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
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
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext());

        Assert.Equal(GovernanceDecisionOutcome.Allowed, decision.Outcome);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.Reasons);
    }

    [Fact]
    public void WhitespaceReasonRequiresAcknowledgment()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(reason: "   "));

        Assert.Equal(
            GovernanceDecisionOutcome.AcknowledgmentRequired,
            decision.Outcome);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            "account.disable.reason-required",
            Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void AdministratorRequirementTakesPrecedenceOverResourceConditions()
    {
        GovernanceDecision decision = DisableAccountPolicy.Evaluate(
            CreateContext(
                isAdministrator: false,
                isProtected: true,
                isAlreadyDisabled: true,
                maintenanceHoldActive: true,
                reason: string.Empty));

        Assert.Equal(GovernanceDecisionOutcome.Denied, decision.Outcome);
        Assert.Equal(
            "account.disable.not-administrator",
            Assert.Single(decision.Reasons).Code);
    }

    private static DisableAccountPolicyContext CreateContext(
        bool isAdministrator = true,
        string actorTenantId = "tenant-a",
        string accountTenantId = "tenant-a",
        bool isProtected = false,
        bool isAlreadyDisabled = false,
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
                TenantId: actorTenantId,
                IsAdministrator: isAdministrator),
            Account: new AccountContext(
                AccountId: "user-100",
                TenantId: accountTenantId,
                IsProtected: isProtected,
                IsAlreadyDisabled: isAlreadyDisabled),
            Environment: new EnvironmentContext(
                MaintenanceHoldActive: maintenanceHoldActive,
                Region: "us-central"),
            CorrelationId: "test-user-100",
            PolicyVersion: "2.0");
    }
}
