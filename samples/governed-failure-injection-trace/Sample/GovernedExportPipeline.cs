namespace GovernedFailureInjectionTrace;

/// <summary>
/// Runs one fictional customer export through every trust boundary and records which
/// stages were entered, who owned each one, and where progress stopped.
/// </summary>
public static class GovernedExportPipeline
{
    public static DateTimeOffset BaselineUtc { get; } = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private static readonly TimeSpan _receiptLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan _authorityLifetime = TimeSpan.FromMinutes(2);

    public static ExportIntent CreateIntent()
    {
        return new(
            IntentId: "intent-5310",
            ActorId: "analyst-7",
            ResourceId: "customer-batch-42",
            Operation: ExportContract.Operation);
    }

    public static TraceResult Run(FailureScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        // Every run gets fresh components, so no run can observe state from another.
        IssuedAuthorityRegistry registry = new();
        AuthorityUseStore useStore = new();
        CountingExecutor executor = new();
        ProtectedHost host = new(registry, useStore, executor);
        AuthorityIssuer issuer = new(registry);
        TraceRecorder trace = new(scenario);

        ExportIntent intent = CreateIntent();
        DateTimeOffset decidedAt = BaselineUtc;
        DateTimeOffset verifiedAt = decidedAt.AddMinutes(1);
        DateTimeOffset enforcedAt = decidedAt.AddMinutes(2);

        if (scenario.Injection == FailureInjection.BypassEntryPoint)
        {
            return RunBypass(intent, host, executor, trace, enforcedAt);
        }

        // Stage 1: the governed entry point accepts only the operation it governs.
        if (!string.Equals(intent.Operation, ExportContract.Operation, StringComparison.Ordinal))
        {
            trace.Record(TraceStage.Intent, StageStatus.Refused, "intent.operation-unsupported");
            return trace.Stop(TraceOutcome.Denied, "intent.operation-unsupported", executor);
        }

        trace.Record(TraceStage.Intent, StageStatus.Completed, "intent.accepted");

        // Stage 2: authoritative facts come from the store, not from the caller.
        AuthoritativeContextStore contextStore = new(
            new AuthoritativeContext("tenant-a", scenario.Region, scenario.Risk),
            available: scenario.Injection != FailureInjection.ContextUnavailable);
        AuthoritativeContext? context = contextStore.Load(intent);

        trace.Record(
            TraceStage.AuthoritativeContext,
            context is null ? StageStatus.Unavailable : StageStatus.Completed,
            context is null ? "context.unavailable" : "context.loaded");

        // Stage 3: policy turns the facts into an explicit decision and records it.
        PolicyDecision decision = PolicyEvaluator.Evaluate(context);
        DecisionReceipt receipt = CreateReceipt(intent, decision, decidedAt);
        trace.RecordDecision(receipt);

        switch (decision.Outcome)
        {
            case PolicyOutcome.Denied:
                trace.Record(TraceStage.PolicyDecision, StageStatus.Refused, decision.ReasonCode);
                return trace.Stop(TraceOutcome.Denied, decision.ReasonCode, executor);

            case PolicyOutcome.Deferred:
                trace.Record(TraceStage.PolicyDecision, StageStatus.Deferred, decision.ReasonCode);
                return trace.Stop(TraceOutcome.Deferred, decision.ReasonCode, executor);

            case PolicyOutcome.AcknowledgmentRequired:
                trace.Record(TraceStage.PolicyDecision, StageStatus.AwaitingContinuation, decision.ReasonCode);
                break;

            case PolicyOutcome.Allowed:
            default:
                trace.Record(TraceStage.PolicyDecision, StageStatus.Completed, decision.ReasonCode);
                break;
        }

        // Stage 4: the continuation presents decision evidence, which is verified before
        // any authority exists.
        DecisionReceipt presented = PresentReceipt(scenario.Injection, receipt, intent, decidedAt);
        AcknowledgmentRecord? acknowledgment =
            scenario.AcknowledgmentProvided && scenario.Injection != FailureInjection.OmitAcknowledgment
                ? new AcknowledgmentRecord(receipt.ReceiptId, intent.ActorId, verifiedAt)
                : null;
        DateTimeOffset verificationTime = scenario.Injection == FailureInjection.ExpireReceipt
            ? receipt.ExpiresAtUtc.AddMinutes(1)
            : verifiedAt;

        VerificationResult verification = ContinuationVerifier.Verify(
            presented,
            intent,
            acknowledgment,
            verificationTime);
        trace.Record(TraceStage.ContinuationVerification, verification.Status, verification.ReasonCode);

        if (verification.StopOutcome is { } stopOutcome)
        {
            return trace.Stop(stopOutcome, verification.ReasonCode, executor);
        }

        // Stage 5: narrow authority for this operation, resource, and host only.
        ScopedAuthority authority = issuer.Issue(
            presented,
            audience: scenario.Injection == FailureInjection.WrongAudience
                ? "reporting-host"
                : ExportContract.HostAudience,
            operation: scenario.Injection == FailureInjection.WrongOperation
                ? "customer-report.read"
                : ExportContract.Operation,
            expiresAtUtc: verifiedAt + _authorityLifetime);
        trace.RecordAuthorityIssued(authority);

        if (scenario.Injection == FailureInjection.ReplayAuthority)
        {
            // An earlier execution already consumed this authority.
            useStore.TryConsume(authority.AuthorityId);
        }

        // Stages 6 and 7: the host validates the authority and alone decides whether
        // the executor runs.
        DateTimeOffset hostTime = scenario.Injection == FailureInjection.ExpireAuthority
            ? authority.ExpiresAtUtc.AddMinutes(1)
            : enforcedAt;

        return trace.Finish(host.Execute(authority, intent, hostTime), executor);
    }

