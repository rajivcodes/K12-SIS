using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Attendance;

/// <summary>
/// Stored absence totals for a student and school year, district-wide. Always replaced with totals
/// recalculated from the records, never incremented (fixes D-09).
/// </summary>
public sealed class StudentAttendanceSummary
{
    private StudentAttendanceSummary()
    {
        SchoolYearLabel = string.Empty;
    }

    private StudentAttendanceSummary(int studentId, SchoolYearLabel label)
    {
        StudentId = studentId;
        SchoolYearLabel = label.ToString();
    }

    public int StudentId { get; private set; }

    public string SchoolYearLabel { get; private set; }

    public int TotalAbsences { get; private set; }

    public int ExcusedAbsences { get; private set; }

    public int UnexcusedAbsences { get; private set; }

    public DateTime LastUpdated { get; private set; }

    public AttendanceTotals Totals => new(TotalAbsences, ExcusedAbsences, UnexcusedAbsences);

    public static StudentAttendanceSummary Create(int studentId, SchoolYearLabel label, AttendanceTotals totals, DateTime utcNow)
    {
        var summary = new StudentAttendanceSummary(studentId, label);
        summary.Replace(totals, utcNow);
        return summary;
    }

    public void Replace(AttendanceTotals totals, DateTime utcNow)
    {
        if (totals.TotalAbsences != totals.ExcusedAbsences + totals.UnexcusedAbsences || totals.ExcusedAbsences < 0 || totals.UnexcusedAbsences < 0)
        {
            throw new DomainException("Absence totals are inconsistent.");
        }

        TotalAbsences = totals.TotalAbsences;
        ExcusedAbsences = totals.ExcusedAbsences;
        UnexcusedAbsences = totals.UnexcusedAbsences;
        LastUpdated = Guard.Utc(utcNow, nameof(utcNow));
    }
}
