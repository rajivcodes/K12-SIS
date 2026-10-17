namespace AttendanceApi.Domain.Attendance;

/// <summary>Where a record sits: the school, date, and the school year and term that contain the date.</summary>
public sealed record AttendancePlacement(int SchoolId, DateOnly Date, int SchoolYearId, int? TermId);
