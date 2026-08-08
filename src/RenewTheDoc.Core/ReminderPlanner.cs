namespace RenewTheDoc.Core;

/// <summary>What the scheduler should do for a document's single Reminder.</summary>
public abstract record ReminderInstruction
{
    /// <summary>Already expired at creation — the Expired list state is the signal, no notification.</summary>
    public sealed record None : ReminderInstruction;

    /// <summary>Reminder moment already passed but the document is not expired — fire once, now.</summary>
    public sealed record Immediate : ReminderInstruction;

    /// <summary>Fire at the given local time (09:00 on expiry − remind-before).</summary>
    public sealed record At(DateTime LocalTime) : ReminderInstruction;
}

public static class ReminderPlanner
{
    public static readonly TimeOnly FireTime = new(9, 0);

    public static ReminderInstruction Plan(Document document, DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        if (document.ExpiryDate < today) return new ReminderInstruction.None();

        var remindDate = document.ExpiryDate.AddDays(-document.RemindBefore.Days);
        var fireAt = remindDate.ToDateTime(FireTime);
        return fireAt <= nowLocal ? new ReminderInstruction.Immediate() : new ReminderInstruction.At(fireAt);
    }
}
