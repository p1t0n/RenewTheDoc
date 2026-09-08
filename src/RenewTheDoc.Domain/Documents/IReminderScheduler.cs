namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Platform notification scheduling seam. Implementations wrap Plugin.LocalNotification (or raw
/// platform interop if the plugin dies — see REN-6 risk list). Mind the iOS 64-pending cap.
/// </summary>
public interface IReminderScheduler
{
    Task ScheduleAsync(Document document, CancellationToken ct = default);
    Task CancelAsync(Guid documentId, CancellationToken ct = default);

    /// <summary>Asks the user for notification permission if it has not been granted yet.</summary>
    Task EnsurePermissionAsync(CancellationToken ct = default);
}
