using System.Diagnostics;

namespace CentralizedErrorHandlingAndProblemDetails;

public static class SampleApplication
{
    public static WebApplicationBuilder CreateBuilder(string[] args)
    {
        WebApplicationBuilder builder =
            WebApplication.CreateBuilder(args);

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Instance ??=
                    context.HttpContext.Request.Path.Value;

                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.TraceId.ToString()
                    ?? context.HttpContext.TraceIdentifier;
            };
        });

        builder.Services.AddExceptionHandler<ApplicationExceptionHandler>();

        return builder;
    }

    public static WebApplication Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.MapGet("/", () => Results.Ok(new
        {
            Sample = "Centralized Error Handling and Problem Details",
            Principle = "Expected outcomes remain data; unexpected failures cross the exception boundary."
        }));

        app.MapGet("/failure/unexpected", UnexpectedFailure);
        app.MapGet("/failure/catalog", KnownCatalogFailure);

        app.MapGet("/governance/{scenario}", (
            string scenario,
            HttpContext httpContext) =>
        {
            if (!GovernanceDecision.TryFromScenario(
                    scenario,
                    out GovernanceDecision decision))
            {
                return Results.NotFound();
            }

            return GovernanceHttpMapper.ToHttpResult(
                decision,
                httpContext);
        });

        return app;
    }

    private static IResult UnexpectedFailure()
    {
        throw new InvalidOperationException(
            "Connection string Server=private-db;Password=super-secret");
    }

    private static IResult KnownCatalogFailure()
    {
        throw new CatalogUnavailableException(
            "Catalog database sql.internal.example failed with password=demo-secret");
    }
}
