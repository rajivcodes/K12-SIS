using AttendanceApi.Application.Admin;
using AttendanceApi.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceApi.Api.Controllers;

/// <summary>User accounts (Admin only).</summary>
[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Policy = Policies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class AdminUsersController : ControllerBase
{
    private readonly UsersAdmin _users;

    public AdminUsersController(UsersAdmin users)
    {
        _users = users;
    }

    /// <summary>All users with their roles and schools.</summary>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet]
    public Task<List<UserResponse>> Get(CancellationToken ct) => _users.ListAsync(ct);

    /// <summary>One user.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpGet("{userId:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<UserResponse> GetOne(int userId, CancellationToken ct) => _users.GetAsync(userId, ct);

    /// <summary>Creates a user. Passwords must be at least 12 characters.</summary>
    /// <param name="request">The new user.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPost]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(CreateUserRequest request, CancellationToken ct)
    {
        var user = await _users.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetOne), new { userId = user.Id }, user);
    }

    /// <summary>Updates profile, role, active flag and schools; <c>newPassword</c> resets the password.</summary>
    /// <param name="userId">The user.</param>
    /// <param name="request">The new values.</param>
    /// <param name="ct">Request cancellation.</param>
    [HttpPut("{userId:int}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public Task<UserResponse> Put(int userId, UpdateUserRequest request, CancellationToken ct) => _users.UpdateAsync(userId, request, ct);
}
