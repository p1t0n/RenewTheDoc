using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using RenewTheDoc.App.Localization;
using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence.Notifications;

namespace RenewTheDoc.App.Services;

/// <summary>
/// IReminderScheduler seam over Plugin.LocalNotification (bus-factor-1 risk — keep all plugin
/// usage inside this class). iOS caps pending local notifications at 64; with one Reminder per
/// document that allows 64 documents — queue refreshing is fogged until it matters.
/// </summary>
/// <remarks>
/// The plugin identifies a notification by an int, and which int a Document owns is this adapter's
/// business — it just no longer computes it. <see cref="SqliteNotificationNumbers"/> hands out one
/// per Document and remembers it, so two Documents can no longer fold onto the same number and
/// cancel each other's Reminder (REN-54).
/// </remarks>
public sealed class LocalNotificationReminderScheduler : IReminderScheduler
{
    private readonly SqliteNotificationNumbers _numbers;

    public LocalNotificationReminderScheduler(SqliteNotificationNumbers numbers) => _numbers = numbers;

    public async Task ScheduleAsync(
        DocumentId documentId, ReminderInstruction instruction, ReminderContent content)
    {
        // Executing a decided instruction, not deciding one: None means the aggregate already said
        // there is nothing to alert about, so the platform is never touched.
        if (instruction is ReminderInstruction.None) return;

        var number = await _numbers.ForAsync(documentId);
        ClearSupersededNotification(number);

        var request = new NotificationRequest
        {
            NotificationId = number.Value,
            Title = L.T("NotificationTitle"),
            Description = L.F("NotificationText", content.DocumentName, content.ExpiryDate.ToString("d")),
        };

        if (instruction is ReminderInstruction.At at)
        {
            request.Schedule = new NotificationRequestSchedule
            {
                NotifyTime = at.LocalTime,
            };
        }

        await LocalNotificationCenter.Current.Show(request);
    }

    public async Task CancelAsync(DocumentId documentId)
    {
        // Asking for the number of a Document that is about to be deleted assigns it one, which
        // leaves a row behind. That is the point: the row is what stops the number being handed to
        // another Document, and this is also the path that clears a pre-REN-54 notification.
        var number = await _numbers.ForAsync(documentId);
        LocalNotificationCenter.Current.Cancel(number.Value);
        ClearSupersededNotification(number);
    }

    public async Task<bool> EnsurePermissionAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return true;

        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    /// <summary>
    /// Clears the notification an older build left standing under the derived number, the first time
    /// this Document is seen after the upgrade. Without it an existing install alerts twice for that
    /// Document, and keeps alerting after it is deleted — the derived number is no longer known to
    /// anything that cancels.
    /// </summary>
    private static void ClearSupersededNotification(NotificationNumber number)
    {
        if (number.Superseded is { } derived) LocalNotificationCenter.Current.Cancel(derived);
    }
}
