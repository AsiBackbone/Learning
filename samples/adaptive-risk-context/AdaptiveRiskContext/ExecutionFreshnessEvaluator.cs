namespace AdaptiveRiskContext;

public sealed class ExecutionFreshnessEvaluator
{
    public FreshnessAssessment Evaluate(
        ExecutionAuthority authority,
        PaymentContext currentContext,
        RiskSignalInput currentRisk,
        RiskGovernancePolicy currentPolicy,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(currentContext);
        ArgumentNullException.ThrowIfNull(currentRisk);
        ArgumentNullException.ThrowIfNull(currentPolicy);

        // Hard execution-boundary failures take precedence over softer drift.
        if (authority.IssuedAtUtc > nowUtc)
        {
            return Reject("authority.not-yet-valid");
        }

        if (authority.ExpiresAtUtc <= nowUtc)
        {
            return Reject("authority.expired");
        }

        if (!string.Equals(
                authority.Audience,
                PaymentExecutionContract.Audience,
                StringComparison.Ordinal))
        {
            return Reject("authority.audience-mismatch");
        }

        if (!string.Equals(
                authority.Operation,
                PaymentExecutionContract.Operation,
                StringComparison.Ordinal))
        {
            return Reject("authority.operation-mismatch");
        }

        if (!string.Equals(
                authority.PaymentId,
                currentContext.PaymentId,
                StringComparison.Ordinal))
        {
            return Reject("authority.resource-mismatch");
        }

        if (!string.Equals(
                authority.PolicyId,
                currentPolicy.PolicyId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authority.PolicyVersion,
                currentPolicy.PolicyVersion,
                StringComparison.Ordinal))
        {
            return Reevaluate("risk.policy-version-drift");
        }

        if (!string.Equals(
                authority.ThresholdVersion,
                currentPolicy.ThresholdVersion,
                StringComparison.Ordinal))
        {
            return Reevaluate("risk.threshold-policy-drift");
        }

        if (!string.Equals(
                authority.FreshnessRuleVersion,
                currentPolicy.FreshnessRuleVersion,
                StringComparison.Ordinal))
        {
            return Reevaluate("risk.freshness-policy-drift");
        }

        if (!string.Equals(
                authority.ResourceVersion,
                currentContext.ResourceVersion,
                StringComparison.Ordinal))
        {
            return Reevaluate("context.resource-drift");
        }

        if (authority.Amount != currentContext.Amount)
        {
            return Reevaluate("context.amount-drift");
        }

        if (authority.DestinationApproved != currentContext.DestinationApproved)
        {
            return Reevaluate("context.destination-drift");
        }

        if (authority.IncidentPosture != currentContext.IncidentPosture)
        {
            return Reevaluate("context.incident-posture-drift");
        }

        if (!string.Equals(
                authority.EnvironmentVersion,
                currentContext.EnvironmentVersion,
                StringComparison.Ordinal))
        {
            return Reevaluate("context.environment-drift");
        }

        if (currentRisk.Availability == RiskSignalAvailability.Unavailable)
        {
            return Defer("risk.provider-unavailable");
        }

        RiskObservation? observation = currentRisk.Observation;
        if (observation is null)
        {
            return Defer("risk.observation-missing");
        }

        if (observation.ObservedAtUtc > nowUtc)
        {
            return Defer("risk.observation-not-yet-valid");
        }

        bool sameObservationIdentity = string.Equals(
            authority.RiskObservationId,
            observation.ObservationId,
            StringComparison.Ordinal);

        // Reusing an observation identity means the captured evidence is expected
        // to be immutable. Detect mutation before softer policy-acceptance states.
        if (sameObservationIdentity &&
            (!string.Equals(authority.SignalName, observation.SignalName, StringComparison.Ordinal) ||
             authority.FraudProbability != observation.FraudProbability ||
             !string.Equals(authority.ProviderId, observation.ProviderId, StringComparison.Ordinal) ||
             !string.Equals(authority.ModelId, observation.ModelId, StringComparison.Ordinal) ||
             !string.Equals(authority.ModelVersion, observation.ModelVersion, StringComparison.Ordinal) ||
             !string.Equals(authority.ScoringMethodVersion, observation.ScoringMethodVersion, StringComparison.Ordinal) ||
             !string.Equals(authority.CalibrationVersion, observation.CalibrationVersion, StringComparison.Ordinal) ||
             authority.ModelHealth != observation.ModelHealth ||
             authority.RiskObservedAtUtc != observation.ObservedAtUtc ||
             authority.RiskProviderValidUntilUtc != observation.ProviderValidUntilUtc))
        {
            return Reject("risk.observation-integrity-mismatch");
        }

        // Current policy decides whether a new/current observation is acceptable
        // evidence. Unapproved is a different fact from approved provenance drift.
        if (!currentPolicy.ApprovedSignalNames.Contains(observation.SignalName))
        {
            return Defer("risk.signal-unapproved");
        }

        if (!currentPolicy.ApprovedProviderIds.Contains(currentRisk.ProviderId) ||
            !currentPolicy.ApprovedProviderIds.Contains(observation.ProviderId))
        {
            return Defer("risk.provider-unapproved");
        }

        if (!string.Equals(
                observation.ModelId,
                currentPolicy.RequiredModelId,
                StringComparison.Ordinal) ||
            !currentPolicy.ApprovedModelVersions.Contains(observation.ModelVersion))
        {
            return Defer("risk.model-unapproved");
        }

        if (!sameObservationIdentity)
        {
            if (!string.Equals(
                    observation.SignalName,
                    authority.SignalName,
                    StringComparison.Ordinal))
            {
                return Reevaluate("risk.signal-drift");
            }

            if (!string.Equals(
                    observation.ProviderId,
                    authority.ProviderId,
                    StringComparison.Ordinal))
            {
                return Reevaluate("risk.provider-drift");
            }

            if (!string.Equals(
                    authority.ModelId,
                    observation.ModelId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    authority.ModelVersion,
                    observation.ModelVersion,
                    StringComparison.Ordinal))
            {
                return Reevaluate("risk.model-drift");
            }

            if (!string.Equals(
                    authority.ScoringMethodVersion,
                    observation.ScoringMethodVersion,
                    StringComparison.Ordinal))
            {
                return Reevaluate("risk.scoring-method-drift");
            }

            if (!string.Equals(
                    authority.CalibrationVersion,
                    observation.CalibrationVersion,
                    StringComparison.Ordinal))
            {
                return Reevaluate("risk.calibration-drift");
            }

            if (authority.ModelHealth != observation.ModelHealth)
            {
                return Reevaluate("risk.model-health-drift");
            }

            return Reevaluate("risk.observation-drift");
        }

        // Staleness applies only after identity/integrity and policy-acceptance
        // checks establish what current observation is actually being evaluated.
        if (RiskFreshnessRules.IsStale(observation, currentPolicy, nowUtc))
        {
            return currentPolicy.StaleSignalDisposition switch
            {
                StaleSignalDisposition.Reevaluate =>
                    Reevaluate("risk.signal-stale"),
                StaleSignalDisposition.Defer =>
                    Defer("risk.signal-stale"),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(currentPolicy),
                    currentPolicy.StaleSignalDisposition,
                    "Unknown stale-signal disposition.")
            };
        }

        return new FreshnessAssessment(
            FreshnessAction.Proceed,
            "freshness.current");
    }

    private static FreshnessAssessment Reevaluate(string reasonCode) =>
        new(FreshnessAction.Reevaluate, reasonCode);

    private static FreshnessAssessment Defer(string reasonCode) =>
        new(FreshnessAction.Defer, reasonCode);

    private static FreshnessAssessment Reject(string reasonCode) =>
        new(FreshnessAction.Reject, reasonCode);
}
