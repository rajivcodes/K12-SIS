using AttendanceApi.Domain.Alerts;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Chronic;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Schools;

namespace AttendanceApi.Domain.Tests.Alerts;

public class StudentAlertTests
{
    private static readonly SchoolYearLabel Year = new(2026);

    private static ChronicAbsenceResult Result(int absences, int days, ChronicMethod method) =>
        ChronicAbsenceEvaluator.Evaluate(absences, EnrolledDaysResult.Determinable(days), method, 10, 10m);

    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void Raised_only_when_chronic_and_no_alert_is_open(bool chronic, bool hasOpenAlert, bool expected)
    {
        var result = Result(chronic ? 10 : 1, 200, ChronicMethod.Count);

        Assert.Equal(expected, StudentAlert.ShouldRaiseChronicAbsence(result, hasOpenAlert));
    }

    [Fact]
    public void Chronic_alert_records_student_current_school_year_and_count()
    {
        var alert = StudentAlert.RaiseChronicAbsence(100, 2, Year, Result(10, 200, ChronicMethod.Count), TestData.UtcNow);

        Assert.Equal((100, 2, "CHRONIC_ABSENCE", "2026-2027"), (alert.StudentId, alert.SchoolId, alert.AlertType, alert.SchoolYearLabel));
        Assert.Equal(TestData.UtcNow, alert.RaisedAt);
        Assert.True(alert.IsOpen);
        Assert.Equal("Student is chronically absent in 2026-2027: 10 absences (threshold 10).", alert.Message);
    }

    [Fact]
    public void Rate_alert_message_describes_the_rate()
    {
        var alert = StudentAlert.RaiseChronicAbsence(100, 2, Year, Result(5, 40, ChronicMethod.Rate), TestData.UtcNow);

        Assert.Equal("Student is chronically absent in 2026-2027: 12.5% of 40 enrolled school days absent (threshold 10%).", alert.Message);
    }

    [Fact]
    public void Either_alert_message_lists_every_method_that_flagged()
    {
        var alert = StudentAlert.RaiseChronicAbsence(100, 2, Year, Result(10, 50, ChronicMethod.Either), TestData.UtcNow);

        Assert.Contains("10 absences", alert.Message, StringComparison.Ordinal);
        Assert.Contains("20% of 50", alert.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Cannot_raise_for_a_result_that_is_not_chronic()
    {
        Assert.Throws<DomainException>(() => StudentAlert.RaiseChronicAbsence(100, 2, Year, Result(1, 200, ChronicMethod.Count), TestData.UtcNow));
    }

    [Fact]
    public void Resolve_closes_the_alert_once()
    {
        var alert = StudentAlert.RaiseChronicAbsence(100, 2, Year, Result(10, 200, ChronicMethod.Count), TestData.UtcNow);
        var later = TestData.UtcNow.AddDays(1);

        alert.Resolve(TestData.Clerk, later);

        Assert.False(alert.IsOpen);
        Assert.Equal((later, 7, "clerk.jones"), (alert.ResolvedAt!.Value, alert.ResolvedByUserId!.Value, alert.ResolvedBy));
        Assert.Throws<DomainException>(() => alert.Resolve(TestData.Clerk, later));
    }
}
