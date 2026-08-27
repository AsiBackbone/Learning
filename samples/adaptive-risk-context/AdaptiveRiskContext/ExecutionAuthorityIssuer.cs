namespace AdaptiveRiskContext;

public sealed class ExecutionAuthorityIssuer
{
    public AuthorityIssueResult TryIssue(
        GovernanceDecision decision,
        RiskGovernancePolicy policy,
        DateTimeOffset nowUtc,
        TimeSpan? maximumAuthorityLifetime = null)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(policy);

        if (decision.Outcome != DecisionOutcome.Allowed)
        {
            return AuthorityIssueResult.Reject(
                "authority.decision-not-allowed");
        }

        if (decision.RiskInput.Availability != RiskSignalAvailability.Available ||
            decision.RiskInput.Observation is not RiskObservation observation)
        {
            return AuthorityIssueResult.Reject(
                "authority.risk-evidence-missing");
        }

        if (!string.Equals(decision.PolicyId, policy.PolicyId, StringComparison.Ordinal) ||
            !string.Equals(decision.PolicyVersion, policy.PolicyVersion, StringComparison.Ordinal) ||
            !string.Equals(decision.ThresholdVersion, policy.ThresholdVersion, StringComparison.Ordinal) ||
            !string.Equals(decision.FreshnessRuleVersion, policy.FreshnessRuleVersion, StringComparison.Ordinal))
        {
            return AuthorityIssueResult.Reject(
                "authority.policy-mismatch");
        }

        DateTimeOffset riskValidUntil =
            RiskFreshnessRules.EffectiveValidUntil(observation, policy);
        TimeSpan requestedLifetime =
            maximumAuthorityLifetime ?? TimeSpan.FromMinutes(2);
        DateTimeOffset requestedExpiry = nowUtc + requestedLifetime;
        DateTimeOffset authorityExpiry = requestedExpiry < riskValidUntil
            ? requestedExpiry
            : riskValidUntil;

        if (authorityExpiry <= nowUtc)
        {
            return AuthorityIssueResult.Reject(
                "authority.risk-evidence-stale");
        }

        ExecutionAuthority authority = new(
            AuthorityId: $"authority-{decision.DecisionId}",
            DecisionId: decision.DecisionId,
            Audience: PaymentExecutionContract.Audience,
            Operation: PaymentExecutionContract.Operation,
            PaymentId: decision.Context.PaymentId,
            ResourceVersion: decision.Context.ResourceVersion,
            Amount: decision.Context.Amount,
            DestinationApproved: decision.Context.DestinationApproved,
            IncidentPosture: decision.Context.IncidentPosture,
            EnvironmentVersion: decision.Context.EnvironmentVersion,
            RiskObservationId: observation.ObservationId,
            SignalName: observation.SignalName,
            FraudProbability: observation.FraudProbability,
            ProviderId: observation.ProviderId,
            ModelId: observation.ModelId,
            ModelVersion: observation.ModelVersion,
            ScoringMethodVersion: observation.ScoringMethodVersion,
            CalibrationVersion: observation.CalibrationVersion,
            ModelHealth: observation.ModelHealth,
            RiskObservedAtUtc: observation.ObservedAtUtc,
            RiskProviderValidUntilUtc: observation.ProviderValidUntilUtc,
            PolicyId: decision.PolicyId,
            PolicyVersion: decision.PolicyVersion,
            ThresholdVersion: decision.ThresholdVersion,
            FreshnessRuleVersion: decision.FreshnessRuleVersion,
            IssuedAtUtc: nowUtc,
            ExpiresAtUtc: authorityExpiry);

        return AuthorityIssueResult.Success(authority);
    }
}
