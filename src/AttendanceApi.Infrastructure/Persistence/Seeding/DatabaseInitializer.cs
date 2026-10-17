using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AttendanceApi.Infrastructure.Persistence.Seeding;

/// <summary>
/// Runs at startup: optionally applies migrations, then seeds the standard attendance codes (only into an
/// empty table), the first admin (only when no admin exists) and, when enabled, demo data (only into a
/// database without schools). Safe to run on every start.
/// </summary>
public sealed partial class DatabaseInitializer
{
    private readonly AttendanceDbContext _db;
    private readonly IPasswordHasher<AppUser> _passwordHasher;
    private readonly DatabaseOptions _databaseOptions;
    private readonly SeedAdminOptions _seedAdmin;
    private readonly DemoDataOptions _demoData;
    private readonly IClock _clock;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        AttendanceDbContext db,
        IPasswordHasher<AppUser> passwordHasher,
        IOptions<DatabaseOptions> databaseOptions,
        IOptions<SeedAdminOptions> seedAdmin,
        IOptions<DemoDataOptions> demoData,
        IClock clock,
        ILogger<DatabaseInitializer> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _databaseOptions = databaseOptions.Value;
        _seedAdmin = seedAdmin.Value;
        _demoData = demoData.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_databaseOptions.ApplyMigrationsOnStartup)
            {
                await _db.Database.MigrateAsync(cancellationToken);
            }

            await SeedAttendanceCodesAsync(cancellationToken);
            await SeedAdminAsync(cancellationToken);
            await SeedDemoDataAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Logged with context here; rethrown so the host stops instead of serving without a usable database.
            LogInitializationFailed(ex, _databaseOptions.ApplyMigrationsOnStartup);
            throw;
        }
    }

    public async Task SeedAttendanceCodesAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.AttendanceCodes.AnyAsync(cancellationToken))
        {
            return;
        }

        var codes = StandardAttendanceCodes.Create();
        _db.AttendanceCodes.AddRange(codes);
        await _db.SaveChangesAsync(cancellationToken);
        LogCodesSeeded(codes.Count);
    }

    public async Task SeedAdminAsync(CancellationToken cancellationToken = default)
    {
        if (await _db.AppUsers.AnyAsync(u => u.Role == UserRole.Admin, cancellationToken))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_seedAdmin.UserName) || string.IsNullOrEmpty(_seedAdmin.Password))
        {
            LogNoAdmin();
            return;
        }

        var displayName = string.IsNullOrWhiteSpace(_seedAdmin.DisplayName) ? "Administrator" : _seedAdmin.DisplayName;
        var admin = AppUser.Create(_seedAdmin.UserName, displayName, _seedAdmin.Email, "pending", UserRole.Admin);
        admin.SetPasswordHash(_passwordHasher.HashPassword(admin, _seedAdmin.Password));
        _db.AppUsers.Add(admin);
        await _db.SaveChangesAsync(cancellationToken);
        LogAdminSeeded(admin.UserName);
    }

    public async Task SeedDemoDataAsync(CancellationToken cancellationToken = default)
    {
        if (!_demoData.Enabled)
        {
            return;
        }

        var seeded = await new DemoDataSeeder(_db, _passwordHasher)
            .SeedAsync(_clock.Today, _clock.UtcNow, _demoData.UserPassword, cancellationToken);
        if (seeded)
        {
            LogDemoSeeded(!string.IsNullOrEmpty(_demoData.UserPassword));
        }
        else
        {
            LogDemoSkipped();
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Demo data seeded (demo user accounts: {WithUsers}). Not for production use")]
    private partial void LogDemoSeeded(bool withUsers);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data is enabled but the database already has schools; nothing was added")]
    private partial void LogDemoSkipped();

    [LoggerMessage(Level = LogLevel.Critical, Message = "Database initialization failed (apply migrations: {ApplyMigrations}). Check ConnectionStrings:AttendanceDb and that SQL Server is reachable")]
    private partial void LogInitializationFailed(Exception exception, bool applyMigrations);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded {Count} standard attendance codes")]
    private partial void LogCodesSeeded(int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seeded admin account {UserName}")]
    private partial void LogAdminSeeded(string userName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No admin account exists and SeedAdmin:UserName/SeedAdmin:Password are not configured; nobody can sign in as an admin")]
    private partial void LogNoAdmin();
}
