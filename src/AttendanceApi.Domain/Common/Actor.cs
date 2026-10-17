namespace AttendanceApi.Domain.Common;

/// <summary>
/// The authenticated user who performs a write. Stored on every audited row as an ID and a name.
/// <see cref="UserId"/> is null only for system writes such as the legacy import.
/// </summary>
public sealed record Actor
{
    public const int MaxNameLength = 100;

    public Actor(int? userId, string userName)
    {
        UserId = userId;
        UserName = Guard.Required(userName, MaxNameLength, "User name");
    }

    public int? UserId { get; }

    public string UserName { get; }
}
