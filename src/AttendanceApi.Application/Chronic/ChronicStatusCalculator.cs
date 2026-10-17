using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Schools;
using AttendanceApi.Domain.Students;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Chronic;

/// <summary>
/// Loads what the domain rules need and evaluates chronic absence for a set of students and one school year.
/// Totals are always counted live from the records across all schools (D-03, D-08, D-09).
/// Used by both the save flow (totals and alerts) and the chronic-status endpoint.
/// </summary>
public sealed class ChronicStatusCalculator
{
    public const string NoEnrollmentReason =
        "The student has no enrollment in this school year, so there is no current school to take the method and thresholds from; the district defaults are shown.";

    private readonly IAttendanceDbContext _db;

    public ChronicStatusCalculator(IAttendanceDbContext db)
    {
        _db = db;
    }

    /// <summary>Absence totals per student, from the records of every school, for the school year.</summary>
    public async Task<Dictionary<int, AttendanceTotals>> CountTotalsAsync(
        IReadOnlyCollection<int> studentIds,
        SchoolYearLabel year,
        CancellationToken cancellationToken)
    {
        var label = year.ToString();
        var counts = await (
                from r in _db.AttendanceRecords
                join y in _db.SchoolYears on r.SchoolYearId equals y.Id
                where studentIds.Contains(r.StudentId) && y.Label == label
                group r by r.StudentId into g
                select new
                {
                    StudentId = g.Key,
                    Excused = g.Count(r => r.IsAbsent && r.IsExcused),
                    Unexcused = g.Count(r => r.IsAbsent && !r.IsExcused),
                })
            .ToListAsync(cancellationToken);

        var totals = studentIds.Distinct().ToDictionary(id => id, _ => default(AttendanceTotals));
        foreach (var c in counts)
        {
            totals[c.StudentId] = new AttendanceTotals(c.Excused + c.Unexcused, c.Excused, c.Unexcused);
        }

        return totals;
    }

    public async Task<IReadOnlyDictionary<int, StudentChronicEvaluation>> EvaluateAsync(
        IReadOnlyCollection<int> studentIds,
        SchoolYearLabel year,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var label = year.ToString();
        var totals = await CountTotalsAsync(studentIds, year, cancellationToken);

        var enrollments = await _db.StudentEnrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .ToListAsync(cancellationToken);
        var recordSchools = await (
                from r in _db.AttendanceRecords
                join y in _db.SchoolYears on r.SchoolYearId equals y.Id
                where studentIds.Contains(r.StudentId) && y.Label == label
                select new { r.StudentId, r.SchoolId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var schoolIds = enrollments.Select(e => e.SchoolId).Concat(recordSchools.Select(r => r.SchoolId)).Distinct().ToList();
        var years = await _db.SchoolYears
            .Include(y => y.Terms)
            .Where(y => schoolIds.Contains(y.SchoolId) && y.Label == label)
            .ToDictionaryAsync(y => y.SchoolId, cancellationToken);
        var settings = await _db.SchoolAttendanceSettings
            .Where(s => schoolIds.Contains(s.SchoolId))
            .ToDictionaryAsync(s => s.SchoolId, cancellationToken);

        var calendars = new Dictionary<int, SchoolCalendar>();
        if (years.Count > 0)
        {
            var from = years.Values.Min(y => y.StartDate);
            var to = years.Values.Max(y => y.EndDate);
            var closures = await _db.NonInstructionalDays
                .Where(d => schoolIds.Contains(d.SchoolId) && d.Date >= from && d.Date <= to)
                .ToListAsync(cancellationToken);
            foreach (var (schoolId, schoolYear) in years)
            {
                calendars[schoolId] = new SchoolCalendar(
                    schoolYear,
                    closures.Where(c => c.SchoolId == schoolId).Select(c => c.Date));
            }
        }

        var results = new Dictionary<int, StudentChronicEvaluation>();
        foreach (var studentId in studentIds.Distinct())
        {
            var studentEnrollments = enrollments.Where(e => e.StudentId == studentId).ToList();
            var studentTotals = totals[studentId];
            var currentSchoolId = CurrentSchoolResolver.Resolve(year, studentEnrollments, years);
            var enrolledDays = EnrolledDaysCalculator.Calculate(year, studentEnrollments, calendars, today);

            ChronicAbsenceResult result;
            string? reason = null;
            if (currentSchoolId is { } current)
            {
                var schoolSettings = settings.GetValueOrDefault(current) ?? SchoolAttendanceSettings.CreateDefault(current);
                result = ChronicAbsenceEvaluator.Evaluate(studentTotals.TotalAbsences, enrolledDays, schoolSettings);
            }
            else
            {
                // No current school: show both results with the defaults, but don't decide (Phase 2 follow-up answer).
                result = ChronicAbsenceEvaluator.Evaluate(studentTotals.TotalAbsences, enrolledDays, SchoolAttendanceSettings.CreateDefault(0))
                    with { Status = ChronicStatus.NotDeterminable };
                reason = NoEnrollmentReason;
            }

            if (reason is null && result.Status == ChronicStatus.NotDeterminable)
            {
                reason = result.Rate.NotDeterminableReason;
            }

            var included = studentEnrollments
                .Where(e =>
                {
                    var (start, end) = SchoolYearRanges.For(e.SchoolId, year, years);
                    return e.Overlaps(start, end);
                })
                .Select(e => e.SchoolId)
                .Concat(recordSchools.Where(r => r.StudentId == studentId).Select(r => r.SchoolId))
                .Distinct()
                .Order()
                .ToList();

            results[studentId] = new StudentChronicEvaluation(studentId, year, studentTotals, result, currentSchoolId, reason, included);
        }

        return results;
    }
}
