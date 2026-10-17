using AttendanceApi.Domain.Attendance;

namespace AttendanceApi.Domain.Submissions;

/// <summary>A skipped row, kept for admins to review (plan decision 9).</summary>
public sealed class AttendanceSubmissionIssue
{
    /// <summary>Raw codes longer than this are cut short for storage; the detail keeps the reason.</summary>
    public const int MaxCodeLength = 20;
    public const int MaxDetailLength = 500;

    private AttendanceSubmissionIssue()
    {
        Detail = string.Empty;
    }

    public int Id { get; private set; }

    public int SubmissionId { get; private set; }

    /// <summary>The student, or null when the requested student ID does not exist (the ID is in the detail).</summary>
    public int? StudentId { get; private set; }

    /// <summary>The code as received, if any.</summary>
    public string? Code { get; private set; }

    public SubmissionIssueReason Reason { get; private set; }

    public string Detail { get; private set; }

    internal static AttendanceSubmissionIssue FromSkippedRow(SkippedAttendanceRow row) => new()
    {
        StudentId = row.StudentExists ? row.StudentId : null,
        Code = Truncate(string.IsNullOrWhiteSpace(row.Code) ? null : row.Code.Trim(), MaxCodeLength),
        Reason = row.Reason,
        Detail = Truncate(row.Detail, MaxDetailLength)!,
    };

    private static string? Truncate(string? value, int maxLength) =>
        value is not null && value.Length > maxLength ? value[..maxLength] : value;
}
