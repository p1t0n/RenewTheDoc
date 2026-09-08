namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// What the scheduler should do for a document's single Reminder. Named for the directive crossing
/// the port, not for the Reminder the user experiences — see docs/architecture/ddd-refactor.md §3.2.
/// Produced by <see cref="Document.PlanReminder"/>; nothing reminder-shaped is ever stored.
/// </summary>
public abstract record ReminderInstruction
{
    /// <summary>Already expired — the Expired list state is the signal, no notification.</summary>
    public sealed record None : ReminderInstruction;

    /// <summary>Reminder moment already passed but the document is not expired — fire once, now.</summary>
    public sealed record Immediate : ReminderInstruction;

    /// <summary>Fire at the given local time (09:00 on expiry − remind-before).</summary>
    public sealed record At(DateTime LocalTime) : ReminderInstruction;
}
