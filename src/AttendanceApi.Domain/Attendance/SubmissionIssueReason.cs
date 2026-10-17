namespace AttendanceApi.Domain.Attendance;

/// <summary>Why a row in a save batch was skipped (plan §6 step 4).</summary>
public enum SubmissionIssueReason
{
    UnknownStudent = 1,
    StudentInactive = 2,
    NotEnrolled = 3,
    UnknownCode = 4,
    InactiveCode = 5,
    NegativeMinutesLate = 6,
    NotesTooLong = 7,
    DuplicateStudentInBatch = 8,
}
