using Microsoft.AspNetCore.TestHost;

namespace AuthorizationToGovernanceComparison;

/// <summary>
/// Hosts the sample in memory with a fresh set of fixtures and evidence stores, so every
/// run starts from the same state and opens no network port.
/// </summary>
public sealed class SampleHost : IAsyncDisposable
{
    private readonly WebApplication _app;

    private SampleHost(WebApplication app, OperationalLogSink logSink)
    {
        _app = app;
        LogSink = logSink;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public OperationalLogSink LogSink { get; }

    public RecordingAccountDisabler Disabler => _app.Services.GetRequiredService<RecordingAccountDisabler>();

    public ApplicationAuditLog Audit => _app.Services.GetRequiredService<ApplicationAuditLog>();

    public DecisionLog Decisions => _app.Services.GetRequiredService<DecisionLog>();

    public ExecutionLog Executions => _app.Services.GetRequiredService<ExecutionLog>();

    public GovernedExecutionBoundary Boundary => _app.Services.GetRequiredService<GovernedExecutionBoundary>();

    public FixedTimeProvider Clock => (FixedTimeProvider)_app.Services.GetRequiredService<TimeProvider>();

    public static async Task<SampleHost> StartAsync(CancellationToken cancellationToken)
    {
        OperationalLogSink logSink = new();
        WebApplicationBuilder builder = SampleApplication.CreateBuilder([], logSink);

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logSink);

        WebApplication app = SampleApplication.Configure(builder.Build());
        await app.StartAsync(cancellationToken);

        return new SampleHost(app, logSink);
    }

    public static string RouteFor(Variant variant, string accountId)
    {
        string template = variant switch
        {
            Variant.EndpointAuthorization => EndpointAuthorizationVariant.Route,
            Variant.ResourceAuthorization => ResourceAuthorizationVariant.Route,
            Variant.GovernedExecution => GovernedExecutionVariant.Route,
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown variant.")
        };

        return template.Replace("{accountId}", Uri.EscapeDataString(accountId), StringComparison.Ordinal);
    }

    public async Task<HttpResponseMessage> DisableAsync(
        Variant variant,
        string accountId,
        string? actorId,
        string? acknowledgedDecisionId,
        CancellationToken cancellationToken)
    {
        string route = RouteFor(variant, accountId);

        if (acknowledgedDecisionId is not null)
        {
            route += $"?acknowledgedDecisionId={Uri.EscapeDataString(acknowledgedDecisionId)}";
        }

        using HttpRequestMessage request = new(HttpMethod.Post, route);

        if (actorId is not null)
        {
            request.Headers.Add(DemoAuthenticationHandler.HeaderName, actorId);
        }

        return await Client.SendAsync(request, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.DisposeAsync();
    }
}
