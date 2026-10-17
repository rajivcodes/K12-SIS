using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Students;

/// <summary>
/// A period during which a student is enrolled at a school (G-04). Both dates are inclusive:
/// <see cref="ExitDate"/> is the last day the student is enrolled; null means still enrolled.
/// </summary>
public sealed class StudentEnrollment
{
    private StudentEnrollment()
    {
    }

    private StudentEnrollment(int studentId, int schoolId, DateOnly entryDate, DateOnly? exitDate)
    {
        StudentId = studentId;
        SchoolId = schoolId;
        SetDates(entryDate, exitDate);
    }

    public int Id { get; private set; }

    public int StudentId { get; private set; }

    public int SchoolId { get; private set; }

    public DateOnly EntryDate { get; private set; }

    public DateOnly? ExitDate { get; private set; }

    public static StudentEnrollment Create(int studentId, int schoolId, DateOnly entryDate, DateOnly? exitDate = null) =>
        new(studentId, schoolId, entryDate, exitDate);

    public bool IsEnrolledOn(DateOnly date) =>
        date >= EntryDate && (ExitDate is null || date <= ExitDate.Value);

    /// <summary>True when the enrollment shares at least one day with the inclusive range [start, end].</summary>
    public bool Overlaps(DateOnly start, DateOnly end) =>
        AttendanceDates.Overlaps(EntryDate, ExitDate ?? DateOnly.MaxValue, start, end);

    public void Exit(DateOnly exitDate) => SetDates(EntryDate, exitDate);

    public void ChangeDates(DateOnly entryDate, DateOnly? exitDate) => SetDates(entryDate, exitDate);

    private void SetDates(DateOnly entryDate, DateOnly? exitDate)
    {
        if (exitDate < entryDate)
        {
            throw new DomainException("Exit date must be on or after the entry date.");
        }

        EntryDate = entryDate;
        ExitDate = exitDate;
    }
}
