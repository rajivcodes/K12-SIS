using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace AttendanceApi.Domain.Calendar;

/// <summary>
/// A school-year label such as <c>2026-2027</c>, identified by the calendar year it starts in.
/// Labels are shared across schools so that absences can be combined district-wide per school year.
/// </summary>
public readonly record struct SchoolYearLabel
{
    public const int MinStartYear = 1900;
    public const int MaxStartYear = 9998;

    /// <summary>Month and day on which the default school year starts (BR-05: 1 September).</summary>
    public const int DefaultStartMonth = 9;

    public SchoolYearLabel(int startYear)
    {
        if (startYear is < MinStartYear or > MaxStartYear)
        {
            throw new ArgumentOutOfRangeException(nameof(startYear), startYear, "School year is out of range.");
        }

        StartYear = startYear;
    }

    public int StartYear { get; }

    public int EndYear => StartYear + 1;

    /// <summary>First day of the default school year (1 September).</summary>
    public DateOnly DefaultStartDate => new(StartYear, DefaultStartMonth, 1);

    /// <summary>Last day of the default school year (31 August of the following year).</summary>
    public DateOnly DefaultEndDate => new(EndYear, DefaultStartMonth - 1, 31);

    /// <summary>The default school year containing <paramref name="date"/> (1 September cut-off).</summary>
    public static SchoolYearLabel ForDate(DateOnly date) =>
        new(date.Month >= DefaultStartMonth ? date.Year : date.Year - 1);

    /// <summary>Parses <c>YYYY-YYYY</c> where the second year is the first plus one.</summary>
    public static bool TryParse([NotNullWhen(true)] string? value, out SchoolYearLabel label)
    {
        label = default;
        if (value is null || value.Length != 9 || value[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var start)
            || !int.TryParse(value.AsSpan(5, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var end)
            || end != start + 1
            || start is < MinStartYear or > MaxStartYear)
        {
            return false;
        }

        label = new SchoolYearLabel(start);
        return true;
    }

    public static SchoolYearLabel Parse(string value) =>
        TryParse(value, out var label)
            ? label
            : throw new FormatException($"'{value}' is not a school year in the form YYYY-YYYY.");

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{StartYear:D4}-{EndYear:D4}");
}
