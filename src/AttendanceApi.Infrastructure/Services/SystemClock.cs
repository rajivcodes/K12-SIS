using AttendanceApi.Application.Abstractions;

namespace AttendanceApi.Infrastructure.Services;

internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
