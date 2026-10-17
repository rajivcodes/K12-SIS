namespace AttendanceApi.Infrastructure.Persistence.Seeding;

/// <summary>The first admin account, created only when no admin exists. Bound from the "SeedAdmin" section.</summary>
public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string? UserName { get; set; }

    /// <summary>Secret: user-secrets or environment only.</summary>
    public string? Password { get; set; }

    public string? DisplayName { get; set; }

    public string? Email { get; set; }
}
