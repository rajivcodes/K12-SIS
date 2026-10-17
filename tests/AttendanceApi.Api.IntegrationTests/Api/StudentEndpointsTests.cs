using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Attendance;
using AttendanceApi.Application.Students;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Users;

namespace AttendanceApi.Api.IntegrationTests.Api;

public class StudentEndpointsTests : ApiTestBase
{
    public StudentEndpointsTests(SqlServerFixture server)
        : base(server)
    {
    }

    private async Task SaveDaysAsync(HttpClient client, int schoolId, int studentId, string code, params DateOnly[] days)
    {
        foreach (var day in days)
        {
            await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, day, new { studentId, code }));
        }
    }

    [Fact]
    public async Task History_lists_all_schools_for_the_year_newest_first()
    {
        var schoolA = await CreateSchoolAsync("Adams Elementary");
        var schoolB = await CreateSchoolAsync("Baker Middle");
        var studentId = await CreateStudentAsync("Mover", schoolA, exit: new DateOnly(2026, 9, 30));
        await EnrollAsync(studentId, schoolB, new DateOnly(2026, 10, 1));
        var client = await AdminClientAsync();
        await SaveDaysAsync(client, schoolA, studentId, "A", new DateOnly(2026, 9, 14));
        await SaveDaysAsync(client, schoolB, studentId, "T", new DateOnly(2026, 10, 14));

        var history = await ReadAsync<AttendanceHistoryResponse>(await client.GetAsync($"/api/v1/students/{studentId}/attendance"));

        Assert.Equal(("2026-2027", "Test Mover"), (history.SchoolYear, history.StudentName));
        Assert.Collection(
            history.Records,
            r => Assert.Equal((new DateOnly(2026, 10, 14), "Baker Middle", "T", "Tardy (unexcused)", false, "Full year"), (r.Date, r.SchoolName, r.Code, r.CodeDescription, r.IsAbsent, r.Term)),
            r => Assert.Equal((new DateOnly(2026, 9, 14), "Adams Elementary", "A", true, "admin"), (r.Date, r.SchoolName, r.Code, r.IsAbsent, r.RecordedBy)));
    }

    [Fact]
    public async Task History_for_another_year_and_validation_errors()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();
        await SaveDaysAsync(client, schoolId, studentId, "A", new DateOnly(2026, 10, 14));

        var lastYear = await ReadAsync<AttendanceHistoryResponse>(await client.GetAsync($"/api/v1/students/{studentId}/attendance?schoolYear=2025-2026"));
        Assert.Empty(lastYear.Records);

        await ProblemAsync(await client.GetAsync($"/api/v1/students/{studentId}/attendance?schoolYear=2026"), 400);
        await ProblemAsync(await client.GetAsync("/api/v1/students/999999/attendance"), 404);
    }

    [Fact]
    public async Task Clerk_can_read_a_student_enrolled_at_their_school_including_other_schools_records()
    {
        var mine = await CreateSchoolAsync("Mine");
        var previous = await CreateSchoolAsync("Previous");
        var studentId = await CreateStudentAsync("Transfer", previous, exit: new DateOnly(2026, 9, 30));
        await EnrollAsync(studentId, mine, new DateOnly(2026, 10, 1));
        var admin = await AdminClientAsync();
        await SaveDaysAsync(admin, previous, studentId, "A", new DateOnly(2026, 9, 14));
        await CreateUserAsync("clerk", UserRole.AttendanceClerk, mine);
        var clerk = await ClientForAsync("clerk");

        var history = await ReadAsync<AttendanceHistoryResponse>(await clerk.GetAsync($"/api/v1/students/{studentId}/attendance"));

        Assert.Equal("Previous", Assert.Single(history.Records).SchoolName);
        await ProblemAsync(await clerk.GetAsync($"/api/v1/students/{studentId}/attendance?schoolYear=2025-2026"), 403);
    }

    [Fact]
    public async Task Chronic_status_returns_both_results_and_the_count_method_decides_by_default()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId, entry: new DateOnly(2026, 10, 1));
        var client = await AdminClientAsync();

        // Enrolled 1–15 Oct: 11 school days. 3 absences = 27.27% (rate chronic) but below the count of 10.
        await SaveDaysAsync(client, schoolId, studentId, "A", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2));
        await SaveDaysAsync(client, schoolId, studentId, "E", new DateOnly(2026, 10, 5));

        var status = await ReadAsync<ChronicStatusResponse>(await client.GetAsync($"/api/v1/students/{studentId}/chronic-absenteeism"));

        Assert.Equal(("2026-2027", new DateOnly(2026, 10, 15)), (status.SchoolYear, status.AsOf));
        Assert.Equal((3, 1, 2), (status.TotalAbsences, status.ExcusedAbsences, status.UnexcusedAbsences));
        Assert.Equal((ChronicMethod.Count, ChronicStatus.NotChronic, false), (status.Method, status.Status, status.IsChronicallyAbsent));
        Assert.Equal(new ChronicCountResponse(3, 10, false), status.Count);
        Assert.Equal((true, 11, 27.27m, 10m, true), (status.Rate.IsDeterminable, status.Rate.EnrolledDays!.Value, status.Rate.RatePercent!.Value, status.Rate.ThresholdPercent, status.Rate.IsChronic));
        Assert.Equal("Lincoln", status.CurrentSchool?.Name);
        Assert.Equal(["Lincoln"], status.SchoolsIncluded.Select(s => s.Name));
        Assert.Contains(ChronicStatusQuery.OutsideSchoolsNote, status.DataCompletenessNotes);
        Assert.Null(status.OpenAlert);
    }

    [Fact]
    public async Task Rate_method_school_flags_the_student_and_raises_an_alert()
    {
        var schoolId = await CreateSchoolAsync("Rate School", configure: s => s.SetChronicRules(ChronicMethod.Rate, 10, 10m));
        var studentId = await CreateStudentAsync("Adams", schoolId, entry: new DateOnly(2026, 10, 1));
        var client = await AdminClientAsync();
        await using (var db = Db())
        {
            db.NonInstructionalDays.Add(NonInstructionalDay.Create(schoolId, new DateOnly(2026, 10, 12), "Staff day"));
            await db.SaveChangesAsync();
        }

        // 1–15 Oct with 12 Oct closed = 10 school days so far. The first absence is already 10%, so the
        // alert is raised on that save; the second takes the rate to 20%.
        await SaveDaysAsync(client, schoolId, studentId, "A", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2));

        var status = await ReadAsync<ChronicStatusResponse>(await client.GetAsync($"/api/v1/students/{studentId}/chronic-absenteeism"));

        Assert.Equal((ChronicMethod.Rate, ChronicStatus.Chronic, true), (status.Method, status.Status, status.IsChronicallyAbsent));
        Assert.Equal((10, 20m), (status.Rate.EnrolledDays!.Value, status.Rate.RatePercent!.Value));
        Assert.False(status.Count.IsChronic);
        Assert.NotNull(status.OpenAlert);
        Assert.Contains("10% of 10 enrolled school days", status.OpenAlert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Student_without_enrollment_in_the_year_is_not_determinable_with_default_thresholds()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Left", schoolId, entry: new DateOnly(2025, 9, 1), exit: new DateOnly(2026, 6, 30));
        var client = await AdminClientAsync();

        var status = await ReadAsync<ChronicStatusResponse>(await client.GetAsync($"/api/v1/students/{studentId}/chronic-absenteeism?schoolYear=2026-2027"));

        Assert.Equal((ChronicStatus.NotDeterminable, false), (status.Status, status.IsChronicallyAbsent));
        Assert.Equal(10, status.Count.Threshold);
        Assert.Equal(10m, status.Rate.ThresholdPercent);
        Assert.False(status.Rate.IsDeterminable);
        Assert.Null(status.CurrentSchool);
        Assert.Contains("no enrollment", status.StatusReason, StringComparison.OrdinalIgnoreCase);
    }
}

