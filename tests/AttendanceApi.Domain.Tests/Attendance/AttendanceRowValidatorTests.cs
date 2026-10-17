using AttendanceApi.Domain.Attendance;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Tests.Attendance;

public class AttendanceRowValidatorTests
{
    private const int SchoolId = 1;
    private const int OtherSchoolId = 2;
    private static readonly DateOnly Date = new(2026, 10, 15);

    private static readonly IReadOnlyList<Student> Students =
    [
        TestData.Student(100),
        TestData.Student(101),
        TestData.Student(102),
        TestData.Student(103, isActive: false),
        TestData.Student(104),
        TestData.Student(105),
    ];

    private static readonly IReadOnlyList<StudentEnrollment> Enrollments =
    [
        TestData.Enrollment(100, SchoolId, new DateOnly(2026, 9, 1)),
        TestData.Enrollment(101, SchoolId, new DateOnly(2026, 9, 1)),
        TestData.Enrollment(102, SchoolId, new DateOnly(2026, 9, 1)),
        TestData.Enrollment(103, SchoolId, new DateOnly(2026, 9, 1)),
        TestData.Enrollment(104, OtherSchoolId, new DateOnly(2026, 9, 1)),
        TestData.Enrollment(105, SchoolId, new DateOnly(2026, 9, 1), exit: new DateOnly(2026, 10, 14)),
    ];

    private static AttendanceRowValidationResult Validate(params AttendanceRowInput[] rows) =>
        AttendanceRowValidator.Validate(rows, SchoolId, Date, Students, Enrollments, TestData.StandardCodes());

    private static SkippedAttendanceRow SingleSkip(AttendanceRowInput row)
    {
        var result = Validate(row);
        Assert.Empty(result.Valid);
        return Assert.Single(result.Skipped);
    }

    [Fact]
    public void Valid_rows_are_returned_with_their_resolved_codes()
    {
        var result = Validate(
            new AttendanceRowInput(100, "P", 0, null),
            new AttendanceRowInput(101, "A", 0, "Sick"),
            new AttendanceRowInput(102, "T", 12, null));

        Assert.Empty(result.Skipped);
        Assert.Collection(
            result.Valid,
            r => Assert.Equal((0, 100, "P"), (r.RowIndex, r.StudentId, r.Code.Code)),
            r => Assert.Equal((1, 101, "A", "Sick"), (r.RowIndex, r.StudentId, r.Code.Code, r.Notes)),
            r => Assert.Equal((2, 102, "T", 12), (r.RowIndex, r.StudentId, r.Code.Code, r.MinutesLate)));
    }

    [Fact]
    public void Each_row_resolves_its_own_code_flags_D02_regression()
    {
        // Legacy D-02: an unknown code inherited the previous student's flags. Here the absent row is valid
        // with its own flags and the unknown code is skipped instead of borrowing them.
        var result = Validate(
            new AttendanceRowInput(100, "E", 0, null),
            new AttendanceRowInput(101, "ZZ", 0, null),
            new AttendanceRowInput(102, "P", 0, null));

        Assert.Equal([true, false], result.Valid.Select(r => r.Code.IsAbsent));
        Assert.Equal(101, Assert.Single(result.Skipped).StudentId);
    }

    [Theory]
    [InlineData("p")]
    [InlineData(" P ")]
    [InlineData("oss")]
    public void Codes_match_case_insensitively_after_trimming(string code)
    {
        var result = Validate(new AttendanceRowInput(100, code, 0, null));

        Assert.Equal(code.Trim().ToUpperInvariant(), Assert.Single(result.Valid).Code.Code);
    }

