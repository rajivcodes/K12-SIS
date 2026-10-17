using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Chronic;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Schools;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Students;

public sealed record SchoolReference(int Id, string Name);

public sealed record ChronicCountResponse(int TotalAbsences, int Threshold, bool IsChronic);

public sealed record ChronicRateResponse(
    bool IsDeterminable,
    int? EnrolledDays,
    decimal? RatePercent,
    decimal ThresholdPercent,
    bool IsChronic,
    string? NotDeterminableReason);

public sealed record OpenAlertResponse(int Id, DateTime RaisedAt, string Message);

public sealed record ChronicStatusResponse(
    int StudentId,
    string SchoolYear,
    DateOnly AsOf,
    int TotalAbsences,
    int ExcusedAbsences,
    int UnexcusedAbsences,
    ChronicMethod Method,
    ChronicStatus Status,
    bool IsChronicallyAbsent,
    string? StatusReason,
    ChronicCountResponse Count,
    ChronicRateResponse Rate,
    SchoolReference? CurrentSchool,
    IReadOnlyList<SchoolReference> SchoolsIncluded,
    IReadOnlyList<string> DataCompletenessNotes,
    OpenAlertResponse? OpenAlert);

/// <summary>Chronic-absence status: both the count and the rate results, and the overall status (plan §5, §7).</summary>
public sealed class ChronicStatusQuery
{
    public const string OutsideSchoolsNote =
        "Absences are combined from every school in this system. Absences recorded by schools outside this system are not available and are not included.";

    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly IClock _clock;
    private readonly ChronicStatusCalculator _calculator;

    public ChronicStatusQuery(IAttendanceDbContext db, AccessGuard access, IClock clock, ChronicStatusCalculator calculator)
    {
        _db = db;
        _access = access;
        _clock = clock;
        _calculator = calculator;
    }

    public async Task<ChronicStatusResponse> HandleAsync(int studentId, string? schoolYear, CancellationToken cancellationToken)
    {
        // Access first: a non-admin gets 403 for any student outside their schools, existing or not.
        var today = _clock.Today;
        var year = await SchoolYearParameter.ResolveAsync(schoolYear, studentId, _db, today, cancellationToken);
        await _access.EnsureStudentAccessAsync(studentId, year, cancellationToken);
        if (!await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken))
        {
            throw new NotFoundException($"Student {studentId} does not exist.");
        }

        var evaluation = (await _calculator.EvaluateAsync([studentId], year, today, cancellationToken))[studentId];
        var label = year.ToString();

        var schoolIds = evaluation.SchoolIdsIncluded.Append(evaluation.CurrentSchoolId ?? 0).ToList();
        var schools = await _db.Schools
            .Where(s => schoolIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new SchoolReference(s.Id, s.Name), cancellationToken);
        var openAlert = await _db.StudentAlerts
            .Where(a => a.StudentId == studentId && a.SchoolYearLabel == label
                && a.AlertType == StudentAlert.ChronicAbsenceType && a.ResolvedAt == null)
            .Select(a => new OpenAlertResponse(a.Id, a.RaisedAt, a.Message))
            .SingleOrDefaultAsync(cancellationToken);

        var notes = new List<string> { OutsideSchoolsNote };
        if (!evaluation.Result.Rate.IsDeterminable)
        {
            notes.Add("Rate not determinable: " + evaluation.Result.Rate.NotDeterminableReason);
        }

        var result = evaluation.Result;
        return new ChronicStatusResponse(
            studentId,
            label,
            today,
            evaluation.Totals.TotalAbsences,
            evaluation.Totals.ExcusedAbsences,
            evaluation.Totals.UnexcusedAbsences,
            result.Method,
            result.Status,
            result.IsChronicallyAbsent,
            evaluation.StatusReason,
            new ChronicCountResponse(result.Count.TotalAbsences, result.Count.Threshold, result.Count.IsChronic),
            new ChronicRateResponse(
                result.Rate.IsDeterminable,
                result.Rate.EnrolledDays,
                result.Rate.RatePercent is { } rate ? Math.Round(rate, 2, MidpointRounding.AwayFromZero) : null,
                result.Rate.ThresholdPercent,
                result.Rate.IsChronic,
                result.Rate.NotDeterminableReason),
            evaluation.CurrentSchoolId is { } current ? schools.GetValueOrDefault(current) : null,
            evaluation.SchoolIdsIncluded.Where(schools.ContainsKey).Select(id => schools[id]).ToList(),
            notes,
            openAlert);
    }
}
