using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CentralizedErrorHandlingAndProblemDetails;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CentralizedErrorHandlingAndProblemDetails.Tests;

public sealed class ErrorHandlingIntegrationTests
{
    [Fact]
    public async Task Denied_governance_decision_is_explicit_403_without_exception_handler_log()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/governance/denied",
                TestContext.Current.CancellationToken);

        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("governance.denied", GetExtensionString(problem, "code"));

        Assert.DoesNotContain(
            application.LogProvider.Entries,
            entry => entry.CategoryName ==
                typeof(ApplicationExceptionHandler).FullName);
    }

    [Fact]
    public async Task Deferred_governance_decision_maps_to_503_without_throwing()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/governance/deferred",
                TestContext.Current.CancellationToken);

        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("governance.deferred", GetExtensionString(problem, "code"));

        Assert.DoesNotContain(
            application.LogProvider.Entries,
            entry => entry.CategoryName ==
                typeof(ApplicationExceptionHandler).FullName);
    }

    [Fact]
    public async Task Unexpected_exception_becomes_safe_500_without_sensitive_detail()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/failure/unexpected",
                TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken,
            body);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("unexpected.failure", GetExtensionString(problem, "code"));
        Assert.False(body.Contains("private-db", StringComparison.OrdinalIgnoreCase));
        Assert.False(body.Contains("super-secret", StringComparison.OrdinalIgnoreCase));
        Assert.False(body.Contains("InvalidOperationException", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Known_catalog_exception_maps_to_safe_503()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/failure/catalog",
                TestContext.Current.CancellationToken);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken,
            body);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(
            "dependency.catalog-unavailable",
            GetExtensionString(problem, "code"));
        Assert.False(body.Contains("sql.internal.example", StringComparison.OrdinalIgnoreCase));
        Assert.False(body.Contains("demo-secret", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Problem_trace_id_matches_structured_exception_log_trace_id()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/failure/unexpected",
                TestContext.Current.CancellationToken);

        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken);
        string responseTraceId = GetExtensionString(problem, "traceId");

        CapturedLogEntry logEntry = Assert.Single(
            application.LogProvider.Entries,
            entry =>
                entry.CategoryName == typeof(ApplicationExceptionHandler).FullName &&
                entry.EventId.Id == ApplicationExceptionHandler.HandledExceptionEventId);

        Assert.True(logEntry.Properties.TryGetValue("TraceId", out object? loggedTraceId));
        Assert.Equal(responseTraceId, loggedTraceId?.ToString());
    }

    [Fact]
    public async Task Missing_route_uses_problem_details_without_throwing()
    {
        await using TestApplication application =
            await TestApplication.StartAsync(TestContext.Current.CancellationToken);
        HttpClient client = application.Client;

        HttpResponseMessage response =
            await client.GetAsync(
                "/not-mapped",
                TestContext.Current.CancellationToken);

        ProblemDetails problem = await ReadProblemAsync(
            response,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(404, problem.Status);
        Assert.False(
            string.IsNullOrWhiteSpace(
                GetExtensionString(problem, "traceId")));

        Assert.DoesNotContain(
            application.LogProvider.Entries,
            entry => entry.CategoryName ==
                typeof(ApplicationExceptionHandler).FullName);
    }

    private static async Task<ProblemDetails> ReadProblemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        string? body = null)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        if (body is null)
        {
            ProblemDetails? problem =
                await response.Content.ReadFromJsonAsync<ProblemDetails>(
                    cancellationToken: cancellationToken);

            return Assert.IsType<ProblemDetails>(problem);
        }

        ProblemDetails? parsed =
            JsonSerializer.Deserialize<ProblemDetails>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));

        return Assert.IsType<ProblemDetails>(parsed);
    }

    private static string GetExtensionString(
        ProblemDetails problem,
        string key)
    {
        Assert.True(problem.Extensions.TryGetValue(key, out object? rawValue));

        JsonElement element = Assert.IsType<JsonElement>(rawValue);
        string? value = element.GetString();

        Assert.False(string.IsNullOrWhiteSpace(value));
        return value!;
    }
}

internal sealed class TestApplication : IAsyncDisposable
{
    private TestApplication(
        WebApplication app,
        HttpClient client,
        CapturingLoggerProvider logProvider)
    {
        App = app;
        Client = client;
        LogProvider = logProvider;
    }

    public WebApplication App { get; }

    public HttpClient Client { get; }

    public CapturingLoggerProvider LogProvider { get; }

    public static async Task<TestApplication> StartAsync(
        CancellationToken cancellationToken)
    {
        var logProvider = new CapturingLoggerProvider();

        WebApplicationBuilder builder =
            SampleApplication.CreateBuilder([]);

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logProvider);

        WebApplication app =
            SampleApplication.Configure(builder.Build());

        await app.StartAsync(cancellationToken);

        return new TestApplication(
            app,
            app.GetTestClient(),
            logProvider);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await App.DisposeAsync();
    }
}

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> entries = new();

    public IReadOnlyCollection<CapturedLogEntry> Entries =>
        entries.ToArray();

    public ILogger CreateLogger(string categoryName)
    {
        return new CapturingLogger(
            categoryName,
            entries);
    }

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(
        string categoryName,
        ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return EmptyScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            var properties = new Dictionary<string, object?>(
                StringComparer.Ordinal);

            if (state is IEnumerable<KeyValuePair<string, object?>> values)
            {
                foreach (KeyValuePair<string, object?> value in values)
                {
                    properties[value.Key] = value.Value;
                }
            }

            entries.Enqueue(
                new CapturedLogEntry(
                    categoryName,
                    eventId,
                    logLevel,
                    properties,
                    formatter(state, exception)));
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public static EmptyScope Instance { get; } = new();

        public void Dispose()
        {
        }
    }
}

internal sealed record CapturedLogEntry(
    string CategoryName,
    EventId EventId,
    LogLevel LogLevel,
    IReadOnlyDictionary<string, object?> Properties,
    string Message);
