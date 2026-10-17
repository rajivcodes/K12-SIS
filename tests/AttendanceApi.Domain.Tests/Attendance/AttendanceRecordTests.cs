using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Tests.Attendance;

public class AttendanceRecordTests
{
    private static readonly DateOnly Date = new(2026, 10, 15);
    private static readonly AttendancePlacement Placement = new(SchoolId: 1, Date, SchoolYearId: 20, TermId: 30);

    [Fact]
    public void Create_copies_the_code_flags_and_audit_fields()
    {
        var code = TestData.Code(5, "E", isAbsent: true, isExcused: true);

        var record = AttendanceRecord.Create(100, Placement, code, 0, "Doctor", TestData.Clerk, TestData.UtcNow);

        Assert.Equal((100, 1, Date, 20, 30), (record.StudentId, record.SchoolId, record.Date, record.SchoolYearId, record.TermId));
        Assert.Equal(5, record.AttendanceCodeId);
        Assert.True(record.IsAbsent);
        Assert.True(record.IsExcused);
        Assert.Equal("Doctor", record.Notes);
        Assert.Equal((7, "clerk.jones", TestData.UtcNow), (record.CreatedByUserId, record.CreatedBy, record.CreatedAt));
        Assert.Null(record.ModifiedAt);
    }

    [Fact]
    public void Flags_are_a_snapshot_taken_at_save_time()
    {
        var code = TestData.Code(4, "A", isAbsent: true, isExcused: false);
        var record = AttendanceRecord.Create(100, Placement, code, 0, null, TestData.Clerk, TestData.UtcNow);

        code.Update("Absent", isAbsent: false, isExcused: false, isActive: true);

        Assert.True(record.IsAbsent);
    }

    [Fact]
    public void Update_overwrites_the_entry_and_records_the_modifier()
    {
        var record = AttendanceRecord.Create(100, Placement, TestData.Code(4, "A", true, false), 0, "x", TestData.Clerk, TestData.UtcNow);
        var admin = new Actor(1, "admin");
        var later = TestData.UtcNow.AddHours(1);

        record.Update(Placement with { SchoolId = 2, TermId = null }, TestData.Code(2, "T", false, false), 15, null, admin, later);

        Assert.Equal((2, (int?)null, 2, false, 15), (record.SchoolId, record.TermId, record.AttendanceCodeId, record.IsAbsent, record.MinutesLate));
        Assert.Null(record.Notes);
        Assert.Equal((1, "admin", later), (record.ModifiedByUserId, record.ModifiedBy, record.ModifiedAt));
        Assert.Equal("clerk.jones", record.CreatedBy);
    }

    [Fact]
    public void Update_cannot_change_the_date()
    {
        var record = AttendanceRecord.Create(100, Placement, TestData.Code(1, "P", false, false), 0, null, TestData.Clerk, TestData.UtcNow);

        Assert.Throws<DomainException>(() => record.Update(
            Placement with { Date = Date.AddDays(1) }, TestData.Code(1, "P", false, false), 0, null, TestData.Clerk, TestData.UtcNow));
    }

    [Fact]
    public void Whitespace_notes_are_stored_as_null()
    {
        var record = AttendanceRecord.Create(100, Placement, TestData.Code(1, "P", false, false), 0, "   ", TestData.Clerk, TestData.UtcNow);

        Assert.Null(record.Notes);
    }

    [Fact]
    public void Guards_reject_values_the_row_validator_should_have_skipped()
    {
        var p = TestData.Code(1, "P", false, false);

        Assert.Throws<DomainException>(() => AttendanceRecord.Create(100, Placement, TestData.Code(10, "X", true, false, isActive: false), 0, null, TestData.Clerk, TestData.UtcNow));
        Assert.Throws<DomainException>(() => AttendanceRecord.Create(100, Placement, p, -1, null, TestData.Clerk, TestData.UtcNow));
        Assert.Throws<DomainException>(() => AttendanceRecord.Create(100, Placement, p, 0, new string('x', 501), TestData.Clerk, TestData.UtcNow));
    }

    [Fact]
    public void Timestamps_must_be_UTC()
    {
        var local = new DateTime(2026, 10, 15, 9, 0, 0, DateTimeKind.Local);

        Assert.Throws<DomainException>(() => AttendanceRecord.Create(100, Placement, TestData.Code(1, "P", false, false), 0, null, TestData.Clerk, local));
    }
}
