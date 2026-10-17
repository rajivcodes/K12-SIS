using AttendanceApi.Application.Admin;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

/// <summary>The skipped-row log (Admin only).</summary>
[ApiController]
[Route("api/v1/admin/submission-issues")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminSubmissionIssuesController : ControllerBase
{
    /// <summary>Rows skipped by saves, newest submission first.</summary>
    /// <param name="schoolId">Only this school.</param>
    /// <param name="from">Attendance dates on or after.</param>
    /// <param name="to">Attendance dates on or before.</param>
    /// <param name="page">1-based page (default 1).</param>
    /// <param name="pageSize">Items per page, 1–500 (default 100).</param>
    /// <param name="query">The issues query.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public Task<SubmissionIssuesPage> Get(
        [FromQuery] int? schoolId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromServices] SubmissionIssuesQuery query,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 100) =>
        query.HandleAsync(schoolId, from, to, page, pageSize, ct);
}
