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

    private static DatabaseInitializer Initializer(AttendanceDbContext db, SeedAdminOptions? admin = null) =>
        new(
            db,
            new PasswordHasher<AppUser>(),
            Options.Create(new DatabaseOptions { ApplyMigrationsOnStartup = true }),
            Options.Create(admin ?? ConfiguredAdmin),
            NullLogger<DatabaseInitializer>.Instance);

    private async Task InitializeAsync(SeedAdminOptions? admin = null)
    {
        await using var db = NewContext();
        await Initializer(db, admin).InitializeAsync();
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
}
