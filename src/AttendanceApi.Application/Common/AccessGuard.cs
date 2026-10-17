using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Calendar;
using AttendanceApi.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Common;

/// <summary>
/// School scoping (plan §11): admins see everything; other users only their assigned schools (AppUserSchool).
/// Also rejects tokens of accounts deactivated after the token was issued.
/// </summary>
public sealed class AccessGuard
{
    private readonly IAttendanceDbContext _db;
    private readonly ICurrentUser _user;

    public AccessGuard(IAttendanceDbContext db, ICurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public async Task EnsureSchoolAccessAsync(int schoolId, CancellationToken cancellationToken)
    {
        await EnsureActiveAsync(cancellationToken);
        if (_user.Role == UserRole.Admin)
        {
            return;
        }

        var assigned = await _db.AppUserSchools
            .AnyAsync(s => s.UserId == _user.UserId && s.SchoolId == schoolId, cancellationToken);
        if (!assigned)
        {
            throw new ForbiddenException($"You do not have access to school {schoolId}.");
        }
    }

    /// <summary>
    /// A non-admin may read a student's attendance for a school year when the student has an enrollment
    /// overlapping that year at one of the user's schools. The data returned still covers all schools.
    /// </summary>
    public async Task EnsureStudentAccessAsync(int studentId, SchoolYearLabel year, CancellationToken cancellationToken)
    {
        await EnsureActiveAsync(cancellationToken);
        if (_user.Role == UserRole.Admin)
        {
            return;
        }

        var schoolIds = await _db.AppUserSchools
            .Where(s => s.UserId == _user.UserId)
            .Select(s => s.SchoolId)
            .ToListAsync(cancellationToken);
        var enrollments = await _db.StudentEnrollments
            .Where(e => e.StudentId == studentId && schoolIds.Contains(e.SchoolId))
            .ToListAsync(cancellationToken);
        var label = year.ToString();
        var yearsBySchool = await _db.SchoolYears
            .Where(y => schoolIds.Contains(y.SchoolId) && y.Label == label)
            .ToDictionaryAsync(y => y.SchoolId, cancellationToken);

        var allowed = enrollments.Exists(e =>
        {
            var (start, end) = SchoolYearRanges.For(e.SchoolId, year, yearsBySchool);
            return e.Overlaps(start, end);
        });
        if (!allowed)
        {
            throw new ForbiddenException($"You do not have access to student {studentId} for school year {year}.");
        }
    }

    private async Task EnsureActiveAsync(CancellationToken cancellationToken)
    {
        var active = await _db.AppUsers.AnyAsync(u => u.Id == _user.UserId && u.IsActive, cancellationToken);
        if (!active)
        {
            throw new AccountInactiveException();
        }
    }
}
