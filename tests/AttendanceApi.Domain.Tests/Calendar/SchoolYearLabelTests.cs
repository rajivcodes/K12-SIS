using AttendanceApi.Domain.Calendar;

namespace AttendanceApi.Domain.Tests.Calendar;

public class SchoolYearLabelTests
{
    [Theory]
    [InlineData("2026-09-01", "2026-2027")]
    [InlineData("2027-08-31", "2026-2027")]
    [InlineData("2026-08-31", "2025-2026")]
    [InlineData("2027-01-01", "2026-2027")]
    [InlineData("2026-12-31", "2026-2027")]
    [InlineData("2024-02-29", "2023-2024")]
    public void ForDate_uses_the_1_September_cut_off(string date, string expected)
    {
        var label = SchoolYearLabel.ForDate(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Equal(expected, label.ToString());
    }

    [Fact]
    public void Default_range_runs_from_1_September_to_31_August()
    {
        var label = new SchoolYearLabel(2026);

        Assert.Equal(new DateOnly(2026, 9, 1), label.DefaultStartDate);
        Assert.Equal(new DateOnly(2027, 8, 31), label.DefaultEndDate);
        Assert.Equal(2027, label.EndYear);
    }

    [Fact]
    public void Parse_round_trips()
    {
        Assert.Equal(new SchoolYearLabel(2026), SchoolYearLabel.Parse("2026-2027"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2026")]
    [InlineData("2026-2028")]
    [InlineData("2026/2027")]
    [InlineData("26-27")]
    [InlineData("abcd-efgh")]
    [InlineData("+026-2027")]
    [InlineData("1899-1900")]
    public void TryParse_rejects_malformed_labels(string value)
    {
        Assert.False(SchoolYearLabel.TryParse(value, out _));
        Assert.Throws<FormatException>(() => SchoolYearLabel.Parse(value));
    }

    [Fact]
    public void TryParse_rejects_null()
    {
        Assert.False(SchoolYearLabel.TryParse(null, out _));
    }
}