public class RosterAndCodesTests : ApiTestBase
{
    public RosterAndCodesTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Fact]
    public async Task Roster_lists_students_enrolled_on_the_date_with_existing_records()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var otherSchool = await CreateSchoolAsync("Other");
        var marked = await CreateStudentAsync("Adams", schoolId);
        var unmarked = await CreateStudentAsync("Baker", schoolId);
        var sixth = await CreateStudentAsync("Clark", schoolId, grade: "06");
        await CreateStudentAsync("Exited", schoolId, exit: new DateOnly(2026, 10, 14));
        await CreateStudentAsync("Future", schoolId, entry: new DateOnly(2026, 10, 16));
        await CreateStudentAsync("Elsewhere", otherSchool);
        var client = await AdminClientAsync();
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, new DateOnly(2026, 10, 15), new { studentId = marked, code = "T", minutesLate = 7 }));

        var roster = await ReadAsync<RosterResponse>(await client.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15"));
        var fifth = await ReadAsync<RosterResponse>(await client.GetAsync($"/api/v1/schools/{schoolId}/attendance/2026-10-15?grade=05"));

        Assert.Equal([marked, unmarked, sixth], roster.Students.Select(s => s.StudentId));
        Assert.Equal(("T", 7), (roster.Students[0].Record!.Code, roster.Students[0].Record!.MinutesLate));
        Assert.Null(roster.Students[1].Record);
        Assert.Equal([marked, unmarked], fifth.Students.Select(s => s.StudentId));
        await ProblemAsync(await client.GetAsync("/api/v1/schools/999999/attendance/2026-10-15"), 404);
    }

    [Fact]
    public async Task Attendance_codes_lists_the_seeded_active_codes()
    {
        var client = await AdminClientAsync();

        var codes = await ReadAsync<List<AttendanceApi.Application.Codes.AttendanceCodeResponse>>(await client.GetAsync("/api/v1/attendance-codes"));

        Assert.Equal(["A", "E", "FT", "ISS", "M", "OSS", "P", "T", "TE"], codes.Select(c => c.Code));
    }

    [Fact]
    public async Task Health_check_is_public_and_reports_the_database()
    {
        var response = await AnonymousClient().GetAsync("/health");

        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Problem_responses_carry_a_correlation_id()
    {
        var client = await AdminClientAsync();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "test-correlation-1");

        var response = await client.GetAsync("/api/v1/students/999999/attendance");

        var problem = await ProblemAsync(response, 404);
        Assert.Equal("test-correlation-1", problem.GetProperty("correlationId").GetString());
        Assert.Equal("test-correlation-1", response.Headers.GetValues("X-Correlation-ID").Single());
    }
}
