using AttendanceApi.Application.Abstractions;
using AttendanceApi.Application.Common;
using AttendanceApi.Domain.Attendance;
using Microsoft.EntityFrameworkCore;

namespace AttendanceApi.Application.Admin;

public sealed record SubmissionIssueResponse(
    int Id,
    int SubmissionId,
    int SchoolId,
    string SchoolName,
    DateOnly Date,
    string SubmittedBy,
    DateTime SubmittedAt,
    int? StudentId,
    string? Code,
    SubmissionIssueReason Reason,
    string Detail);

public sealed record SubmissionIssuesPage(int Page, int PageSize, int TotalCount, IReadOnlyList<SubmissionIssueResponse> Items);

/// <summary>The skipped-row log for admins (plan decision 9), newest first, filterable by school and date.</summary>
public sealed class SubmissionIssuesQuery
{
    public const int MaxPageSize = 500;

    private readonly IAttendanceDbContext _db;
    private readonly AccessGuard _access;

    public SubmissionIssuesQuery(IAttendanceDbContext db, AccessGuard access)
    {
        _db = db;
        _access = access;
    }

    /// <param name="schoolId">Only this school.</param>
    /// <param name="fromDate">Attendance dates on or after.</param>
    /// <param name="toDate">Attendance dates on or before.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Items per page (1–500).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    public async Task<SubmissionIssuesPage> HandleAsync(int? schoolId, DateOnly? fromDate, DateOnly? toDate, int page, int pageSize, CancellationToken cancellationToken)
    {
        await _access.EnsureActiveUserAsync(cancellationToken);
        if (page < 1 || pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new RequestValidationException($"page must be at least 1 and pageSize between 1 and {MaxPageSize}.");
        }

        if (fromDate > toDate)
        {
            throw new RequestValidationException("from must be on or before to.");
        }

        var query =
            from i in _db.AttendanceSubmissionIssues
            join s in _db.AttendanceSubmissions on i.SubmissionId equals s.Id
            join school in _db.Schools on s.SchoolId equals school.Id
            where (schoolId == null || s.SchoolId == schoolId)
                && (fromDate == null || s.Date >= fromDate)
                && (toDate == null || s.Date <= toDate)
            select new { i, s, school.Name };

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.s.SubmittedAt)
            .ThenBy(x => x.i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SubmissionIssueResponse(
                x.i.Id, x.s.Id, x.s.SchoolId, x.Name, x.s.Date, x.s.SubmittedBy, x.s.SubmittedAt,
                x.i.StudentId, x.i.Code, x.i.Reason, x.i.Detail))
            .ToListAsync(cancellationToken);

        return new SubmissionIssuesPage(page, pageSize, total, items);
    }
}
