using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuthorizationToGovernanceComparison;

/// <summary>
/// One request, sent unchanged to all three variants, with the result each is expected to
/// produce: the HTTP status and how many times the protected side effect ran.
/// </summary>
public sealed record ComparisonScenario(
    string Id,
    string Description,
    string? ActorId,
    string AccountId,
    (int Status, int Invocations) Endpoint,
    (int Status, int Invocations) Resource,
    (int Status, int Invocations) Governed,
    bool AcknowledgeAndContinue = false);

public sealed record VariantRun(
    Variant Variant,
    int StatusCode,
    int Invocations,
    IReadOnlyList<string> Evidence);

public static class ComparisonRunner
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static IReadOnlyList<ComparisonScenario> Scenarios { get; } =
    [
        new("ordinary-disable", "admin-a (tenant-a Administrator) disables acct-100 (tenant-a)",
            "admin-a", "acct-100", (204, 1), (204, 1), (200, 1)),
        new("non-administrator", "support-a (tenant-a Support) disables acct-100",
            "support-a", "acct-100", (403, 0), (403, 0), (403, 0)),
        new("cross-tenant-administrator", "admin-b (tenant-b Administrator) disables acct-100 (tenant-a)",
            "admin-b", "acct-100", (204, 1), (403, 0), (403, 0)),
        new("protected-account", "admin-a disables acct-200, a protected account",
            "admin-a", "acct-200", (204, 1), (403, 0), (403, 0)),
        new("active-sessions", "admin-a disables acct-400, which has active sessions, then acknowledges",
            "admin-a", "acct-400", (204, 1), (204, 1), (200, 1), AcknowledgeAndContinue: true),
        new("privileged-account", "admin-a disables acct-500, a privileged account",
            "admin-a", "acct-500", (204, 1), (204, 1), (202, 0)),
        new("directory-sync-pending", "admin-a disables acct-600 while its directory sync is pending",
            "admin-a", "acct-600", (204, 1), (204, 1), (503, 0)),
        new("unauthenticated", "an anonymous caller disables acct-100",
            null, "acct-100", (401, 0), (401, 0), (401, 0))
    ];

    public static (int Status, int Invocations) Expected(ComparisonScenario scenario, Variant variant)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        return variant switch
        {
            Variant.EndpointAuthorization => scenario.Endpoint,
            Variant.ResourceAuthorization => scenario.Resource,
            Variant.GovernedExecution => scenario.Governed,
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown variant.")
        };
    }

    public static async Task<VariantRun> RunAsync(
        ComparisonScenario scenario,
        Variant variant,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        await using SampleHost host = await SampleHost.StartAsync(cancellationToken);

        using HttpResponseMessage response = await host.DisableAsync(
            variant, scenario.AccountId, scenario.ActorId, acknowledgedDecisionId: null, cancellationToken);
        int status = (int)response.StatusCode;

        if (variant == Variant.GovernedExecution &&
            scenario.AcknowledgeAndContinue &&
            response.StatusCode == System.Net.HttpStatusCode.Accepted)
        {
            // The continuation: the caller acknowledges the earlier decision and asks again.
            GovernedResponse? first = await response.Content.ReadFromJsonAsync<GovernedResponse>(
                _jsonOptions, cancellationToken);

            using HttpResponseMessage continued = await host.DisableAsync(
                variant, scenario.AccountId, scenario.ActorId, first?.DecisionId, cancellationToken);
            status = (int)continued.StatusCode;
        }

        return new(variant, status, host.Disabler.Invocations.Count, DescribeEvidence(variant, host));
    }

    public static async Task<int> RunAllAsync(TextWriter output, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        output.WriteLine("Authorization-to-Governance Comparison");
        output.WriteLine(new string('=', 38));
        output.WriteLine();
        output.WriteLine("The same DisableAccount request is sent to three alternative designs. Each run starts from");
        output.WriteLine("the same fixtures; the protected side effect only records that it was called.");
        output.WriteLine("These are alternatives chosen by requirements, not maturity levels.");
        output.WriteLine();

        int mismatches = 0;

        foreach (ComparisonScenario scenario in Scenarios)
        {
            output.WriteLine($"Scenario: {scenario.Id} - {scenario.Description}");

            foreach (Variant variant in Enum.GetValues<Variant>())
            {
                VariantRun run = await RunAsync(scenario, variant, cancellationToken);
                (int expectedStatus, int expectedInvocations) = Expected(scenario, variant);
                bool met = run.StatusCode == expectedStatus && run.Invocations == expectedInvocations;
                mismatches += met ? 0 : 1;

                output.WriteLine(
                    $"  {(int)variant} {Label(variant),-24} HTTP {run.StatusCode}  executed {run.Invocations}x" +
                    (met ? string.Empty : $"  [expected HTTP {expectedStatus}, {expectedInvocations}x]"));

                foreach (string evidence in run.Evidence)
                {
                    output.WriteLine($"      {evidence}");
                }
            }

            output.WriteLine();
        }

        output.WriteLine(
            mismatches == 0
                ? $"{Scenarios.Count} scenario(s) x 3 variants behaved as expected."
                : $"{mismatches} variant run(s) did not behave as expected.");

        return mismatches == 0 ? 0 : 1;
    }

    public static string Label(Variant variant)
    {
        return variant switch
        {
            Variant.EndpointAuthorization => "Endpoint authorization",
            Variant.ResourceAuthorization => "Resource authorization",
            Variant.GovernedExecution => "Governed execution",
            _ => variant.ToString()
        };
    }

    private static List<string> DescribeEvidence(Variant variant, SampleHost host)
    {
        List<string> lines = [];

        switch (variant)
        {
            case Variant.EndpointAuthorization:
                lines.AddRange(host.LogSink.Entries.Select(entry =>
                    $"log [{ShortCategory(entry.Category)}] {Truncate(entry.Message.ReplaceLineEndings(" "), 90)}"));
                break;

            case Variant.ResourceAuthorization:
                lines.AddRange(host.Audit.Entries.Select(entry =>
                    $"audit: {entry.ActorId} {entry.Action} {entry.AccountId} " +
                    (entry.Succeeded ? "succeeded" : $"failed [{string.Join(", ", entry.FailureReasons)}]")));
                break;

            case Variant.GovernedExecution:
                if (host.Decisions.Entries.Count == 0)
                {
                    lines.Add("decision: none; the request was rejected before the endpoint ran");
                }

                lines.AddRange(host.Decisions.Entries.Select(decision =>
                    $"decision: {decision.DecisionId} {decision.Outcome} {decision.ReasonCode} " +
                    $"({decision.PolicyVersion})" +
                    (decision.AcknowledgedDecisionId is { } acknowledged
                        ? $" acknowledging {acknowledged}"
                        : string.Empty)));
                lines.AddRange(host.Executions.Entries.Select(execution =>
                    $"execution: {execution.ExecutionId} for {execution.DecisionId}"));

                if (host.Executions.Entries.Count == 0)
                {
                    lines.Add("execution: none");
                }

                break;

            default:
                break;
        }

        if (lines.Count == 0)
        {
            lines.Add("(no evidence recorded by this variant)");
        }

        return lines;
    }

    private static string ShortCategory(string category)
    {
        int index = category.LastIndexOf('.');
        return index < 0 ? category : category[(index + 1)..];
    }

    private static string Truncate(string value, int length)
    {
        return value.Length <= length ? value : string.Concat(value.AsSpan(0, length), "...");
    }
}
