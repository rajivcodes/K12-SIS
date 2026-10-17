using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AttendanceApi.Api.IntegrationTests.Persistence;

public class MigrationTests : PersistenceTestBase
{
    public MigrationTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Fact]
    public async Task Migrations_apply_to_an_empty_database_with_none_left_pending()
    {
        await using var db = NewContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
    }

    [Fact]
    public void Model_has_no_changes_missing_from_the_migrations()
    {
        // Fails when an entity or configuration changes without `dotnet ef migrations add`.
        using var db = new AttendanceDbContext(
            new DbContextOptionsBuilder<AttendanceDbContext>().UseSqlServer("Server=unused").Options);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot?.Model
            ?? throw new InvalidOperationException("No model snapshot.");
        if (snapshot is IMutableModel mutable)
        {
            snapshot = mutable.FinalizeModel();
        }

        var snapshotModel = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot);
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(
            snapshotModel.GetRelationalModel(),
            db.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }
}
