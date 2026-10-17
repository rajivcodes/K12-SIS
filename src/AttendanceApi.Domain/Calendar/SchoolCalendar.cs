using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Calendar;

/// <summary>
/// A school's year together with its non-instructional days. A school day is a weekday inside a term
/// that is not a non-instructional day (plan §7).
/// </summary>
public sealed class SchoolCalendar
{
    private readonly HashSet<DateOnly> _nonInstructionalDays;

    public SchoolCalendar(SchoolYear year, IEnumerable<DateOnly> nonInstructionalDays)
    {
        Year = year;
        _nonInstructionalDays = [.. nonInstructionalDays];
    }

    public SchoolYear Year { get; }

    public int SchoolId => Year.SchoolId;

    public bool IsSchoolDay(DateOnly date) =>
        Year.Contains(date)
        && !AttendanceDates.IsWeekend(date)
        && Year.FindTerm(date) is not null
        && !_nonInstructionalDays.Contains(date);
}
