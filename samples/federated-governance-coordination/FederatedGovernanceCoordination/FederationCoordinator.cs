namespace FederatedGovernanceCoordination;

public sealed class FederationCoordinator
{
    public FederatedDecision Compose(
        AuthoritySetDescriptor authoritySet,
        FederationContract contract,
        IReadOnlyList<AuthorityContribution> contributions)
    {
        if (authoritySet.Mode != CoordinationMode.Federated)
        {
            throw new InvalidOperationException(
                "Federated composition requires a federated authority set.");
        }

        if (contract.Strategy !=
            CompositionStrategy.AllRequiredAuthoritiesMustAllow)
        {
            throw new InvalidOperationException(
                "The teaching coordinator implements only the mandatory-gate strategy.");
        }

        Dictionary<string, AuthorityContribution> byDomain = new(
            StringComparer.Ordinal);

        foreach (AuthorityContribution contribution in contributions)
        {
            if (!byDomain.TryAdd(
                    contribution.AuthorityDomainId,
                    contribution))
            {
                return Decision(
                    authoritySet,
                    contract,
                    FederatedOutcome.Deferred,
                    "federation.duplicate-contribution",
                    contributions);
            }
        }

        foreach (string requiredDomain in
                 authoritySet.RequiredAuthorityDomains.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            if (!byDomain.TryGetValue(
                    requiredDomain,
                    out AuthorityContribution? contribution) ||
                contribution is null)
            {
                return Decision(
                    authoritySet,
                    contract,
                    FederatedOutcome.Deferred,
                    "federation.contribution-missing",
                    contributions);
            }

            if (!string.Equals(
                    contribution.ResourceVersion,
                    authoritySet.ResourceVersion,
                    StringComparison.Ordinal))
            {
                return Decision(
                    authoritySet,
                    contract,
                    FederatedOutcome.Deferred,
                    "federation.contribution-stale",
                    contributions);
            }

            if (contribution.Status != ContributionStatus.Available)
            {
                return Decision(
                    authoritySet,
                    contract,
                    FederatedOutcome.Deferred,
                    contribution.Status switch
                    {
                        ContributionStatus.Unavailable =>
                            "federation.contribution-unavailable",
                        ContributionStatus.Invalid =>
                            "federation.contribution-invalid",
                        ContributionStatus.Stale =>
                            "federation.contribution-stale",
                        _ => "federation.contribution-unacceptable"
                    },
                    contributions);
            }

            if (contribution.Outcome is null)
            {
                return Decision(
                    authoritySet,
                    contract,
                    FederatedOutcome.Deferred,
                    "federation.contribution-invalid",
                    contributions);
            }
        }

        AuthorityContribution[] required = authoritySet
            .RequiredAuthorityDomains
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(domain => byDomain[domain])
            .ToArray();

        if (required.Any(
                contribution =>
                    contribution.Outcome ==
                    AuthorityOutcome.EscalationRecommended))
        {
            return Decision(
                authoritySet,
                contract,
                FederatedOutcome.EscalationRecommended,
                "federation.escalation-recommended",
                required);
        }

        if (required.Any(
                contribution =>
                    contribution.Outcome == AuthorityOutcome.Defer))
        {
            return Decision(
                authoritySet,
                contract,
                FederatedOutcome.Deferred,
                "federation.authority-deferred",
                required);
        }

        bool anyAllowed = required.Any(
            contribution =>
                contribution.Outcome == AuthorityOutcome.Allow);
        bool anyDenied = required.Any(
            contribution =>
                contribution.Outcome == AuthorityOutcome.Deny);

        if (anyAllowed &&
            anyDenied &&
            contract.DisagreementDisposition ==
                DisagreementDisposition.PreserveConflict)
        {
            return Decision(
                authoritySet,
                contract,
                FederatedOutcome.Conflict,
                "federation.peer-conflict",
                required);
        }

        if (anyAllowed &&
            anyDenied &&
            contract.DisagreementDisposition ==
                DisagreementDisposition.RouteToEscalation)
        {
            return Decision(
                authoritySet,
                contract,
                FederatedOutcome.EscalationRecommended,
                "federation.disagreement-escalation-recommended",
                required);
        }

        if (anyDenied)
        {
            return Decision(
                authoritySet,
                contract,
                FederatedOutcome.Denied,
                "federation.required-authority-denied",
                required);
        }

        return Decision(
            authoritySet,
            contract,
            FederatedOutcome.Allowed,
            "federation.allowed",
            required);
    }

    private static FederatedDecision Decision(
        AuthoritySetDescriptor authoritySet,
        FederationContract contract,
        FederatedOutcome outcome,
        string reasonCode,
        IEnumerable<AuthorityContribution> contributions)
    {
        ContributionEvidence[] evidence = contributions
            .OrderBy(
                contribution => contribution.AuthorityDomainId,
                StringComparer.Ordinal)
            .Select(
                contribution => new ContributionEvidence(
                    contribution.AuthorityDomainId,
                    contribution.Status,
                    contribution.Outcome,
                    contribution.PolicyId,
                    contribution.PolicyVersion,
                    contribution.ReasonCode))
            .ToArray();

        return new FederatedDecision(
            DecisionId:
                $"fed-{authoritySet.AuthoritySetId}-{contract.ContractVersion}",
            Outcome: outcome,
            ReasonCode: reasonCode,
            AuthoritySetId: authoritySet.AuthoritySetId,
            AuthoritySetVersion: authoritySet.AuthoritySetVersion,
            ContractId: contract.ContractId,
            ContractVersion: contract.ContractVersion,
            Evidence: evidence);
    }
}
