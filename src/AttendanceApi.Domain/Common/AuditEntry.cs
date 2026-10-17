namespace AttendanceApi.Domain.Common;

/// <summary>
/// Who changed what and when, for writes to tables without their own audit columns: admin changes to
/// settings, calendars and users, demo-data seeding and legacy import runs.
/// </summary>
public sealed class AuditEntry
{
    public const int MaxActionLength = 100;
    public const int MaxEntityTypeLength = 100;
    public const int MaxEntityIdLength = 50;
    public const int MaxDetailsLength = 4000;

    private AuditEntry()
    {
        UserName = string.Empty;
        Action = string.Empty;
        EntityType = string.Empty;
        EntityId = string.Empty;
    }

    public long Id { get; private set; }

    public DateTime OccurredAt { get; private set; }

    public int? UserId { get; private set; }

    public string UserName { get; private set; }

    /// <summary>What happened, e.g. <c>SchoolYearCreated</c>.</summary>
    public string Action { get; private set; }

    public string EntityType { get; private set; }

    public string EntityId { get; private set; }

    /// <summary>The new values or a summary, usually JSON.</summary>
    public string? Details { get; private set; }

    public static AuditEntry Record(Actor actor, DateTime utcNow, string action, string entityType, string entityId, string? details) => new()
    {
        OccurredAt = Guard.Utc(utcNow, nameof(utcNow)),
        UserId = actor.UserId,
        UserName = actor.UserName,
        Action = Guard.Required(action, MaxActionLength, "Action"),
        EntityType = Guard.Required(entityType, MaxEntityTypeLength, "Entity type"),
        EntityId = Guard.Required(entityId, MaxEntityIdLength, "Entity ID"),
        Details = details is { Length: > MaxDetailsLength } ? details[..MaxDetailsLength] : details,
    };
}
