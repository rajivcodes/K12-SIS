using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Submissions;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Persistence;

public class RoundTripTests : PersistenceTestBase
{
    public RoundTripTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Fact]
    public async Task School_year_and_terms_round_trip()
    {
        int schoolId;
        await using (var db = NewContext())
        {
            var school = School.Create("Roosevelt Middle");
            db.Schools.Add(school);
            await db.SaveChangesAsync();
            schoolId = school.Id;

            var year = SchoolYear.Create(school.Id, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4));
            year.AddTerm("Fall", new DateOnly(2026, 8, 17), new DateOnly(2026, 12, 18));
            year.AddTerm("Spring", new DateOnly(2027, 1, 5), new DateOnly(2027, 6, 4));
            db.SchoolYears.Add(year);
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        var loaded = await check.SchoolYears.Include(y => y.Terms).SingleAsync(y => y.SchoolId == schoolId);
        Assert.Equal("2026-2027", loaded.Label);
        Assert.Equal(new DateOnly(2026, 8, 17), loaded.StartDate);
        Assert.Equal(["Fall", "Spring"], loaded.Terms.OrderBy(t => t.StartDate).Select(t => t.Name));
        Assert.Equal("Spring", loaded.FindTerm(new DateOnly(2027, 3, 1))?.Name);
    }

    [Fact]
    public async Task Attendance_record_round_trips_with_UTC_timestamps_and_a_row_version()
    {
        var s = await SeedScenarioAsync();
        await using (var db = NewContext())
        {
            var code = await db.AttendanceCodes.SingleAsync();
            var record = AttendanceRecord.Create(
                s.StudentId,
                new AttendancePlacement(s.SchoolId, new DateOnly(2026, 10, 15), s.SchoolYearId, s.TermId),
                code,
                5,
                "Bus was late",
                new Domain.Common.Actor(null, "clerk"),
                UtcNow);
            db.AttendanceRecords.Add(record);
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        var loaded = await check.AttendanceRecords.SingleAsync();
        Assert.Equal((s.StudentId, s.SchoolId, s.SchoolYearId, (int?)s.TermId), (loaded.StudentId, loaded.SchoolId, loaded.SchoolYearId, loaded.TermId));
        Assert.Equal((true, false, 5, "Bus was late", "clerk"), (loaded.IsAbsent, loaded.IsExcused, loaded.MinutesLate, loaded.Notes, loaded.CreatedBy));
        Assert.Equal(UtcNow, loaded.CreatedAt);
        Assert.Equal(DateTimeKind.Utc, loaded.CreatedAt.Kind);
        Assert.NotEmpty(loaded.RowVersion);
    }

    [Fact]
    public async Task Enums_are_stored_by_name()
    {
        var s = await SeedScenarioAsync();
        await using (var db = NewContext())
        {
            var settings = await db.SchoolAttendanceSettings.SingleAsync(x => x.SchoolId == s.SchoolId);
            settings.SetChronicRules(ChronicMethod.Either, 12, 12.5m);
            db.AppUsers.Add(AppUser.Create("clerk", "Clerk", null, "hash", UserRole.AttendanceClerk));
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        Assert.Equal("Either", await check.Database.SqlQueryRaw<string>("SELECT ChronicMethod AS Value FROM SchoolAttendanceSettings").SingleAsync());
        Assert.Equal("AttendanceClerk", await check.Database.SqlQueryRaw<string>("SELECT Role AS Value FROM AppUser").SingleAsync());
        var settingsBack = await check.SchoolAttendanceSettings.SingleAsync();
        Assert.Equal((ChronicMethod.Either, 12, 12.5m), (settingsBack.ChronicMethod, settingsBack.ChronicCountThreshold, settingsBack.ChronicRateThreshold));
    }

    [Fact]
    public async Task User_school_assignments_round_trip()
    {
        var s = await SeedScenarioAsync();
        await using (var db = NewContext())
        {
            var clerk = AppUser.Create("clerk", "Clerk", null, "hash", UserRole.AttendanceClerk);
            clerk.AssignSchools([s.SchoolId]);
            db.AppUsers.Add(clerk);
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        var loaded = await check.AppUsers.Include(u => u.Schools).SingleAsync();
        Assert.True(loaded.CanAccessSchool(s.SchoolId));
        Assert.False(loaded.CanAccessSchool(s.SchoolId + 1));
    }

    [Fact]
    public async Task Submission_and_issues_round_trip_including_an_unknown_student()
    {
        var s = await SeedScenarioAsync();
        var date = new DateOnly(2026, 10, 15);
        await using (var db = NewContext())
        {
            SkippedAttendanceRow[] skipped =
            [
                new(0, 9999, false, "P", SubmissionIssueReason.UnknownStudent, "Student 9999 does not exist."),
                new(1, s.StudentId, true, "ZZ", SubmissionIssueReason.UnknownCode, "Attendance code 'ZZ' does not exist."),
            ];
            db.AttendanceSubmissions.Add(AttendanceSubmission.Record(s.SchoolId, date, SystemActor, UtcNow, 3, 1, skipped));
            await db.SaveChangesAsync();
        }

        await using var check = NewContext();
        var submission = await check.AttendanceSubmissions.Include(x => x.Issues).SingleAsync();
        Assert.Equal((3, 1, 2), (submission.RowsReceived, submission.RowsSaved, submission.RowsSkipped));
        Assert.Equal(
            [((int?)null, SubmissionIssueReason.UnknownStudent), (s.StudentId, SubmissionIssueReason.UnknownCode)],
            submission.Issues.OrderBy(i => i.Id).Select(i => (i.StudentId, i.Reason)));
    }
}
