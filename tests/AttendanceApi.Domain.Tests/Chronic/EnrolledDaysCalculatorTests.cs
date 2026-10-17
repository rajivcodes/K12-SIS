using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Tests.Chronic;

public class EnrolledDaysCalculatorTests
{
    private static readonly SchoolYearLabel Year = new(2026);

    // 1 Sep 2026 is a Tuesday; September 2026 has 22 weekdays and October 1–16 has 12.
    private static readonly DateOnly EndOfSeptember = new(2026, 9, 30);

    private static SchoolCalendar DefaultCalendar(int schoolId, params DateOnly[] closures) =>
        new(SchoolYear.CreateDefault(schoolId, new DateOnly(2026, 9, 1)), closures);

    private static EnrolledDaysResult Calculate(
        IEnumerable<StudentEnrollment> enrollments,
        DateOnly today,
        params SchoolCalendar[] calendars) =>
        EnrolledDaysCalculator.Calculate(Year, enrollments, calendars.ToDictionary(c => c.SchoolId), today);

    [Fact]
    public void Counts_weekdays_from_the_year_start_up_to_today()
    {
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], EndOfSeptember, DefaultCalendar(1));

        Assert.True(result.IsDeterminable);
        Assert.Equal(22, result.Days);
    }

    [Fact]
    public void Today_is_included()
    {
        // Tuesday 1 Sep only.
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], new DateOnly(2026, 9, 1), DefaultCalendar(1));

        Assert.Equal(1, result.Days);
    }

    [Fact]
    public void Non_instructional_days_are_excluded()
    {
        var calendar = DefaultCalendar(1, new DateOnly(2026, 9, 7), new DateOnly(2026, 9, 12));

        // 7 Sep is a Monday (excluded); 12 Sep is a Saturday (already excluded).
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], EndOfSeptember, calendar);

        Assert.Equal(21, result.Days);
    }

    [Fact]
    public void Days_outside_every_term_are_excluded()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        year.AddTerm("Fall", new DateOnly(2026, 9, 8), new DateOnly(2027, 6, 11));
        var calendar = new SchoolCalendar(year, []);

        // Tue 1 Sep to Mon 7 Sep (5 weekdays) are before the term starts.
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], EndOfSeptember, calendar);

        Assert.Equal(17, result.Days);
    }

    [Fact]
    public void Only_days_inside_the_enrollment_count_with_inclusive_exit()
    {
        // Enrolled Mon 14 Sep to Fri 18 Sep: 5 days.
        var result = Calculate(
            [TestData.Enrollment(100, 1, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 18))],
            EndOfSeptember,
            DefaultCalendar(1));

        Assert.Equal(5, result.Days);
    }

    [Fact]
    public void Enrollment_starting_before_the_year_counts_from_the_year_start()
    {
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2024, 9, 1))], EndOfSeptember, DefaultCalendar(1));

        Assert.Equal(22, result.Days);
    }

    [Fact]
    public void Counting_stops_at_the_year_end_when_today_is_later()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));
        year.AddTerm("Short", year.StartDate, year.EndDate);

        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], new DateOnly(2027, 3, 1), new SchoolCalendar(year, []));

        Assert.Equal(22, result.Days);
    }

    [Fact]
    public void Transfer_combines_days_from_each_schools_own_calendar()
    {
        // School 1 until Fri 11 Sep (9 days), then school 2 from Mon 14 Sep (13 days), and school 2 is
        // closed on Mon 21 Sep.
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11)),
            TestData.Enrollment(100, 2, new DateOnly(2026, 9, 14)),
        };

        var result = Calculate(enrollments, EndOfSeptember, DefaultCalendar(1), DefaultCalendar(2, new DateOnly(2026, 9, 21)));

        Assert.Equal(9 + 12, result.Days);
    }

    [Fact]
    public void A_day_enrolled_at_two_schools_counts_once()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 15)),
            TestData.Enrollment(100, 2, new DateOnly(2026, 9, 14)),
        };

        var result = Calculate(enrollments, EndOfSeptember, DefaultCalendar(1), DefaultCalendar(2));

        Assert.Equal(22, result.Days);
    }

    [Fact]
    public void No_enrollment_in_the_year_is_not_determinable()
    {
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30))], EndOfSeptember, DefaultCalendar(1));

        Assert.False(result.IsDeterminable);
        Assert.Null(result.Days);
        Assert.Contains("no enrollment", result.NotDeterminableReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Missing_calendar_for_an_enrolled_school_is_not_determinable()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 11)),
            TestData.Enrollment(100, 2, new DateOnly(2026, 9, 14)),
        };

        var result = Calculate(enrollments, EndOfSeptember, DefaultCalendar(1));

        Assert.False(result.IsDeterminable);
        Assert.Contains("School 2", result.NotDeterminableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void Calendar_for_a_different_year_is_treated_as_missing()
    {
        var lastYear = new SchoolCalendar(SchoolYear.CreateDefault(1, new DateOnly(2025, 9, 1)), []);

        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], EndOfSeptember, lastYear);

        Assert.False(result.IsDeterminable);
    }

    [Fact]
    public void Year_without_terms_is_not_determinable()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));

        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1))], EndOfSeptember, new SchoolCalendar(year, []));

        Assert.False(result.IsDeterminable);
        Assert.Contains("no terms", result.NotDeterminableReason, StringComparison.Ordinal);
    }

    [Fact]
    public void No_school_days_yet_is_not_determinable_rather_than_dividing_by_zero()
    {
        // Enrollment starts Saturday 17 Oct and today is Sunday 18 Oct.
        var result = Calculate([TestData.Enrollment(100, 1, new DateOnly(2026, 10, 17))], new DateOnly(2026, 10, 18), DefaultCalendar(1));

        Assert.False(result.IsDeterminable);
    }
}
