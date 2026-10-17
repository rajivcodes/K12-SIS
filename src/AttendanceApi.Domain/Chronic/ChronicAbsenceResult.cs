using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Chronic;

public enum ChronicStatus
{
    NotChronic = 1,
    Chronic = 2,

    /// <summary>The deciding method needs the rate, and the rate can't be determined (and count doesn't already flag).</summary>
    NotDeterminable = 3,
}

/// <summary>The count method: absent days ≥ threshold.</summary>
public sealed record ChronicCountResult(int TotalAbsences, int Threshold, bool IsChronic);

/// <summary>The rate method: absent days ÷ enrolled school days ≥ threshold percent.</summary>
public sealed record ChronicRateResult(
    bool IsDeterminable,
    int? EnrolledDays,
    decimal? RatePercent,
    decimal ThresholdPercent,
    bool IsChronic,
    string? NotDeterminableReason);

/// <summary>Both results, plus the overall status decided by the school's <see cref="ChronicMethod"/>.</summary>
public sealed record ChronicAbsenceResult(
    ChronicMethod Method,
    ChronicCountResult Count,
    ChronicRateResult Rate,
    ChronicStatus Status)
{
    public bool IsChronicallyAbsent => Status == ChronicStatus.Chronic;
}