    private static TraceResult RunBypass(
        ExportIntent intent,
        ProtectedHost host,
        CountingExecutor executor,
        TraceRecorder trace,
        DateTimeOffset enforcedAt)
    {
        foreach (TraceStage skipped in new[]
                 {
                     TraceStage.Intent,
                     TraceStage.AuthoritativeContext,
                     TraceStage.PolicyDecision,
                     TraceStage.ContinuationVerification,
                     TraceStage.AuthorityIssuance
                 })
        {
            trace.Record(skipped, StageStatus.SkippedByCaller, null);
        }

        // The caller fabricates authority that looks well formed but was never issued.
        ScopedAuthority fabricated = new(
            AuthorityId: "auth-fabricated",
            ReceiptId: "rcpt-none",
            Audience: ExportContract.HostAudience,
            Operation: ExportContract.Operation,
            ResourceId: intent.ResourceId,
            ExpiresAtUtc: enforcedAt.AddMinutes(5));

        return trace.Finish(host.Execute(fabricated, intent, enforcedAt), executor);
    }

    private static DecisionReceipt CreateReceipt(
        ExportIntent intent,
        PolicyDecision decision,
        DateTimeOffset decidedAt)
    {
        return SimulatedReceiptSeal.Apply(
            new DecisionReceipt(
                ReceiptId: $"rcpt-{intent.IntentId}",
                IntentId: intent.IntentId,
                ActorId: intent.ActorId,
                ResourceId: intent.ResourceId,
                Operation: intent.Operation,
                Outcome: decision.Outcome,
                ReasonCode: decision.ReasonCode,
                PolicyId: ExportContract.PolicyId,
                PolicyVersion: ExportContract.PolicyVersion,
                IssuedAtUtc: decidedAt,
                ExpiresAtUtc: decidedAt + _receiptLifetime,
                Seal: string.Empty));
    }

    private static DecisionReceipt PresentReceipt(
        FailureInjection injection,
        DecisionReceipt receipt,
        ExportIntent intent,
        DateTimeOffset decidedAt)
    {
        if (injection == FailureInjection.TamperReceipt)
        {
            // The caller flips the recorded outcome to skip acknowledgment but cannot
            // reseal it through the verifier's channel.
            return receipt with
            {
                Outcome = PolicyOutcome.Allowed,
                ReasonCode = "export.allowed"
            };
        }

        if (injection == FailureInjection.ReceiptForDifferentIntent)
        {
            // A genuine, intact receipt, but for a different intent and resource.
            return CreateReceipt(
                intent with { IntentId = "intent-5311", ResourceId = "customer-batch-77" },
                new PolicyDecision(PolicyOutcome.Allowed, "export.allowed"),
                decidedAt);
        }

        return receipt;
    }

    private sealed class TraceRecorder(FailureScenario scenario)
    {
        private readonly List<StageRecord> _stages = [];
        private DecisionEvidence? _decision;
        private bool _authorityIssued;

        public void Record(TraceStage stage, StageStatus status, string? reasonCode)
        {
            _stages.Add(new(stage, Components.For(stage), status, reasonCode));
        }

        public void RecordDecision(DecisionReceipt receipt)
        {
            _decision = new(
                receipt.ReceiptId,
                receipt.Outcome,
                receipt.ReasonCode,
                receipt.PolicyId,
                receipt.PolicyVersion);
        }

        public void RecordAuthorityIssued(ScopedAuthority authority)
        {
            _authorityIssued = true;
            Record(TraceStage.AuthorityIssuance, StageStatus.Completed, $"authority.issued:{authority.AuthorityId}");
        }

        public TraceResult Stop(TraceOutcome outcome, string reasonCode, CountingExecutor executor)
        {
            StageRecord stopping = _stages[^1];
            return Build(outcome, reasonCode, stopping.Stage, stopping.Component, executor, execution: null);
        }

        public TraceResult Finish(HostEnforcementResult result, CountingExecutor executor)
        {
            if (!result.Executed)
            {
                Record(TraceStage.HostEnforcement, StageStatus.Refused, result.ReasonCode);
                return Stop(TraceOutcome.EnforcementRefused, result.ReasonCode, executor);
            }

            Record(TraceStage.HostEnforcement, StageStatus.Completed, "authority.accepted");
            Record(TraceStage.ProtectedExecutor, StageStatus.Completed, result.ReasonCode);
            return Build(TraceOutcome.Executed, result.ReasonCode, null, null, executor, result.Execution);
        }

        private TraceResult Build(
            TraceOutcome outcome,
            string reasonCode,
            TraceStage? stoppingStage,
            string? stoppingComponent,
            CountingExecutor executor,
            ExecutionEvidence? execution)
        {
            HashSet<TraceStage> recorded = [.. _stages.Select(record => record.Stage)];
            List<StageRecord> stages = [.. _stages];

            foreach (TraceStage stage in Enum.GetValues<TraceStage>().Where(stage => !recorded.Contains(stage)))
            {
                stages.Add(new(stage, Components.For(stage), StageStatus.NotReached, null));
            }

            return new(
                ScenarioId: scenario.Id,
                Injection: scenario.Injection,
                Outcome: outcome,
                ReasonCode: reasonCode,
                FirstStoppingStage: stoppingStage,
                StoppingComponent: stoppingComponent,
                AuthorityIssued: _authorityIssued,
                ExecutorInvocations: executor.Invocations,
                Stages: [.. stages.OrderBy(record => record.Stage)],
                Decision: _decision,
                Execution: execution);
        }
    }
}
