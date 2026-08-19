using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CentralizedErrorHandlingAndProblemDetails;

public sealed class ApplicationExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApplicationExceptionHandler> logger) : IExceptionHandler
{
    public const int HandledExceptionEventId = 5101;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        ExceptionProblem problem = Map(exception);

        string traceId =
            Activity.Current?.TraceId.ToString()
            ?? httpContext.TraceIdentifier;

        ErrorHandlingLog.ApplicationExceptionHandled(
            logger,
            exception,
            problem.StatusCode,
            problem.Code,
            traceId,
            httpContext.Request.Path.Value ?? "/");

        var problemDetails = new ProblemDetails
        {
            Status = problem.StatusCode,
            Title = problem.Title,
            Type = problem.Type,
            Detail = problem.PublicDetail,
            Instance = httpContext.Request.Path.Value
        };

        problemDetails.Extensions["code"] = problem.Code;
        problemDetails.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = problem.StatusCode;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });
    }

    private static ExceptionProblem Map(Exception exception)
    {
        return exception switch
        {
            CatalogUnavailableException => new ExceptionProblem(
                StatusCodes.Status503ServiceUnavailable,
                "Service Unavailable",
                "/problems/catalog-unavailable",
                "dependency.catalog-unavailable",
                "The catalog is temporarily unavailable."),

            _ => new ExceptionProblem(
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "/problems/unexpected-failure",
                "unexpected.failure",
                "An unexpected error occurred. Contact support with the trace ID.")
        };
    }

    private sealed record ExceptionProblem(
        int StatusCode,
        string Title,
        string Type,
        string Code,
        string PublicDetail);
}

public sealed class CatalogUnavailableException(string message)
    : Exception(message);

internal static partial class ErrorHandlingLog
{
    [LoggerMessage(
        EventId = ApplicationExceptionHandler.HandledExceptionEventId,
        Level = LogLevel.Error,
        Message = "Application exception handled centrally. StatusCode: {StatusCode}. ProblemCode: {ProblemCode}. TraceId: {TraceId}. Path: {Path}.")]
    public static partial void ApplicationExceptionHandled(
        ILogger logger,
        Exception exception,
        int statusCode,
        string problemCode,
        string traceId,
        string path);
}
