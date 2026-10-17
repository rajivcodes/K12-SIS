using AttendanceApi.Application.Abstractions;

namespace AttendanceApi.Api.IntegrationTests.Fixtures;

/// <summary>A settable clock so "today" is fixed in tests.</summary>
public sealed class TestClock : IClock
{
    /// <summary>Thursday 15 October 2026, 12:00 UTC.</summary>
    public static readonly DateTime Default = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = Default;
}
