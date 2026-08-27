namespace FederatedGovernanceCoordination;

public enum ContributionStatus
{
    Available,
    Unavailable,
    Invalid,
    Stale
}

public enum AuthorityOutcome
{
    Allow,
    Deny,
    Defer,
    EscalationRecommended
}

public enum FederatedOutcome
{
    Allowed,
    Denied,
    Deferred,
    Conflict,
    EscalationRecommended
}

public enum CoordinationMode
{
    LocalOnly,
    Federated
}

public enum CompositionStrategy
{
    AllRequiredAuthoritiesMustAllow
}

public enum DisagreementDisposition
{
    DenialWins,
    PreserveConflict,
    RouteToEscalation
}

public sealed record ResourceState(
    string ResourceId,
    string ResourceVersion,
    string CurrentRegion,
    string DestinationRegion);

public sealed record AuthoritySetDescriptor(
    string AuthoritySetId,
    string AuthoritySetVersion,
    string ResourceVersion,
    CoordinationMode Mode,
    IReadOnlySet<string> RequiredAuthorityDomains);

public sealed record AuthorityContribution(
    string AuthorityDomainId,
    ContributionStatus Status,
    AuthorityOutcome? Outcome,
    string PolicyId,
    string PolicyVersion,
    string ResourceVersion,
    string ReasonCode);

public sealed record FederationContract(
    string ContractId,
    string ContractVersion,
    CompositionStrategy Strategy,
    DisagreementDisposition DisagreementDisposition);

public sealed record ContributionEvidence(
    string AuthorityDomainId,
    ContributionStatus Status,
    AuthorityOutcome? Outcome,
    string PolicyId,
    string PolicyVersion,
    string ReasonCode);

public sealed record FederatedDecision(
    string DecisionId,
    FederatedOutcome Outcome,
    string ReasonCode,
    string AuthoritySetId,
    string AuthoritySetVersion,
    string ContractId,
    string ContractVersion,
    IReadOnlyList<ContributionEvidence> Evidence);

public sealed record EvaluationRequest(
    ResourceState Resource,
    bool CoordinatorAvailable,
    bool LocalPolicyAllows);
