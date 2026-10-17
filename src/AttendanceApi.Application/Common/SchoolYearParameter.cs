using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Calendar;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Common;

/// <summary>Resolves the <c>schoolYear</c> query parameter of the student endpoints.</summary>
public static class SchoolYearParameter
{
    /// <summary>
    /// Parses <paramref name="value"/> (<c>YYYY-YYYY</c>). When it is empty, uses the current school year:
    /// the year containing today at the school of the student's latest enrollment, or the default
    /// 1 September rule when that school has no year containing today.
    /// </summary>
    public static async Task<SchoolYearLabel> ResolveAsync(
        string? value,
        int studentId,
        IAttendanceDbContext db,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return SchoolYearLabel.TryParse(value.Trim(), out var parsed)
                ? parsed
                : throw new RequestValidationException($"'{value}' is not a school year in the form YYYY-YYYY, e.g. 2026-2027.");
        }

        var latestSchoolId = await db.StudentEnrollments
            .Where(e => e.StudentId == studentId && e.EntryDate <= today)
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.Id)
            .Select(e => (int?)e.SchoolId)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestSchoolId is { } schoolId)
        {
            var label = await db.SchoolYears
                .Where(y => y.SchoolId == schoolId && y.StartDate <= today && y.EndDate >= today)
                .Select(y => y.Label)
                .FirstOrDefaultAsync(cancellationToken);
            if (label is not null)
            {
                return SchoolYearLabel.Parse(label);
            }
        }

        return SchoolYearLabel.ForDate(today);
    }
}
