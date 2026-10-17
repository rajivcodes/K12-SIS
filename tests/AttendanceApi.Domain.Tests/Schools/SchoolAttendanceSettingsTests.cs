using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Tests.Schools;

public class SchoolAttendanceSettingsTests
{
    [Fact]
    public void Defaults_match_the_plan()
    {
        var settings = SchoolAttendanceSettings.CreateDefault(4);

        Assert.Equal(4, settings.SchoolId);
        Assert.Equal(ChronicMethod.Count, settings.ChronicMethod);
        Assert.Equal(10, settings.ChronicCountThreshold);
        Assert.Equal(10.0m, settings.ChronicRateThreshold);
        Assert.False(settings.AllowFutureDates);
        Assert.False(settings.AllowWeekends);
        Assert.False(settings.AllowOutsideTerm);
        Assert.Null(settings.MaxBackdateDays);
        Assert.Null(settings.MaxFutureDays);
    }

    [Fact]
    public void SetChronicRules_stores_valid_values()
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);

        settings.SetChronicRules(ChronicMethod.Either, 15, 12.5m);

        Assert.Equal(ChronicMethod.Either, settings.ChronicMethod);
        Assert.Equal(15, settings.ChronicCountThreshold);
        Assert.Equal(12.5m, settings.ChronicRateThreshold);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 0)]
    [InlineData(10, 100.1)]
    public void SetChronicRules_rejects_out_of_range_thresholds(int count, double rate)
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);

        Assert.Throws<DomainException>(() => settings.SetChronicRules(ChronicMethod.Count, count, (decimal)rate));
    }

    [Fact]
    public void SetChronicRules_rejects_an_undefined_method()
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);

        Assert.Throws<DomainException>(() => settings.SetChronicRules((ChronicMethod)99, 10, 10m));
    }

    [Fact]
    public void SetDateRules_stores_values_and_rejects_negative_limits()
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);

        settings.SetDateRules(true, true, true, 0, 5);
        Assert.True(settings.AllowFutureDates);
        Assert.Equal(0, settings.MaxBackdateDays);
        Assert.Equal(5, settings.MaxFutureDays);

        Assert.Throws<DomainException>(() => settings.SetDateRules(false, false, false, -1, null));
        Assert.Throws<DomainException>(() => settings.SetDateRules(true, false, false, null, -1));
    }
}
