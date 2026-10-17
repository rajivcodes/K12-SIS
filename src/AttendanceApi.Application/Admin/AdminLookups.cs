using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Admin;

internal static class AdminLookups
{
    public static async Task EnsureSchoolExistsAsync(IAttendanceDbContext db, int schoolId, CancellationToken cancellationToken)
    {
        if (!await db.Schools.AnyAsync(s => s.Id == schoolId, cancellationToken))
        {
            throw new NotFoundException($"School {schoolId} does not exist.");
        }
    }
}
