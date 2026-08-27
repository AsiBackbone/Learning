namespace AdaptiveRiskContext;

public static class RiskFreshnessRules
{
    public static DateTimeOffset EffectiveValidUntil(
        RiskObservation observation,
        RiskGovernancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(policy);

        DateTimeOffset hostValidUntil =
            observation.ObservedAtUtc + policy.MaximumSignalAge;
        return observation.ProviderValidUntilUtc < hostValidUntil
            ? observation.ProviderValidUntilUtc
            : hostValidUntil;
    }

    public static bool IsStale(
        RiskObservation observation,
        RiskGovernancePolicy policy,
        DateTimeOffset nowUtc) =>
        nowUtc >= EffectiveValidUntil(observation, policy);
}

public sealed class RiskPolicyEvaluator
{
    public GovernanceDecision Evaluate(
        string decisionId,
        PaymentContext context,
        RiskSignalInput riskInput,
        RiskGovernancePolicy policy,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionId);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(riskInput);
        ArgumentNullException.ThrowIfNull(policy);

        if (!context.DestinationApproved)
        {
            return Decision(
                decisionId,
                DecisionOutcome.Denied,
                "payment.destination-not-approved",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (riskInput.Availability == RiskSignalAvailability.Unavailable)
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.provider-unavailable",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        RiskObservation? observation = riskInput.Observation;
        if (observation is null)
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.observation-missing",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (observation.ObservedAtUtc > nowUtc)
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.observation-not-yet-valid",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (!policy.ApprovedSignalNames.Contains(observation.SignalName))
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.signal-unapproved",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (!policy.ApprovedProviderIds.Contains(riskInput.ProviderId) ||
            !policy.ApprovedProviderIds.Contains(observation.ProviderId))
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.provider-unapproved",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (!string.Equals(
                observation.ModelId,
                policy.RequiredModelId,
                StringComparison.Ordinal) ||
            !policy.ApprovedModelVersions.Contains(observation.ModelVersion))
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.model-unapproved",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (RiskFreshnessRules.IsStale(observation, policy, nowUtc))
        {
            return Decision(
                decisionId,
                DecisionOutcome.Deferred,
                "risk.signal-stale",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (observation.ModelHealth == ModelHealth.Degraded)
        {
            return Decision(
                decisionId,
                DecisionOutcome.EscalationRecommended,
                "risk.model-health-degraded",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (observation.FraudProbability >= policy.DenialThreshold)
        {
            return Decision(
                decisionId,
                DecisionOutcome.Denied,
                "risk.probability-denied",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (observation.FraudProbability >= policy.EscalationThreshold)
        {
            return Decision(
                decisionId,
                DecisionOutcome.EscalationRecommended,
                "risk.probability-escalated",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        if (context.IncidentPosture == IncidentPosture.Elevated &&
            context.Amount >= 100_000m)
        {
            return Decision(
                decisionId,
                DecisionOutcome.EscalationRecommended,
                "risk.incident-posture-escalated",
                context,
                riskInput,
                policy,
                nowUtc);
        }

        return Decision(
            decisionId,
            DecisionOutcome.Allowed,
            "risk.acceptable",
            context,
            riskInput,
            policy,
            nowUtc);
    }

    private static GovernanceDecision Decision(
        string decisionId,
        DecisionOutcome outcome,
        string reasonCode,
        PaymentContext context,
        RiskSignalInput riskInput,
        RiskGovernancePolicy policy,
        DateTimeOffset nowUtc) =>
        new(
            DecisionId: decisionId,
            Outcome: outcome,
            ReasonCode: reasonCode,
            PolicyId: policy.PolicyId,
            PolicyVersion: policy.PolicyVersion,
            ThresholdVersion: policy.ThresholdVersion,
            FreshnessRuleVersion: policy.FreshnessRuleVersion,
            Context: context,
            RiskInput: riskInput,
            DecidedAtUtc: nowUtc);
}
