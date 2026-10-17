namespace AttendanceApi.Domain.Chronic;

/// <summary>Enrolled school days for the rate method, or the reason they can't be determined.</summary>
public sealed record EnrolledDaysResult
{
    private EnrolledDaysResult(int? days, string? notDeterminableReason)
    {
        Days = days;
        NotDeterminableReason = notDeterminableReason;
    }

    public bool IsDeterminable => Days is not null;

    public int? Days { get; }

    public string? NotDeterminableReason { get; }

    public static EnrolledDaysResult Determinable(int days) =>
        days > 0
            ? new EnrolledDaysResult(days, null)
            : throw new ArgumentOutOfRangeException(nameof(days), days, "Enrolled days must be positive.");

    public static EnrolledDaysResult NotDeterminable(string reason) => new(null, reason);
}
