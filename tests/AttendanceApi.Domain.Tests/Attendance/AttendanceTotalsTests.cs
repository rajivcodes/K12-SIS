using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Tests.Attendance;

public class AttendanceTotalsTests
{
    [Fact]
    public void Every_absent_day_counts_excused_included_and_tardies_do_not()
    {
        var totals = AttendanceTotals.FromFlags(
        [
            (true, false),  // A
            (true, true),   // E
            (true, true),   // M
            (true, false),  // OSS
            (false, false), // P
            (false, false), // T
            (false, true),  // TE: excused tardy is not an absence
        ]);

        Assert.Equal(new AttendanceTotals(4, 2, 2), totals);
    }

    [Fact]
    public void Records_from_several_schools_are_combined()
    {
        var a = TestData.Code(4, "A", true, false);
        var e = TestData.Code(5, "E", true, true);
        var records = new[]
        {
            AttendanceRecord.Create(100, new AttendancePlacement(1, new DateOnly(2026, 9, 14), 20, null), a, 0, null, TestData.Clerk, TestData.UtcNow),
            AttendanceRecord.Create(100, new AttendancePlacement(2, new DateOnly(2026, 10, 14), 21, null), e, 0, null, TestData.Clerk, TestData.UtcNow),
        };

        Assert.Equal(new AttendanceTotals(2, 1, 1), AttendanceTotals.FromRecords(records));
    }

    [Fact]
    public void No_records_gives_zero()
    {
        Assert.Equal(default, AttendanceTotals.FromRecords([]));
    }

    [Fact]
    public void Summary_is_replaced_not_incremented()
    {
        var summary = StudentAttendanceSummary.Create(100, new SchoolYearLabel(2026), new AttendanceTotals(5, 2, 3), TestData.UtcNow);
        var later = TestData.UtcNow.AddMinutes(5);

        summary.Replace(new AttendanceTotals(4, 2, 2), later);

        Assert.Equal("2026-2027", summary.SchoolYearLabel);
        Assert.Equal(new AttendanceTotals(4, 2, 2), summary.Totals);
        Assert.Equal(later, summary.LastUpdated);
    }

    [Fact]
    public void Summary_rejects_inconsistent_totals()
    {
        Assert.Throws<DomainException>(() =>
            StudentAttendanceSummary.Create(100, new SchoolYearLabel(2026), new AttendanceTotals(5, 1, 1), TestData.UtcNow));
    }
}
