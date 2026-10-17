using System.Globalization;
using System.Security.Claims;
using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using Microsoft.AspNetCore.Http;

namespace AttendanceApi.Infrastructure.Auth;

/// <summary>The caller, read from the validated JWT claims of the current request.</summary>
internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public int UserId => int.Parse(Claim(JwtClaims.UserId), CultureInfo.InvariantCulture);

    public string UserName => Claim(JwtClaims.UserName);

    public UserRole Role => Enum.Parse<UserRole>(Claim(JwtClaims.Role));

    private string Claim(string type) =>
        _accessor.HttpContext?.User.FindFirstValue(type)
        ?? throw new InvalidOperationException($"The request has no authenticated user (missing '{type}' claim).");
}
