namespace AttendanceApi.Domain.Common;

/// <summary>Calendar helpers for attendance dates (calendar dates with no time zone).</summary>
public static class AttendanceDates
{
    public static bool IsWeekend(DateOnly date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static DateOnly Min(DateOnly a, DateOnly b) => a <= b ? a : b;

    public static DateOnly Max(DateOnly a, DateOnly b) => a >= b ? a : b;

    /// <summary>True when the inclusive ranges [aStart, aEnd] and [bStart, bEnd] share at least one day.</summary>
    public static bool Overlaps(DateOnly aStart, DateOnly aEnd, DateOnly bStart, DateOnly bEnd) =>
        aStart <= bEnd && bStart <= aEnd;
}
