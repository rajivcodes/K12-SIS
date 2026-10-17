using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Attendance;

public sealed record RosterRecord(int SchoolId, string Code, int MinutesLate, string? Notes, DateTime RecordedAt, DateTime? ModifiedAt);

public sealed record RosterEntry(int StudentId, string FirstName, string LastName, string Grade, RosterRecord? Record);

public sealed record RosterResponse(int SchoolId, DateOnly Date, string? Grade, IReadOnlyList<RosterEntry> Students);

/// <summary>
/// The roster for attendance entry (Q5): active students enrolled at the school on the date, optionally for
/// one grade, each with the record already saved for that date (if any). Replaces sp_GetStudentsForAttendance.
/// </summary>
public sealed class RosterQuery
{
    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;

    public RosterQuery(IAttendanceDbContext db, AccessGuard access)
    {
        _db = db;
        _access = access;
    }

    public async Task<RosterResponse> HandleAsync(int schoolId, DateOnly date, string? grade, CancellationToken cancellationToken)
    {
        await _access.EnsureSchoolAccessAsync(schoolId, cancellationToken);
        if (!await _db.Schools.AnyAsync(s => s.Id == schoolId, cancellationToken))
        {
            throw new NotFoundException($"School {schoolId} does not exist.");
        }

        var gradeFilter = string.IsNullOrWhiteSpace(grade) ? null : grade.Trim();
        var students = await (
                from s in _db.Students
                where s.IsActive
                    && (gradeFilter == null || s.Grade == gradeFilter)
                    && _db.StudentEnrollments.Any(e => e.StudentId == s.Id && e.SchoolId == schoolId
                        && e.EntryDate <= date && (e.ExitDate == null || e.ExitDate >= date))
                join r in _db.AttendanceRecords.Where(r => r.Date == date) on s.Id equals r.StudentId into records
                from r in records.DefaultIfEmpty()
                join c in _db.AttendanceCodes on r.AttendanceCodeId equals c.Id into codes
                from c in codes.DefaultIfEmpty()
                orderby s.LastName, s.FirstName, s.Id
                select new RosterEntry(
                    s.Id,
                    s.FirstName,
                    s.LastName,
                    s.Grade,
                    r == null ? null : new RosterRecord(r.SchoolId, c!.Code, r.MinutesLate, r.Notes, r.CreatedAt, r.ModifiedAt)))
            .ToListAsync(cancellationToken);

        return new RosterResponse(schoolId, date, gradeFilter, students);
    }
}
