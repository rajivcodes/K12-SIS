namespace AttendanceApi.Domain.Calendar;

/// <summary>Resolves the date range a school uses for a given school-year label.</summary>
public static class SchoolYearRanges
{
    /// <summary>
    /// The school's own year with that label if it has one; otherwise the default 1 Sep – 31 Aug range.
    /// </summary>
    public static (DateOnly Start, DateOnly End) For(
        int schoolId,
        SchoolYearLabel label,
        IReadOnlyDictionary<int, SchoolYear> yearsBySchool)
    {
        if (yearsBySchool.TryGetValue(schoolId, out var year) && year.YearLabel == label)
        {
            return (year.StartDate, year.EndDate);
        }

        return (label.DefaultStartDate, label.DefaultEndDate);
    }
}
