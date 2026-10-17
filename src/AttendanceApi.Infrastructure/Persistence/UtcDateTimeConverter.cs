using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AttendanceApi.Infrastructure.Persistence;

/// <summary>Stores DateTime values unchanged and marks values read from the database as UTC.</summary>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
