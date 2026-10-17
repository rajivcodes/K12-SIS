namespace AttendanceApi.Application.Abstractions;

/// <summary>Tells someone that a save skipped rows (plan decision 9). The v1 implementation does nothing.</summary>
public interface INotificationSender
{
    Task NotifyRowsSkippedAsync(RowsSkippedNotification notification, CancellationToken cancellationToken);
}

public sealed record RowsSkippedNotification(int SubmissionId, int SchoolId, DateOnly Date, string SubmittedBy, int RowsSkipped);
