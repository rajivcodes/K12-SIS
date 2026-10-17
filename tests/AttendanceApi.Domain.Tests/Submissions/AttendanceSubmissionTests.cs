using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Common;
using AttendanceApi.Domain.Submissions;

namespace AttendanceApi.Domain.Tests.Submissions;

public class AttendanceSubmissionTests
{
    private static readonly DateOnly Date = new(2026, 10, 15);

    [Fact]
    public void Records_counts_submitter_and_one_issue_per_skipped_row()
    {
        SkippedAttendanceRow[] skipped =
        [
            new(1, 999, false, "P", SubmissionIssueReason.UnknownStudent, "Student 999 does not exist."),
            new(3, 101, true, "zz", SubmissionIssueReason.UnknownCode, "Attendance code 'zz' does not exist."),
        ];

        var submission = AttendanceSubmission.Record(1, Date, TestData.Clerk, TestData.UtcNow, rowsReceived: 5, rowsSaved: 3, skipped);

        Assert.Equal((1, Date, 7, "clerk.jones", TestData.UtcNow), (submission.SchoolId, submission.Date, submission.SubmittedByUserId!.Value, submission.SubmittedBy, submission.SubmittedAt));
        Assert.Equal((5, 3, 2), (submission.RowsReceived, submission.RowsSaved, submission.RowsSkipped));
        Assert.Collection(
            submission.Issues,
            i => Assert.Equal(((int?)null, "P", SubmissionIssueReason.UnknownStudent), (i.StudentId, i.Code, i.Reason)),
            i => Assert.Equal(((int?)101, "zz", SubmissionIssueReason.UnknownCode), (i.StudentId, i.Code, i.Reason)));
    }

    [Fact]
    public void Issue_keeps_no_reference_to_a_student_that_does_not_exist()
    {
        SkippedAttendanceRow[] skipped = [new(0, 999, false, "P", SubmissionIssueReason.DuplicateStudentInBatch, "Duplicate.")];

        var submission = AttendanceSubmission.Record(1, Date, TestData.Clerk, TestData.UtcNow, 1, 0, skipped);

        Assert.Null(Assert.Single(submission.Issues).StudentId);
    }

    [Fact]
    public void Long_or_blank_raw_codes_are_stored_safely()
    {
        SkippedAttendanceRow[] skipped =
        [
            new(0, 100, true, new string('Q', 50), SubmissionIssueReason.UnknownCode, "Unknown."),
            new(1, 101, true, "  ", SubmissionIssueReason.UnknownCode, "No code."),
        ];

        var submission = AttendanceSubmission.Record(1, Date, TestData.Clerk, TestData.UtcNow, 2, 0, skipped);

        Assert.Collection(
            submission.Issues,
            i => Assert.Equal(AttendanceSubmissionIssue.MaxCodeLength, i.Code!.Length),
            i => Assert.Null(i.Code));
    }

    [Fact]
    public void Row_counts_must_add_up()
    {
        Assert.Throws<DomainException>(() => AttendanceSubmission.Record(1, Date, TestData.Clerk, TestData.UtcNow, 3, 1, []));
    }
}
