using DecisionExplainability;
using Xunit;

namespace DecisionExplainability.Tests;

public sealed class DecisionExplainabilityTests
{
    private readonly ExplanationProjector _projector = new();

    [Fact]
    public void RegionalDataResidencyDenialProducesSafeEndUserExplanation()
    {
        DecisionEvidence evidence = SampleScenarios.RegionalResidencyDenial();

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.Denied, projection.Outcome);
        Assert.Contains("regional data-handling requirements", projection.Details[0]);
        Assert.DoesNotContain(
            SampleScenarios.ProtectedResidencyContext,
            string.Join(" ", projection.Details));
        Assert.Equal(DisclosureStatus.PartiallyWithheld, projection.DisclosureStatus);
    }

    [Fact]
    public void DisclosureStatusIsCatalogOwnedRatherThanPayloadDriven()
    {
        DecisionEvidence evidence = SampleScenarios.RegionalResidencyDenial();
        ReasonEvidence reason = Assert.Single(evidence.Reasons);
        DecisionEvidence evidenceWithoutProtectedPayload = evidence with
        {
            Reasons = [reason with { ProtectedContextValue = null }]
        };

        ExplanationProjection projection = _projector.Project(
            evidenceWithoutProtectedPayload,
            ExplanationAudience.EndUser);

        Assert.Equal(DisclosureStatus.PartiallyWithheld, projection.DisclosureStatus);
        Assert.NotNull(projection.DisclosureNotice);
    }

    [Fact]
    public void OperatorProjectionPreservesPolicyIdentityAndVersionWithoutProtectedContext()
    {
        DecisionEvidence evidence = SampleScenarios.RegionalResidencyDenial();

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.Operator);

        Assert.Equal(
            new PolicyReference("customer-export", "7.3"),
            Assert.Single(projection.SourcePolicies));
        Assert.DoesNotContain(
            SampleScenarios.ProtectedResidencyContext,
            string.Join(" ", projection.Details));
    }

    [Fact]
    public void DeferredExplanationDoesNotClaimPolicyDenial()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.DeferredContextUnavailable(),
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.Deferred, projection.Outcome);
        Assert.Contains("deferred", projection.Details[0]);
        Assert.Contains("not a policy denial", projection.Details[0]);
    }

    [Fact]
    public void AcknowledgmentRequiredExplanationDoesNotClaimApproval()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.AcknowledgmentRequired(),
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.AcknowledgmentRequired, projection.Outcome);
        Assert.Contains("acknowledgment", projection.Headline.ToLowerInvariant());
        Assert.DoesNotContain("approved", ProjectionText(projection).ToLowerInvariant());
    }

    [Fact]
    public void EscalationExplanationDoesNotPromiseApproval()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.EscalationRecommended(),
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.EscalationRecommended, projection.Outcome);
        Assert.Contains("review", ProjectionText(projection).ToLowerInvariant());
        Assert.DoesNotContain("approved", ProjectionText(projection).ToLowerInvariant());
        Assert.DoesNotContain("will be approved", ProjectionText(projection).ToLowerInvariant());
    }

    [Fact]
    public void MultipleReasonsUseDeterministicPresentationOrder()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.MultiReasonDenial(),
            ExplanationAudience.Operator);

        Assert.Equal(2, projection.Details.Count);
        Assert.Contains("regional", projection.Details[0].ToLowerInvariant());
        Assert.Contains("tenant", projection.Details[1].ToLowerInvariant());
        Assert.Equal(
            new[] { "regional.data-residency", "tenant.operation-restricted" },
            projection.SourceReasonCodes.ToArray());
    }

    [Fact]
    public void ReasonInputOrderDoesNotChangeProjection()
    {
        ExplanationProjection first = _projector.Project(
            SampleScenarios.MultiReasonDenial(reverse: false),
            ExplanationAudience.Operator);
        ExplanationProjection second = _projector.Project(
            SampleScenarios.MultiReasonDenial(reverse: true),
            ExplanationAudience.Operator);

        Assert.Equal(first.Headline, second.Headline);
        Assert.Equal(first.Details.ToArray(), second.Details.ToArray());
        Assert.Equal(
            first.SourceReasonCodes.ToArray(),
            second.SourceReasonCodes.ToArray());
        Assert.Equal(
            first.SourcePolicies.ToArray(),
            second.SourcePolicies.ToArray());
    }

    [Theory]
    [InlineData("regional-denial", ExplanationAudience.EndUser)]
    [InlineData("regional-denial", ExplanationAudience.Operator)]
    [InlineData("deferred", ExplanationAudience.EndUser)]
    [InlineData("deferred", ExplanationAudience.Operator)]
    [InlineData("acknowledgment", ExplanationAudience.EndUser)]
    [InlineData("acknowledgment", ExplanationAudience.Operator)]
    [InlineData("escalation", ExplanationAudience.EndUser)]
    [InlineData("escalation", ExplanationAudience.Operator)]
    [InlineData("allowed", ExplanationAudience.EndUser)]
    [InlineData("allowed", ExplanationAudience.Operator)]
    [InlineData("multi-reason-denial", ExplanationAudience.EndUser)]
    [InlineData("multi-reason-denial", ExplanationAudience.Operator)]
    public void ProjectionPreservesDecisionIdentityAndOutcome(
        string scenario,
        ExplanationAudience audience)
    {
        DecisionEvidence evidence = Scenario(scenario);

        ExplanationProjection projection = _projector.Project(
            evidence,
            audience);

        Assert.Equal(evidence.DecisionId, projection.DecisionId);
        Assert.Equal(evidence.Outcome, projection.Outcome);
    }

    [Fact]
    public void AudienceChangesPresentationButNotDecisionLineage()
    {
        DecisionEvidence evidence = SampleScenarios.RegionalResidencyDenial();

        ExplanationProjection endUser = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);
        ExplanationProjection operatorView = _projector.Project(
            evidence,
            ExplanationAudience.Operator);

        Assert.NotEqual(endUser.Headline, operatorView.Headline);
        Assert.NotEqual(endUser.Details[0], operatorView.Details[0]);
        Assert.Equal(endUser.DecisionId, operatorView.DecisionId);
        Assert.Equal(endUser.Outcome, operatorView.Outcome);
        Assert.Equal(
            endUser.SourceReasonCodes.ToArray(),
            operatorView.SourceReasonCodes.ToArray());
        Assert.Equal(
            endUser.SourcePolicies.ToArray(),
            operatorView.SourcePolicies.ToArray());
        Assert.Equal(endUser.CorrelationId, operatorView.CorrelationId);
        Assert.Equal(endUser.DecidedAtUtc, operatorView.DecidedAtUtc);
    }

    [Fact]
    public void OperatorOnlyReasonIsWithheldFromEndUserProjection()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.MultiReasonDenial(),
            ExplanationAudience.EndUser);

        Assert.Single(projection.Details);
        Assert.Equal(DisclosureStatus.PartiallyWithheld, projection.DisclosureStatus);
        Assert.NotNull(projection.DisclosureNotice);
        Assert.Contains("tenant.operation-restricted", projection.SourceReasonCodes);
        Assert.DoesNotContain("tenant-scoped", ProjectionText(projection).ToLowerInvariant());
    }

    [Fact]
    public void SensitiveSignalCanBeWithheldFromOperatorProjection()
    {
        const string protectedSignal = "fraud-score=0.997/internal-model-rule-17";

        DecisionEvidence evidence = new(
            DecisionId: "dec-sensitive-7003",
            Outcome: DecisionOutcome.Denied,
            Reasons:
            [
                new ReasonEvidence(
                    "security.sensitive-signal",
                    10,
                    new PolicyReference("security-controls", "9.1"),
                    protectedSignal)
            ],
            CorrelationId: "corr-sensitive-7003",
            DecidedAtUtc: DateTimeOffset.UnixEpoch);

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.Operator);

        Assert.Equal(DisclosureStatus.PartiallyWithheld, projection.DisclosureStatus);
        Assert.Equal(
            "The current governed decision does not permit the operation.",
            Assert.Single(projection.Details));
        Assert.Contains("security.sensitive-signal", projection.SourceReasonCodes);
        Assert.DoesNotContain(protectedSignal, ProjectionText(projection));
    }

    [Fact]
    public void AllWithheldReasonsUseSafeDefaultDetail()
    {
        DecisionEvidence evidence = new(
            DecisionId: "dec-withheld-7002",
            Outcome: DecisionOutcome.Denied,
            Reasons:
            [
                new ReasonEvidence(
                    "tenant.operation-restricted",
                    10,
                    new PolicyReference("tenant-controls", "4.0"))
            ],
            CorrelationId: "corr-withheld-7002",
            DecidedAtUtc: DateTimeOffset.UnixEpoch);

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);

        Assert.Equal(DisclosureStatus.PartiallyWithheld, projection.DisclosureStatus);
        Assert.Equal(
            "The current governed decision does not permit the operation.",
            Assert.Single(projection.Details));
        Assert.DoesNotContain("tenant-scoped", ProjectionText(projection).ToLowerInvariant());
    }

    [Fact]
    public void WithheldAndUnmappedReasonsPreserveBothDisclosureFacts()
    {
        DecisionEvidence evidence = SampleScenarios.MultiReasonDenial();
        DecisionEvidence mixedEvidence = evidence with
        {
            Reasons =
            [
                .. evidence.Reasons,
                new ReasonEvidence(
                    "future.reason-not-yet-mapped",
                    30,
                    new PolicyReference("future-policy", "1.0"))
            ]
        };

        ExplanationProjection projection = _projector.Project(
            mixedEvidence,
            ExplanationAudience.EndUser);

        Assert.Equal(
            DisclosureStatus.PartiallyWithheldAndIncomplete,
            projection.DisclosureStatus);
        Assert.NotNull(projection.DisclosureNotice);
        Assert.Contains("intentionally withheld", projection.DisclosureNotice);
        Assert.Contains("also incomplete", projection.DisclosureNotice);
    }

    [Fact]
    public void UnknownReasonUsesSafeFallbackAndMarksProjectionIncomplete()
    {
        DecisionEvidence evidence = new(
            DecisionId: "dec-unknown-7001",
            Outcome: DecisionOutcome.Denied,
            Reasons:
            [
                new ReasonEvidence(
                    "future.reason-not-yet-mapped",
                    10,
                    new PolicyReference("future-policy", "1.0"))
            ],
            CorrelationId: "corr-unknown-7001",
            DecidedAtUtc: DateTimeOffset.UnixEpoch);

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);

        Assert.Equal(DisclosureStatus.Incomplete, projection.DisclosureStatus);
        Assert.Contains("cannot currently describe", projection.Details[0]);
        Assert.Contains("future.reason-not-yet-mapped", projection.SourceReasonCodes);
        Assert.DoesNotContain("probably", ProjectionText(projection).ToLowerInvariant());
    }

    [Fact]
    public void MultipleUnknownReasonsUseOneAggregateFallback()
    {
        DecisionEvidence evidence = new(
            DecisionId: "dec-unknown-7004",
            Outcome: DecisionOutcome.Denied,
            Reasons:
            [
                new ReasonEvidence(
                    "future.reason-one",
                    10,
                    new PolicyReference("future-policy-a", "1.0")),
                new ReasonEvidence(
                    "future.reason-two",
                    20,
                    new PolicyReference("future-policy-b", "2.0"))
            ],
            CorrelationId: "corr-unknown-7004",
            DecidedAtUtc: DateTimeOffset.UnixEpoch);

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);

        Assert.Equal(DisclosureStatus.Incomplete, projection.DisclosureStatus);
        Assert.Single(projection.Details);
        Assert.Contains("cannot currently describe", projection.Details[0]);
        Assert.Equal(2, projection.SourceReasonCodes.Count);
        Assert.Contains("future.reason-one", projection.SourceReasonCodes);
        Assert.Contains("future.reason-two", projection.SourceReasonCodes);
    }

    [Fact]
    public void ReasonOutcomeMismatchDoesNotRenderContradictoryCause()
    {
        DecisionEvidence evidence = SampleScenarios.RegionalResidencyDenial() with
        {
            Outcome = DecisionOutcome.Deferred
        };

        ExplanationProjection projection = _projector.Project(
            evidence,
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.Deferred, projection.Outcome);
        Assert.Equal(DisclosureStatus.Incomplete, projection.DisclosureStatus);
        Assert.Contains("not compatible", projection.Details[0]);
        Assert.DoesNotContain(
            "regional data-handling requirements",
            ProjectionText(projection));
    }

    [Fact]
    public void ProjectionVersionIsExplicit()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.RegionalResidencyDenial(),
            ExplanationAudience.EndUser);

        Assert.Equal(
            ExplanationProjector.CurrentProjectionVersion,
            projection.ProjectionVersion);
    }

    [Fact]
    public void NewPolicyDecisionDoesNotRewriteHistoricalProjection()
    {
        DecisionEvidence historicalEvidence = SampleScenarios.RegionalResidencyDenial("7.3");
        ExplanationProjection historical = _projector.Project(
            historicalEvidence,
            ExplanationAudience.Operator);

        DecisionEvidence currentEvidence = SampleScenarios.RegionalResidencyDenial("8.0") with
        {
            DecisionId = "dec-regional-1043"
        };
        ExplanationProjection current = _projector.Project(
            currentEvidence,
            ExplanationAudience.Operator);

        Assert.Equal("dec-regional-1042", historical.DecisionId);
        Assert.Equal(
            new PolicyReference("customer-export", "7.3"),
            Assert.Single(historical.SourcePolicies));
        Assert.Equal("dec-regional-1043", current.DecisionId);
        Assert.Equal(
            new PolicyReference("customer-export", "8.0"),
            Assert.Single(current.SourcePolicies));
    }

    [Fact]
    public void AllowedExplanationDoesNotClaimExecutionOccurred()
    {
        ExplanationProjection projection = _projector.Project(
            SampleScenarios.Allowed(),
            ExplanationAudience.EndUser);

        Assert.Equal(DecisionOutcome.Allowed, projection.Outcome);
        Assert.Contains("continue", ProjectionText(projection).ToLowerInvariant());
        Assert.DoesNotContain("executed", ProjectionText(projection).ToLowerInvariant());
        Assert.DoesNotContain("completed", ProjectionText(projection).ToLowerInvariant());
    }

    private static DecisionEvidence Scenario(string scenario) => scenario switch
    {
        "regional-denial" => SampleScenarios.RegionalResidencyDenial(),
        "deferred" => SampleScenarios.DeferredContextUnavailable(),
        "acknowledgment" => SampleScenarios.AcknowledgmentRequired(),
        "escalation" => SampleScenarios.EscalationRecommended(),
        "allowed" => SampleScenarios.Allowed(),
        "multi-reason-denial" => SampleScenarios.MultiReasonDenial(),
        _ => throw new ArgumentOutOfRangeException(
            nameof(scenario),
            scenario,
            "Unknown sample scenario.")
    };

    private static string ProjectionText(ExplanationProjection projection) =>
        string.Join(
            " ",
            new[] { projection.Headline }
                .Concat(projection.Details)
                .Append(projection.DisclosureNotice ?? string.Empty));
}
