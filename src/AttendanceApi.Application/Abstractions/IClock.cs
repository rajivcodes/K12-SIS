namespace AttendanceApi.Application.Abstractions;

/// <summary>The current time. Everything is UTC (Q2): "today" is the current UTC date.</summary>
public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly Today => DateOnly.FromDateTime(UtcNow);
}
