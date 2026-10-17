using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Tests.Students;

public class CurrentSchoolResolverTests
{
    private static readonly SchoolYearLabel Year = new(2026);
    private static readonly Dictionary<int, SchoolYear> NoYears = [];

    [Fact]
    public void Latest_enrollment_in_the_year_wins_after_a_transfer()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 11, 20), id: 1),
            TestData.Enrollment(100, 2, new DateOnly(2026, 11, 23), id: 2),
        };

        Assert.Equal(2, CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }

    [Fact]
    public void Latest_enrollment_wins_even_if_it_has_ended()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), id: 1),
            TestData.Enrollment(100, 2, new DateOnly(2027, 1, 4), new DateOnly(2027, 3, 1), id: 2),
        };

        Assert.Equal(2, CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }

    [Fact]
    public void Enrollments_outside_the_year_are_ignored()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30), id: 1),
            TestData.Enrollment(100, 2, new DateOnly(2027, 9, 1), id: 2),
        };

        Assert.Null(CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }

    [Fact]
    public void An_enrollment_that_started_in_an_earlier_year_still_counts()
    {
        var enrollments = new[] { TestData.Enrollment(100, 3, new DateOnly(2022, 9, 1), id: 1) };

        Assert.Equal(3, CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }

    [Fact]
    public void Each_school_uses_its_own_year_dates_when_it_has_them()
    {
        // School 2's 2026-2027 year starts 17 Aug, so an entry on 20 Aug 2026 belongs to it,
        // although the default 1 Sep cut-off would put that date in 2025-2026.
        var years = new Dictionary<int, SchoolYear>
        {
            [2] = SchoolYear.Create(2, new DateOnly(2026, 8, 17), new DateOnly(2027, 6, 4)),
        };
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2025, 9, 1), new DateOnly(2026, 6, 30), id: 1),
            TestData.Enrollment(100, 2, new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 25), id: 2),
        };

        Assert.Equal(2, CurrentSchoolResolver.Resolve(Year, enrollments, years));
        Assert.Null(CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }

    [Fact]
    public void Same_entry_date_is_broken_by_the_latest_enrollment_id()
    {
        var enrollments = new[]
        {
            TestData.Enrollment(100, 1, new DateOnly(2026, 9, 1), id: 5),
            TestData.Enrollment(100, 2, new DateOnly(2026, 9, 1), id: 6),
        };

        Assert.Equal(2, CurrentSchoolResolver.Resolve(Year, enrollments, NoYears));
    }
}
