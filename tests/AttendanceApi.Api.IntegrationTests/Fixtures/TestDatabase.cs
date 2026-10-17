using AttendanceApi.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Fixtures;

/// <summary>A freshly migrated, uniquely named database, dropped on dispose.</summary>
public sealed class TestDatabase : IAsyncDisposable
{
    private TestDatabase(string connectionString)
    {
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync(SqlServerFixture server)
    {
        var builder = new SqlConnectionStringBuilder(server.ServerConnectionString)
        {
            InitialCatalog = "AttendanceTest_" + Guid.NewGuid().ToString("N"),
        };
        var database = new TestDatabase(builder.ConnectionString);
        await using var db = database.CreateContext();
        await db.Database.MigrateAsync();
        return database;
    }

    public AttendanceDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AttendanceDbContext>().UseSqlServer(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
    }
}
