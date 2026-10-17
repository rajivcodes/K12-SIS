namespace AttendanceApi.Domain.Users;

/// <summary>A school a non-admin user may access.</summary>
public sealed class AppUserSchool
{
    private AppUserSchool()
    {
    }

    internal AppUserSchool(int userId, int schoolId)
    {
        UserId = userId;
        SchoolId = schoolId;
    }

    public int UserId { get; private set; }

    public int SchoolId { get; private set; }
}
