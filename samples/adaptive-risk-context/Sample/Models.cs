namespace AdaptiveRiskContext;

public enum DecisionOutcome
{
    Allowed,
    Denied,
    Deferred,
    EscalationRecommended
}

public enum RiskSignalAvailability
{
    Available,
    Unavailable
}

public enum ModelHealth
{
    Healthy,
    Degraded
}

public enum IncidentPosture
{
    Normal,
    Elevated
}

public enum StaleSignalDisposition
{
    Reevaluate,
    Defer
}

public enum FreshnessAction
{
    Proceed,
    Reevaluate,
    Defer,
    Reject
}

public static class PaymentExecutionContract
{
    public const string Audience = "payment-release-executor";
    public const string Operation = "payment.release";
}

public sealed record RiskObservation(
    string ObservationId,
    string SignalName,
    decimal FraudProbability,
    string ProviderId,
    string ModelId,
    string ModelVersion,
    string ScoringMethodVersion,
    string CalibrationVersion,
    ModelHealth ModelHealth,
    DateTimeOffset ObservedAtUtc,
    DateTimeOffset ProviderValidUntilUtc);

public sealed record RiskSignalInput(
    RiskSignalAvailability Availability,
    string ProviderId,
    RiskObservation? Observation)
{
    public static RiskSignalInput Available(RiskObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        return new RiskSignalInput(
            RiskSignalAvailability.Available,
            observation.ProviderId,
            observation);
    }

    public static RiskSignalInput Unavailable(string providerId) =>
        new(RiskSignalAvailability.Unavailable, providerId, Observation: null);
}

public sealed record PaymentContext(
    string PaymentId,
    string ResourceVersion,
    decimal Amount,
    bool DestinationApproved,
    IncidentPosture IncidentPosture,
    string EnvironmentVersion);

public sealed record RiskGovernancePolicy(
    string PolicyId,
    string PolicyVersion,
    string ThresholdVersion,
    string FreshnessRuleVersion,
    IReadOnlySet<string> ApprovedSignalNames,
    IReadOnlySet<string> ApprovedProviderIds,
    string RequiredModelId,
    IReadOnlySet<string> ApprovedModelVersions,
    TimeSpan MaximumSignalAge,
    StaleSignalDisposition StaleSignalDisposition,
    decimal EscalationThreshold,
    decimal DenialThreshold);

public sealed record GovernanceDecision(
    string DecisionId,
    DecisionOutcome Outcome,
    string ReasonCode,
    string PolicyId,
    string PolicyVersion,
    string ThresholdVersion,
    string FreshnessRuleVersion,
    PaymentContext Context,
    RiskSignalInput RiskInput,
    DateTimeOffset DecidedAtUtc);

public sealed record ExecutionAuthority(
    string AuthorityId,
    string DecisionId,
    string Audience,
    string Operation,
    string PaymentId,
    string ResourceVersion,
    decimal Amount,
    bool DestinationApproved,
    IncidentPosture IncidentPosture,
    string EnvironmentVersion,
    string RiskObservationId,
    string SignalName,
    decimal FraudProbability,
    string ProviderId,
    string ModelId,
    string ModelVersion,
    string ScoringMethodVersion,
    string CalibrationVersion,
    ModelHealth ModelHealth,
    DateTimeOffset RiskObservedAtUtc,
    DateTimeOffset RiskProviderValidUntilUtc,
    string PolicyId,
    string PolicyVersion,
    string ThresholdVersion,
    string FreshnessRuleVersion,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc);

public sealed record AuthorityIssueResult(
    bool Issued,
    string ReasonCode,
    ExecutionAuthority? Authority)
{
    public static AuthorityIssueResult Success(ExecutionAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        return new AuthorityIssueResult(
            Issued: true,
            ReasonCode: "authority.issued",
            Authority: authority);
    }

    public static AuthorityIssueResult Reject(string reasonCode) =>
        new(
            Issued: false,
            ReasonCode: reasonCode,
            Authority: null);
}

public sealed record FreshnessAssessment(
    FreshnessAction Action,
    string ReasonCode);

public sealed record ValidatedPaymentCommand(
    string PaymentId,
    string ExpectedResourceVersion,
    decimal Amount,
    bool DestinationApproved,
    IncidentPosture IncidentPosture,
    string EnvironmentVersion,
    DateTimeOffset ValidatedAtUtc,
    string DecisionId,
    string AuthorityId,
    string Audience,
    string Operation,
    string RiskObservationId,
    string PolicyId,
    string PolicyVersion,
    string ThresholdVersion,
    string FreshnessRuleVersion);

public sealed record PaymentExecutionAttempt(
    bool Executed,
    string ReasonCode)
{
    public static PaymentExecutionAttempt Success() =>
        new(Executed: true, ReasonCode: "execution.completed");

    public static PaymentExecutionAttempt Reject(string reasonCode) =>
        new(Executed: false, ReasonCode: reasonCode);
}

public sealed record ExecutionResult(
    bool Executed,
    FreshnessAction Action,
    string ReasonCode);
