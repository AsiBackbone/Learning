namespace DecisionExplainability;

public sealed class ExplanationProjector
{
    public const string CurrentProjectionVersion = "decision-explanation-v1";

    private static readonly IReadOnlyDictionary<string, ReasonTemplate> Templates =
        new Dictionary<string, ReasonTemplate>(StringComparer.Ordinal)
        {
            ["regional.data-residency"] = new(
                Outcome: DecisionOutcome.Denied,
                EndUserText:
                    "This operation cannot proceed because regional data-handling requirements apply.",
                EndUserWithholdsKnownDetail: true,
                OperatorText:
                    "The active regional data-residency rule blocked this operation.",
                OperatorWithholdsKnownDetail: true),
            ["tenant.operation-restricted"] = new(
                Outcome: DecisionOutcome.Denied,
                EndUserText: null,
                EndUserWithholdsKnownDetail: true,
                OperatorText:
                    "A tenant-scoped restriction also blocks this operation.",
                OperatorWithholdsKnownDetail: false),
            ["dependency.current-context-unavailable"] = new(
                Outcome: DecisionOutcome.Deferred,
                EndUserText:
                    "Required current information could not be established, so the request is deferred. This is not a policy denial.",
                EndUserWithholdsKnownDetail: false,
                OperatorText:
                    "Current authoritative context is unavailable; the governed result remains deferred rather than denied.",
                OperatorWithholdsKnownDetail: false),
            ["ack.bulk-impact"] = new(
                Outcome: DecisionOutcome.AcknowledgmentRequired,
                EndUserText:
                    "The required acknowledgment must be completed before the operation can be reevaluated for continuation.",
                EndUserWithholdsKnownDetail: false,
                OperatorText:
                    "The current policy requires the bound bulk-impact acknowledgment before reevaluation.",
                OperatorWithholdsKnownDetail: false),
            ["review.security"] = new(
                Outcome: DecisionOutcome.EscalationRecommended,
                EndUserText:
                    "This request requires additional review before it can continue.",
                EndUserWithholdsKnownDetail: false,
                OperatorText:
                    "The current decision routes this request to the configured security review path.",
                OperatorWithholdsKnownDetail: false),
            ["policy.currently-allows"] = new(
                Outcome: DecisionOutcome.Allowed,
                EndUserText:
                    "The current governed decision permits the operation to continue to its next host-owned boundary.",
                EndUserWithholdsKnownDetail: false,
                OperatorText:
                    "The current policy decision is allowed; execution has not been asserted by this explanation.",
                OperatorWithholdsKnownDetail: false),
            ["security.sensitive-signal"] = new(
                Outcome: DecisionOutcome.Denied,
                EndUserText: null,
                EndUserWithholdsKnownDetail: true,
                OperatorText: null,
                OperatorWithholdsKnownDetail: true)
        };

    public ExplanationProjection Project(
        DecisionEvidence evidence,
        ExplanationAudience audience)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        ReasonEvidence[] orderedReasons = evidence.Reasons
            .OrderBy(reason => reason.DisplayPriority)
            .ThenBy(reason => reason.ReasonCode, StringComparer.Ordinal)
            .ToArray();

        List<string> details = [];
        bool withheld = false;
        bool incomplete = false;
        bool unmappedFallbackAdded = false;

        foreach (ReasonEvidence reason in orderedReasons)
        {
            if (!Templates.TryGetValue(reason.ReasonCode, out ReasonTemplate? template) ||
                template is null)
            {
                if (!unmappedFallbackAdded)
                {
                    details.Add(
                        "The governed decision contains additional information that this explanation profile cannot currently describe.");
                    unmappedFallbackAdded = true;
                }

                incomplete = true;
                continue;
            }

            if (template.Outcome != evidence.Outcome)
            {
                details.Add(
                    "The governed decision contains a structured reason that is not compatible with this outcome under the current explanation profile.");
                incomplete = true;
                continue;
            }

            (string? text, bool withholdsKnownDetail) = audience switch
            {
                ExplanationAudience.EndUser =>
                    (template.EndUserText, template.EndUserWithholdsKnownDetail),
                ExplanationAudience.Operator =>
                    (template.OperatorText, template.OperatorWithholdsKnownDetail),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(audience),
                    audience,
                    "Unknown explanation audience.")
            };

