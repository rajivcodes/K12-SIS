using AttendanceApi.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Codes;

public sealed record AttendanceCodeResponse(string Code, string Description, bool IsAbsent, bool IsExcused);

/// <summary>The active attendance codes, for pick lists.</summary>
public sealed class AttendanceCodesQuery
{
    private readonly IAttendanceDbContext _db;

    public AttendanceCodesQuery(IAttendanceDbContext db)
    {
        _db = db;
    }

    public Task<List<AttendanceCodeResponse>> HandleAsync(CancellationToken cancellationToken) =>
        _db.AttendanceCodes
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new AttendanceCodeResponse(c.Code, c.Description, c.IsAbsent, c.IsExcused))
            .ToListAsync(cancellationToken);
}
