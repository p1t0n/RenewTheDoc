using Plugin.LocalNotification;
using Plugin.LocalNotification.Core.Models;
using RenewTheDoc.App.Localization;
using RenewTheDoc.Core;

namespace RenewTheDoc.App.Services;

/// <summary>
/// IReminderScheduler seam over Plugin.LocalNotification (bus-factor-1 risk — keep all plugin
/// usage inside this class). iOS caps pending local notifications at 64; with one Reminder per
/// document that allows 64 documents — queue refreshing is fogged until it matters.
/// </summary>
public sealed class LocalNotificationReminderScheduler : IReminderScheduler
{
    public async Task ScheduleAsync(Document document, CancellationToken ct = default)
    {
        var plan = ReminderPlanner.Plan(document, DateTime.Now);
        if (plan is ReminderInstruction.None) return;

        var request = new NotificationRequest
        {
            NotificationId = ToNotificationId(document.Id),
            Title = L.T("NotificationTitle"),
            Description = L.F("NotificationText", document.Name, document.ExpiryDate.ToString("d")),
        };

        if (plan is ReminderInstruction.At at)
        {
            request.Schedule = new NotificationRequestSchedule
            {
                NotifyTime = at.LocalTime,
            };
        }

        await LocalNotificationCenter.Current.Show(request);
    }

    public Task CancelAsync(Guid documentId, CancellationToken ct = default)
    {
        LocalNotificationCenter.Current.Cancel(ToNotificationId(documentId));
        return Task.CompletedTask;
    }

    public static async Task EnsurePermissionAsync()
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
        {
            await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
    }

    private static int ToNotificationId(Guid id) => id.GetHashCode() & 0x7FFFFFFF;
}
