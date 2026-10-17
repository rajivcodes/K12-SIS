using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Tests.Students;

public class StudentEnrollmentTests
{
    [Fact]
    public void Entry_and_exit_dates_are_both_inclusive()
    {
        var enrollment = StudentEnrollment.Create(1, 1, new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 18));

        Assert.False(enrollment.IsEnrolledOn(new DateOnly(2026, 8, 31)));
        Assert.True(enrollment.IsEnrolledOn(new DateOnly(2026, 9, 1)));
        Assert.True(enrollment.IsEnrolledOn(new DateOnly(2026, 12, 18)));
        Assert.False(enrollment.IsEnrolledOn(new DateOnly(2026, 12, 19)));
    }

    [Fact]
    public void Open_enrollment_has_no_end()
    {
        var enrollment = StudentEnrollment.Create(1, 1, new DateOnly(2026, 9, 1));

        Assert.True(enrollment.IsEnrolledOn(new DateOnly(2030, 1, 1)));
        Assert.True(enrollment.Overlaps(new DateOnly(2030, 9, 1), new DateOnly(2031, 8, 31)));
    }

    [Fact]
    public void Single_day_enrollment_is_allowed_but_exit_before_entry_is_not()
    {
        var day = new DateOnly(2026, 9, 1);
        Assert.True(StudentEnrollment.Create(1, 1, day, day).IsEnrolledOn(day));

        Assert.Throws<DomainException>(() => StudentEnrollment.Create(1, 1, day, day.AddDays(-1)));
    }

    [Fact]
    public void Exit_sets_the_last_enrolled_day()
    {
        var enrollment = StudentEnrollment.Create(1, 1, new DateOnly(2026, 9, 1));

        enrollment.Exit(new DateOnly(2026, 10, 2));

        Assert.Equal(new DateOnly(2026, 10, 2), enrollment.ExitDate);
        Assert.Throws<DomainException>(() => enrollment.Exit(new DateOnly(2026, 8, 1)));
    }
}
