using Microsoft.AspNetCore.Http;
using Xunit;

namespace MiddlewareOrderingChangesBehavior.Tests;

public sealed class MiddlewareOrderTests
{
    [Fact]
    public async Task RequestAndResponseTraverseInOppositeOrders()
    {
        List<string> events = [];

        RequestDelegate pipeline =
            MiddlewareOrderDemo.Build(
                correctOrder: true,
                events.Add);

        DefaultHttpContext context =
            CreateContext("/");

        await pipeline(context);

        string[] expected =
        [
            "exception-boundary:request",
            "outer:request",
            "inner:request",
            "endpoint",
            "inner:response",
            "outer:response",
            "exception-boundary:response"
        ];

        Assert.Equal(
            expected,
            events.ToArray());
    }

    [Fact]
    public async Task CorrectOrder_CatchesFaultInsideExceptionBoundary()
    {
        List<string> events = [];

        RequestDelegate pipeline =
            MiddlewareOrderDemo.Build(
                correctOrder: true,
                events.Add);

        DefaultHttpContext context =
            CreateContext("/fault");

        await pipeline(context);

        Assert.Equal(
            StatusCodes.Status500InternalServerError,
            context.Response.StatusCode);

        Assert.Contains(
            "exception-boundary:handled",
            events);
    }

    [Fact]
    public async Task CorrectOrder_FaultProducesControlledProblemBoundaryResponse()
    {
        RequestDelegate pipeline = MiddlewareOrderDemo.Build(correctOrder: true);
        DefaultHttpContext context = CreateContext("/fault");

        await pipeline(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal("text/plain", context.Response.ContentType);
        Assert.Equal(
            "Handled by demo exception boundary.",
            body);
    }

    [Fact]
    public async Task CorrectOrder_FaultStopsInnerMiddlewareAndEndpoint()
    {
        List<string> events = [];
        RequestDelegate pipeline = MiddlewareOrderDemo.Build(true, events.Add);
        DefaultHttpContext context = CreateContext("/fault");

        await pipeline(context);

        Assert.DoesNotContain("inner:request", events);
        Assert.DoesNotContain("endpoint", events);
        Assert.DoesNotContain("outer:response", events);
        Assert.Equal("exception-boundary:response", events[^1]);
    }

    [Fact]
    public async Task IncorrectOrder_LeavesEarlierFaultOutsideExceptionBoundary()
    {
        List<string> events = [];

        RequestDelegate pipeline =
            MiddlewareOrderDemo.Build(
                correctOrder: false,
                events.Add);

        DefaultHttpContext context =
            CreateContext("/fault");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline(context));

        Assert.DoesNotContain(
            "exception-boundary:handled",
            events);
    }

    [Fact]
    public async Task IncorrectOrder_FaultProbeIsOnlyObservedStage()
    {
        List<string> events = [];
        RequestDelegate pipeline = MiddlewareOrderDemo.Build(false, events.Add);
        DefaultHttpContext context = CreateContext("/fault");

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline(context));

        Assert.Equal(["fault-probe:throw"], events);
    }

    [Fact]
    public async Task NormalEndpointReportsConfiguredPipelineMode()
    {
        RequestDelegate pipeline = MiddlewareOrderDemo.Build(correctOrder: false);
        DefaultHttpContext context = CreateContext("/");

        await pipeline(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        string body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("Endpoint reached. Pipeline mode: incorrect.", body);
    }

    [Fact]
    public void ConfigureRejectsMissingApplicationBuilder()
    {
        Assert.Throws<ArgumentNullException>(
            () => MiddlewareOrderDemo.Configure(null!, correctOrder: true));
    }

    private static DefaultHttpContext CreateContext(string path)
    {
        var context =
            new DefaultHttpContext();

        context.Request.Path =
            path;

        context.Response.Body =
            new MemoryStream();

        return context;
    }
}
