using System.Text.Json;
using Xunit;

namespace GovernedFailureInjectionTrace.Tests;

public sealed class GovernedFailureInjectionTraceTests
{
    public static TheoryData<string> AllScenarioIds { get; } =
        [.. FailureScenarios.All.Select(scenario => scenario.Id)];

    public static TheoryData<string> BlockedScenarioIds { get; } =
    [
        .. FailureScenarios.All
            .Where(scenario => scenario.ExpectedOutcome != TraceOutcome.Executed)
            .Select(scenario => scenario.Id)
    ];

    [Theory]
    [MemberData(nameof(AllScenarioIds))]
    public void Every_scenario_meets_its_declared_expectation(string scenarioId)
    {
        FailureScenario scenario = Get(scenarioId);

        TraceResult result = GovernedExportPipeline.Run(scenario);

        Assert.Equal(scenario.ExpectedOutcome, result.Outcome);
        Assert.Equal(scenario.ExpectedReasonCode, result.ReasonCode);
        Assert.Equal(scenario.ExpectedExecutorInvocations, result.ExecutorInvocations);
    }

    [Theory]
    [MemberData(nameof(BlockedScenarioIds))]
    public void Every_blocked_invalid_expired_replayed_or_bypass_path_invokes_the_executor_zero_times(
        string scenarioId)
    {
        TraceResult result = Run(scenarioId);

        Assert.Equal(0, result.ExecutorInvocations);
        Assert.Null(result.Execution);
        Assert.NotNull(result.FirstStoppingStage);
        Assert.Equal(
            StageStatus.NotReached,
            Stage(result, TraceStage.ProtectedExecutor).Status);
    }

    [Theory]
    [InlineData("allowed-executes-once")]
    [InlineData("acknowledged-continuation")]
    public void Valid_path_invokes_the_executor_exactly_once(string scenarioId)
    {
        TraceResult result = Run(scenarioId);

        Assert.Equal(TraceOutcome.Executed, result.Outcome);
        Assert.Equal(1, result.ExecutorInvocations);
        Assert.True(result.AuthorityIssued);
        Assert.Null(result.FirstStoppingStage);
        Assert.All(
            result.Stages,
            stage => Assert.Contains(stage.Status, new[] { StageStatus.Completed, StageStatus.AwaitingContinuation }));
        Assert.Equal(StageStatus.Completed, Stage(result, TraceStage.ContinuationVerification).Status);
    }

