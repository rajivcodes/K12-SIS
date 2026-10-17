using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Tests.Attendance;

public class AttendanceDatePolicyTests
{
    // Thursday 15 Oct 2026 (UTC).
    private static readonly DateOnly Today = new(2026, 10, 15);

    private static readonly SchoolYear Year = SchoolYear.CreateDefault(1, Today);

    private static SchoolAttendanceSettings Settings(
        bool future = false,
        bool weekends = false,
        bool outsideTerm = false,
        int? maxBack = null,
        int? maxFuture = null)
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);
        settings.SetDateRules(future, weekends, outsideTerm, maxBack, maxFuture);
        return settings;
    }

    [Fact]
    public void Today_is_allowed_with_default_settings()
    {
        Assert.Null(AttendanceDatePolicy.Check(Today, Today, Settings(), Year));
    }

    [Fact]
    public void Future_date_is_rejected_unless_allowed()
    {
        var tomorrow = Today.AddDays(1);

        var rejection = AttendanceDatePolicy.Check(tomorrow, Today, Settings(), Year);

        Assert.Equal(AttendanceDateRejectionReason.FutureDateNotAllowed, rejection?.Reason);
        Assert.Null(AttendanceDatePolicy.Check(tomorrow, Today, Settings(future: true), Year));
    }

    [Fact]
    public void MaxFutureDays_limits_how_far_ahead_when_future_dates_are_allowed()
    {
        var settings = Settings(future: true, maxFuture: 5);

        Assert.Null(AttendanceDatePolicy.Check(Today.AddDays(5), Today, settings, Year));
        Assert.Equal(
            AttendanceDateRejectionReason.TooFarInFuture,
            AttendanceDatePolicy.Check(Today.AddDays(6), Today, settings, Year)?.Reason);
    }

    [Fact]
    public void MaxFutureDays_is_ignored_when_future_dates_are_not_allowed()
    {
        var rejection = AttendanceDatePolicy.Check(Today.AddDays(1), Today, Settings(maxFuture: 30), Year);

        Assert.Equal(AttendanceDateRejectionReason.FutureDateNotAllowed, rejection?.Reason);
    }

    [Fact]
    public void MaxBackdateDays_limits_how_far_back()
    {
        var settings = Settings(maxBack: 3);

        Assert.Null(AttendanceDatePolicy.Check(Today.AddDays(-3), Today, settings, Year));
        Assert.Equal(
            AttendanceDateRejectionReason.TooFarInPast,
            AttendanceDatePolicy.Check(Today.AddDays(-4), Today, settings, Year)?.Reason);
    }

    [Fact]
    public void MaxBackdateDays_zero_allows_today_only()
    {
        var settings = Settings(maxBack: 0);

        Assert.Null(AttendanceDatePolicy.Check(Today, Today, settings, Year));
        Assert.NotNull(AttendanceDatePolicy.Check(Today.AddDays(-1), Today, settings, Year));
    }

    [Fact]
    public void Unlimited_backdating_by_default()
    {
        Assert.Null(AttendanceDatePolicy.Check(new DateOnly(2026, 9, 1), Today, Settings(), Year));
    }

    [Theory]
    [InlineData(10)] // Saturday
    [InlineData(11)] // Sunday
    public void Weekend_is_rejected_unless_allowed(int day)
    {
        var weekend = new DateOnly(2026, 10, day);

        Assert.Equal(
            AttendanceDateRejectionReason.WeekendNotAllowed,
            AttendanceDatePolicy.Check(weekend, Today, Settings(), Year)?.Reason);
        Assert.Null(AttendanceDatePolicy.Check(weekend, Today, Settings(weekends: true), Year));
    }

    [Fact]
    public void Date_outside_every_term_is_rejected_unless_allowed()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));
        year.AddTerm("Fall", new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 9));

        Assert.Equal(
            AttendanceDateRejectionReason.OutsideTerm,
            AttendanceDatePolicy.Check(Today, Today, Settings(), year)?.Reason);
        Assert.Null(AttendanceDatePolicy.Check(Today, Today, Settings(outsideTerm: true), year));
    }

    [Fact]
    public void Year_without_terms_rejects_every_date_unless_outside_term_is_allowed()
    {
        var year = SchoolYear.Create(1, new DateOnly(2026, 9, 1), new DateOnly(2027, 8, 31));

        Assert.Equal(AttendanceDateRejectionReason.OutsideTerm, AttendanceDatePolicy.Check(Today, Today, Settings(), year)?.Reason);
    }

    [Fact]
    public void Rejection_message_names_the_date()
    {
        var rejection = AttendanceDatePolicy.Check(Today.AddDays(1), Today, Settings(), Year);

        Assert.Contains("2026-10-16", rejection?.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Year_must_contain_the_date()
    {
        Assert.Throws<ArgumentException>(() => AttendanceDatePolicy.Check(new DateOnly(2027, 9, 1), Today, Settings(), Year));
    }
}
