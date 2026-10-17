using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Attendance;

/// <summary>The values of a legacy attendance row, as stored by the legacy system.</summary>
public sealed record ImportedAttendance(
    int CodeId,
    bool IsAbsent,
    bool IsExcused,
    int MinutesLate,
    string? Notes,
    Actor CreatedBy,
    DateTime CreatedAt,
    Actor? ModifiedBy,
    DateTime? ModifiedAt);
