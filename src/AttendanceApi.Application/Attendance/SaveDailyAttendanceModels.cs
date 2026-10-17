using AttendanceApi.Domain.Attendance;

namespace AttendanceApi.Application.Attendance;

/// <summary>One row of a save request.</summary>
public sealed record AttendanceRowRequest(int StudentId, string? Code, int? MinutesLate, string? Notes);

public sealed record SkippedRowResponse(int RowIndex, int StudentId, string? Code, SubmissionIssueReason Reason, string Detail);

public sealed record SaveDailyAttendanceResponse(
    int SubmissionId,
    int SchoolId,
    DateOnly Date,
    string SchoolYear,
    int RowsReceived,
    int RowsSaved,
    int Inserted,
    int Updated,
    IReadOnlyList<SkippedRowResponse> Skipped,
    IReadOnlyList<int> ChronicAlertsRaisedForStudentIds);
