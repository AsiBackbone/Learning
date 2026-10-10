using AuthorizationToGovernanceComparison;

if (args.Contains("--serve", StringComparer.Ordinal))
{
    // Interactive exploration: host the three variants on a local port for curl or HTTP files.
    OperationalLogSink logSink = new();
    WebApplication app = SampleApplication.Configure(
        SampleApplication.CreateBuilder([.. args.Where(arg => arg != "--serve")], logSink).Build());

    await app.RunAsync();
    return 0;
}

// Default: run every scenario against every variant in memory and print the comparison.
return await ComparisonRunner.RunAllAsync(Console.Out, CancellationToken.None);
