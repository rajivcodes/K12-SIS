using AttendanceApi.Application.Admin;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

/// <summary>Per-school settings and calendar (Admin only).</summary>
[ApiController]
[Route("api/v1/admin/schools/{schoolId:int}")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
public sealed class AdminSchoolsController : ControllerBase
{
    private readonly SchoolSettingsAdmin _settings;
    private readonly SchoolCalendarAdmin _calendar;

    public AdminSchoolsController(SchoolSettingsAdmin settings, SchoolCalendarAdmin calendar)
    {
        _settings = settings;
        _calendar = calendar;
    }

    /// <summary>Chronic-absence method and thresholds, and date rules.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("settings")]
    public Task<SchoolSettingsResponse> GetSettings(int schoolId, CancellationToken ct) => _settings.GetAsync(schoolId, ct);

    /// <summary>Replaces the settings. Omitted <c>maxBackdateDays</c>/<c>maxFutureDays</c> mean unlimited.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="request">The new settings.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPut("settings")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<SchoolSettingsResponse> PutSettings(int schoolId, UpdateSchoolSettingsRequest request, CancellationToken ct) =>
        _settings.UpdateAsync(schoolId, request, ct);

    /// <summary>The school's years with their terms, newest first.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("school-years")]
    public Task<List<SchoolYearResponse>> GetSchoolYears(int schoolId, CancellationToken ct) => _calendar.ListYearsAsync(schoolId, ct);

    /// <summary>Adds a school year (at most one year long, not overlapping another). Add its terms next.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="request">Start and end dates.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPost("school-years")]
    [ProducesResponseType<SchoolYearResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostSchoolYear(int schoolId, SchoolYearRequest request, CancellationToken ct)
    {
        var year = await _calendar.CreateYearAsync(schoolId, request, ct);
        return CreatedAtAction(nameof(GetSchoolYears), new { schoolId }, year);
    }

    /// <summary>Changes a school year's dates. Rejected (422) if recorded attendance would fall outside or its label would change.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="yearId">The school year.</param>
    /// <param name="request">Start and end dates.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPut("school-years/{yearId:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<SchoolYearResponse> PutSchoolYear(int schoolId, int yearId, SchoolYearRequest request, CancellationToken ct) =>
        _calendar.UpdateYearAsync(schoolId, yearId, request, ct);

    /// <summary>Adds a term inside the year. Existing records are re-tagged with their term.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="yearId">The school year.</param>
    /// <param name="request">Name, start and end dates.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPost("school-years/{yearId:int}/terms")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<SchoolYearResponse> PostTerm(int schoolId, int yearId, TermRequest request, CancellationToken ct) =>
        _calendar.AddTermAsync(schoolId, yearId, request, ct);

    /// <summary>Renames or moves a term. Existing records are re-tagged with their term.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="yearId">The school year.</param>
    /// <param name="termId">The term.</param>
    /// <param name="request">Name, start and end dates.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPut("school-years/{yearId:int}/terms/{termId:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<SchoolYearResponse> PutTerm(int schoolId, int yearId, int termId, TermRequest request, CancellationToken ct) =>
        _calendar.UpdateTermAsync(schoolId, yearId, termId, request, ct);

    /// <summary>Removes a term (e.g. the auto-created whole-year term). Its records lose or change their term tag.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="yearId">The school year.</param>
    /// <param name="termId">The term.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpDelete("school-years/{yearId:int}/terms/{termId:int}")]
    public Task<SchoolYearResponse> DeleteTerm(int schoolId, int yearId, int termId, CancellationToken ct) =>
        _calendar.DeleteTermAsync(schoolId, yearId, termId, ct);

    /// <summary>Holidays and closures, optionally within a date range.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="from">On or after.</param>
    /// <param name="to">On or before.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("non-instructional-days")]
    public Task<List<NonInstructionalDayResponse>> GetNonInstructionalDays(int schoolId, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        _calendar.ListNonInstructionalDaysAsync(schoolId, from, to, ct);

    /// <summary>Adds a holiday or closure (excluded from enrolled days in the rate method).</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="request">Date and reason.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPost("non-instructional-days")]
    [ProducesResponseType<NonInstructionalDayResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PostNonInstructionalDay(int schoolId, NonInstructionalDayRequest request, CancellationToken ct)
    {
        var day = await _calendar.AddNonInstructionalDayAsync(schoolId, request, ct);
        return CreatedAtAction(nameof(GetNonInstructionalDays), new { schoolId }, day);
    }

    /// <summary>Removes a holiday or closure.</summary>
    /// <param name="schoolId">The school.</param>
    /// <param name="dayId">The non-instructional day.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpDelete("non-instructional-days/{dayId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteNonInstructionalDay(int schoolId, int dayId, CancellationToken ct)
    {
        await _calendar.DeleteNonInstructionalDayAsync(schoolId, dayId, ct);
        return NoContent();
    }
}
