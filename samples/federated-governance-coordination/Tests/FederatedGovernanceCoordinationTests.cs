using FederatedGovernanceCoordination;
using Xunit;

namespace FederatedGovernanceCoordination.Tests;

public sealed class FederatedGovernanceCoordinationTests
{
    [Fact]
    public void AllRequiredAuthoritiesAllowProducesAllowed()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            SampleScenarios.CreateAllowedContributions());

        Assert.Equal(FederatedOutcome.Allowed, decision.Outcome);
        Assert.Equal("federation.allowed", decision.ReasonCode);
        Assert.Equal(2, decision.Evidence.Count);
    }

    [Fact]
    public void ContributionOrderDoesNotChangePeerConflictOutcome()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();
        EvaluationRequest request =
            SampleScenarios.CreateRequest();

        AuthorityContribution cedar =
            SampleScenarios.CreateContribution(
                "cedar-release",
                AuthorityOutcome.Allow);
        AuthorityContribution harbor =
            SampleScenarios.CreateContribution(
                "harbor-intake",
                AuthorityOutcome.Deny);

        FederatedDecision first = service.Evaluate(
            request,
            [cedar, harbor]);
        FederatedDecision second = service.Evaluate(
            request,
            [harbor, cedar]);

        Assert.Equal(FederatedOutcome.Conflict, first.Outcome);
        Assert.Equal(first.Outcome, second.Outcome);
        Assert.Equal(first.ReasonCode, second.ReasonCode);
        Assert.Equal(
            first.Evidence.Select(item => item.AuthorityDomainId).ToArray(),
            second.Evidence.Select(item => item.AuthorityDomainId).ToArray());
    }

    [Fact]
    public void UnavailableRequiredAuthorityDoesNotBecomeDenied()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution("cedar-release"),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    outcome: null,
                    status: ContributionStatus.Unavailable)
            ]);

        Assert.Equal(FederatedOutcome.Deferred, decision.Outcome);
        Assert.NotEqual(FederatedOutcome.Denied, decision.Outcome);
        Assert.Equal(
            "federation.contribution-unavailable",
            decision.ReasonCode);
    }

    [Fact]
    public void UnavailableRequiredAuthorityDoesNotBecomeAllowed()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution("cedar-release"),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    outcome: null,
                    status: ContributionStatus.Unavailable)
            ]);

        Assert.Equal(FederatedOutcome.Deferred, decision.Outcome);
        Assert.NotEqual(FederatedOutcome.Allowed, decision.Outcome);
    }

    [Fact]
    public void InvalidContributionHasExplicitNonExecutingOutcome()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution("cedar-release"),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    outcome: null,
                    status: ContributionStatus.Invalid)
            ]);

        Assert.Equal(FederatedOutcome.Deferred, decision.Outcome);
        Assert.Equal(
            "federation.contribution-invalid",
            decision.ReasonCode);
    }

    [Fact]
    public void AuthoritySetDriftMakesOldFederatedDecisionStale()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();
        ResourceState original =
            SampleScenarios.CreateCrossRegionResource();

        FederatedDecision originalDecision = service.Evaluate(
            SampleScenarios.CreateRequest(resource: original),
            SampleScenarios.CreateAllowedContributions());

        ResourceState moved = original with
        {
            ResourceVersion = "v18",
            CurrentRegion = "delta"
        };

        Assert.True(service.IsCurrent(originalDecision, original));
        Assert.False(service.IsCurrent(originalDecision, moved));
    }

    [Fact]
    public void CoordinatorOutageDoesNotReclassifyFederatedOperationAsLocal()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(
                coordinatorAvailable: false,
                localPolicyAllows: true),
            []);

        Assert.Equal(FederatedOutcome.Deferred, decision.Outcome);
        Assert.Equal(
            "federation.coordinator-unavailable",
            decision.ReasonCode);
        Assert.Equal(
            "records.transfer:cedar:harbor",
            decision.AuthoritySetId);
    }

    [Fact]
    public void PreclassifiedLocalOnlyOperationCanIgnoreCoordinatorOutage()
    {
        FederatedGovernanceService service =
            SampleScenarios.CreateService();

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(
                resource: SampleScenarios.CreateLocalResource(),
                coordinatorAvailable: false,
                localPolicyAllows: true),
            []);

        Assert.Equal(FederatedOutcome.Allowed, decision.Outcome);
        Assert.Equal("local.allowed", decision.ReasonCode);
        Assert.Equal(
            "records.transfer:cedar:local",
            decision.AuthoritySetId);
    }

    [Fact]
    public void PreserveConflictContractProducesConflict()
    {
        FederationContract contract = SampleScenarios.CreateContract(
            DisagreementDisposition.PreserveConflict);
        FederatedGovernanceService service =
            SampleScenarios.CreateService(contract);

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution(
                    "cedar-release",
                    AuthorityOutcome.Allow),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    AuthorityOutcome.Deny)
            ]);

        Assert.Equal(FederatedOutcome.Conflict, decision.Outcome);
        Assert.Equal("federation.peer-conflict", decision.ReasonCode);
    }

    [Fact]
    public void DenialWinsContractProducesDeniedInsteadOfConflict()
    {
        FederationContract contract = SampleScenarios.CreateContract(
            DisagreementDisposition.DenialWins);
        FederatedGovernanceService service =
            SampleScenarios.CreateService(contract);

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution(
                    "cedar-release",
                    AuthorityOutcome.Allow),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    AuthorityOutcome.Deny)
            ]);

        Assert.Equal(FederatedOutcome.Denied, decision.Outcome);
        Assert.Equal(
            "federation.required-authority-denied",
            decision.ReasonCode);
    }

    [Fact]
    public void RouteToEscalationContractProducesEscalationRecommended()
    {
        FederationContract contract = SampleScenarios.CreateContract(
            DisagreementDisposition.RouteToEscalation);
        FederatedGovernanceService service =
            SampleScenarios.CreateService(contract);

        FederatedDecision decision = service.Evaluate(
            SampleScenarios.CreateRequest(),
            [
                SampleScenarios.CreateContribution(
                    "cedar-release",
                    AuthorityOutcome.Allow),
                SampleScenarios.CreateContribution(
                    "harbor-intake",
                    AuthorityOutcome.Deny)
            ]);

        Assert.Equal(
            FederatedOutcome.EscalationRecommended,
            decision.Outcome);
        Assert.Equal(
            "federation.disagreement-escalation-recommended",
            decision.ReasonCode);
    }
}
