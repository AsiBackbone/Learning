namespace FederatedGovernanceCoordination;

public sealed class FederatedGovernanceService(
    FederationContract contract)
{
    public FederatedDecision Evaluate(
        EvaluationRequest request,
        IReadOnlyList<AuthorityContribution> contributions)
    {
        // Resolve governance classification before considering dependency health.
        // An outage must never turn a federated operation into a local-only one.
        AuthoritySetDescriptor authoritySet =
            AuthoritySetResolver.Resolve(request.Resource);

        return authoritySet.Mode == CoordinationMode.LocalOnly
            ? new FederatedDecision(
                DecisionId: $"local-{authoritySet.AuthoritySetId}",
                Outcome: request.LocalPolicyAllows
                    ? FederatedOutcome.Allowed
                    : FederatedOutcome.Denied,
                ReasonCode: request.LocalPolicyAllows
                    ? "local.allowed"
                    : "local.denied",
                AuthoritySetId: authoritySet.AuthoritySetId,
                AuthoritySetVersion: authoritySet.AuthoritySetVersion,
                ContractId: contract.ContractId,
                ContractVersion: contract.ContractVersion,
                Evidence: [])
            : !request.CoordinatorAvailable
            ? new FederatedDecision(
                DecisionId: $"fed-{authoritySet.AuthoritySetId}-unavailable",
                Outcome: FederatedOutcome.Deferred,
                ReasonCode: "federation.coordinator-unavailable",
                AuthoritySetId: authoritySet.AuthoritySetId,
                AuthoritySetVersion: authoritySet.AuthoritySetVersion,
                ContractId: contract.ContractId,
                ContractVersion: contract.ContractVersion,
                Evidence: [])
            : FederationCoordinator.Compose(
            authoritySet,
            contract,
            contributions);
    }

    public static bool IsCurrent(
        FederatedDecision decision,
        ResourceState currentResource)
    {
        AuthoritySetDescriptor currentAuthoritySet =
            AuthoritySetResolver.Resolve(currentResource);

        return string.Equals(
                   decision.AuthoritySetId,
                   currentAuthoritySet.AuthoritySetId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   decision.AuthoritySetVersion,
                   currentAuthoritySet.AuthoritySetVersion,
                   StringComparison.Ordinal);
    }
}