    [Fact]
    public void The_scenario_catalog_covers_every_injected_failure()
    {
        HashSet<FailureInjection> covered = [.. FailureScenarios.All.Select(scenario => scenario.Injection)];

        Assert.All(Enum.GetValues<FailureInjection>(), injection => Assert.Contains(injection, covered));
        Assert.Equal(
            FailureScenarios.All.Count,
            FailureScenarios.All.Select(scenario => scenario.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Policy_denial_stops_before_authority_issuance()
    {
        TraceResult result = Run("policy-denied");

        Assert.Equal(TraceStage.PolicyDecision, result.FirstStoppingStage);
        Assert.Equal(Components.PolicyEvaluator, result.StoppingComponent);
        Assert.False(result.AuthorityIssued);
        Assert.Equal(StageStatus.NotReached, Stage(result, TraceStage.AuthorityIssuance).Status);
        Assert.Equal(PolicyOutcome.Denied, result.Decision?.Outcome);
    }

    [Fact]
    public void Unavailable_context_is_an_explicit_deferral_not_a_denial()
    {
        TraceResult result = Run("context-unavailable");

        Assert.Equal(TraceOutcome.Deferred, result.Outcome);
        Assert.Equal(StageStatus.Unavailable, Stage(result, TraceStage.AuthoritativeContext).Status);
        Assert.Equal(StageStatus.Deferred, Stage(result, TraceStage.PolicyDecision).Status);
        Assert.Equal(PolicyOutcome.Deferred, result.Decision?.Outcome);
        Assert.False(result.AuthorityIssued);
    }

    [Fact]
    public void Missing_acknowledgment_waits_for_continuation_rather_than_failing_verification()
    {
        TraceResult result = Run("acknowledgment-missing");

        Assert.Equal(TraceOutcome.ContinuationRequired, result.Outcome);
        Assert.Equal(
            StageStatus.AwaitingContinuation,
            Stage(result, TraceStage.ContinuationVerification).Status);
        Assert.Equal(PolicyOutcome.AcknowledgmentRequired, result.Decision?.Outcome);
        Assert.False(result.AuthorityIssued);
    }

    [Theory]
    [InlineData("policy-denied", TraceStage.PolicyDecision, Components.PolicyEvaluator)]
    [InlineData("receipt-tampered", TraceStage.ContinuationVerification, Components.ContinuationVerifier)]
    [InlineData("authority-expired", TraceStage.HostEnforcement, Components.ProtectedHost)]
    public void Policy_refusal_verification_failure_and_host_enforcement_have_different_owners(
        string scenarioId,
        TraceStage expectedStage,
        string expectedComponent)
    {
        TraceResult result = Run(scenarioId);

        Assert.Equal(expectedStage, result.FirstStoppingStage);
        Assert.Equal(expectedComponent, result.StoppingComponent);
        Assert.Equal(StageStatus.Refused, Stage(result, expectedStage).Status);
    }

    [Theory]
    [InlineData("receipt-tampered")]
    [InlineData("receipt-expired")]
    [InlineData("receipt-intent-mismatch")]
    public void Verification_failures_never_issue_authority(string scenarioId)
    {
        TraceResult result = Run(scenarioId);

        Assert.Equal(TraceOutcome.VerificationFailed, result.Outcome);
        Assert.False(result.AuthorityIssued);
    }

    [Theory]
    [InlineData("authority-expired")]
    [InlineData("authority-replayed")]
    [InlineData("authority-wrong-audience")]
    [InlineData("authority-wrong-operation")]
    public void Invalid_authority_is_issued_but_refused_by_the_host(string scenarioId)
    {
        TraceResult result = Run(scenarioId);

        Assert.True(result.AuthorityIssued);
        Assert.Equal(TraceOutcome.EnforcementRefused, result.Outcome);
        Assert.Equal(StageStatus.Completed, Stage(result, TraceStage.AuthorityIssuance).Status);
    }

    [Fact]
    public void Bypass_skips_every_governed_stage_and_the_host_still_refuses()
    {
        TraceResult result = Run("bypass-entry-point");

        Assert.All(
            result.Stages.Where(stage => stage.Stage < TraceStage.HostEnforcement),
            stage => Assert.Equal(StageStatus.SkippedByCaller, stage.Status));
        Assert.Equal(StageStatus.Refused, Stage(result, TraceStage.HostEnforcement).Status);
        Assert.Null(result.Decision);
        Assert.False(result.AuthorityIssued);
    }

    [Fact]
    public void Bypass_differs_from_an_ordinary_policy_denial()
    {
        TraceResult bypass = Run("bypass-entry-point");
        TraceResult denial = Run("policy-denied");

        Assert.Equal(TraceOutcome.EnforcementRefused, bypass.Outcome);
        Assert.Equal(TraceOutcome.Denied, denial.Outcome);
        Assert.Null(bypass.Decision);
        Assert.NotNull(denial.Decision);
    }

    [Fact]
    public void Replaying_consumed_authority_against_the_same_host_executes_only_once()
    {
        IssuedAuthorityRegistry registry = new();
        CountingExecutor executor = new();
        ProtectedHost host = new(registry, new AuthorityUseStore(), executor);
        ExportIntent intent = GovernedExportPipeline.CreateIntent();
        DateTimeOffset now = GovernedExportPipeline.BaselineUtc;
        DecisionReceipt receipt = SimulatedReceiptSeal.Apply(
            new DecisionReceipt(
                "rcpt-replay", intent.IntentId, intent.ActorId, intent.ResourceId, intent.Operation,
                PolicyOutcome.Allowed, "export.allowed", ExportContract.PolicyId, ExportContract.PolicyVersion,
                now, now.AddMinutes(10), string.Empty));
        ScopedAuthority authority = new AuthorityIssuer(registry).Issue(
            receipt, ExportContract.HostAudience, ExportContract.Operation, now.AddMinutes(2));

        HostEnforcementResult first = host.Execute(authority, intent, now.AddMinutes(1));
        HostEnforcementResult replay = host.Execute(authority, intent, now.AddMinutes(1));

        Assert.True(first.Executed);
        Assert.False(replay.Executed);
        Assert.Equal("authority.already-used", replay.ReasonCode);
        Assert.Equal(1, executor.Invocations);
    }

    [Fact]
    public void A_refused_attempt_does_not_consume_authority()
    {
        IssuedAuthorityRegistry registry = new();
        CountingExecutor executor = new();
        ProtectedHost host = new(registry, new AuthorityUseStore(), executor);
        ExportIntent intent = GovernedExportPipeline.CreateIntent();
        DateTimeOffset now = GovernedExportPipeline.BaselineUtc;
        ScopedAuthority authority = new AuthorityIssuer(registry).Issue(
            SimulatedReceiptSeal.Apply(
                new DecisionReceipt(
                    "rcpt-refused", intent.IntentId, intent.ActorId, intent.ResourceId, intent.Operation,
                    PolicyOutcome.Allowed, "export.allowed", ExportContract.PolicyId,
                    ExportContract.PolicyVersion, now, now.AddMinutes(10), string.Empty)),
            ExportContract.HostAudience,
            ExportContract.Operation,
            now.AddMinutes(2));

        HostEnforcementResult wrongResource = host.Execute(
            authority, intent with { ResourceId = "customer-batch-77" }, now.AddMinutes(1));
        HostEnforcementResult valid = host.Execute(authority, intent, now.AddMinutes(1));

        Assert.Equal("authority.resource-mismatch", wrongResource.ReasonCode);
        Assert.True(valid.Executed);
        Assert.Equal(1, executor.Invocations);
    }

    [Fact]
    public void Decision_evidence_and_execution_evidence_are_separate_records()
    {
        TraceResult executed = Run("allowed-executes-once");
        TraceResult refused = Run("authority-expired");

        Assert.NotNull(executed.Decision);
        Assert.NotNull(executed.Execution);
        Assert.Equal(executed.Decision.ReceiptId, executed.Execution.ReceiptId);
        Assert.NotEqual(executed.Decision.ReceiptId, executed.Execution.ExecutionId);

        Assert.Equal(PolicyOutcome.Allowed, refused.Decision?.Outcome);
        Assert.Null(refused.Execution);
    }

    [Fact]
    public void Simulated_seal_detects_a_change_to_any_sealed_field()
    {
        DecisionReceipt sealedReceipt = SimulatedReceiptSeal.Apply(
            new DecisionReceipt(
                "rcpt-seal", "intent-1", "analyst-7", "customer-batch-42", ExportContract.Operation,
                PolicyOutcome.AcknowledgmentRequired, "export.region-eu-acknowledgment",
                ExportContract.PolicyId, ExportContract.PolicyVersion,
                GovernedExportPipeline.BaselineUtc, GovernedExportPipeline.BaselineUtc.AddMinutes(10),
                string.Empty));

        Assert.True(SimulatedReceiptSeal.Matches(sealedReceipt));
        Assert.False(SimulatedReceiptSeal.Matches(sealedReceipt with { Outcome = PolicyOutcome.Allowed }));
        Assert.False(SimulatedReceiptSeal.Matches(sealedReceipt with { ResourceId = "customer-batch-77" }));
        Assert.False(SimulatedReceiptSeal.Matches(
            sealedReceipt with { ExpiresAtUtc = sealedReceipt.ExpiresAtUtc.AddHours(1) }));
    }

    [Fact]
    public void Runs_are_deterministic()
    {
        string first = TraceFormatter.ToJson(Reports());
        string second = TraceFormatter.ToJson(Reports());

        Assert.Equal(first, second);
    }

    [Fact]
    public void Cli_runs_every_scenario_without_input_and_reports_success()
    {
        (int exitCode, string output, string error) = InvokeCli("--all");

        Assert.Equal(Program.Success, exitCode);
        Assert.Empty(error);
        Assert.Contains(
            $"{FailureScenarios.All.Count} scenario(s) run; {FailureScenarios.All.Count} met expectations",
            output,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Cli_emits_structured_json_for_a_named_scenario()
    {
        (int exitCode, string output, _) = InvokeCli("--scenario", "authority-replayed", "--format", "json");

        Assert.Equal(Program.Success, exitCode);

        using var document = JsonDocument.Parse(output);
        JsonElement report = Assert.Single(document.RootElement.EnumerateArray());
        JsonElement trace = report.GetProperty("trace");
        Assert.Equal("authority-replayed", report.GetProperty("scenario").GetString());
        Assert.True(report.GetProperty("expectationMet").GetBoolean());
        Assert.Equal("EnforcementRefused", trace.GetProperty("outcome").GetString());
        Assert.Equal("HostEnforcement", trace.GetProperty("firstStoppingStage").GetString());
        Assert.Equal(0, trace.GetProperty("executorInvocations").GetInt32());
        Assert.Equal(7, trace.GetProperty("stages").GetArrayLength());
    }

    [Theory]
    [InlineData("--scenario", "no-such-scenario")]
    [InlineData("--format", "xml")]
    [InlineData("--unknown")]
    public void Cli_rejects_invalid_arguments_with_a_usage_exit_code(params string[] args)
    {
        (int exitCode, _, string error) = InvokeCli(args);

        Assert.Equal(Program.UsageError, exitCode);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Cli_lists_every_scenario()
    {
        (int exitCode, string output, _) = InvokeCli("--list");

        Assert.Equal(Program.Success, exitCode);
        Assert.All(FailureScenarios.All, scenario => Assert.Contains(scenario.Id, output, StringComparison.Ordinal));
    }

    private static FailureScenario Get(string scenarioId)
    {
        return FailureScenarios.Find(scenarioId)
            ?? throw new InvalidOperationException($"Unknown scenario '{scenarioId}'.");
    }

    private static TraceResult Run(string scenarioId)
    {
        return GovernedExportPipeline.Run(Get(scenarioId));
    }

    private static StageRecord Stage(TraceResult result, TraceStage stage)
    {
        return result.Stages.Single(record => record.Stage == stage);
    }

    private static List<ScenarioReport> Reports()
    {
        return
        [
            .. FailureScenarios.All.Select(scenario =>
                TraceFormatter.CreateReport(scenario, GovernedExportPipeline.Run(scenario)))
        ];
    }

    private static (int ExitCode, string Output, string Error) InvokeCli(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();

        int exitCode = Program.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
