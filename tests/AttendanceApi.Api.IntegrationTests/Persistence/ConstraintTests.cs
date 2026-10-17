using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Schools;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Persistence;

public class ConstraintTests : PersistenceTestBase
{
    private static readonly DateOnly Date = new(2026, 10, 15);

    public ConstraintTests(SqlServerFixture server)
        : base(server)
    {
    }

    private static ChronicAbsenceResult Chronic() =>
        ChronicAbsenceEvaluator.Evaluate(10, EnrolledDaysResult.Determinable(30), ChronicMethod.Count, 10, 10m);

    [Fact]
    public async Task A_student_can_have_only_one_record_per_day()
    {
        var s = await SeedScenarioAsync();
        await using var db = NewContext();
        var code = await db.AttendanceCodes.SingleAsync();
        db.AttendanceRecords.Add(NewRecord(s, Date, code));
        await db.SaveChangesAsync();

        await using var second = NewContext();
        second.AttendanceRecords.Add(NewRecord(s, Date, code));

        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Only_one_open_chronic_alert_per_student_per_year_but_a_new_one_after_resolving()
    {
        var s = await SeedScenarioAsync();
        var year = new SchoolYearLabel(2026);
        await using (var db = NewContext())
        {
            db.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(s.StudentId, s.SchoolId, year, Chronic(), UtcNow));
            await db.SaveChangesAsync();
        }

        await using (var duplicate = NewContext())
        {
            duplicate.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(s.StudentId, s.SchoolId, year, Chronic(), UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicate.SaveChangesAsync());
        }

        await using (var resolve = NewContext())
        {
            (await resolve.StudentAlerts.SingleAsync()).Resolve(SystemActor, UtcNow.AddDays(1));
            resolve.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(s.StudentId, s.SchoolId, year, Chronic(), UtcNow.AddDays(2)));
            resolve.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(s.StudentId, s.SchoolId, new SchoolYearLabel(2027), Chronic(), UtcNow.AddDays(2)));
            await resolve.SaveChangesAsync();
        }

        await using var check = NewContext();
        Assert.Equal(2, await check.StudentAlerts.CountAsync(a => a.ResolvedAt == null));
    }

    [Fact]
    public async Task School_year_label_is_unique_per_school()
    {
        var s = await SeedScenarioAsync();
        await using var db = NewContext();
        db.SchoolYears.Add(SchoolYear.Create(s.SchoolId, new DateOnly(2026, 8, 17), new DateOnly(2026, 8, 31)));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Attendance_code_is_unique()
    {
        await SeedScenarioAsync();
        await using var db = NewContext();
        db.AttendanceCodes.Add(AttendanceCode.Create("a", "Duplicate in another case", isAbsent: true, isExcused: false));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Check_constraints_reject_values_the_domain_never_produces()
    {
        var s = await SeedScenarioAsync();
        await using var db = NewContext();
        db.AttendanceRecords.Add(NewRecord(s, Date, await db.AttendanceCodes.SingleAsync()));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE AttendanceRecord SET MinutesLate = -1"));
        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE SchoolAttendanceSettings SET ChronicMethod = 'Sometimes'"));
        await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE StudentEnrollment SET ExitDate = '2026-08-01'"));
    }

    [Fact]
    public async Task Records_must_reference_existing_students()
    {
        var s = await SeedScenarioAsync();
        await using var db = NewContext();
        var code = await db.AttendanceCodes.SingleAsync();
        db.AttendanceRecords.Add(NewRecord(s with { StudentId = 9999 }, Date, code));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
