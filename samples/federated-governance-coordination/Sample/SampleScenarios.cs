namespace FederatedGovernanceCoordination;

public static class SampleScenarios
{
    public static FederationContract CreateContract(
        DisagreementDisposition disagreementDisposition =
            DisagreementDisposition.PreserveConflict) =>
        new(
            ContractId: "records-transfer-federation",
            ContractVersion: "4",
            Strategy:
                CompositionStrategy.AllRequiredAuthoritiesMustAllow,
            DisagreementDisposition: disagreementDisposition);

    public static FederatedGovernanceService CreateService(
        FederationContract? contract = null) =>
        new(
            new AuthoritySetResolver(),
            new FederationCoordinator(),
            contract ?? CreateContract());

    public static ResourceState CreateCrossRegionResource(
        string currentRegion = "cedar",
        string destinationRegion = "harbor",
        string resourceVersion = "v17") =>
        new(
            ResourceId: "record-204",
            ResourceVersion: resourceVersion,
            CurrentRegion: currentRegion,
            DestinationRegion: destinationRegion);

    public static ResourceState CreateLocalResource(
        string region = "cedar",
        string resourceVersion = "v17") =>
        CreateCrossRegionResource(
            currentRegion: region,
            destinationRegion: region,
            resourceVersion: resourceVersion);

    public static EvaluationRequest CreateRequest(
        ResourceState? resource = null,
        bool coordinatorAvailable = true,
        bool localPolicyAllows = true) =>
        new(
            resource ?? CreateCrossRegionResource(),
            coordinatorAvailable,
            localPolicyAllows);

    public static AuthorityContribution CreateContribution(
        string authorityDomainId,
        AuthorityOutcome? outcome = AuthorityOutcome.Allow,
        ContributionStatus status = ContributionStatus.Available,
        string resourceVersion = "v17") =>
        new(
            AuthorityDomainId: authorityDomainId,
            Status: status,
            Outcome: outcome,
            PolicyId: $"{authorityDomainId}.records-transfer",
            PolicyVersion: "7",
            ResourceVersion: resourceVersion,
            ReasonCode: status == ContributionStatus.Available
                ? $"{authorityDomainId}.evaluated"
                : $"{authorityDomainId}.{status.ToString().ToLowerInvariant()}");

    public static AuthorityContribution[] CreateAllowedContributions() =>
    [
        CreateContribution("cedar-release"),
        CreateContribution("harbor-intake")
    ];
}
