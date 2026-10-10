using System.Text.Json;
using System.Text.Json.Serialization;

namespace GovernedFailureInjectionTrace;

public sealed record ScenarioReport(
    string Scenario,
    string Summary,
    TraceOutcome ExpectedOutcome,
    string ExpectedReasonCode,
    int ExpectedExecutorInvocations,
    bool ExpectationMet,
    TraceResult Trace);

public static class TraceFormatter
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static ScenarioReport CreateReport(FailureScenario scenario, TraceResult result)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return new(
            scenario.Id,
            scenario.Summary,
            scenario.ExpectedOutcome,
            scenario.ExpectedReasonCode,
            scenario.ExpectedExecutorInvocations,
            FailureScenarios.MeetsExpectation(scenario, result),
            result);
    }

    public static string ToJson(IReadOnlyList<ScenarioReport> reports)
    {
        return JsonSerializer.Serialize(reports, _jsonOptions);
    }

    public static void WriteText(TextWriter output, ScenarioReport report)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(report);

        TraceResult trace = report.Trace;

        output.WriteLine($"Scenario: {report.Scenario}");
        output.WriteLine($"Injected failure: {trace.Injection}");
        output.WriteLine($"  {report.Summary}");
        output.WriteLine("Stage trace:");

        foreach (StageRecord stage in trace.Stages)
        {
            string marker = stage.Stage == trace.FirstStoppingStage ? "  <- stopped here" : string.Empty;
            output.WriteLine(
                $"  {(int)stage.Stage + 1}. {stage.Stage,-25} {stage.Component,-26} " +
                $"{stage.Status,-20} {stage.ReasonCode ?? "-"}{marker}");
        }

        output.WriteLine($"Outcome: {trace.Outcome} ({trace.ReasonCode})");
        output.WriteLine(
            trace.FirstStoppingStage is { } stoppedAt
                ? $"First stopping stage: {stoppedAt}, owned by {trace.StoppingComponent}"
                : "First stopping stage: none; every stage completed");
        output.WriteLine($"Execution authority issued: {(trace.AuthorityIssued ? "yes" : "no")}");
        output.WriteLine($"Protected executor invocations: {trace.ExecutorInvocations}");
        output.WriteLine(
            trace.Decision is { } decision
                ? $"Decision evidence: {decision.ReceiptId} {decision.Outcome} {decision.ReasonCode} " +
                  $"({decision.PolicyId}@{decision.PolicyVersion})"
                : "Decision evidence: none; no policy decision was made");
        output.WriteLine(
            trace.Execution is { } execution
                ? $"Execution evidence: {execution.ExecutionId} using {execution.AuthorityId}"
                : "Execution evidence: none; the protected executor did not run");
        output.WriteLine(
            report.ExpectationMet
                ? "Expectation: met"
                : $"Expectation: NOT met (expected {report.ExpectedOutcome}, {report.ExpectedReasonCode}, " +
                  $"{report.ExpectedExecutorInvocations} invocation(s))");
        output.WriteLine();
    }
}
