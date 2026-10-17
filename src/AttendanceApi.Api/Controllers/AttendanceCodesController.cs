using AttendanceApi.Application.Codes;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

[ApiController]
[Route("api/v1/attendance-codes")]
[Authorize(Policy = Policies.AttendanceRead)]
public sealed class AttendanceCodesController : ControllerBase
{
    /// <summary>The active attendance codes.</summary>
    /// <param name="query">The codes query.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet]
    public Task<List<AttendanceCodeResponse>> Get([FromServices] AttendanceCodesQuery query, CancellationToken cancellationToken) =>
        query.HandleAsync(cancellationToken);
}
