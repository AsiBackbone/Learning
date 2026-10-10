namespace GovernedFailureInjectionTrace;

public static class Program
{
    public const int Success = 0;
    public const int ExpectationNotMet = 1;
    public const int UsageError = 2;

    public static int Main(string[] args)
    {
        return Run(args, Console.Out, Console.Error);
    }

    /// <summary>
    /// Non-interactive entry point. Every scenario can be selected by name, so CI can run
    /// any of them without input.
    /// </summary>
    public static int Run(IReadOnlyList<string> args, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        List<string> selected = [];
        bool json = false;

        for (int index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--help" or "-h":
                    WriteUsage(output);
                    return Success;

                case "--list":
                    foreach (FailureScenario scenario in FailureScenarios.All)
                    {
                        output.WriteLine($"{scenario.Id,-28} {scenario.Summary}");
                    }

                    return Success;

                case "--all":
                    break;

                case "--scenario" when index + 1 < args.Count:
                    selected.Add(args[++index]);
                    break;

                case "--format" when index + 1 < args.Count:
                    string format = args[++index];
                    if (format is not ("text" or "json"))
                    {
                        error.WriteLine($"Unknown format '{format}'. Use text or json.");
                        return UsageError;
                    }

                    json = format == "json";
                    break;

                default:
                    error.WriteLine($"Unrecognized argument '{args[index]}'.");
                    WriteUsage(error);
                    return UsageError;
            }
        }

        List<FailureScenario> scenarios = [];

        if (selected.Count == 0)
        {
            scenarios.AddRange(FailureScenarios.All);
        }

        foreach (string id in selected)
        {
            if (FailureScenarios.Find(id) is not { } scenario)
            {
                error.WriteLine($"Unknown scenario '{id}'. Run with --list to see the available scenarios.");
                return UsageError;
            }

            scenarios.Add(scenario);
        }

        List<ScenarioReport> reports =
        [
            .. scenarios.Select(scenario =>
                TraceFormatter.CreateReport(scenario, GovernedExportPipeline.Run(scenario)))
        ];

        if (json)
        {
            output.WriteLine(TraceFormatter.ToJson(reports));
        }
        else
        {
            output.WriteLine("Governed Failure-Injection Trace");
            output.WriteLine(new string('=', 32));
            output.WriteLine();
            output.WriteLine("Each run injects one failure into a fictional customer.export and records every stage.");
            output.WriteLine("Nothing leaves the process: the protected executor only counts its invocations.");
            output.WriteLine();

            foreach (ScenarioReport report in reports)
            {
                TraceFormatter.WriteText(output, report);
            }

            int met = reports.Count(report => report.ExpectationMet);
            int invocations = reports.Sum(report => report.Trace.ExecutorInvocations);
            output.WriteLine(
                $"{reports.Count} scenario(s) run; {met} met expectations; " +
                $"protected executor invoked {invocations} time(s) in total.");
        }

        return reports.TrueForAll(report => report.ExpectationMet) ? Success : ExpectationNotMet;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: GovernedFailureInjectionTrace [--all | --scenario <id> ...] [--format text|json]");
        writer.WriteLine("       GovernedFailureInjectionTrace --list");
        writer.WriteLine();
        writer.WriteLine("With no scenario selected, every scenario runs.");
        writer.WriteLine("Exit codes: 0 all expectations met, 1 an expectation was not met, 2 usage error.");
    }
}
