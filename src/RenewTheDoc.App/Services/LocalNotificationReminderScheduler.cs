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
public sealed class LocalNotificationReminderScheduler : IReminderScheduler
{
    public async Task ScheduleAsync(
        DocumentId documentId, ReminderInstruction instruction, ReminderContent content)
    {
        // Executing a decided instruction, not deciding one: None means the aggregate already said
        // there is nothing to alert about, so the platform is never touched.
        if (instruction is ReminderInstruction.None) return;

        var request = new NotificationRequest
        {
            NotificationId = ToNotificationId(documentId),
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

    public Task CancelAsync(DocumentId documentId)
    {
        LocalNotificationCenter.Current.Cancel(ToNotificationId(documentId));
        return Task.CompletedTask;
    }

    public async Task<bool> EnsurePermissionAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled()) return true;

        return await LocalNotificationCenter.Current.RequestNotificationPermission();
    }

    // REN-54's known bug, now extracted so a test can pin it: two Documents can fold to the same
    // number and then one's cancel kills the other's Reminder.
    private static int ToNotificationId(DocumentId id) => DerivedNotificationNumber.For(id);
}
