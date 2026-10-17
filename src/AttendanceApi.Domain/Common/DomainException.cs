namespace AttendanceApi.Domain.Common;

/// <summary>
/// Thrown when an operation would break a domain invariant (for example a term outside its school year).
/// The API maps it to a 400/422 ProblemDetails response.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
