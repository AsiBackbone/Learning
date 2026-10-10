using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace AuthorizationToGovernanceComparison;

public static class SampleApplication
{
    public static WebApplicationBuilder CreateBuilder(string[] args, OperationalLogSink logSink)
    {
        ArgumentNullException.ThrowIfNull(logSink);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Logging.AddProvider(logSink);

        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services
            .AddAuthentication(DemoAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DemoAuthenticationHandler>(
                DemoAuthenticationHandler.SchemeName,
                configureOptions: null);

        builder.Services
            .AddAuthorizationBuilder()
            .AddPolicy(
                EndpointAuthorizationVariant.PolicyName,
                policy => policy.RequireRole(Fixtures.AdministratorRole))
            .AddPolicy(
                ResourceAuthorizationVariant.PolicyName,
                policy => policy.AddRequirements(new DisableAccountRequirement()));

        builder.Services.AddSingleton<IAuthorizationHandler, DisableAccountHandler>();

        // Shared by all three variants: the same accounts and the same recorded side effect.
        builder.Services.AddSingleton<AccountStore>();
        builder.Services.AddSingleton<RecordingAccountDisabler>();
        builder.Services.AddSingleton<IAccountDisabler>(services =>
            services.GetRequiredService<RecordingAccountDisabler>());
        builder.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(FixedTimeProvider.SampleUtc));

        // Variant 2 evidence.
        builder.Services.AddSingleton<ApplicationAuditLog>();

        // Variant 3 evidence and execution boundary.
        builder.Services.AddSingleton<DecisionLog>();
        builder.Services.AddSingleton<ExecutionLog>();
        builder.Services.AddSingleton<GovernedExecutionBoundary>();

        return builder;
    }

    public static WebApplication Configure(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAuthentication();
        app.UseAuthorization();

        EndpointAuthorizationVariant.Map(app);
        ResourceAuthorizationVariant.Map(app);
        GovernedExecutionVariant.Map(app);

        return app;
    }
}
