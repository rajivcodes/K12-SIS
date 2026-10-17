using System.Net;
using System.Net.Http.Json;
using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Admin;
using AttendanceApi.Application.Attendance;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Api;

public class AdminTests : ApiTestBase
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    public AdminTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Theory]
    [InlineData(UserRole.AttendanceClerk)]
    [InlineData(UserRole.Viewer)]
    public async Task Admin_endpoints_are_admin_only(UserRole role)
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        await CreateUserAsync("someone", role, schoolId);
        var client = await ClientForAsync("someone");

        foreach (var url in new[]
        {
            $"/api/v1/admin/schools/{schoolId}/settings",
            $"/api/v1/admin/schools/{schoolId}/school-years",
            $"/api/v1/admin/schools/{schoolId}/non-instructional-days",
            "/api/v1/admin/submission-issues",
            "/api/v1/admin/users",
        })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task Settings_can_be_read_and_replaced_and_take_effect_with_an_audit_entry()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var admin = await AdminClientAsync();
        var url = $"/api/v1/admin/schools/{schoolId}/settings";

        var defaults = await ReadAsync<SchoolSettingsResponse>(await admin.GetAsync(url));
        Assert.Equal((ChronicMethod.Count, 10, 10m, false), (defaults.ChronicMethod, defaults.ChronicCountThreshold, defaults.ChronicRateThreshold, defaults.AllowWeekends));
        await ProblemAsync(await SaveAsync(admin, schoolId, new DateOnly(2026, 10, 10), new { studentId, code = "P" }), 422);

        var updated = await ReadAsync<SchoolSettingsResponse>(await admin.PutAsJsonAsync(url, new
        {
            chronicMethod = "Either",
            chronicCountThreshold = 8,
            chronicRateThreshold = 12.5m,
            allowFutureDates = false,
            allowWeekends = true,
            allowOutsideTerm = false,
            maxBackdateDays = 30,
        }));

        Assert.Equal((ChronicMethod.Either, 8, 12.5m, true, 30, (int?)null), (updated.ChronicMethod, updated.ChronicCountThreshold, updated.ChronicRateThreshold, updated.AllowWeekends, updated.MaxBackdateDays!.Value, updated.MaxFutureDays));
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(admin, schoolId, new DateOnly(2026, 10, 10), new { studentId, code = "P" }));
        await using var db = Db();
        var audit = await db.AuditEntries.SingleAsync(a => a.Action == "SchoolSettingsUpdated");
        Assert.Equal(("admin", schoolId.ToString(System.Globalization.CultureInfo.InvariantCulture), TestClock.Default), (audit.UserName, audit.EntityId, audit.OccurredAt));
        Assert.Contains("\"chronicMethod\":\"Either\"", audit.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_settings_get_400_and_unknown_school_404()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var admin = await AdminClientAsync();
        var valid = new { chronicMethod = "Count", chronicCountThreshold = 10, chronicRateThreshold = 10m, allowFutureDates = false, allowWeekends = false, allowOutsideTerm = false };

        await ProblemAsync(await admin.PutAsJsonAsync($"/api/v1/admin/schools/{schoolId}/settings", valid with { chronicRateThreshold = 0m }), 400);
        await ProblemAsync(await admin.PutAsJsonAsync($"/api/v1/admin/schools/{schoolId}/settings", new { chronicMethod = "Count" }), 400);
        await ProblemAsync(await admin.PutAsJsonAsync($"/api/v1/admin/schools/{schoolId}/settings", valid with { chronicCountThreshold = 0 }), 400);
        await ProblemAsync(await admin.GetAsync("/api/v1/admin/schools/999999/settings"), 404);
    }

    [Fact]
    public async Task School_years_can_be_added_but_not_overlapping_or_duplicated()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var admin = await AdminClientAsync();
        var url = $"/api/v1/admin/schools/{schoolId}/school-years";

        var created = await admin.PostAsJsonAsync(url, new { startDate = "2027-09-01", endDate = "2027-10-31" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("2027-2028", (await ReadAsync<SchoolYearResponse>(created)).Label);

        await ProblemAsync(await admin.PostAsJsonAsync(url, new { startDate = "2027-08-16", endDate = "2027-08-31" }), 409); // overlaps 2026-2027 (ends 31 Aug)
        await ProblemAsync(await admin.PostAsJsonAsync(url, new { startDate = "2027-11-01", endDate = "2028-06-30" }), 409); // label 2027-2028 again
        await ProblemAsync(await admin.PostAsJsonAsync(url, new { startDate = "2028-09-01", endDate = "2029-09-30" }), 400); // longer than a year

        var years = await ReadAsync<List<SchoolYearResponse>>(await admin.GetAsync(url));
        Assert.Equal(["2027-2028", "2026-2027"], years.Select(y => y.Label));
    }

    [Fact]
    public async Task A_school_year_with_attendance_cannot_be_moved_away_from_its_records()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var admin = await AdminClientAsync();
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(admin, schoolId, new DateOnly(2026, 9, 2), new { studentId, code = "P" }));
        var yearId = (await ReadAsync<List<SchoolYearResponse>>(await admin.GetAsync($"/api/v1/admin/schools/{schoolId}/school-years"))).Single().Id;
        var url = $"/api/v1/admin/schools/{schoolId}/school-years/{yearId}";

        var outside = await ProblemAsync(await admin.PutAsJsonAsync(url, new { startDate = "2026-09-03", endDate = "2027-08-31" }), 422);
        Assert.Equal("RecordsOutsideYear", outside.GetProperty("reason").GetString());

        // Ending earlier is fine as long as records and terms still fit; here the whole-year term doesn't.
        await ProblemAsync(await admin.PutAsJsonAsync(url, new { startDate = "2026-09-01", endDate = "2027-06-30" }), 400);
    }

    [Fact]
    public async Task Replacing_the_auto_created_whole_year_term_retags_existing_records()
    {
        var schoolId = await CreateSchoolAsync("New School", withSchoolYear: false);
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var admin = await AdminClientAsync();
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(admin, schoolId, new DateOnly(2026, 10, 14), new { studentId, code = "A" }));
        var year = (await ReadAsync<List<SchoolYearResponse>>(await admin.GetAsync($"/api/v1/admin/schools/{schoolId}/school-years"))).Single();
        var fullYear = year.Terms.Single();
        var termsUrl = $"/api/v1/admin/schools/{schoolId}/school-years/{year.Id}/terms";

        await ProblemAsync(await admin.PostAsJsonAsync(termsUrl, new { name = "Fall", startDate = "2026-09-01", endDate = "2026-12-18" }), 409);
        await ReadAsync<SchoolYearResponse>(await admin.DeleteAsync($"{termsUrl}/{fullYear.Id}"));
        Assert.Null((await RecordsAsync()).Single().TermId);

        var withFall = await ReadAsync<SchoolYearResponse>(await admin.PostAsJsonAsync(termsUrl, new { name = "Fall", startDate = "2026-09-01", endDate = "2026-12-18" }));
        var fall = withFall.Terms.Single();
        Assert.Equal(fall.Id, (await RecordsAsync()).Single().TermId);

        var moved = await ReadAsync<SchoolYearResponse>(await admin.PutAsJsonAsync($"{termsUrl}/{fall.Id}", new { name = "Autumn", startDate = "2026-10-15", endDate = "2026-12-18" }));
        Assert.Equal("Autumn", moved.Terms.Single().Name);
        Assert.Null((await RecordsAsync()).Single().TermId); // 14 Oct is now outside every term

        await using var db = Db();
        Assert.Equal(3, await db.AuditEntries.CountAsync(a => a.EntityType == "SchoolYear" && a.UserName == "admin"));
    }

    [Fact]
    public async Task Non_instructional_days_can_be_added_listed_and_removed()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var admin = await AdminClientAsync();
        var url = $"/api/v1/admin/schools/{schoolId}/non-instructional-days";

        var created = await admin.PostAsJsonAsync(url, new { date = "2026-11-26", reason = "Thanksgiving" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var day = await ReadAsync<NonInstructionalDayResponse>(created);
        await ProblemAsync(await admin.PostAsJsonAsync(url, new { date = "2026-11-26", reason = "Again" }), 409);
        await ProblemAsync(await admin.PostAsJsonAsync(url, new { reason = "No date" }), 400);

        var listed = await ReadAsync<List<NonInstructionalDayResponse>>(await admin.GetAsync($"{url}?from=2026-11-01&to=2026-11-30"));
        Assert.Equal("Thanksgiving", Assert.Single(listed).Reason);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"{url}/{day.Id}")).StatusCode);
        await ProblemAsync(await admin.DeleteAsync($"{url}/{day.Id}"), 404);
    }

    [Fact]
    public async Task Submission_issues_can_be_filtered_and_paged()
    {
        var lincoln = await CreateSchoolAsync("Lincoln");
        var roosevelt = await CreateSchoolAsync("Roosevelt");
        var a = await CreateStudentAsync("Adams", lincoln);
        var b = await CreateStudentAsync("Baker", roosevelt);
        var admin = await AdminClientAsync();
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(admin, lincoln, Today, new { studentId = a, code = "ZZ" }, new { studentId = 999_999, code = "P" }));
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(admin, roosevelt, new DateOnly(2026, 10, 14), new { studentId = b, code = "P", minutesLate = -3 }));

        var all = await ReadAsync<SubmissionIssuesPage>(await admin.GetAsync("/api/v1/admin/submission-issues"));
        var lincolnOnly = await ReadAsync<SubmissionIssuesPage>(await admin.GetAsync($"/api/v1/admin/submission-issues?schoolId={lincoln}"));
        var byDate = await ReadAsync<SubmissionIssuesPage>(await admin.GetAsync("/api/v1/admin/submission-issues?from=2026-10-14&to=2026-10-14"));
        var paged = await ReadAsync<SubmissionIssuesPage>(await admin.GetAsync("/api/v1/admin/submission-issues?page=2&pageSize=2"));

        Assert.Equal(3, all.TotalCount);
        Assert.Equal([SubmissionIssueReason.UnknownCode, SubmissionIssueReason.UnknownStudent], lincolnOnly.Items.Select(i => i.Reason));
        Assert.Equal("Lincoln", lincolnOnly.Items[0].SchoolName);
        Assert.Equal(SubmissionIssueReason.NegativeMinutesLate, Assert.Single(byDate.Items).Reason);
        Assert.Equal((3, 1), (paged.TotalCount, paged.Items.Count));
        await ProblemAsync(await admin.GetAsync("/api/v1/admin/submission-issues?pageSize=1000"), 400);
    }

    [Fact]
    public async Task Users_can_be_created_updated_deactivated_and_have_passwords_reset()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var admin = await AdminClientAsync();
        const string password = "Clerk-Password-1";

        var created = await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "new.clerk", displayName = "New Clerk", password, role = "AttendanceClerk", schoolIds = new[] { schoolId } });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var user = await ReadAsync<UserResponse>(created);
        Assert.Equal((UserRole.AttendanceClerk, true), (user.Role, user.IsActive));
        Assert.Equal([schoolId], user.SchoolIds);
        await ClientForAsync("new.clerk", password); // can sign in

        await ProblemAsync(await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "new.clerk", displayName = "Dup", password, role = "Viewer" }), 409);
        await ProblemAsync(await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "short", displayName = "Short", password = "short", role = "Viewer" }), 400);
        await ProblemAsync(await admin.PostAsJsonAsync("/api/v1/admin/users", new { userName = "x", displayName = "X", password, role = "Viewer", schoolIds = new[] { 424242 } }), 400);

        var url = $"/api/v1/admin/users/{user.Id}";
        await ReadAsync<UserResponse>(await admin.PutAsJsonAsync(url, new { displayName = "New Clerk", role = "AttendanceClerk", isActive = false, schoolIds = new[] { schoolId } }));
        Assert.Equal(HttpStatusCode.Unauthorized, (await AnonymousClient().PostAsJsonAsync("/api/v1/auth/login", new { userName = "new.clerk", password })).StatusCode);

        var reset = await ReadAsync<UserResponse>(await admin.PutAsJsonAsync(url, new { displayName = "Renamed", role = "Viewer", isActive = true, schoolIds = Array.Empty<int>(), newPassword = "Brand-New-Password-2" }));
        Assert.Equal(("Renamed", UserRole.Viewer, 0), (reset.DisplayName, reset.Role, reset.SchoolIds.Count));
        await ClientForAsync("new.clerk", "Brand-New-Password-2");

        var list = await ReadAsync<List<UserResponse>>(await admin.GetAsync("/api/v1/admin/users"));
        Assert.Equal(["admin", "new.clerk"], list.Select(u => u.UserName));
        await using var db = Db();
        Assert.Equal(3, await db.AuditEntries.CountAsync(a => a.EntityType == "AppUser"));
        Assert.DoesNotContain(await db.AuditEntries.Select(a => a.Details).ToListAsync(), d => d != null && d.Contains("Password-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_last_active_admin_cannot_be_demoted_or_deactivated()
    {
        var admin = await AdminClientAsync();
        var me = (await ReadAsync<List<UserResponse>>(await admin.GetAsync("/api/v1/admin/users"))).Single();

        var problem = await ProblemAsync(await admin.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}", new { displayName = "Admin", role = "Viewer", isActive = true }), 422);
        Assert.Equal("LastActiveAdmin", problem.GetProperty("reason").GetString());
        await ProblemAsync(await admin.PutAsJsonAsync($"/api/v1/admin/users/{me.Id}", new { displayName = "Admin", role = "Admin", isActive = false }), 422);
        await ProblemAsync(await admin.GetAsync("/api/v1/admin/users/999999"), 404);
    }
}
