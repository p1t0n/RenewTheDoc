namespace RenewTheDoc.Core;

/// <summary>
/// Per-document lead time before the expiry date. Doubles as the Expiring Soon window.
/// Presets: 1 week, 1 month (30 days), 3 months (90 days) — or a custom number of days.
/// </summary>
public readonly record struct RemindBefore
{
    public int Days { get; }

    public RemindBefore(int days)
    {
        if (days < 0) throw new ArgumentOutOfRangeException(nameof(days), "Remind-before cannot be negative.");
        Days = days;
    }

    public static readonly RemindBefore OneWeek = new(7);
    public static readonly RemindBefore OneMonth = new(30);
    public static readonly RemindBefore ThreeMonths = new(90);
}
