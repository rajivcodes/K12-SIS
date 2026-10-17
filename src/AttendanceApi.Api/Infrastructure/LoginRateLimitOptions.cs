namespace AttendanceApi.Api.Infrastructure;

/// <summary>Bound from "Auth:LoginRateLimit". Login attempts allowed per client IP per window.</summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "Auth:LoginRateLimit";
    public const string PolicyName = "login";

    public int PermitLimit { get; set; } = 10;

    public int WindowSeconds { get; set; } = 60;
}
