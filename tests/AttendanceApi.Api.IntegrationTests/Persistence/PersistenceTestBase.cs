using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using AttendanceApi.Infrastructure.Persistence;

namespace AttendanceApi.Api.IntegrationTests.Persistence;

[Collection(SqlServerCollection.Name)]
public abstract class PersistenceTestBase : IAsyncLifetime
{
    protected static readonly DateTime UtcNow = new(2026, 10, 15, 14, 0, 0, DateTimeKind.Utc);
    protected static readonly Actor SystemActor = new(null, "system");

    private readonly SqlServerFixture _server;
    private TestDatabase? _database;

    protected PersistenceTestBase(SqlServerFixture server)
    {
        _server = server;
    }

    protected TestDatabase Database => _database ?? throw new InvalidOperationException("Database not created yet.");

    public async Task InitializeAsync() => _database = await TestDatabase.CreateAsync(_server);

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    protected AttendanceDbContext NewContext() => Database.CreateContext();

    /// <summary>A school with default settings, a default 2026-2027 year, one enrolled student and the code "A".</summary>
    protected async Task<Scenario> SeedScenarioAsync()
    {
        await using var db = NewContext();
        var school = School.Create("Lincoln Elementary");
        var student = Student.Create("Ada", "Lovelace", "05", new DateOnly(2015, 12, 10));
        var code = AttendanceCode.Create("A", "Absent, unexcused", isAbsent: true, isExcused: false);
        db.AddRange(school, student, code);
        await db.SaveChangesAsync();

        var year = SchoolYear.CreateDefault(school.Id, new DateOnly(2026, 10, 15));
        db.AddRange(
            SchoolAttendanceSettings.CreateDefault(school.Id),
            year,
            StudentEnrollment.Create(student.Id, school.Id, new DateOnly(2026, 9, 1)));
        await db.SaveChangesAsync();

        return new Scenario(school.Id, student.Id, code.Id, year.Id, year.Terms.Single().Id);
    }

    protected static AttendanceRecord NewRecord(Scenario s, DateOnly date, AttendanceCode code) =>
        AttendanceRecord.Create(s.StudentId, new AttendancePlacement(s.SchoolId, date, s.SchoolYearId, s.TermId), code, 0, null, SystemActor, UtcNow);

    protected sealed record Scenario(int SchoolId, int StudentId, int CodeId, int SchoolYearId, int TermId);
}
