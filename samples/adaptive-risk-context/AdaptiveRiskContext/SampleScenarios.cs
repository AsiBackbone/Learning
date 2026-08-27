namespace AdaptiveRiskContext;

public static class SampleScenarios
{
    public static readonly DateTimeOffset BaselineUtc =
        new(2032, 4, 5, 10, 0, 0, TimeSpan.Zero);

    public static PaymentContext CreatePayment(
        string paymentId = "pay-981",
        string resourceVersion = "pay-981:v1",
        decimal amount = 250_000m,
        bool destinationApproved = true,
        IncidentPosture incidentPosture = IncidentPosture.Normal,
        string environmentVersion = "env-normal-v1") =>
        new(
            PaymentId: paymentId,
            ResourceVersion: resourceVersion,
            Amount: amount,
            DestinationApproved: destinationApproved,
            IncidentPosture: incidentPosture,
            EnvironmentVersion: environmentVersion);

    public static RiskObservation CreateObservation(
        decimal fraudProbability = 0.21m,
        string observationId = "risk-observation-1001",
        string providerId = "fraud-service",
        string modelId = "fraud-detector",
        string modelVersion = "risk-v7",
        string scoringMethodVersion = "fraud-score-v3",
        string calibrationVersion = "fraud-cal-2026-08",
        ModelHealth modelHealth = ModelHealth.Healthy,
        DateTimeOffset? observedAtUtc = null,
        DateTimeOffset? providerValidUntilUtc = null) =>
        new(
            ObservationId: observationId,
            SignalName: "payment.fraud-probability",
            FraudProbability: fraudProbability,
            ProviderId: providerId,
            ModelId: modelId,
            ModelVersion: modelVersion,
            ScoringMethodVersion: scoringMethodVersion,
            CalibrationVersion: calibrationVersion,
            ModelHealth: modelHealth,
            ObservedAtUtc: observedAtUtc ?? BaselineUtc,
            ProviderValidUntilUtc:
                providerValidUntilUtc ?? BaselineUtc.AddMinutes(10));

    public static RiskGovernancePolicy CreatePolicy(
        string policyVersion = "payment-policy-v12",
        string thresholdVersion = "threshold-v12",
        string freshnessRuleVersion = "freshness-v1",
        IReadOnlySet<string>? approvedSignalNames = null,
        IReadOnlySet<string>? approvedProviderIds = null,
        string requiredModelId = "fraud-detector",
        IReadOnlySet<string>? approvedModelVersions = null,
        TimeSpan? maximumSignalAge = null,
        StaleSignalDisposition staleSignalDisposition =
            StaleSignalDisposition.Reevaluate,
        decimal escalationThreshold = 0.70m,
        decimal denialThreshold = 0.90m) =>
        new(
            PolicyId: "payment-release-risk",
            PolicyVersion: policyVersion,
            ThresholdVersion: thresholdVersion,
            FreshnessRuleVersion: freshnessRuleVersion,
            ApprovedSignalNames:
                approvedSignalNames ??
                new HashSet<string>(
                    ["payment.fraud-probability", "payment.fraud-probability-v2"],
                    StringComparer.Ordinal),
            ApprovedProviderIds:
                approvedProviderIds ??
                new HashSet<string>(
                    ["fraud-service", "backup-fraud-service"],
                    StringComparer.Ordinal),
            RequiredModelId: requiredModelId,
            ApprovedModelVersions:
                approvedModelVersions ??
                new HashSet<string>(
                    ["risk-v7", "risk-v8"],
                    StringComparer.Ordinal),
            MaximumSignalAge:
                maximumSignalAge ?? TimeSpan.FromMinutes(10),
            StaleSignalDisposition: staleSignalDisposition,
            EscalationThreshold: escalationThreshold,
            DenialThreshold: denialThreshold);

    public static ExecutionAuthority CreateAuthority(
        RiskGovernancePolicy? policy = null,
        DateTimeOffset? issuedAtUtc = null,
        TimeSpan? maximumAuthorityLifetime = null,
        string decisionId = "decision-authority-baseline")
    {
        RiskGovernancePolicy selectedPolicy = policy ?? CreatePolicy();
        DateTimeOffset issueTime = issuedAtUtc ?? BaselineUtc.AddMinutes(1);
        RiskPolicyEvaluator evaluator = new();
        GovernanceDecision decision = evaluator.Evaluate(
            decisionId,
            CreatePayment(),
            RiskSignalInput.Available(CreateObservation()),
            selectedPolicy,
            issueTime);

        AuthorityIssueResult issue = new ExecutionAuthorityIssuer().TryIssue(
            decision,
            selectedPolicy,
            issueTime,
            maximumAuthorityLifetime);

        if (!issue.Issued || issue.Authority is null)
        {
            throw new InvalidOperationException(
                $"Teaching fixture failed to issue authority: {issue.ReasonCode}.");
        }

        return issue.Authority;
    }
}
