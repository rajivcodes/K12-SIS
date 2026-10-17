namespace AttendanceApi.Domain.Attendance;

public enum AttendanceDateRejectionReason
{
    FutureDateNotAllowed = 1,
    TooFarInFuture = 2,
    TooFarInPast = 3,
    WeekendNotAllowed = 4,
    OutsideTerm = 5,
}

/// <summary>Why a school's settings don't allow attendance for a date. The API returns it as a 422.</summary>
public sealed record AttendanceDateRejection(AttendanceDateRejectionReason Reason, string Message);
