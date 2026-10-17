using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Users;
using AttendanceApi.Infrastructure.Persistence;
using AttendanceApi.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AttendanceApi.Api.IntegrationTests.Persistence;

public class SeedingTests : PersistenceTestBase
{
    private static readonly SeedAdminOptions ConfiguredAdmin = new()
    {
        UserName = "admin",
        Password = "Initial-Admin-Pw-1",
        DisplayName = "District Admin",
        Email = "admin@example.org",
    };

    public SeedingTests(SqlServerFixture server)
        : base(server)
    {
    }

    private static DatabaseInitializer Initializer(AttendanceDbContext db, SeedAdminOptions? admin = null, DemoDataOptions? demo = null) =>
        new(
            db,
            new PasswordHasher<AppUser>(),
            Options.Create(new DatabaseOptions { ApplyMigrationsOnStartup = true }),
            Options.Create(admin ?? ConfiguredAdmin),
            Options.Create(demo ?? new DemoDataOptions()),
            new TestClock(),
            NullLogger<DatabaseInitializer>.Instance);

    private async Task InitializeAsync(SeedAdminOptions? admin = null, DemoDataOptions? demo = null)
    {
        await using var db = NewContext();
        await Initializer(db, admin, demo).InitializeAsync();
    }

    [Fact]
    public async Task Standard_codes_are_seeded_into_an_empty_table()
    {
        await InitializeAsync();

        await using var db = NewContext();
        var codes = await db.AttendanceCodes.OrderBy(c => c.Code).ToListAsync();
        Assert.Equal(["A", "E", "FT", "ISS", "M", "OSS", "P", "T", "TE"], codes.Select(c => c.Code));
        Assert.All(codes, c => Assert.True(c.IsActive));
        Assert.Equal(
            ["A", "E", "M", "OSS"],
            codes.Where(c => c.IsAbsent).Select(c => c.Code));
        Assert.Equal(
            ["E", "M", "TE"],
            codes.Where(c => c.IsExcused).Select(c => c.Code));
    }

    [Fact]
    public async Task Seeding_runs_safely_more_than_once()
    {
        await InitializeAsync();
        await InitializeAsync();

        await using var db = NewContext();
        Assert.Equal(9, await db.AttendanceCodes.CountAsync());
        Assert.Equal(1, await db.AppUsers.CountAsync());
    }

    [Fact]
    public async Task Codes_are_not_seeded_when_the_table_already_has_any_code()
    {
        await using (var db = NewContext())
        {
            db.AttendanceCodes.Add(AttendanceCode.Create("LEG", "Imported legacy code", isAbsent: true, isExcused: false));
            await db.SaveChangesAsync();
        }

        await InitializeAsync();

        await using var check = NewContext();
        Assert.Equal("LEG", Assert.Single(await check.AttendanceCodes.ToListAsync()).Code);
    }

    [Fact]
    public async Task First_admin_is_created_from_configuration_with_a_hashed_password()
    {
        await InitializeAsync();

        await using var db = NewContext();
        var admin = await db.AppUsers.SingleAsync();
        Assert.Equal(("admin", "District Admin", "admin@example.org", UserRole.Admin, true), (admin.UserName, admin.DisplayName, admin.Email, admin.Role, admin.IsActive));
        Assert.NotEqual(ConfiguredAdmin.Password, admin.PasswordHash);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            new PasswordHasher<AppUser>().VerifyHashedPassword(admin, admin.PasswordHash, ConfiguredAdmin.Password!));
    }

    [Fact]
    public async Task No_admin_is_created_when_one_already_exists()
    {
        await using (var db = NewContext())
        {
            db.AppUsers.Add(AppUser.Create("existing", "Existing Admin", null, "hash", UserRole.Admin));
            await db.SaveChangesAsync();
        }

        await InitializeAsync();

        await using var check = NewContext();
        Assert.Equal("existing", (await check.AppUsers.SingleAsync()).UserName);
    }

    [Fact]
    public async Task No_admin_is_created_when_configuration_is_missing()
    {
        await InitializeAsync(new SeedAdminOptions { UserName = "admin", Password = "" });

        await using var db = NewContext();
        Assert.False(await db.AppUsers.AnyAsync());
        Assert.Equal(9, await db.AttendanceCodes.CountAsync());
    }

    [Fact]
    public async Task Demo_data_fills_an_empty_database_once()
    {
        var demo = new DemoDataOptions { Enabled = true, UserPassword = "Demo-User-Pw-1" };

        await InitializeAsync(demo: demo);
        await InitializeAsync(demo: demo);

        await using var db = NewContext();
        Assert.Equal(["Lincoln School", "Roosevelt School"], await db.Schools.OrderBy(s => s.Name).Select(s => s.Name).ToListAsync());
        Assert.Equal(11, await db.Students.CountAsync());
        Assert.Equal(12, await db.StudentEnrollments.CountAsync()); // the transfer student has two
        Assert.True(await db.AttendanceRecords.CountAsync() > 300);
        Assert.False(await db.AttendanceRecords.AnyAsync(r => r.Date >= new DateOnly(2026, 10, 15))); // nothing for today or later
        Assert.Equal(11, await db.StudentAttendanceSummaries.CountAsync());
        Assert.True(await db.StudentAlerts.CountAsync() >= 2);
        Assert.Equal(
            ["admin", "district.viewer", "lincoln.clerk", "roosevelt.clerk"],
            await db.AppUsers.OrderBy(u => u.UserName).Select(u => u.UserName).ToListAsync());
        Assert.Equal(1, await db.AuditEntries.CountAsync(a => a.Action == "DemoDataSeeded"));
    }

    [Fact]
    public async Task Demo_data_never_touches_a_database_that_already_has_schools()
    {
        await using (var db = NewContext())
        {
            db.Schools.Add(Domain.Schools.School.Create("Real School"));
            await db.SaveChangesAsync();
        }

        await InitializeAsync(demo: new DemoDataOptions { Enabled = true });

        await using var check = NewContext();
        Assert.Equal("Real School", (await check.Schools.SingleAsync()).Name);
        Assert.False(await check.Students.AnyAsync());
    }

    [Fact]
    public async Task Demo_data_is_off_by_default()
    {
        await InitializeAsync();

        await using var db = NewContext();
        Assert.False(await db.Schools.AnyAsync());
    }
}
