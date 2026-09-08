using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using RenewTheDoc.App.Localization;
using RenewTheDoc.Domain.Documents;

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

    // Unwraps to the Guid so the mapping stays bit-for-bit what it was before typed ids — a
    // different hash would orphan every already-scheduled notification. Still REN-54's known bug.
    private static int ToNotificationId(DocumentId id) => id.Value.GetHashCode() & 0x7FFFFFFF;
}
