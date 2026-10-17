using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Chronic;

/// <summary>
/// Calculates both chronic-absence results and decides the overall status (plan §7 and decision 4).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Count: chronic when total absences ≥ the count threshold.</item>
/// <item>Rate: chronic when absences ÷ enrolled days × 100 ≥ the rate threshold (compared unrounded).</item>
/// <item>Method Count: the count result decides.</item>
/// <item>Method Rate: the rate result decides; NotDeterminable when the rate can't be calculated.</item>
/// <item>Method Either: Chronic when either result is chronic; otherwise NotDeterminable when the rate
/// can't be calculated, else NotChronic.</item>
/// </list>
/// The thresholds and method come from the student's current school (Q3).
/// </remarks>
public static class ChronicAbsenceEvaluator
{
    public static ChronicAbsenceResult Evaluate(
        int totalAbsences,
        EnrolledDaysResult enrolledDays,
        SchoolAttendanceSettings currentSchoolSettings) =>
        Evaluate(
            totalAbsences,
            enrolledDays,
            currentSchoolSettings.ChronicMethod,
            currentSchoolSettings.ChronicCountThreshold,
            currentSchoolSettings.ChronicRateThreshold);

    public static ChronicAbsenceResult Evaluate(
        int totalAbsences,
        EnrolledDaysResult enrolledDays,
        ChronicMethod method,
        int countThreshold,
        decimal rateThresholdPercent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(totalAbsences);

        var count = new ChronicCountResult(totalAbsences, countThreshold, totalAbsences >= countThreshold);

        ChronicRateResult rate;
        if (enrolledDays.Days is { } days)
        {
            var percent = totalAbsences * 100m / days;
            rate = new ChronicRateResult(true, days, percent, rateThresholdPercent, percent >= rateThresholdPercent, null);
        }
        else
        {
            rate = new ChronicRateResult(false, null, null, rateThresholdPercent, false, enrolledDays.NotDeterminableReason);
        }

        var status = method switch
        {
            ChronicMethod.Count => count.IsChronic ? ChronicStatus.Chronic : ChronicStatus.NotChronic,
            ChronicMethod.Rate => RateStatus(rate),
            ChronicMethod.Either => count.IsChronic ? ChronicStatus.Chronic : RateStatus(rate),
            _ => throw new ArgumentOutOfRangeException(nameof(method), method, "Unknown chronic method."),
        };

        return new ChronicAbsenceResult(method, count, rate, status);
    }

    private static ChronicStatus RateStatus(ChronicRateResult rate) =>
        !rate.IsDeterminable ? ChronicStatus.NotDeterminable
        : rate.IsChronic ? ChronicStatus.Chronic
        : ChronicStatus.NotChronic;
}
