using System.Globalization;
using AttendanceApi.Domain.Students;

namespace AttendanceApi.Domain.Attendance;

/// <summary>One row of a save batch, as received.</summary>
public sealed record AttendanceRowInput(int StudentId, string? Code, int MinutesLate, string? Notes);

/// <summary>A row that passed every check, with its code resolved.</summary>
public sealed record ValidAttendanceRow(int RowIndex, int StudentId, AttendanceCode Code, int MinutesLate, string? Notes);

/// <summary>
/// A row that was skipped, with the first reason it failed. <see cref="StudentExists"/> is false when the
/// student ID doesn't match any student, so the issue log doesn't reference a missing student.
/// </summary>
public sealed record SkippedAttendanceRow(int RowIndex, int StudentId, bool StudentExists, string? Code, SubmissionIssueReason Reason, string Detail);

public sealed record AttendanceRowValidationResult(
    IReadOnlyList<ValidAttendanceRow> Valid,
    IReadOnlyList<SkippedAttendanceRow> Skipped);

/// <summary>
/// Checks each row of a one-school, one-day batch independently, so a bad row is skipped and the rest are
/// saved (plan decision 9). Every row is looked up on its own; nothing carries over between rows (D-01, D-02).
/// </summary>
/// <remarks>
/// Checks run in this order and a row reports only the first one it fails: duplicate student in the batch
/// (every copy is skipped), unknown student, inactive student, not enrolled at the school on the date,
/// unknown or missing code, inactive code, negative minutes late, notes too long.
/// </remarks>
public static class AttendanceRowValidator
{
    /// <param name="rows">The batch rows in request order.</param>
    /// <param name="schoolId">The school the batch is for.</param>
    /// <param name="date">The attendance date.</param>
    /// <param name="students">Students referenced by the batch (unknown IDs are simply absent).</param>
    /// <param name="enrollments">Enrollments of those students (any school).</param>
    /// <param name="codes">All attendance codes, active and inactive.</param>
    public static AttendanceRowValidationResult Validate(
        IReadOnlyList<AttendanceRowInput> rows,
        int schoolId,
        DateOnly date,
        IEnumerable<Student> students,
        IEnumerable<StudentEnrollment> enrollments,
        IEnumerable<AttendanceCode> codes)
    {
        var studentsById = students.ToDictionary(s => s.Id);
        var enrolledStudentIds = enrollments
            .Where(e => e.SchoolId == schoolId && e.IsEnrolledOn(date))
            .Select(e => e.StudentId)
            .ToHashSet();
        var codesByValue = codes.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var duplicateStudentIds = rows
            .GroupBy(r => r.StudentId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet();

        var valid = new List<ValidAttendanceRow>(rows.Count);
        var skipped = new List<SkippedAttendanceRow>();

        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var failure = Check(row);
            if (failure is { } f)
            {
                skipped.Add(new SkippedAttendanceRow(index, row.StudentId, studentsById.ContainsKey(row.StudentId), row.Code, f.Reason, f.Detail));
            }
            else
            {
                var code = codesByValue[row.Code!.Trim()];
                valid.Add(new ValidAttendanceRow(index, row.StudentId, code, row.MinutesLate, row.Notes));
            }
        }

        return new AttendanceRowValidationResult(valid, skipped);

        (SubmissionIssueReason Reason, string Detail)? Check(AttendanceRowInput row)
        {
            var id = row.StudentId.ToString(CultureInfo.InvariantCulture);

            if (duplicateStudentIds.Contains(row.StudentId))
            {
                return (SubmissionIssueReason.DuplicateStudentInBatch, $"Student {id} appears more than once in the batch; none of their rows were saved.");
            }

            if (!studentsById.TryGetValue(row.StudentId, out var student))
            {
                return (SubmissionIssueReason.UnknownStudent, $"Student {id} does not exist.");
            }

            if (!student.IsActive)
            {
                return (SubmissionIssueReason.StudentInactive, $"Student {id} is inactive.");
            }

            if (!enrolledStudentIds.Contains(row.StudentId))
            {
                return (SubmissionIssueReason.NotEnrolled, $"Student {id} is not enrolled at this school on {date:yyyy-MM-dd}.");
            }

            if (string.IsNullOrWhiteSpace(row.Code))
            {
                return (SubmissionIssueReason.UnknownCode, "No attendance code was given.");
            }

            if (!codesByValue.TryGetValue(row.Code.Trim(), out var code))
            {
                return (SubmissionIssueReason.UnknownCode, $"Attendance code '{row.Code.Trim()}' does not exist.");
            }

            if (!code.IsActive)
            {
                return (SubmissionIssueReason.InactiveCode, $"Attendance code '{code.Code}' is inactive.");
            }

            if (row.MinutesLate < 0)
            {
                return (SubmissionIssueReason.NegativeMinutesLate, $"Minutes late ({row.MinutesLate}) cannot be negative.");
            }

            if (row.Notes?.Length > AttendanceRecord.MaxNotesLength)
            {
                return (SubmissionIssueReason.NotesTooLong, $"Notes are {row.Notes.Length} characters; the limit is {AttendanceRecord.MaxNotesLength}.");
            }

            return null;
        }
    }
}
