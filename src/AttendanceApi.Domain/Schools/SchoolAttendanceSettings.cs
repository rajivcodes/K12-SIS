using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Schools;

/// <summary>Per-school chronic-absence thresholds and date rules (plan decisions 4 and 10, Q4).</summary>
public sealed class SchoolAttendanceSettings
{
    public const int DefaultChronicCountThreshold = 10;
    public const decimal DefaultChronicRateThreshold = 10.0m;

    /// <summary>Upper bound for <see cref="MaxBackdateDays"/> and <see cref="MaxFutureDays"/> (10 years); use null for unlimited.</summary>
    public const int MaxDayLimit = 3650;

    private SchoolAttendanceSettings()
    {
    }

    public int SchoolId { get; private set; }

    public ChronicMethod ChronicMethod { get; private set; }

    /// <summary>Absent days at or above which a student is chronically absent (count method).</summary>
    public int ChronicCountThreshold { get; private set; }

    /// <summary>Percentage of enrolled school days (0–100) at or above which a student is chronically absent (rate method).</summary>
    public decimal ChronicRateThreshold { get; private set; }

    public bool AllowFutureDates { get; private set; }

    public bool AllowWeekends { get; private set; }

    public bool AllowOutsideTerm { get; private set; }

    /// <summary>How many days before today entry is allowed. Null = unlimited; 0 = today only.</summary>
    public int? MaxBackdateDays { get; private set; }

    /// <summary>How many days after today entry is allowed when <see cref="AllowFutureDates"/> is true. Null = unlimited.</summary>
    public int? MaxFutureDays { get; private set; }

    public static SchoolAttendanceSettings CreateDefault(int schoolId) => new()
    {
        SchoolId = schoolId,
        ChronicMethod = ChronicMethod.Count,
        ChronicCountThreshold = DefaultChronicCountThreshold,
        ChronicRateThreshold = DefaultChronicRateThreshold,
        AllowFutureDates = false,
        AllowWeekends = false,
        AllowOutsideTerm = false,
        MaxBackdateDays = null,
        MaxFutureDays = null,
    };

    public void SetChronicRules(ChronicMethod method, int countThreshold, decimal rateThreshold)
    {
        if (!Enum.IsDefined(method))
        {
            throw new DomainException($"Unknown chronic method '{method}'.");
        }

        if (rateThreshold <= 0m || rateThreshold > 100m)
        {
            throw new DomainException("Chronic rate threshold must be greater than 0 and at most 100 percent.");
        }

        ChronicMethod = method;
        ChronicCountThreshold = Guard.Positive(countThreshold, "Chronic count threshold");
        ChronicRateThreshold = rateThreshold;
    }

    public void SetDateRules(
        bool allowFutureDates,
        bool allowWeekends,
        bool allowOutsideTerm,
        int? maxBackdateDays,
        int? maxFutureDays)
    {
        AllowFutureDates = allowFutureDates;
        AllowWeekends = allowWeekends;
        AllowOutsideTerm = allowOutsideTerm;
        MaxBackdateDays = DayLimit(maxBackdateDays, "Max backdate days");
        MaxFutureDays = DayLimit(maxFutureDays, "Max future days");
    }

    private static int? DayLimit(int? value, string name)
    {
        Guard.NonNegative(value, name);
        return value > MaxDayLimit
            ? throw new DomainException($"{name} must be at most {MaxDayLimit}; leave it empty for unlimited.")
            : value;
    }
}
