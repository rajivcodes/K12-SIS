using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Tests.Chronic;

public class ChronicAbsenceEvaluatorTests
{
    private static readonly EnrolledDaysResult HundredDays = EnrolledDaysResult.Determinable(100);
    private static readonly EnrolledDaysResult Unknown = EnrolledDaysResult.NotDeterminable("No calendar.");

    [Theory]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    public void Count_is_chronic_at_or_above_the_threshold(int absences, bool expected)
    {
        var result = ChronicAbsenceEvaluator.Evaluate(absences, HundredDays, ChronicMethod.Count, 10, 10m);

        Assert.Equal(expected, result.Count.IsChronic);
        Assert.Equal(absences, result.Count.TotalAbsences);
        Assert.Equal(10, result.Count.Threshold);
    }

    [Theory]
    [InlineData(9, 100, false)]  // 9%
    [InlineData(10, 100, true)]  // exactly 10%
    [InlineData(4, 39, true)]    // 10.256%
    [InlineData(4, 41, false)]   // 9.756%
    [InlineData(1, 10, true)]    // 10%
    public void Rate_is_chronic_at_or_above_the_threshold_percent(int absences, int days, bool expected)
    {
        var result = ChronicAbsenceEvaluator.Evaluate(absences, EnrolledDaysResult.Determinable(days), ChronicMethod.Rate, 10, 10m);

        Assert.True(result.Rate.IsDeterminable);
        Assert.Equal(days, result.Rate.EnrolledDays);
        Assert.Equal(absences * 100m / days, result.Rate.RatePercent);
        Assert.Equal(expected, result.Rate.IsChronic);
    }

    [Fact]
    public void Rate_is_compared_unrounded()
    {
        // 2 / 21 = 9.5238%: rounds to 10% at zero decimals but is below the threshold.
        var result = ChronicAbsenceEvaluator.Evaluate(2, EnrolledDaysResult.Determinable(21), ChronicMethod.Rate, 10, 10m);

        Assert.False(result.Rate.IsChronic);
    }

    [Fact]
    public void Both_results_are_always_returned()
    {
        var result = ChronicAbsenceEvaluator.Evaluate(3, EnrolledDaysResult.Determinable(20), ChronicMethod.Count, 10, 10m);

        Assert.False(result.Count.IsChronic);
        Assert.True(result.Rate.IsChronic);
        Assert.Equal(15m, result.Rate.RatePercent);
        Assert.Equal(ChronicStatus.NotChronic, result.Status);
    }

    [Theory]
    // method, absences, enrolled days (0 = not determinable), expected status
    [InlineData(ChronicMethod.Count, 10, 200, ChronicStatus.Chronic)]       // count yes, rate 5% no
    [InlineData(ChronicMethod.Count, 3, 20, ChronicStatus.NotChronic)]      // count no, rate 15% yes
    [InlineData(ChronicMethod.Count, 3, 0, ChronicStatus.NotChronic)]       // rate unknown is irrelevant
    [InlineData(ChronicMethod.Rate, 10, 200, ChronicStatus.NotChronic)]
    [InlineData(ChronicMethod.Rate, 3, 20, ChronicStatus.Chronic)]
    [InlineData(ChronicMethod.Rate, 12, 0, ChronicStatus.NotDeterminable)]  // even though count would flag
    [InlineData(ChronicMethod.Either, 10, 200, ChronicStatus.Chronic)]
    [InlineData(ChronicMethod.Either, 3, 20, ChronicStatus.Chronic)]
    [InlineData(ChronicMethod.Either, 3, 200, ChronicStatus.NotChronic)]
    [InlineData(ChronicMethod.Either, 12, 0, ChronicStatus.Chronic)]        // count already flags
    [InlineData(ChronicMethod.Either, 3, 0, ChronicStatus.NotDeterminable)]
    public void Method_decides_the_overall_status(ChronicMethod method, int absences, int enrolledDays, ChronicStatus expected)
    {
        var days = enrolledDays == 0 ? Unknown : EnrolledDaysResult.Determinable(enrolledDays);

        var result = ChronicAbsenceEvaluator.Evaluate(absences, days, method, 10, 10m);

        Assert.Equal(expected, result.Status);
        Assert.Equal(expected == ChronicStatus.Chronic, result.IsChronicallyAbsent);
        Assert.Equal(method, result.Method);
    }

    [Fact]
    public void Not_determinable_rate_carries_its_reason_and_no_numbers()
    {
        var result = ChronicAbsenceEvaluator.Evaluate(5, Unknown, ChronicMethod.Count, 10, 10m);

        Assert.False(result.Rate.IsDeterminable);
        Assert.Null(result.Rate.EnrolledDays);
        Assert.Null(result.Rate.RatePercent);
        Assert.False(result.Rate.IsChronic);
        Assert.Equal("No calendar.", result.Rate.NotDeterminableReason);
    }

    [Fact]
    public void Settings_overload_uses_the_schools_method_and_thresholds()
    {
        var settings = SchoolAttendanceSettings.CreateDefault(1);
        settings.SetChronicRules(ChronicMethod.Rate, 5, 20m);

        var result = ChronicAbsenceEvaluator.Evaluate(4, EnrolledDaysResult.Determinable(20), settings);

        Assert.Equal(ChronicMethod.Rate, result.Method);
        Assert.Equal(5, result.Count.Threshold);
        Assert.Equal(20m, result.Rate.ThresholdPercent);
        Assert.Equal(ChronicStatus.Chronic, result.Status);
    }

    [Fact]
    public void Zero_absences_is_never_chronic()
    {
        var result = ChronicAbsenceEvaluator.Evaluate(0, HundredDays, ChronicMethod.Either, 10, 10m);

        Assert.Equal(ChronicStatus.NotChronic, result.Status);
        Assert.Equal(0m, result.Rate.RatePercent);
    }

    [Fact]
    public void Negative_absences_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ChronicAbsenceEvaluator.Evaluate(-1, HundredDays, ChronicMethod.Count, 10, 10m));
    }

    [Fact]
    public void Enrolled_days_must_be_positive_when_determinable()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EnrolledDaysResult.Determinable(0));
    }
}
