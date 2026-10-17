using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Api.IntegrationTests.Persistence;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;
using AttendanceApi.LegacyImport.Import;
using AttendanceApi.LegacyImport.Legacy;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AttendanceApi.Api.IntegrationTests.LegacyImport;

/// <summary>Imports a sample legacy database (assumed schema + data with known defects) into a fresh target.</summary>
public class LegacyImportTests : PersistenceTestBase
{
    private static readonly DateTime ImportTime = new(2026, 10, 17, 6, 0, 0, DateTimeKind.Utc);

    private readonly SqlServerFixture _server;
    private string _legacyConnection = string.Empty;

    public LegacyImportTests(SqlServerFixture server)
        : base(server)
    {
        _server = server;
    }

    private async Task CreateLegacyDatabaseAsync()
    {
        var builder = new SqlConnectionStringBuilder(_server.ServerConnectionString) { InitialCatalog = "LegacyTest_" + Guid.NewGuid().ToString("N") };
        await using (var master = new SqlConnection(_server.ServerConnectionString))
        {
            await master.OpenAsync();
            await using var create = new SqlCommand($"CREATE DATABASE [{builder.InitialCatalog}]", master);
            await create.ExecuteNonQueryAsync();
        }

        _legacyConnection = builder.ConnectionString;
        await ExecuteLegacyAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "LegacyImport", "assumed-legacy-schema.sql")));
        await ExecuteLegacyAsync(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "LegacyImport", "legacy-sample-data.sql")));
    }

    private async Task ExecuteLegacyAsync(string sql)
    {
        await using var connection = new SqlConnection(_legacyConnection);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<ImportReport> ImportAsync(bool dryRun = false)
    {
        await using var legacy = new LegacyDbContext(new DbContextOptionsBuilder<LegacyDbContext>().UseSqlServer(_legacyConnection).Options);
        await using var target = NewContext();
        var importer = new LegacyImporter(legacy, target, NullLogger<LegacyImporter>.Instance, () => ImportTime);
        return await importer.RunAsync(new ImportOptions(dryRun, ApplyMigrations: false, StudentBatchSize: 2));
    }

    private static (int Read, int Inserted, int Updated, int Unchanged, int Skipped) Counts(ImportReport report, string table)
    {
        var c = report.Tables[table];
        return (c.Read, c.Inserted, c.Updated, c.Unchanged, c.Skipped);
    }

    [Fact]
    public async Task Imports_the_legacy_database_and_reports_every_problem_row()
    {
        await CreateLegacyDatabaseAsync();

        var report = await ImportAsync();

        Assert.Equal((3, 3, 0, 0, 0), Counts(report, "Schools"));
        Assert.Equal((7, 4, 0, 0, 3), Counts(report, "SchoolTerms"));
        Assert.Equal((6, 1, 0, 5, 0), Counts(report, "AttendanceCodes"));
        Assert.Equal((6, 5, 0, 0, 1), Counts(report, "Students"));
        Assert.Equal((15, 11, 0, 0, 4), Counts(report, "StudentAttendance"));
        Assert.Equal((5, 2, 0, 0, 3), Counts(report, "StudentAlerts"));
        Assert.Equal(1, report.CountIssues(IssueCategories.Duplicate));
        Assert.Equal(1, report.CountIssues(IssueCategories.UnknownCode));
        Assert.Equal(1, report.CountIssues(IssueCategories.LegacyCode));
        Assert.Equal(1, report.CountIssues(IssueCategories.CodeDifference));
        Assert.Equal(1, report.CountIssues(IssueCategories.DuplicateAlert));
        Assert.Contains("| StudentAttendance |", report.ToMarkdown(), StringComparison.Ordinal);

        await using var db = NewContext();
        var schools = await db.Schools.OrderBy(s => s.Name).ToListAsync();
        Assert.Equal(["Closed School", "Lincoln Elementary", "Roosevelt Middle"], schools.Select(s => s.Name));
        Assert.False(schools[0].IsActive);
        var lincoln = schools[1];
        Assert.Equal(8, (await db.SchoolAttendanceSettings.SingleAsync(s => s.SchoolId == lincoln.Id)).ChronicCountThreshold);

        // Calendar: 2025-2026 starts with the August term and ends the day before 2026-2027 starts.
        var years = await db.SchoolYears.Include(y => y.Terms).Where(y => y.SchoolId == lincoln.Id).OrderBy(y => y.StartDate).ToListAsync();
        Assert.Equal(
            [("2025-2026", new DateOnly(2025, 8, 20), new DateOnly(2026, 8, 18), 2), ("2026-2027", new DateOnly(2026, 8, 19), new DateOnly(2027, 8, 18), 1)],
            years.Select(y => (y.Label, y.StartDate, y.EndDate, y.Terms.Count)));

        // Students and enrollments: the student whose school doesn't exist has none.
        Assert.Equal(5, await db.Students.CountAsync());
        Assert.Equal(4, await db.StudentEnrollments.CountAsync());
    }

    [Fact]
    public async Task Attendance_keeps_the_stored_flags_and_legacy_audit_fields_and_totals_are_recalculated()
    {
        await CreateLegacyDatabaseAsync();
        await ImportAsync();

        await using var db = NewContext();
        var ada = await db.Students.SingleAsync(s => s.LastName == "Lovelace");
        var alan = await db.Students.SingleAsync(s => s.LastName == "Turing");
        var records = await db.AttendanceRecords.ToListAsync();
        var codes = await db.AttendanceCodes.ToDictionaryAsync(c => c.Id, c => c);

        // Row 4: code A but the legacy row stored "present" – the stored flags are kept (plan §8).
        var flagged = records.Single(r => r.StudentId == ada.Id && r.Date == new DateOnly(2026, 9, 17));
        Assert.Equal(("A", false), (codes[flagged.AttendanceCodeId].Code, flagged.IsAbsent));
        Assert.Equal(("DOMAIN\\clerk1", new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)), (flagged.CreatedBy, flagged.CreatedAt));

        // Duplicate: the most recently changed legacy row wins.
        var duplicate = records.Single(r => r.StudentId == alan.Id && r.Date == new DateOnly(2026, 9, 15));
        Assert.Equal(("A", "Second save", "DOMAIN\\clerk2"), (codes[duplicate.AttendanceCodeId].Code, duplicate.Notes, duplicate.ModifiedBy));

        // Unknown code ZZ kept under an inactive placeholder; negative minutes imported as 0.
        var zz = codes.Values.Single(c => c.Code == "ZZ");
        Assert.False(zz.IsActive);
        Assert.True(records.Single(r => r.AttendanceCodeId == zz.Id).IsAbsent);
        Assert.Equal(0, records.Single(r => r.StudentId == alan.Id && r.Date == new DateOnly(2026, 9, 17)).MinutesLate);

        // Totals come from the imported records per school year, not from any legacy summary.
        var summaries = await db.StudentAttendanceSummaries.Where(s => s.StudentId == ada.Id).OrderBy(s => s.SchoolYearLabel).ToListAsync();
        Assert.Equal([("2025-2026", 2, 0), ("2026-2027", 2, 1)], summaries.Select(s => (s.SchoolYearLabel, s.TotalAbsences, s.ExcusedAbsences)));

        // Alerts: the newest open chronic alert is kept, the resolved one keeps its resolution.
        var alerts = await db.StudentAlerts.OrderBy(a => a.RaisedAt).ToListAsync();
        Assert.Collection(
            alerts,
            a => Assert.Equal((alan.Id, "2025-2026", false, "DOMAIN\\admin"), (a.StudentId, a.SchoolYearLabel, a.IsOpen, a.ResolvedBy)),
            a => Assert.Equal((ada.Id, "2026-2027", true, "Student has reached 10 absences"), (a.StudentId, a.SchoolYearLabel, a.IsOpen, a.Message)));
    }

    [Fact]
    public async Task Running_again_changes_nothing_and_never_overwrites_records_entered_in_the_new_system()
    {
        await CreateLegacyDatabaseAsync();
        await ImportAsync();
        await using (var db = NewContext())
        {
            // After the first import, someone records 18 Sep in the new system; legacy also has a row for it.
            var grace = await db.Students.SingleAsync(s => s.LastName == "Hopper");
            var year = await db.SchoolYears.Include(y => y.Terms).SingleAsync(y => y.SchoolId == db.StudentEnrollments.Single(e => e.StudentId == grace.Id).SchoolId);
            var present = await db.AttendanceCodes.SingleAsync(c => c.Code == "P");
            var clerk = Domain.Users.AppUser.Create("clerk", "Clerk", null, "hash", Domain.Users.UserRole.AttendanceClerk);
            db.AppUsers.Add(clerk);
            await db.SaveChangesAsync();
            db.AttendanceRecords.Add(AttendanceRecord.Create(grace.Id, new AttendancePlacement(year.SchoolId, new DateOnly(2026, 9, 18), year.Id, year.FindTerm(new DateOnly(2026, 9, 18))?.Id), present, 0, "Entered in new system", new Actor(clerk.Id, clerk.UserName), ImportTime));
            await db.SaveChangesAsync();
        }

        await ExecuteLegacyAsync("INSERT INTO StudentAttendance (AttendanceID, StudentID, SchoolID, AttendDate, AttendCode, IsAbsent, IsExcused) VALUES (16, 102, 2, '2026-09-18', 'A', 1, 0)");

        var second = await ImportAsync();

        Assert.Equal((3, 0, 0, 3, 0), Counts(second, "Schools"));
        Assert.Equal(0, second.Tables["SchoolTerms"].Inserted);
        Assert.Equal((6, 0, 0, 5, 1), Counts(second, "Students"));
        Assert.Equal((16, 0, 11, 0, 5), Counts(second, "StudentAttendance"));
        Assert.Equal(0, second.Tables["StudentAlerts"].Inserted);
        Assert.Equal(1, second.CountIssues(IssueCategories.KeptNewSystem));

        await using var check = NewContext();
        Assert.Equal(12, await check.AttendanceRecords.CountAsync());
        Assert.Equal("Entered in new system", (await check.AttendanceRecords.SingleAsync(r => r.Date == new DateOnly(2026, 9, 18))).Notes);
        Assert.Equal(5, await check.Students.CountAsync());
        Assert.Equal(2, await check.StudentAlerts.CountAsync());
        Assert.Equal(2, await check.AuditEntries.CountAsync(a => a.Action == "LegacyImportRun"));
    }

    [Fact]
    public async Task Running_again_keeps_corrections_made_in_the_new_system_to_imported_records()
    {
        await CreateLegacyDatabaseAsync();
        await ImportAsync();
        DateOnly date;
        await using (var db = NewContext())
        {
            // A clerk corrects an imported absence to excused through the API (same record, now modified by a user).
            var clerk = Domain.Users.AppUser.Create("clerk", "Clerk", null, "hash", Domain.Users.UserRole.AttendanceClerk);
            db.AppUsers.Add(clerk);
            await db.SaveChangesAsync();
            var ada = await db.Students.SingleAsync(s => s.LastName == "Lovelace");
            var record = await db.AttendanceRecords.SingleAsync(r => r.StudentId == ada.Id && r.Date == new DateOnly(2026, 9, 15));
            date = record.Date;
            var excused = await db.AttendanceCodes.SingleAsync(c => c.Code == "E");
            record.Update(new AttendancePlacement(record.SchoolId, record.Date, record.SchoolYearId, record.TermId), excused, 0, "Note from parent", new Actor(clerk.Id, clerk.UserName), ImportTime);
            await db.SaveChangesAsync();
        }

        var second = await ImportAsync();

        Assert.Equal(1, second.CountIssues(IssueCategories.KeptNewSystem));
        await using var check = NewContext();
        var kept = await check.AttendanceRecords.SingleAsync(r => r.Date == date && r.Notes == "Note from parent");
        Assert.True(kept.IsExcused);
        Assert.Equal("clerk", kept.ModifiedBy);
    }

    [Fact]
    public async Task A_dry_run_reports_everything_and_changes_nothing()
    {
        await CreateLegacyDatabaseAsync();

        var report = await ImportAsync(dryRun: true);

        Assert.True(report.DryRun);
        Assert.Equal(11, report.Tables["StudentAttendance"].Inserted);
        await using var db = NewContext();
        Assert.False(await db.Schools.AnyAsync());
        Assert.False(await db.AttendanceCodes.AnyAsync());
        Assert.False(await db.LegacyIdMaps.AnyAsync());
    }
}
