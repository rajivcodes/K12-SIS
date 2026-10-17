namespace AttendanceApi.Domain.Common;

public enum DomainErrorKind
{
    /// <summary>The values themselves are invalid (API: 400).</summary>
    Invalid = 1,

    /// <summary>The values clash with existing data, e.g. overlapping dates or a duplicate label (API: 409).</summary>
    Conflict = 2,
}

/// <summary>
/// Thrown when an operation would break a domain invariant (for example a term outside its school year).
/// The API maps it to a 400 or 409 ProblemDetails response depending on <see cref="Kind"/>.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException(string message, DomainErrorKind kind = DomainErrorKind.Invalid)
        : base(message)
    {
        Kind = kind;
    }

    public DomainErrorKind Kind { get; }
}
