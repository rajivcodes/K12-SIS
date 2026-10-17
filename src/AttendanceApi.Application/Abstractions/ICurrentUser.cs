using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Users;

namespace AttendanceApi.Application.Abstractions;

/// <summary>The authenticated caller, read from the JWT.</summary>
public interface ICurrentUser
{
    int UserId { get; }

    string UserName { get; }

    UserRole Role { get; }

    Actor Actor => new(UserId, UserName);
}
