using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Infrastructure;

/// <summary>
/// Last line of defence for exceptions nothing else handled: logs the full exception once (with the
/// correlation ID from the logging scope) and returns a generic 500 ProblemDetails without internal details.
/// </summary>
internal sealed partial class UnhandledExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<UnhandledExceptionHandler> _logger;

    public UnhandledExceptionHandler(IProblemDetailsService problemDetails, ILogger<UnhandledExceptionHandler> logger)
    {
        _problemDetails = problemDetails;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            LogCancelled(httpContext.Request.Method, httpContext.Request.Path);
            httpContext.Response.StatusCode = 499; // client closed request; nobody reads the body
            return true;
        }

        LogUnhandled(exception, httpContext.Request.Method, httpContext.Request.Path, httpContext.User.Identity?.Name);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred",
                Detail = "The error has been logged. Quote the correlation ID when reporting it.",
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path} (user {UserName})")]
    private partial void LogUnhandled(Exception exception, string method, string path, string? userName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Request {Method} {Path} was cancelled by the client")]
    private partial void LogCancelled(string method, string path);
}
