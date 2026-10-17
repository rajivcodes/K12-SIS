using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Chronic;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Submissions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendanceApi.Application.Attendance;

/// <summary>
/// Saves one school-day batch in one database transaction (plan §6). Each row is validated and upserted on
/// its own by (StudentId, Date), so the legacy D-01/D-02 shared-state bugs can't happen. If two saves
/// collide on the unique key or the row version, the whole save is retried once.
/// </summary>
public sealed partial class SaveDailyAttendanceHandler
{
    public const int MaxRows = 1000;

    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;
    private readonly IDatabaseErrors _errors;
    private readonly INotificationSender _notifications;
    private readonly ChronicStatusCalculator _chronic;
    private readonly ILogger<SaveDailyAttendanceHandler> _logger;

    public SaveDailyAttendanceHandler(
        IAttendanceDbContext db,
        AccessGuard access,
        ICurrentUser user,
        IClock clock,
        IDatabaseErrors errors,
        INotificationSender notifications,
        ChronicStatusCalculator chronic,
        ILogger<SaveDailyAttendanceHandler> logger)
    {
        _db = db;
        _access = access;
        _user = user;
        _clock = clock;
        _errors = errors;
        _notifications = notifications;
        _chronic = chronic;
        _logger = logger;
    }

    public async Task<SaveDailyAttendanceResponse> HandleAsync(
        int schoolId,
        DateOnly date,
        IReadOnlyList<AttendanceRowRequest>? rows,
        CancellationToken cancellationToken)
    {
        // 1. Request shape.
        if (rows is null || rows.Count == 0)
        {
            throw new RequestValidationException("The request must contain at least one attendance row.");
        }

        if (rows.Count > MaxRows)
        {
            throw new RequestValidationException($"A batch can contain at most {MaxRows} rows.");
        }

        await _access.EnsureSchoolAccessAsync(schoolId, cancellationToken);

        SaveDailyAttendanceResponse response;
        try
        {
            response = await SaveOnceAsync(schoolId, date, rows, cancellationToken);
        }
        catch (Exception ex) when (_errors.IsConcurrentWriteConflict(ex))
        {
            // Another save for the same students/date committed first: retry once on fresh data.
            // A second conflict propagates and is logged as an unexpected error (500).
            LogRetrying(ex, schoolId, date);
            _db.ChangeTracker.Clear();
            response = await SaveOnceAsync(schoolId, date, rows, cancellationToken);
        }

        LogSaved(response.SubmissionId, schoolId, date, response.Inserted, response.Updated, response.Skipped.Count, response.ChronicAlertsRaisedForStudentIds.Count);

        if (response.Skipped.Count > 0)
        {
            await NotifySkippedRowsAsync(response, schoolId, date, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// The batch is already committed, so a notification failure must not turn the save into an error
    /// response (the client would retry a save that succeeded). It is logged instead.
    /// </summary>
    private async Task NotifySkippedRowsAsync(SaveDailyAttendanceResponse response, int schoolId, DateOnly date, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.NotifyRowsSkippedAsync(
                new RowsSkippedNotification(response.SubmissionId, schoolId, date, _user.UserName, response.Skipped.Count),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogNotificationFailed(ex, response.SubmissionId);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Concurrent save conflict for school {SchoolId} on {Date}; retrying once")]
    private partial void LogRetrying(Exception exception, int schoolId, DateOnly date);

    [LoggerMessage(Level = LogLevel.Information, Message = "Submission {SubmissionId} saved for school {SchoolId} on {Date}: {Inserted} inserted, {Updated} updated, {Skipped} skipped, {AlertsRaised} chronic alert(s) raised")]
    private partial void LogSaved(int submissionId, int schoolId, DateOnly date, int inserted, int updated, int skipped, int alertsRaised);

    [LoggerMessage(Level = LogLevel.Error, Message = "Submission {SubmissionId} was saved, but the skipped-rows notification failed")]
    private partial void LogNotificationFailed(Exception exception, int submissionId);

    private Task<SaveDailyAttendanceResponse> SaveOnceAsync(
        int schoolId,
        DateOnly date,
        IReadOnlyList<AttendanceRowRequest> rows,
        CancellationToken cancellationToken)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async ct =>
        {
            _db.ChangeTracker.Clear();
            await using var transaction = await _db.Database.BeginTransactionAsync(ct);
            var response = await SaveInTransactionAsync(schoolId, date, rows, ct);
            await transaction.CommitAsync(ct);
            return response;
        }, cancellationToken);
    }

    private async Task<SaveDailyAttendanceResponse> SaveInTransactionAsync(
        int schoolId,
        DateOnly date,
        IReadOnlyList<AttendanceRowRequest> rows,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var today = _clock.Today;
        var actor = _user.Actor;

        // 2. School, settings, school year and term.
        if (!await _db.Schools.AnyAsync(s => s.Id == schoolId, ct))
        {
            throw new NotFoundException($"School {schoolId} does not exist.");
        }

        var settings = await _db.SchoolAttendanceSettings.SingleOrDefaultAsync(s => s.SchoolId == schoolId, ct);
        if (settings is null)
        {
            settings = SchoolAttendanceSettings.CreateDefault(schoolId);
            _db.SchoolAttendanceSettings.Add(settings);
        }

        var schoolYear = await FindOrCreateSchoolYearAsync(schoolId, date, ct);

        // 3. Date rules: a disallowed date rejects the whole batch.
        var rejection = AttendanceDatePolicy.Check(date, today, settings, schoolYear);
        if (rejection is not null)
        {
            throw new BusinessRuleException("Date not allowed", rejection.Message, rejection.Reason.ToString());
        }

        // 4. Row checks, each row on its own.
        var studentIds = rows.Select(r => r.StudentId).Distinct().ToList();
        var students = await _db.Students.Where(s => studentIds.Contains(s.Id)).ToListAsync(ct);
        var enrollments = await _db.StudentEnrollments
            .Where(e => e.SchoolId == schoolId && studentIds.Contains(e.StudentId))
            .ToListAsync(ct);
        var codes = await _db.AttendanceCodes.ToListAsync(ct);
        var inputs = rows.Select(r => new AttendanceRowInput(r.StudentId, r.Code, r.MinutesLate ?? 0, r.Notes)).ToList();
        var validation = AttendanceRowValidator.Validate(inputs, schoolId, date, students, enrollments, codes);

        // 5. Upsert by (StudentId, Date).
        var validIds = validation.Valid.Select(v => v.StudentId).ToList();
        var existing = await _db.AttendanceRecords
            .Where(r => r.Date == date && validIds.Contains(r.StudentId))
            .ToDictionaryAsync(r => r.StudentId, ct);
        var placement = new AttendancePlacement(schoolId, date, schoolYear.Id, schoolYear.FindTerm(date)?.Id);
        int inserted = 0, updated = 0;
        foreach (var row in validation.Valid)
        {
            if (existing.TryGetValue(row.StudentId, out var record))
            {
                record.Update(placement, row.Code, row.MinutesLate, row.Notes, actor, now);
                updated++;
            }
            else
            {
                _db.AttendanceRecords.Add(AttendanceRecord.Create(row.StudentId, placement, row.Code, row.MinutesLate, row.Notes, actor, now));
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);

        // 6–7. Recalculate stored totals from the records (all schools) and raise alerts.
        var alertsRaised = await UpdateTotalsAndAlertsAsync(validIds, schoolYear.YearLabel, today, now, ct);

        // 8. Submission log with the skipped rows.
        var submission = AttendanceSubmission.Record(schoolId, date, actor, now, rows.Count, validation.Valid.Count, validation.Skipped);
        _db.AttendanceSubmissions.Add(submission);
        await _db.SaveChangesAsync(ct);

        return new SaveDailyAttendanceResponse(
            submission.Id,
            schoolId,
            date,
            schoolYear.Label,
            rows.Count,
            validation.Valid.Count,
            inserted,
            updated,
            validation.Skipped.Select(s => new SkippedRowResponse(s.RowIndex, s.StudentId, s.Code, s.Reason, s.Detail)).ToList(),
            alertsRaised);
    }

    private async Task<SchoolYear> FindOrCreateSchoolYearAsync(int schoolId, DateOnly date, CancellationToken ct)
    {
        var year = await _db.SchoolYears
            .Include(y => y.Terms)
            .SingleOrDefaultAsync(y => y.SchoolId == schoolId && y.StartDate <= date && y.EndDate >= date, ct);
        if (year is not null)
        {
            return year;
        }

        // No year covers the date: create the default 1 Sep – 31 Aug year with one whole-year term (Q1).
        var created = SchoolYear.CreateDefault(schoolId, date);
        var others = await _db.SchoolYears.Where(y => y.SchoolId == schoolId).ToListAsync(ct);
        try
        {
            created.EnsureNoConflictWith(others);
        }
        catch (Domain.Common.DomainException ex)
        {
            throw new BusinessRuleException(
                "No school year for date",
                $"{date:yyyy-MM-dd} is not inside any of this school's school years, and the default year can't be created: {ex.Message} An admin must adjust the school calendar.",
                "NoSchoolYear");
        }

        _db.SchoolYears.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<IReadOnlyList<int>> UpdateTotalsAndAlertsAsync(
        IReadOnlyCollection<int> studentIds,
        SchoolYearLabel year,
        DateOnly today,
        DateTime now,
        CancellationToken ct)
    {
        if (studentIds.Count == 0)
        {
            return [];
        }

        var label = year.ToString();
        var evaluations = await _chronic.EvaluateAsync(studentIds, year, today, ct);

        var summaries = await _db.StudentAttendanceSummaries
            .Where(s => studentIds.Contains(s.StudentId) && s.SchoolYearLabel == label)
            .ToDictionaryAsync(s => s.StudentId, ct);
        var openAlerts = await _db.StudentAlerts
            .Where(a => studentIds.Contains(a.StudentId) && a.SchoolYearLabel == label
                && a.AlertType == StudentAlert.ChronicAbsenceType && a.ResolvedAt == null)
            .Select(a => a.StudentId)
            .ToListAsync(ct);

        var raised = new List<int>();
        foreach (var evaluation in evaluations.Values)
        {
            if (summaries.TryGetValue(evaluation.StudentId, out var summary))
            {
                summary.Replace(evaluation.Totals, now);
            }
            else
            {
                _db.StudentAttendanceSummaries.Add(StudentAttendanceSummary.Create(evaluation.StudentId, year, evaluation.Totals, now));
            }

            var hasOpen = openAlerts.Contains(evaluation.StudentId);
            if (evaluation.CurrentSchoolId is { } currentSchoolId
                && StudentAlert.ShouldRaiseChronicAbsence(evaluation.Result, hasOpen))
            {
                _db.StudentAlerts.Add(StudentAlert.RaiseChronicAbsence(evaluation.StudentId, currentSchoolId, year, evaluation.Result, now));
                raised.Add(evaluation.StudentId);
            }
        }

        await _db.SaveChangesAsync(ct);
        return raised.Order().ToList();
    }
}
