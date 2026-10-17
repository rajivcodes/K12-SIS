namespace AttendanceApi.Domain.Schools;

/// <summary>Which chronic-absence result decides <c>isChronicallyAbsent</c> and the alert for a school.</summary>
public enum ChronicMethod
{
    /// <summary>Absent days ≥ the count threshold (legacy rule, the default).</summary>
    Count = 1,

    /// <summary>Absent days ÷ enrolled school days ≥ the rate threshold.</summary>
    Rate = 2,

    /// <summary>Chronic when either the count or the rate result is chronic.</summary>
    Either = 3,
}
