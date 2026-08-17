using Microsoft.AspNetCore.Http;
using MiddlewareOrderingChangesBehavior;
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
