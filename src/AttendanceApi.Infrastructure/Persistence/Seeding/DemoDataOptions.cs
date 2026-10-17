namespace AttendanceApi.Infrastructure.Persistence.Seeding;

/// <summary>
/// Bound from the "DemoData" section. For local evaluation only: on in <c>appsettings.Development.json</c>,
/// off everywhere else. Demo data is only ever added to a database that has no schools.
/// </summary>
public sealed class DemoDataOptions
{
    public const string SectionName = "DemoData";

    public bool Enabled { get; set; }

    /// <summary>
    /// Optional secret. When set, demo accounts <c>lincoln.clerk</c>, <c>roosevelt.clerk</c> and
    /// <c>district.viewer</c> are created with this password.
    /// </summary>
    public string? UserPassword { get; set; }
}
