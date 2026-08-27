namespace DecisionExplainability;

public static class SampleScenarios
{
    public const string ProtectedResidencyContext =
        "internal-route=eu-central-1/storage-cluster-42";

    public static DecisionEvidence RegionalResidencyDenial(
        string policyVersion = "7.3") =>
        new(
            DecisionId: "dec-regional-1042",
            Outcome: DecisionOutcome.Denied,
            Reasons:
            [
                new ReasonEvidence(
                    ReasonCode: "regional.data-residency",
                    DisplayPriority: 10,
                    Policy: new PolicyReference(
                        "customer-export",
                        policyVersion),
                    ProtectedContextValue: ProtectedResidencyContext)
            ],
            CorrelationId: "corr-regional-1042",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                0,
                0,
                TimeSpan.Zero));

    public static DecisionEvidence DeferredContextUnavailable() =>
        new(
            DecisionId: "dec-deferred-2001",
            Outcome: DecisionOutcome.Deferred,
            Reasons:
            [
                new ReasonEvidence(
                    "dependency.current-context-unavailable",
                    10,
                    new PolicyReference("customer-export", "7.3"))
            ],
            CorrelationId: "corr-deferred-2001",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                5,
                0,
                TimeSpan.Zero));

    public static DecisionEvidence AcknowledgmentRequired() =>
        new(
            DecisionId: "dec-ack-3001",
            Outcome: DecisionOutcome.AcknowledgmentRequired,
            Reasons:
            [
                new ReasonEvidence(
                    "ack.bulk-impact",
                    10,
                    new PolicyReference("bulk-suspend", "5.0"))
            ],
            CorrelationId: "corr-ack-3001",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                10,
                0,
                TimeSpan.Zero));

    public static DecisionEvidence EscalationRecommended() =>
        new(
            DecisionId: "dec-review-4001",
            Outcome: DecisionOutcome.EscalationRecommended,
            Reasons:
            [
                new ReasonEvidence(
                    "review.security",
                    10,
                    new PolicyReference("high-risk-review", "2.1"))
            ],
            CorrelationId: "corr-review-4001",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                15,
                0,
                TimeSpan.Zero));

    public static DecisionEvidence Allowed() =>
        new(
            DecisionId: "dec-allow-5001",
            Outcome: DecisionOutcome.Allowed,
            Reasons:
            [
                new ReasonEvidence(
                    "policy.currently-allows",
                    10,
                    new PolicyReference("customer-export", "7.3"))
            ],
            CorrelationId: "corr-allow-5001",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                20,
                0,
                TimeSpan.Zero));

    public static DecisionEvidence MultiReasonDenial(bool reverse = false)
    {
        ReasonEvidence regional = new(
            "regional.data-residency",
            10,
            new PolicyReference("customer-export", "7.3"),
            ProtectedResidencyContext);

        ReasonEvidence tenant = new(
            "tenant.operation-restricted",
            20,
            new PolicyReference("tenant-controls", "4.0"));

        ReasonEvidence[] reasons = reverse
            ? [tenant, regional]
            : [regional, tenant];

        return new DecisionEvidence(
            DecisionId: "dec-multi-6001",
            Outcome: DecisionOutcome.Denied,
            Reasons: reasons,
            CorrelationId: "corr-multi-6001",
            DecidedAtUtc: new DateTimeOffset(
                2032,
                4,
                5,
                14,
                25,
                0,
                TimeSpan.Zero));
    }
}
