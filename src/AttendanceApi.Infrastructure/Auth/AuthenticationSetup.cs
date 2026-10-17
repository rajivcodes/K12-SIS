using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceApi.Infrastructure.Auth;

/// <summary>Authorization policy names used by the controllers.</summary>
public static class Policies
{
    public const string AttendanceRead = "Attendance.Read";
    public const string AttendanceWrite = "Attendance.Write";
    public const string Admin = "Admin";
}

public static class AuthenticationSetup
{
    /// <summary>JWT bearer validation of the API's own tokens, plus the role policies (plan §9).</summary>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, IClock>((bearer, jwt, clock) =>
            {
                var options = jwt.Value;
                var skew = TimeSpan.FromSeconds(30);
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(options.SigningKeyBytes()),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = JwtClaims.UserName,
                    RoleClaimType = JwtClaims.Role,
                    ClockSkew = skew,

                    // Expiry is checked against IClock, the same clock that issued the token.
                    LifetimeValidator = (notBefore, expires, _, _) =>
                    {
                        var now = clock.UtcNow;
                        return (notBefore is null || notBefore.Value <= now + skew)
                            && expires is not null && expires.Value > now - skew;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.AttendanceRead, p => p.RequireRole(
                nameof(UserRole.Admin), nameof(UserRole.AttendanceClerk), nameof(UserRole.Viewer)))
            .AddPolicy(Policies.AttendanceWrite, p => p.RequireRole(nameof(UserRole.Admin), nameof(UserRole.AttendanceClerk)))
            .AddPolicy(Policies.Admin, p => p.RequireRole(nameof(UserRole.Admin)));

        return services;
    }
}
