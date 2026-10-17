using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Calendar;

public sealed class Term
{
    public const int MaxNameLength = 100;

    private Term()
    {
        Name = string.Empty;
    }

    internal Term(SchoolYear schoolYear, string name, DateOnly startDate, DateOnly endDate)
    {
        SchoolYearId = schoolYear.Id;
        Name = Guard.Required(name, MaxNameLength, "Term name");
        StartDate = startDate;
        EndDate = endDate;
    }

    public int Id { get; private set; }

    public int SchoolYearId { get; private set; }

    public string Name { get; private set; }

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    internal void Change(string name, DateOnly startDate, DateOnly endDate)
    {
        Name = Guard.Required(name, MaxNameLength, "Term name");
        StartDate = startDate;
        EndDate = endDate;
    }
}
