using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Calendar;

/// <summary>A holiday or closure. Not counted as an enrolled school day in the rate method.</summary>
public sealed class NonInstructionalDay
{
    public const int MaxReasonLength = 200;

    private NonInstructionalDay()
    {
        Reason = string.Empty;
    }

    private NonInstructionalDay(int schoolId, DateOnly date, string reason)
    {
        SchoolId = schoolId;
        Date = date;
        Reason = Guard.Required(reason, MaxReasonLength, "Reason");
    }

    public int Id { get; private set; }

    public int SchoolId { get; private set; }

    public DateOnly Date { get; private set; }

    public string Reason { get; private set; }

    public static NonInstructionalDay Create(int schoolId, DateOnly date, string reason) =>
        new(schoolId, date, reason);

    public void ChangeReason(string reason) => Reason = Guard.Required(reason, MaxReasonLength, "Reason");
}
