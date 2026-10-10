namespace GovernedFailureInjectionTrace;

/// <summary>
/// One named run: the ordinary inputs, the single injected failure, and the result the
/// sample expects, so a run can report whether it behaved as taught.
/// </summary>
public sealed record FailureScenario(
    string Id,
    string Summary,
    string Region,
    RiskLevel Risk,
    bool AcknowledgmentProvided,
    FailureInjection Injection,
    TraceOutcome ExpectedOutcome,
    string ExpectedReasonCode,
    int ExpectedExecutorInvocations);

public static class FailureScenarios
{
    public static IReadOnlyList<FailureScenario> All { get; } =
    [
        new(
            "allowed-executes-once",
            "Low-risk US export with no injected failure reaches the executor exactly once.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.None,
            TraceOutcome.Executed, "execution.completed", 1),
        new(
            "policy-denied",
            "Authoritative context reports high risk; policy denies before any authority exists.",
            "US", RiskLevel.High, AcknowledgmentProvided: false, FailureInjection.None,
            TraceOutcome.Denied, "export.risk-high", 0),
        new(
            "context-unavailable",
            "The context store cannot answer; policy returns an explicit Deferred, not a denial.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.ContextUnavailable,
            TraceOutcome.Deferred, "context.unavailable", 0),
        new(
            "acknowledgment-missing",
            "EU export requires acknowledgment and none was given; progress waits for the continuation.",
            "EU", RiskLevel.Low, AcknowledgmentProvided: true, FailureInjection.OmitAcknowledgment,
            TraceOutcome.ContinuationRequired, "continuation.acknowledgment-missing", 0),
        new(
            "acknowledged-continuation",
            "EU export with a valid acknowledgment bound to the receipt and actor executes once.",
            "EU", RiskLevel.Low, AcknowledgmentProvided: true, FailureInjection.None,
            TraceOutcome.Executed, "execution.completed", 1),
        new(
            "receipt-tampered",
            "The caller flips the receipt outcome to Allowed to skip acknowledgment; the seal no longer matches.",
            "EU", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.TamperReceipt,
            TraceOutcome.VerificationFailed, "receipt.seal-mismatch", 0),
        new(
            "receipt-expired",
            "The continuation arrives after the decision receipt expired.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.ExpireReceipt,
            TraceOutcome.VerificationFailed, "receipt.expired", 0),
        new(
            "receipt-intent-mismatch",
            "An intact receipt for a different intent and resource is presented for this one.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.ReceiptForDifferentIntent,
            TraceOutcome.VerificationFailed, "receipt.intent-mismatch", 0),
        new(
            "authority-expired",
            "Authority is issued, but the host is reached after it expired.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.ExpireAuthority,
            TraceOutcome.EnforcementRefused, "authority.expired", 0),
        new(
            "authority-replayed",
            "Authority already consumed by an earlier execution is presented again.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.ReplayAuthority,
            TraceOutcome.EnforcementRefused, "authority.already-used", 0),
        new(
            "authority-wrong-audience",
            "Validly issued authority bound to a different host is presented to the export host.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.WrongAudience,
            TraceOutcome.EnforcementRefused, "authority.audience-mismatch", 0),
        new(
            "authority-wrong-operation",
            "Validly issued authority for a different operation is presented for the export.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.WrongOperation,
            TraceOutcome.EnforcementRefused, "authority.operation-mismatch", 0),
        new(
            "bypass-entry-point",
            "A caller skips the governed entry point and calls the host with fabricated authority.",
            "US", RiskLevel.Low, AcknowledgmentProvided: false, FailureInjection.BypassEntryPoint,
            TraceOutcome.EnforcementRefused, "authority.not-issued", 0)
    ];

    public static FailureScenario? Find(string id)
    {
        return All.FirstOrDefault(scenario => string.Equals(scenario.Id, id, StringComparison.Ordinal));
    }

    public static bool MeetsExpectation(FailureScenario scenario, TraceResult result)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(result);

        return result.Outcome == scenario.ExpectedOutcome &&
               string.Equals(result.ReasonCode, scenario.ExpectedReasonCode, StringComparison.Ordinal) &&
               result.ExecutorInvocations == scenario.ExpectedExecutorInvocations;
    }
}
