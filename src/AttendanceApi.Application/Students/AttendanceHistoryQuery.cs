using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Students;

public sealed record AttendanceHistoryItem(
    DateOnly Date,
    int SchoolId,
    string SchoolName,
    string Code,
    string CodeDescription,
    bool IsAbsent,
    bool IsExcused,
    int MinutesLate,
    string? Notes,
    string? Term,
    string RecordedBy,
    DateTime RecordedAt,
    string? ModifiedBy,
    DateTime? ModifiedAt);

public sealed record AttendanceHistoryResponse(
    int StudentId,
    string StudentName,
    string SchoolYear,
    IReadOnlyList<AttendanceHistoryItem> Records);

/// <summary>A student's attendance for one school year, all schools, newest first (plan §5).</summary>
public sealed class AttendanceHistoryQuery
{
    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;
    private readonly IClock _clock;

    public AttendanceHistoryQuery(IAttendanceDbContext db, AccessGuard access, IClock clock)
    {
        _db = db;
        _access = access;
        _clock = clock;
    }

    public async Task<AttendanceHistoryResponse> HandleAsync(int studentId, string? schoolYear, CancellationToken cancellationToken)
    {
        var student = await _db.Students
            .Where(s => s.Id == studentId)
            .Select(s => new { s.FirstName, s.LastName })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException($"Student {studentId} does not exist.");

        var year = await SchoolYearParameter.ResolveAsync(schoolYear, studentId, _db, _clock.Today, cancellationToken);
        await _access.EnsureStudentAccessAsync(studentId, year, cancellationToken);

        var label = year.ToString();
        var records = await (
                from r in _db.AttendanceRecords
                join y in _db.SchoolYears on r.SchoolYearId equals y.Id
                join s in _db.Schools on r.SchoolId equals s.Id
                join c in _db.AttendanceCodes on r.AttendanceCodeId equals c.Id
                join t in _db.Terms on r.TermId equals (int?)t.Id into terms
                from t in terms.DefaultIfEmpty()
                where r.StudentId == studentId && y.Label == label
                orderby r.Date descending
                select new AttendanceHistoryItem(
                    r.Date,
                    r.SchoolId,
                    s.Name,
                    c.Code,
                    c.Description,
                    r.IsAbsent,
                    r.IsExcused,
                    r.MinutesLate,
                    r.Notes,
                    t == null ? null : t.Name,
                    r.CreatedBy,
                    r.CreatedAt,
                    r.ModifiedBy,
                    r.ModifiedAt))
            .ToListAsync(cancellationToken);

        return new AttendanceHistoryResponse(studentId, $"{student.FirstName} {student.LastName}", label, records);
    }
}
