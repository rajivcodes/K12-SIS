using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AttendanceApi.Infrastructure.Persistence;

/// <summary>
/// Used by <c>dotnet ef</c> only. Adding a migration needs no database; for <c>dotnet ef database update</c>
/// pass <c>--connection</c> or set the <c>ConnectionStrings__AttendanceDb</c> environment variable.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AttendanceDbContext>
{
    public AttendanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AttendanceDb")
            ?? "Server=localhost,1433;Database=AttendanceApi;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<AttendanceDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AttendanceDbContext(options);
    }
}
