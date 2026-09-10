namespace FederatedGovernanceCoordination;

public sealed class FederatedGovernanceService(
    AuthoritySetResolver authoritySetResolver,
    FederationCoordinator coordinator,
    FederationContract contract)
{
    public FederatedDecision Evaluate(
        EvaluationRequest request,
        IReadOnlyList<AuthorityContribution> contributions)
    {
        // Resolve governance classification before considering dependency health.
        // An outage must never turn a federated operation into a local-only one.
        AuthoritySetDescriptor authoritySet =
            authoritySetResolver.Resolve(request.Resource);

        if (authoritySet.Mode == CoordinationMode.LocalOnly)
        {
            return new FederatedDecision(
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
                Evidence: []);
        }

        if (!request.CoordinatorAvailable)
        {
            return new FederatedDecision(
                DecisionId: $"fed-{authoritySet.AuthoritySetId}-unavailable",
                Outcome: FederatedOutcome.Deferred,
                ReasonCode: "federation.coordinator-unavailable",
                AuthoritySetId: authoritySet.AuthoritySetId,
                AuthoritySetVersion: authoritySet.AuthoritySetVersion,
                ContractId: contract.ContractId,
                ContractVersion: contract.ContractVersion,
                Evidence: []);
        }

        return coordinator.Compose(
            authoritySet,
            contract,
            contributions);
    }

    public bool IsCurrent(
        FederatedDecision decision,
        ResourceState currentResource)
    {
        AuthoritySetDescriptor currentAuthoritySet =
            authoritySetResolver.Resolve(currentResource);

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
