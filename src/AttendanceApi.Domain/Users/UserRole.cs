namespace AttendanceApi.Domain.Users;

public enum UserRole
{
    Admin = 1,

    /// <summary>Reads and writes attendance for assigned schools.</summary>
    AttendanceClerk = 2,

    /// <summary>Reads attendance for assigned schools.</summary>
    Viewer = 3,
}

public static class UserRoleExtensions
{
    public static bool CanReadAttendance(this UserRole role) => Enum.IsDefined(role);

    public static bool CanWriteAttendance(this UserRole role) =>
        role is UserRole.Admin or UserRole.AttendanceClerk;

    /// <summary>Admins can access every school; other roles only their assigned schools (AppUserSchool).</summary>
    public static bool CanAccessSchool(this UserRole role, int schoolId, IEnumerable<int> assignedSchoolIds) =>
        role == UserRole.Admin || assignedSchoolIds.Contains(schoolId);
}
