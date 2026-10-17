using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Domain.Submissions;

/// <summary>One save of a school-day batch, with the rows it skipped (replaces the legacy submission log).</summary>
public sealed class AttendanceSubmission
{
    private readonly List<AttendanceSubmissionIssue> _issues = [];

    private AttendanceSubmission()
    {
        SubmittedBy = string.Empty;
    }

    private AttendanceSubmission(int schoolId, DateOnly date, Actor actor, DateTime utcNow)
    {
        SchoolId = schoolId;
        Date = date;
        SubmittedByUserId = actor.UserId;
        SubmittedBy = actor.UserName;
        SubmittedAt = Guard.Utc(utcNow, nameof(utcNow));
    }

    public int Id { get; private set; }

    public int SchoolId { get; private set; }

    public DateOnly Date { get; private set; }

    public int? SubmittedByUserId { get; private set; }

    public string SubmittedBy { get; private set; }

    public DateTime SubmittedAt { get; private set; }

    public int RowsReceived { get; private set; }

    public int RowsSaved { get; private set; }

    public int RowsSkipped { get; private set; }

    public IReadOnlyCollection<AttendanceSubmissionIssue> Issues => _issues.AsReadOnly();

    public static AttendanceSubmission Record(
        int schoolId,
        DateOnly date,
        Actor actor,
        DateTime utcNow,
        int rowsReceived,
        int rowsSaved,
        IReadOnlyCollection<SkippedAttendanceRow> skipped)
    {
        if (rowsSaved < 0 || rowsReceived != rowsSaved + skipped.Count)
        {
            throw new DomainException("Rows received must equal rows saved plus rows skipped.");
        }

        var submission = new AttendanceSubmission(schoolId, date, actor, utcNow)
        {
            RowsReceived = rowsReceived,
            RowsSaved = rowsSaved,
            RowsSkipped = skipped.Count,
        };
        submission._issues.AddRange(skipped.Select(AttendanceSubmissionIssue.FromSkippedRow));
        return submission;
    }
}
