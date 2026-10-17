using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Users;

namespace AttendanceApi.Domain.Tests.Users;

public class AppUserTests
{
    [Theory]
    [InlineData(UserRole.Admin, true)]
    [InlineData(UserRole.AttendanceClerk, true)]
    [InlineData(UserRole.Viewer, false)]
    public void Only_admins_and_clerks_can_write_attendance(UserRole role, bool canWrite)
    {
        Assert.True(role.CanReadAttendance());
        Assert.Equal(canWrite, role.CanWriteAttendance());
    }

    [Fact]
    public void Admins_access_every_school_and_others_only_their_assigned_schools()
    {
        var admin = AppUser.Create("admin", "Admin", null, "hash", UserRole.Admin);
        var clerk = AppUser.Create("clerk", "Clerk", "clerk@example.org", "hash", UserRole.AttendanceClerk);
        clerk.AssignSchools([1, 2]);

        Assert.True(admin.CanAccessSchool(5));
        Assert.True(clerk.CanAccessSchool(2));
        Assert.False(clerk.CanAccessSchool(5));
    }

    [Fact]
    public void AssignSchools_replaces_the_assignment()
    {
        var viewer = AppUser.Create("viewer", "Viewer", null, "hash", UserRole.Viewer);
        viewer.AssignSchools([1, 2]);

        viewer.AssignSchools([2, 3, 3]);

        Assert.Equal([2, 3], viewer.Schools.Select(s => s.SchoolId).Order());
    }

    [Fact]
    public void Create_requires_name_hash_and_a_known_role()
    {
        Assert.Throws<DomainException>(() => AppUser.Create(" ", "X", null, "hash", UserRole.Viewer));
        Assert.Throws<DomainException>(() => AppUser.Create("x", "X", null, "", UserRole.Viewer));
        Assert.Throws<DomainException>(() => AppUser.Create("x", "X", null, "hash", (UserRole)42));
    }

    [Fact]
    public void RecordLogin_requires_UTC()
    {
        var user = AppUser.Create("x", "X", null, "hash", UserRole.Viewer);

        user.RecordLogin(TestData.UtcNow);
        Assert.Equal(TestData.UtcNow, user.LastLoginAt);

        Assert.Throws<DomainException>(() => user.RecordLogin(DateTime.Now));
    }
}
