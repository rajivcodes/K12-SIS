using AttendanceApi.Domain.Attendance;

namespace AttendanceApi.Infrastructure.Persistence.Seeding;

/// <summary>The standard K-12 code set from plan §4. Admins can change the flags later.</summary>
public static class StandardAttendanceCodes
{
    public static IReadOnlyList<AttendanceCode> Create() =>
    [
        AttendanceCode.Create("P", "Present", isAbsent: false, isExcused: false),
        AttendanceCode.Create("T", "Tardy (unexcused)", isAbsent: false, isExcused: false),
        AttendanceCode.Create("TE", "Tardy (excused)", isAbsent: false, isExcused: true),
        AttendanceCode.Create("A", "Absent, unexcused", isAbsent: true, isExcused: false),
        AttendanceCode.Create("E", "Absent, excused", isAbsent: true, isExcused: true),
        AttendanceCode.Create("M", "Absent, medical (excused)", isAbsent: true, isExcused: true),
        AttendanceCode.Create("OSS", "Out-of-school suspension", isAbsent: true, isExcused: false),
        AttendanceCode.Create("ISS", "In-school suspension (counts as present)", isAbsent: false, isExcused: false),
        AttendanceCode.Create("FT", "Field trip / school activity (counts as present)", isAbsent: false, isExcused: false),
    ];
}
