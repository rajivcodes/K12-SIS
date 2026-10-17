namespace AttendanceApi.Domain.Attendance;

/// <summary>
/// Absence totals for one student and school year. Every absent day counts, excused included (BR-08);
/// tardies and minutes late never count.
/// </summary>
public readonly record struct AttendanceTotals(int TotalAbsences, int ExcusedAbsences, int UnexcusedAbsences)
{
    /// <summary>
    /// Counts from the stored records. Pass every record for the student in the school year, across all
    /// schools (fixes D-03 and D-08).
    /// </summary>
    public static AttendanceTotals FromRecords(IEnumerable<AttendanceRecord> records) =>
        FromFlags(records.Select(r => (r.IsAbsent, r.IsExcused)));

    public static AttendanceTotals FromFlags(IEnumerable<(bool IsAbsent, bool IsExcused)> days)
    {
        var excused = 0;
        var unexcused = 0;
        foreach (var (isAbsent, isExcused) in days)
        {
            if (!isAbsent)
            {
                continue;
            }

            if (isExcused)
            {
                excused++;
            }
            else
            {
                unexcused++;
            }
        }

        return new AttendanceTotals(excused + unexcused, excused, unexcused);
    }
}
