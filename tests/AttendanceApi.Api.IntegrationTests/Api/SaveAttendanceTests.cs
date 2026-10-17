using AttendanceApi.Api.IntegrationTests.Fixtures;
using AttendanceApi.Application.Attendance;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Api.IntegrationTests.Api;

public class SaveAttendanceTests : ApiTestBase
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    public SaveAttendanceTests(SqlServerFixture server)
        : base(server)
    {
    }

    [Fact]
    public async Task Saves_a_new_batch_and_records_the_signed_in_user()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var a = await CreateStudentAsync("Adams", schoolId);
        var b = await CreateStudentAsync("Baker", schoolId);
        var clerkId = await CreateUserAsync("clerk", UserRole.AttendanceClerk, schoolId);
        var client = await ClientForAsync("clerk");

        var result = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(
            client,
            schoolId,
            Today,
            new { studentId = a, code = "P" },
            new { studentId = b, code = "t", minutesLate = 10, notes = "Bus" }));

        Assert.Equal((2, 2, 2, 0, "2026-2027"), (result.RowsReceived, result.RowsSaved, result.Inserted, result.Updated, result.SchoolYear));
        Assert.Empty(result.Skipped);
        var records = await RecordsAsync();
        Assert.Equal([a, b], records.Select(r => r.StudentId));
        Assert.All(records, r => Assert.Equal((clerkId, "clerk", TestClock.Default), (r.CreatedByUserId!.Value, r.CreatedBy, r.CreatedAt)));
        Assert.Equal((10, "Bus"), (records[1].MinutesLate, records[1].Notes));
    }

    [Fact]
    public async Task D01_regression_a_batch_mixing_students_with_and_without_a_record_saves_each_students_own_row()
    {
        // Legacy D-01: the loop variable holding the existing row ID was never reset, so a student without a
        // record that day overwrote the previous student's row and never got their own.
        var schoolId = await CreateSchoolAsync("Lincoln");
        var ids = new List<int>();
        foreach (var name in new[] { "One", "Two", "Three", "Four", "Five" })
        {
            ids.Add(await CreateStudentAsync(name, schoolId));
        }

        var client = await AdminClientAsync();

        // Morning save for students 1 and 3 only (e.g. a single-grade save).
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(
            client, schoolId, Today, new { studentId = ids[0], code = "P" }, new { studentId = ids[2], code = "P" }));

        // Whole-school save: existing, new, existing, new, new.
        var result = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(
            client,
            schoolId,
            Today,
            new { studentId = ids[0], code = "A" },
            new { studentId = ids[1], code = "E" },
            new { studentId = ids[2], code = "T", minutesLate = 5 },
            new { studentId = ids[3], code = "M" },
            new { studentId = ids[4], code = "P" }));

        Assert.Equal((5, 3, 2), (result.RowsSaved, result.Inserted, result.Updated));
        var records = await RecordsAsync();
        Assert.Equal(ids, records.Select(r => r.StudentId));
        await using var db = Db();
        var codesById = await db.AttendanceCodes.ToDictionaryAsync(c => c.Id, c => c.Code);
        Assert.Equal(["A", "E", "T", "M", "P"], records.Select(r => codesById[r.AttendanceCodeId]));
        Assert.Equal([true, true, false, true, false], records.Select(r => r.IsAbsent));
        Assert.Equal([false, true, false, true, false], records.Select(r => r.IsExcused));
        Assert.Equal(5, records[2].MinutesLate);
        Assert.All(records.Where(r => r.StudentId == ids[0] || r.StudentId == ids[2]), r => Assert.NotNull(r.ModifiedAt));
    }

    [Fact]
    public async Task Bad_rows_are_skipped_logged_and_returned_while_the_rest_are_saved()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var otherSchool = await CreateSchoolAsync("Other");
        var ok = await CreateStudentAsync("Ok", schoolId);
        var elsewhere = await CreateStudentAsync("Elsewhere", otherSchool);
        var dup = await CreateStudentAsync("Dup", schoolId);
        var client = await AdminClientAsync();

        var result = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(
            client,
            schoolId,
            Today,
            new { studentId = ok, code = "P" },
            new { studentId = 999_999, code = "P" },
            new { studentId = elsewhere, code = "P" },
            new { studentId = dup, code = "P" },
            new { studentId = dup, code = "A" },
            new { studentId = ok + 1000, code = "ZZ" }));

        Assert.Equal((6, 1), (result.RowsReceived, result.RowsSaved));
        Assert.Equal(
            [
                (1, SubmissionIssueReason.UnknownStudent),
                (2, SubmissionIssueReason.NotEnrolled),
                (3, SubmissionIssueReason.DuplicateStudentInBatch),
                (4, SubmissionIssueReason.DuplicateStudentInBatch),
                (5, SubmissionIssueReason.UnknownStudent),
            ],
            result.Skipped.Select(s => (s.RowIndex, s.Reason)));
        Assert.Equal(ok, Assert.Single(await RecordsAsync()).StudentId);

        await using var db = Db();
        var submission = await db.AttendanceSubmissions.Include(s => s.Issues).SingleAsync();
        Assert.Equal((result.SubmissionId, 6, 1, 5, "admin"), (submission.Id, submission.RowsReceived, submission.RowsSaved, submission.RowsSkipped, submission.SubmittedBy));
        Assert.Equal(5, submission.Issues.Count);
        Assert.Contains(submission.Issues, i => i.Reason == SubmissionIssueReason.UnknownStudent && i.StudentId == null);
    }

    [Theory]
    [InlineData("2026-10-16", "FutureDateNotAllowed")]
    [InlineData("2026-10-10", "WeekendNotAllowed")]
    public async Task A_disallowed_date_rejects_the_whole_batch_with_422(string date, string reason)
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();

        var response = await SaveAsync(client, schoolId, DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture), new { studentId, code = "P" });

        var problem = await ProblemAsync(response, 422);
        Assert.Equal(reason, problem.GetProperty("reason").GetString());
        Assert.Empty(await RecordsAsync());
    }

    [Fact]
    public async Task Date_rules_follow_the_school_settings()
    {
        var schoolId = await CreateSchoolAsync("Saturday School", configure: s => s.SetDateRules(true, true, false, 7, 1));
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();

        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, new DateOnly(2026, 10, 10), new { studentId, code = "P" }));
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, new DateOnly(2026, 10, 16), new { studentId, code = "P" }));
        var tooFar = await ProblemAsync(await SaveAsync(client, schoolId, new DateOnly(2026, 10, 17), new { studentId, code = "P" }), 422);
        var tooOld = await ProblemAsync(await SaveAsync(client, schoolId, new DateOnly(2026, 10, 7), new { studentId, code = "P" }), 422);

        Assert.Equal("TooFarInFuture", tooFar.GetProperty("reason").GetString());
        Assert.Equal("TooFarInPast", tooOld.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Malformed_requests_get_400_and_unknown_school_404()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var client = await AdminClientAsync();

        await ProblemAsync(await SaveAsync(client, schoolId, Today), 400);
        await ProblemAsync(await client.PostAsync($"/api/v1/schools/{schoolId}/attendance/2026-13-45", new StringContent("[]", System.Text.Encoding.UTF8, "application/json")), 400);
        await ProblemAsync(await SaveAsync(client, 987_654, Today, new { studentId = 1, code = "P" }), 404);
    }

    [Fact]
    public async Task A_school_without_a_school_year_gets_the_default_year_with_one_whole_year_term()
    {
        var schoolId = await CreateSchoolAsync("New School", withSchoolYear: false);
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();

        var result = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, Today, new { studentId, code = "P" }));

        Assert.Equal("2026-2027", result.SchoolYear);
        await using var db = Db();
        var year = await db.SchoolYears.Include(y => y.Terms).SingleAsync(y => y.SchoolId == schoolId);
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31)), (year.StartDate, year.EndDate));
        var term = Assert.Single(year.Terms);
        Assert.Equal(term.Id, (await RecordsAsync()).Single().TermId);
    }

    [Fact]
    public async Task Totals_combine_all_schools_and_the_alert_is_raised_once()
    {
        // Student transfers from school A (absent 6 days) to school B (absent 4 more days) – 10 in total.
        var schoolA = await CreateSchoolAsync("A");
        var schoolB = await CreateSchoolAsync("B");
        var studentId = await CreateStudentAsync("Mover", schoolA, exit: new DateOnly(2026, 9, 30));
        await EnrollAsync(studentId, schoolB, new DateOnly(2026, 10, 1));
        var client = await AdminClientAsync();

        var septemberDays = new[] { 14, 15, 16, 17, 18, 21 }.Select(d => new DateOnly(2026, 9, d));
        foreach (var day in septemberDays)
        {
            await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolA, day, new { studentId, code = "A" }));
        }

        SaveDailyAttendanceResponse? last = null;
        foreach (var day in new[] { 5, 6, 7, 8 }.Select(d => new DateOnly(2026, 10, d)))
        {
            last = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolB, day, new { studentId, code = "E" }));
        }

        Assert.Equal([studentId], last!.ChronicAlertsRaisedForStudentIds);

        // Another absence later does not raise a second alert.
        var again = await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolB, new DateOnly(2026, 10, 9), new { studentId, code = "A" }));
        Assert.Empty(again.ChronicAlertsRaisedForStudentIds);

        await using var db = Db();
        var summary = await db.StudentAttendanceSummaries.SingleAsync();
        Assert.Equal(("2026-2027", 11, 4, 7), (summary.SchoolYearLabel, summary.TotalAbsences, summary.ExcusedAbsences, summary.UnexcusedAbsences));
        var alert = await db.StudentAlerts.SingleAsync();
        Assert.Equal((StudentAlert.ChronicAbsenceType, schoolB, true), (alert.AlertType, alert.SchoolId, alert.IsOpen));
    }

    [Fact]
    public async Task Correcting_an_absence_to_present_lowers_the_stored_total()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var studentId = await CreateStudentAsync("Adams", schoolId);
        var client = await AdminClientAsync();
        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, Today, new { studentId, code = "A" }));

        await ReadAsync<SaveDailyAttendanceResponse>(await SaveAsync(client, schoolId, Today, new { studentId, code = "P" }));

        await using var db = Db();
        Assert.Equal(0, (await db.StudentAttendanceSummaries.SingleAsync()).TotalAbsences);
        Assert.Single(await RecordsAsync());
    }

    [Fact]
    public async Task Concurrent_saves_for_the_same_students_and_day_both_succeed_with_one_row_each()
    {
        var schoolId = await CreateSchoolAsync("Lincoln");
        var ids = new List<int>();
        for (var i = 0; i < 10; i++)
        {
            ids.Add(await CreateStudentAsync("Student" + i, schoolId));
        }

        var client1 = await AdminClientAsync();
        var client2 = await AdminClientAsync();
        var rows = ids.Select(id => (object)new { studentId = id, code = "P" }).ToArray();

        var responses = await Task.WhenAll(SaveAsync(client1, schoolId, Today, rows), SaveAsync(client2, schoolId, Today, rows));

        Assert.All(responses, r => Assert.True(r.IsSuccessStatusCode, r.StatusCode.ToString()));
        Assert.Equal(ids, (await RecordsAsync()).Select(r => r.StudentId));
    }
}
