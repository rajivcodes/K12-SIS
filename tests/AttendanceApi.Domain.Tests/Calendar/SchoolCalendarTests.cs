using AttendanceApi.Domain.Calendar;

namespace AttendanceApi.Domain.Tests.Calendar;

public class SchoolCalendarTests
{
    private static SchoolCalendar Calendar()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        year.AddTerm("Fall", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 18));
        return new SchoolCalendar(year, [new DateOnly(2026, 10, 12)]);
    }

    [Fact]
    public void A_weekday_inside_a_term_is_a_school_day()
    {
        Assert.True(Calendar().IsSchoolDay(new DateOnly(2026, 10, 13)));
    }

    [Theory]
    [InlineData(2026, 10, 17)] // Saturday
    [InlineData(2026, 10, 18)] // Sunday
    [InlineData(2026, 10, 12)] // non-instructional day
    [InlineData(2026, 12, 21)] // weekday between terms
    [InlineData(2026, 8, 31)]  // before the year
    public void Weekends_closures_gaps_between_terms_and_days_outside_the_year_are_not_school_days(int y, int m, int d)
    {
        Assert.False(Calendar().IsSchoolDay(new DateOnly(y, m, d)));
    }
}
