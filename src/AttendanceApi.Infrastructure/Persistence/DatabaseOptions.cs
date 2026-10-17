namespace AttendanceApi.Infrastructure.Persistence;

/// <summary>Bound from the "Database" section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply pending EF Core migrations at startup. On for local Development runs; production deployments
    /// apply migrations as a separate step.
    /// </summary>
    public bool ApplyMigrationsOnStartup { get; set; }
}
