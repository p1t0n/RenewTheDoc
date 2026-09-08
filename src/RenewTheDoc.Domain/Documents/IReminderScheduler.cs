namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Platform notification scheduling seam. Implementations wrap Plugin.LocalNotification (or raw
/// platform interop if the plugin dies — see REN-6 risk list). Mind the iOS 64-pending cap.
/// </summary>
/// <remarks>
/// No <c>CancellationToken</c> parameters, for the same reason as the repositories: nothing honoured
/// them, and every call is one write behind a button tap (spec §5.6).
/// </remarks>
public interface IReminderScheduler
{
    Task ScheduleAsync(Document document);
    Task CancelAsync(DocumentId documentId);

    /// <summary>Asks the user for notification permission if it has not been granted yet.</summary>
    Task EnsurePermissionAsync();
}
