using System.Globalization;
using System.Security.Claims;
using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceApi.Infrastructure.Auth;

/// <summary>Issues HMAC-SHA256 access tokens carrying the user's ID, name and role (plan §5, §9).</summary>
internal sealed class JwtTokenIssuer : IAccessTokenIssuer
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenIssuer(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AccessToken Issue(AppUser user)
    {
        var now = _clock.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtClaims.UserId, user.Id.ToString(CultureInfo.InvariantCulture)),
                new Claim(JwtClaims.UserName, user.UserName),
                new Claim(JwtClaims.DisplayName, user.DisplayName),
                new Claim(JwtClaims.Role, user.Role.ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_options.SigningKeyBytes()),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
