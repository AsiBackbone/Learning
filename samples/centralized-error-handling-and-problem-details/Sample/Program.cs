using CentralizedErrorHandlingAndProblemDetails;

WebApplicationBuilder builder =
    SampleApplication.CreateBuilder(args);

WebApplication app =
    SampleApplication.Configure(builder.Build());

app.Run();

public partial class Program
{
}
