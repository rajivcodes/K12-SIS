using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace AttendanceApi.Api.Infrastructure;

/// <summary>
/// Turns expected application errors into RFC 7807 ProblemDetails (plan §5). Handled here rather than in the
/// exception-handler middleware so expected outcomes (403, 404, 422…) aren't logged as server errors.
/// </summary>
internal sealed partial class AppExceptionFilter : IExceptionFilter
{
    private readonly ProblemDetailsFactory _factory;
    private readonly ILogger<AppExceptionFilter> _logger;

    public AppExceptionFilter(ProblemDetailsFactory factory, ILogger<AppExceptionFilter> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        var (status, title, reason) = context.Exception switch
        {
            RequestValidationException e => (StatusCodes.Status400BadRequest, e.Title, null),
            DomainException { Kind: DomainErrorKind.Conflict } => (StatusCodes.Status409Conflict, "Conflict", null),
            DomainException => (StatusCodes.Status400BadRequest, "Invalid request", null),
            ConflictException e => (StatusCodes.Status409Conflict, e.Title, null),
            AccountInactiveException e => (StatusCodes.Status401Unauthorized, e.Title, null),
            ForbiddenException e => (StatusCodes.Status403Forbidden, e.Title, null),
            NotFoundException e => (StatusCodes.Status404NotFound, e.Title, null),
            BusinessRuleException e => (StatusCodes.Status422UnprocessableEntity, e.Title, e.Reason),
            _ => (0, null, (string?)null),
        };
        if (status == 0)
        {
            return; // unexpected: left to UnhandledExceptionHandler (logged as an error, 500)
        }

        LogRejected(status, context.HttpContext.Request.Method, context.HttpContext.Request.Path, context.Exception.Message);

        var problem = _factory.CreateProblemDetails(context.HttpContext, status, title, detail: context.Exception.Message);
        if (reason is not null)
        {
            problem.Extensions["reason"] = reason;
        }

        context.Result = new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" },
        };
        context.ExceptionHandled = true;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Request {Method} {Path} rejected with {StatusCode}: {Reason}")]
    private partial void LogRejected(int statusCode, string method, string path, string reason);
}
