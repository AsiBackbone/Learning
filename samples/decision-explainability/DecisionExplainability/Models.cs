namespace DecisionExplainability;

public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    AcknowledgmentRequired,
    EscalationRecommended
}

public enum ExplanationAudience
{
    EndUser,
    Operator
}

public enum DisclosureStatus
{
    Complete,
    PartiallyWithheld,
    Incomplete,
    PartiallyWithheldAndIncomplete
}

public sealed record PolicyReference(
    string PolicyId,
    string PolicyVersion);

public sealed record ReasonEvidence(
    string ReasonCode,
    int DisplayPriority,
    PolicyReference Policy,
    string? ProtectedContextValue = null);

public sealed record DecisionEvidence(
    string DecisionId,
    DecisionOutcome Outcome,
    IReadOnlyList<ReasonEvidence> Reasons,
    string CorrelationId,
    DateTimeOffset DecidedAtUtc);

public sealed record ExplanationProjection(
    string DecisionId,
    DecisionOutcome Outcome,
    string ProjectionVersion,
    ExplanationAudience Audience,
    string Headline,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> SourceReasonCodes,
    IReadOnlyList<PolicyReference> SourcePolicies,
    DisclosureStatus DisclosureStatus,
    string? DisclosureNotice,
    string CorrelationId,
    DateTimeOffset DecidedAtUtc);
