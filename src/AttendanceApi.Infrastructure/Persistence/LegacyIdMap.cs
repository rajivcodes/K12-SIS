namespace AttendanceApi.Infrastructure.Persistence;

/// <summary>
/// Which new row a legacy row was imported into. Lets the legacy import run again without duplicating data,
/// and without legacy columns leaking into the domain model.
/// </summary>
public sealed class LegacyIdMap
{
    public const int MaxEntityTypeLength = 50;
    public const int MaxLegacyIdLength = 100;

    public LegacyIdMap(string entityType, string legacyId, int newId)
    {
        EntityType = entityType;
        LegacyId = legacyId;
        NewId = newId;
    }

    /// <summary>Legacy table, e.g. <c>Students</c>.</summary>
    public string EntityType { get; private set; }

    /// <summary>The legacy key, as text.</summary>
    public string LegacyId { get; private set; }

    public int NewId { get; set; }
}
