using AttendanceApi.Application.Students;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/v1/students/{studentId:int}")]
[Authorize(Policy = Policies.AttendanceRead)]
public sealed class StudentsController : ControllerBase
{
    /// <summary>The student's attendance for a school year across all schools, newest first.</summary>
    /// <param name="studentId">The student.</param>
    /// <param name="schoolYear"><c>YYYY-YYYY</c>; defaults to the current school year.</param>
    /// <param name="query">The history use case.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("attendance")]
    [ProducesResponseType<AttendanceHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<AttendanceHistoryResponse> History(
        int studentId,
        [FromQuery] string? schoolYear,
        [FromServices] AttendanceHistoryQuery query,
        CancellationToken cancellationToken) =>
        query.HandleAsync(studentId, schoolYear, cancellationToken);

    /// <summary>
    /// Chronic-absence status for a school year: totals, the count result, the rate result and the overall
    /// status decided by the current school's method.
    /// </summary>
    /// <param name="studentId">The student.</param>
    /// <param name="schoolYear"><c>YYYY-YYYY</c>; defaults to the current school year.</param>
    /// <param name="query">The chronic-status use case.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("chronic-absenteeism")]
    [ProducesResponseType<ChronicStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ChronicStatusResponse> ChronicAbsenteeism(
        int studentId,
        [FromQuery] string? schoolYear,
        [FromServices] ChronicStatusQuery query,
        CancellationToken cancellationToken) =>
        query.HandleAsync(studentId, schoolYear, cancellationToken);
}
