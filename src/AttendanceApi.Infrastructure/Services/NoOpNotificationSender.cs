using AttendanceApi.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AttendanceApi.Infrastructure.Services;

/// <summary>v1 placeholder: email transport is not decided yet (plan decision 9). Only logs.</summary>
internal sealed partial class NoOpNotificationSender : INotificationSender
{
    private readonly ILogger<NoOpNotificationSender> _logger;

    public NoOpNotificationSender(ILogger<NoOpNotificationSender> logger)
    {
        _logger = logger;
    }

    public Task NotifyRowsSkippedAsync(RowsSkippedNotification notification, CancellationToken cancellationToken)
    {
        LogSkipped(notification.SubmissionId, notification.RowsSkipped, notification.SchoolId, notification.Date);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Submission {SubmissionId} skipped {RowsSkipped} row(s) for school {SchoolId} on {Date}; no notification sent (no-op sender)")]
    private partial void LogSkipped(int submissionId, int rowsSkipped, int schoolId, DateOnly date);
}
