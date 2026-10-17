namespace AttendanceApi.Application.Common;

/// <summary>Base for errors the API turns into ProblemDetails with a specific status code.</summary>
public abstract class AppException : Exception
{
    protected AppException(string title, string detail)
        : base(detail)
    {
        Title = title;
    }

    public string Title { get; }
}

/// <summary>400: the request is malformed.</summary>
public sealed class RequestValidationException : AppException
{
    public RequestValidationException(string detail)
        : base("Invalid request", detail)
    {
    }
}

/// <summary>404: an unknown school or student.</summary>
public sealed class NotFoundException : AppException
{
    public NotFoundException(string detail)
        : base("Not found", detail)
    {
    }
}

/// <summary>403: the user may not act on this school or student.</summary>
public sealed class ForbiddenException : AppException
{
    public ForbiddenException(string detail)
        : base("Forbidden", detail)
    {
    }
}

/// <summary>401: the account behind a valid token is inactive, deleted, or now has a different role.</summary>
public sealed class AccountInactiveException : AppException
{
    public AccountInactiveException()
        : base("Unauthorized", "The account is inactive, no longer exists, or its role has changed. Sign in again.")
    {
    }
}

/// <summary>409: the change clashes with existing data (duplicate name, overlapping dates).</summary>
public sealed class ConflictException : AppException
{
    public ConflictException(string detail)
        : base("Conflict", detail)
    {
    }
}

/// <summary>422: the request is well formed but breaks a business rule for the whole batch (e.g. a disallowed date).</summary>
public sealed class BusinessRuleException : AppException
{
    public BusinessRuleException(string title, string detail, string? reason = null)
        : base(title, detail)
    {
        Reason = reason;
    }

    /// <summary>A machine-readable reason, e.g. <c>WeekendNotAllowed</c>.</summary>
    public string? Reason { get; }
}
