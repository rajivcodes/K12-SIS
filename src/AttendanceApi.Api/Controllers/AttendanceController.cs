using AttendanceApi.Application.Attendance;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/v1/schools/{schoolId:int}/attendance/{date}")]
public sealed class AttendanceController : ControllerBase
{
    /// <summary>
    /// Saves one school-day batch. Bad rows are skipped and listed in <c>skipped</c>; the rest are saved.
    /// A date the school's settings don't allow rejects the whole batch with 422.
    /// </summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="date">The attendance date, <c>yyyy-MM-dd</c>.</param>
    /// <param name="rows">One row per student.</param>
    /// <param name="handler">The save use case.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpPost]
    [Authorize(Policy = Policies.AttendanceWrite)]
    [ProducesResponseType<SaveDailyAttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<SaveDailyAttendanceResponse> Save(
        int schoolId,
        DateOnly date,
        [FromBody] List<AttendanceRowRequest> rows,
        [FromServices] SaveDailyAttendanceHandler handler,
        CancellationToken cancellationToken) =>
        handler.HandleAsync(schoolId, date, rows, cancellationToken);

    /// <summary>
    /// The roster for entry: active students enrolled at the school on the date (optionally one grade),
    /// each with the record already saved for that date, if any.
    /// </summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="date">The attendance date, <c>yyyy-MM-dd</c>.</param>
    /// <param name="grade">Optional grade filter.</param>
    /// <param name="query">The roster use case.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    [Authorize(Policy = Policies.AttendanceRead)]
    [ProducesResponseType<RosterResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<RosterResponse> Roster(
        int schoolId,
        DateOnly date,
        [FromQuery] string? grade,
        [FromServices] RosterQuery query,
        CancellationToken cancellationToken) =>
        query.HandleAsync(schoolId, date, grade, cancellationToken);
}
