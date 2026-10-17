using AttendanceApi.Domain.Calendar;

namespace AttendanceApi.Domain.Students;

/// <summary>
/// Finds a student's "current school" for a school year (Q3): the school of the enrollment with the
/// latest entry date that overlaps that school's year. It decides the count threshold, the deciding
/// chronic method and the alert's school.
/// </summary>
public static class CurrentSchoolResolver
{
    /// <param name="label">The school year.</param>
    /// <param name="enrollments">The student's enrollments (any years).</param>
    /// <param name="yearsBySchool">Each school's school year for <paramref name="label"/>, where one exists.</param>
    /// <returns>The school ID, or null when no enrollment overlaps the year.</returns>
    public static int? Resolve(
        SchoolYearLabel label,
        IEnumerable<StudentEnrollment> enrollments,
        IReadOnlyDictionary<int, SchoolYear> yearsBySchool)
    {
        var latest = enrollments
            .Where(e =>
            {
                var (start, end) = SchoolYearRanges.For(e.SchoolId, label, yearsBySchool);
                return e.Overlaps(start, end);
            })
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.Id)
            .FirstOrDefault();

        return latest?.SchoolId;
    }
}