            if (text is null)
            {
                withheld = true;
                continue;
            }

            details.Add(text);
            withheld |= withholdsKnownDetail;
        }

        DisclosureStatus disclosureStatus = (withheld, incomplete) switch
        {
            (false, false) => DisclosureStatus.Complete,
            (true, false) => DisclosureStatus.PartiallyWithheld,
            (false, true) => DisclosureStatus.Incomplete,
            (true, true) => DisclosureStatus.PartiallyWithheldAndIncomplete
        };

        string? disclosureNotice = disclosureStatus switch
        {
            DisclosureStatus.PartiallyWithheld =>
                "Some decision details are intentionally withheld for this audience.",
            DisclosureStatus.Incomplete =>
                "This explanation is incomplete because one or more structured reasons have no approved mapping in this projection version.",
            DisclosureStatus.PartiallyWithheldAndIncomplete =>
                "Some decision details are intentionally withheld for this audience, and the explanation is also incomplete because one or more structured reasons have no approved mapping in this projection version.",
            _ => null
        };

        PolicyReference[] sourcePolicies = orderedReasons
            .Select(reason => reason.Policy)
            .Distinct()
            .OrderBy(policy => policy.PolicyId, StringComparer.Ordinal)
            .ThenBy(policy => policy.PolicyVersion, StringComparer.Ordinal)
            .ToArray();

        string[] sourceReasonCodes = orderedReasons
            .Select(reason => reason.ReasonCode)
            .ToArray();

        if (details.Count == 0)
        {
            details.Add(DefaultDetail(evidence.Outcome));
        }

        return new ExplanationProjection(
            DecisionId: evidence.DecisionId,
            Outcome: evidence.Outcome,
            ProjectionVersion: CurrentProjectionVersion,
            Audience: audience,
            Headline: Headline(evidence.Outcome, audience),
            Details: details,
            SourceReasonCodes: sourceReasonCodes,
            SourcePolicies: sourcePolicies,
            DisclosureStatus: disclosureStatus,
            DisclosureNotice: disclosureNotice,
            CorrelationId: evidence.CorrelationId,
            DecidedAtUtc: evidence.DecidedAtUtc);
    }

    private static string Headline(
        DecisionOutcome outcome,
        ExplanationAudience audience) =>
        (outcome, audience) switch
        {
            (DecisionOutcome.Allowed, ExplanationAudience.EndUser) =>
                "This operation may continue to the next governed step.",
            (DecisionOutcome.Allowed, ExplanationAudience.Operator) =>
                "The governed decision currently allows continuation.",
            (DecisionOutcome.Denied, ExplanationAudience.EndUser) =>
                "This operation cannot proceed.",
            (DecisionOutcome.Denied, ExplanationAudience.Operator) =>
                "The governed decision denied the operation.",
            (DecisionOutcome.Deferred, _) =>
                "A current decision cannot be completed yet.",
            (DecisionOutcome.AcknowledgmentRequired, _) =>
                "Acknowledgment is required before reevaluation.",
            (DecisionOutcome.EscalationRecommended, _) =>
                "Additional review is required before continuation.",
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unknown decision outcome.")
        };

    private static string DefaultDetail(DecisionOutcome outcome) => outcome switch
    {
        DecisionOutcome.Allowed =>
            "The current governed decision permits continuation, but this explanation does not assert that execution occurred.",
        DecisionOutcome.Denied =>
            "The current governed decision does not permit the operation.",
        DecisionOutcome.Deferred =>
            "The request remains deferred because a current governed result cannot be established yet.",
        DecisionOutcome.AcknowledgmentRequired =>
            "A bound acknowledgment is required before current policy can be reevaluated for continuation.",
        DecisionOutcome.EscalationRecommended =>
            "The request requires another review path; this does not guarantee approval.",
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome),
            outcome,
            "Unknown decision outcome.")
    };

    private sealed record ReasonTemplate(
        DecisionOutcome Outcome,
        string? EndUserText,
        bool EndUserWithholdsKnownDetail,
        string? OperatorText,
        bool OperatorWithholdsKnownDetail);
}
