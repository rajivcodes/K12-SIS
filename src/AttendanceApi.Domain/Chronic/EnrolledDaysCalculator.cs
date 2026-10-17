using System.Globalization;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Chronic;

/// <summary>
/// Counts a student's enrolled school days in a school year for the rate method (plan §7): school days
/// inside the student's enrollment periods, from the year start up to the earlier of today and the year end.
/// Each enrollment uses its own school's calendar; a day enrolled at two schools counts once.
/// </summary>
public static class EnrolledDaysCalculator
{
    /// <param name="label">The school year.</param>
    /// <param name="enrollments">The student's enrollments (any years).</param>
    /// <param name="calendarsBySchool">Each school's calendar for <paramref name="label"/>, where one exists.</param>
    /// <param name="today">The current UTC date (Q2).</param>
    public static EnrolledDaysResult Calculate(
        SchoolYearLabel label,
        IEnumerable<StudentEnrollment> enrollments,
        IReadOnlyDictionary<int, SchoolCalendar> calendarsBySchool,
        DateOnly today)
    {
        var yearsBySchool = calendarsBySchool.ToDictionary(c => c.Key, c => c.Value.Year);
        var inYear = enrollments
            .Where(e =>
            {
                var (start, end) = SchoolYearRanges.For(e.SchoolId, label, yearsBySchool);
                return e.Overlaps(start, end);
            })
            .ToList();

        if (inYear.Count == 0)
        {
            return EnrolledDaysResult.NotDeterminable($"The student has no enrollment in school year {label}.");
        }

        var schoolDays = new HashSet<DateOnly>();
        foreach (var enrollment in inYear)
        {
            if (!calendarsBySchool.TryGetValue(enrollment.SchoolId, out var calendar) || calendar.Year.YearLabel != label)
            {
                return EnrolledDaysResult.NotDeterminable($"School {Id(enrollment.SchoolId)} has no calendar for school year {label}.");
            }

            if (calendar.Year.Terms.Count == 0)
            {
                return EnrolledDaysResult.NotDeterminable($"School {Id(enrollment.SchoolId)} has no terms defined for school year {label}.");
            }

            var from = AttendanceDates.Max(enrollment.EntryDate, calendar.Year.StartDate);
            var to = AttendanceDates.Min(calendar.Year.EndDate, today);
            if (enrollment.ExitDate is { } exit)
            {
                to = AttendanceDates.Min(to, exit);
            }

            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (calendar.IsSchoolDay(day))
                {
                    schoolDays.Add(day);
                }
            }
        }

        return schoolDays.Count == 0
            ? EnrolledDaysResult.NotDeterminable($"No school days have passed yet in school year {label} while the student was enrolled.")
            : EnrolledDaysResult.Determinable(schoolDays.Count);
    }

    private static string Id(int schoolId) => schoolId.ToString(CultureInfo.InvariantCulture);
}
