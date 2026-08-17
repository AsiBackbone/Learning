using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace MiddlewareOrderingChangesBehavior;

/// <summary>
/// Builds two deliberately small ASP.NET Core pipelines so ordering behavior can be observed directly.
/// </summary>
public static class MiddlewareOrderDemo
{
    /// <summary>
    /// Configures either the corrected or deliberately incorrect pipeline.
    /// </summary>
    public static void Configure(
        IApplicationBuilder app,
        bool correctOrder,
        Action<string>? observe = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        observe ??= static _ => { };

        if (correctOrder)
        {
            UseExceptionBoundary(app, observe);
            UseTrace(app, "outer", observe);
            UseFaultProbe(app, observe);
        }
        else
        {
            // Deliberately wrong: /fault can throw before the exception boundary
            // has entered the request pipeline.
            UseFaultProbe(app, observe);
            UseExceptionBoundary(app, observe);
            UseTrace(app, "outer", observe);
        }

        UseTrace(app, "inner", observe);

        app.Run(async context =>
        {
            observe("endpoint");

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(
                $"Endpoint reached. Pipeline mode: {(correctOrder ? "correct" : "incorrect")}.");
        });
    }

    /// <summary>
    /// Builds an in-memory request delegate for focused invariant tests.
    /// </summary>
    public static RequestDelegate Build(
        bool correctOrder,
        Action<string>? observe = null)
    {
        IServiceProvider services =
            new ServiceCollection().BuildServiceProvider();

        IApplicationBuilder app =
            new ApplicationBuilder(services);

        Configure(app, correctOrder, observe);

        return app.Build();
    }

    private static void UseExceptionBoundary(
        IApplicationBuilder app,
        Action<string> observe)
    {
        app.Use(next => async context =>
        {
            observe("exception-boundary:request");

            try
            {
                await next(context);
            }
            catch (InvalidOperationException exception)
            {
                observe("exception-boundary:handled");

                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                context.Response.ContentType =
                    "text/plain";

                await context.Response.WriteAsync(
                    $"Handled by demo exception boundary: {exception.Message}");
            }
            finally
            {
                observe("exception-boundary:response");
            }
        });
    }

    private static void UseFaultProbe(
        IApplicationBuilder app,
        Action<string> observe)
    {
        app.Use(next => async context =>
        {
            if (context.Request.Path == "/fault")
            {
                observe("fault-probe:throw");

                throw new InvalidOperationException(
                    "Demonstration failure.");
            }

            await next(context);
        });
    }

    private static void UseTrace(
        IApplicationBuilder app,
        string name,
        Action<string> observe)
    {
        app.Use(next => async context =>
        {
            observe($"{name}:request");

            await next(context);

            observe($"{name}:response");
        });
    }
}
