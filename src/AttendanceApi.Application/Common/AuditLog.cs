using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using AttendanceApi.Application.Abstractions;
using AttendanceApi.Domain.Common;

namespace AttendanceApi.Application.Common;

/// <summary>Adds an <see cref="AuditEntry"/> to the unit of work, so it commits with the change it describes.</summary>
public sealed class AuditLog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IAttendanceDbContext _db;
    private readonly ICurrentUser _user;
    private readonly IClock _clock;

    public AuditLog(IAttendanceDbContext db, ICurrentUser user, IClock clock)
    {
        _db = db;
        _user = user;
        _clock = clock;
    }

    public void Add(string action, string entityType, int entityId, object? details = null) =>
        _db.AuditEntries.Add(AuditEntry.Record(
            _user.Actor,
            _clock.UtcNow,
            action,
            entityType,
            entityId.ToString(CultureInfo.InvariantCulture),
            details is null ? null : JsonSerializer.Serialize(details, Json)));
}
