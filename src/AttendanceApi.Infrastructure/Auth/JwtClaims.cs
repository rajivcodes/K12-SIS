namespace AttendanceApi.Infrastructure.Auth;

/// <summary>Claim names in the API's tokens. Inbound claim mapping is off, so these arrive unchanged.</summary>
public static class JwtClaims
{
    public const string UserId = "sub";
    public const string UserName = "name";
    public const string DisplayName = "display_name";
    public const string Role = "role";
}
