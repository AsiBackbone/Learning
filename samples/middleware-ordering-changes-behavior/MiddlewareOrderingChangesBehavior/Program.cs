using MiddlewareOrderingChangesBehavior;

WebApplicationBuilder builder =
    WebApplication.CreateBuilder(args);

string configuredMode =
    builder.Configuration["PipelineMode"] ?? "correct";

bool correctOrder =
    !string.Equals(
        configuredMode,
        "incorrect",
        StringComparison.OrdinalIgnoreCase);

WebApplication app =
    builder.Build();

app.Logger.LogInformation(
    "Running middleware-ordering sample in {PipelineMode} mode",
    correctOrder ? "correct" : "incorrect");

MiddlewareOrderDemo.Configure(
    app,
    correctOrder,
    pipelineEvent =>
        app.Logger.LogInformation(
            "Pipeline event: {PipelineEvent}",
            pipelineEvent));

app.Run();