    [Fact]
    public void Unknown_student_is_skipped()
    {
        var skip = SingleSkip(new AttendanceRowInput(999, "P", 0, null));

        Assert.Equal(SubmissionIssueReason.UnknownStudent, skip.Reason);
        Assert.False(skip.StudentExists);
        Assert.Contains("999", skip.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Inactive_student_is_skipped_even_when_enrolled()
    {
        var skip = SingleSkip(new AttendanceRowInput(103, "P", 0, null));

        Assert.Equal(SubmissionIssueReason.StudentInactive, skip.Reason);
        Assert.True(skip.StudentExists);
    }

    [Fact]
    public void Student_enrolled_at_another_school_is_skipped()
    {
        Assert.Equal(SubmissionIssueReason.NotEnrolled, SingleSkip(new AttendanceRowInput(104, "P", 0, null)).Reason);
    }

    [Fact]
    public void Student_whose_enrollment_ended_before_the_date_is_skipped()
    {
        Assert.Equal(SubmissionIssueReason.NotEnrolled, SingleSkip(new AttendanceRowInput(105, "P", 0, null)).Reason);
    }

    [Fact]
    public void Exit_date_is_inclusive()
    {
        var result = AttendanceRowValidator.Validate(
            [new AttendanceRowInput(105, "P", 0, null)],
            SchoolId,
            new DateOnly(2026, 10, 14),
            Students,
            Enrollments,
            TestData.StandardCodes());

        Assert.Single(result.Valid);
    }

    [Theory]
    [InlineData("ZZ")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Unknown_or_missing_code_is_skipped(string? code)
    {
        Assert.Equal(SubmissionIssueReason.UnknownCode, SingleSkip(new AttendanceRowInput(100, code, 0, null)).Reason);
    }

    [Fact]
    public void Inactive_code_is_skipped()
    {
        Assert.Equal(SubmissionIssueReason.InactiveCode, SingleSkip(new AttendanceRowInput(100, "X", 0, null)).Reason);
    }

    [Fact]
    public void Negative_minutes_late_is_skipped()
    {
        Assert.Equal(SubmissionIssueReason.NegativeMinutesLate, SingleSkip(new AttendanceRowInput(100, "T", -1, null)).Reason);
    }

    [Fact]
    public void Notes_over_500_characters_are_skipped_not_truncated()
    {
        var skip = SingleSkip(new AttendanceRowInput(100, "P", 0, new string('x', 501)));

        Assert.Equal(SubmissionIssueReason.NotesTooLong, skip.Reason);
    }

    [Fact]
    public void Notes_of_exactly_500_characters_are_allowed()
    {
        Assert.Single(Validate(new AttendanceRowInput(100, "P", 0, new string('x', 500))).Valid);
    }

    [Fact]
    public void Every_copy_of_a_duplicated_student_is_skipped_and_other_rows_are_kept()
    {
        var result = Validate(
            new AttendanceRowInput(100, "P", 0, null),
            new AttendanceRowInput(101, "A", 0, null),
            new AttendanceRowInput(100, "A", 0, null));

        Assert.Equal(101, Assert.Single(result.Valid).StudentId);
        Assert.Equal([0, 2], result.Skipped.Select(s => s.RowIndex));
        Assert.All(result.Skipped, s => Assert.Equal(SubmissionIssueReason.DuplicateStudentInBatch, s.Reason));
    }

    [Fact]
    public void Duplicate_check_runs_first_and_records_whether_the_student_exists()
    {
        var result = Validate(
            new AttendanceRowInput(999, "P", 0, null),
            new AttendanceRowInput(999, "P", 0, null));

        Assert.All(result.Skipped, s =>
        {
            Assert.Equal(SubmissionIssueReason.DuplicateStudentInBatch, s.Reason);
            Assert.False(s.StudentExists);
        });
    }

    [Fact]
    public void Only_the_first_failing_reason_is_reported()
    {
        // Not enrolled, unknown code and negative minutes: enrollment is checked first.
        var skip = SingleSkip(new AttendanceRowInput(104, "ZZ", -5, null));

        Assert.Equal(SubmissionIssueReason.NotEnrolled, skip.Reason);
    }

    [Fact]
    public void Skipped_rows_keep_their_position_student_and_raw_code()
    {
        var result = Validate(
            new AttendanceRowInput(100, "P", 0, null),
            new AttendanceRowInput(101, "zz", 0, null));

        var skip = Assert.Single(result.Skipped);
        Assert.Equal((1, 101, "zz"), (skip.RowIndex, skip.StudentId, skip.Code));
    }

    [Fact]
    public void Empty_batch_gives_empty_result()
    {
        var result = Validate();

        Assert.Empty(result.Valid);
        Assert.Empty(result.Skipped);
    }
}
