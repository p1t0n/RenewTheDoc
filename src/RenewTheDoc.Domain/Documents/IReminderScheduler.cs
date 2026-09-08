namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Platform notification scheduling seam. Implementations wrap Plugin.LocalNotification (or raw
/// platform interop if the plugin dies — see REN-6 risk list). Mind the iOS 64-pending cap.
/// </summary>
/// <remarks>
/// <para>
/// The port carries a <em>decided</em> <see cref="ReminderInstruction"/>: whether and when a
/// Reminder fires is the aggregate's call, made by <see cref="Document.PlanReminder"/> and
/// orchestrated by the application service. An implementation executes the instruction and does no
/// domain work at all — no planning, no clock read (spec §4.1).
/// </para>
/// <para>
/// No <c>CancellationToken</c> parameters, for the same reason as the repositories: nothing honoured
/// them, and every call is one write behind a button tap (spec §5.6).
/// </para>
/// </remarks>
public interface IReminderScheduler
{
    /// <summary>
    /// Executes the instruction for the Document's single Reminder, rendering the alert from
    /// <paramref name="content"/>. Replaces any Reminder already standing for that id.
    /// </summary>
    Task ScheduleAsync(DocumentId documentId, ReminderInstruction instruction, ReminderContent content);

    Task CancelAsync(DocumentId documentId);

    /// <summary>
    /// Asks the user for notification permission if it has not been granted yet. Answers whether
    /// the app may alert the user once it returns.
    /// </summary>
    Task<bool> EnsurePermissionAsync();
}
