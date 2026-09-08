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
    public async Task ScheduleAsync(Document document, CancellationToken ct = default)
    {
        var plan = document.PlanReminder(DateTime.Now);
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

    public Task CancelAsync(DocumentId documentId, CancellationToken ct = default)
    {
        LocalNotificationCenter.Current.Cancel(ToNotificationId(documentId));
        return Task.CompletedTask;
    }

    public async Task EnsurePermissionAsync(CancellationToken ct = default)
    {
        if (await LocalNotificationCenter.Current.AreNotificationsEnabled() == false)
        {
            await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
    }

    // Unwraps to the Guid so the mapping stays bit-for-bit what it was before typed ids — a
    // different hash would orphan every already-scheduled notification. Still REN-54's known bug.
    private static int ToNotificationId(DocumentId id) => id.Value.GetHashCode() & 0x7FFFFFFF;
}
