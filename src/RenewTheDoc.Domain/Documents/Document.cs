namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// A thing the user wants to renew before it stops being valid. See CONTEXT.md.
/// </summary>
/// <remarks>
/// Aggregate root, immutable, constructible only through <see cref="Create"/> and
/// <see cref="Restore"/> — both validating. <see cref="Edit"/> returns a new instance rather than
/// mutating, so a validation failure part-way through cannot leave a half-edited Document behind
/// (spec §3.1).
/// </remarks>
public sealed record Document
{
    private const int NameMaxLength = 200;

    /// <summary>The single moment a Reminder fires: 09:00 local on the remind date (CONTEXT.md).</summary>
    public static readonly TimeOnly ReminderFireTime = new(9, 0);

    private Document(
        DocumentId id,
        string name,
        DateOnly expiryDate,
        RemindBefore remindBefore,
        DocumentOwner owner,
        string? note,
        Country? country)
    {
        Id = id;
        Name = name;
        ExpiryDate = expiryDate;
        RemindBefore = remindBefore;
        Owner = owner;
        Note = note;
        Country = country;
    }

    public DocumentId Id { get; }

    /// <summary>Non-empty after trimming, at most 200 characters, stored trimmed.</summary>
    public string Name { get; }

    /// <summary>Unbounded on purpose: an already-expired Document is legal (CONTEXT.md).</summary>
    public DateOnly ExpiryDate { get; }

    public RemindBefore RemindBefore { get; }

    /// <summary>
    /// Who the document belongs to — the user themselves or a named person. Always a value, never
    /// an absence: <see cref="DocumentOwner.Me"/> is a case of its own (spec §3.4).
    /// </summary>
    public DocumentOwner Owner { get; }

    public string? Note { get; }

    /// <summary>Optional Country this document is issued/valid in.</summary>
    public Country? Country { get; }

    /// <summary>A brand-new Document, with a fresh identity.</summary>
    public static Document Create(
        string name,
        DateOnly expiryDate,
        RemindBefore remindBefore,
        DocumentOwner owner,
        string? note = null,
        Country? country = null) =>
        new(DocumentId.New(), ValidatedName(name), expiryDate, remindBefore, owner, note, country);

    /// <summary>
    /// Rebuilds a stored Document under its existing identity. Runs the same invariants as
    /// <see cref="Create"/> and throws on a bad row: reconstitution fails loud rather than
    /// circulating an invalid aggregate or silently repairing one (spec §5.2).
    /// </summary>
    public static Document Restore(
        DocumentId id,
        string name,
        DateOnly expiryDate,
        RemindBefore remindBefore,
        DocumentOwner owner,
        string? note = null,
        Country? country = null) =>
        new(id, ValidatedName(name), expiryDate, remindBefore, owner, note, country);

    /// <summary>
    /// One composite edit, matching the UI's single atomic save, returning a new instance under the
    /// same identity. Every invariant is re-checked. Editing a Document cancels its Reminder and
    /// plans a new one — that orchestration is the app service's, this is only the new state.
    /// </summary>
    public Document Edit(
        string name,
        DateOnly expiryDate,
        RemindBefore remindBefore,
        DocumentOwner owner,
        string? note = null,
        Country? country = null) =>
        new(Id, ValidatedName(name), expiryDate, remindBefore, owner, note, country);

    /// <summary>Derives the document's state from today's date. Expiry date itself is not yet expired.</summary>
    public DocumentState StateOn(DateOnly today)
    {
        if (ExpiryDate < today) return DocumentState.Expired;

        var windowStart = ExpiryDate.AddDays(-RemindBefore.Days);
        return today >= windowStart ? DocumentState.ExpiringSoon : DocumentState.Ok;
    }

    /// <summary>
    /// What the scheduler should do for this document's single Reminder. Time enters as an
    /// argument — the domain reads no clock and depends on no clock port.
    /// </summary>
    public ReminderInstruction PlanReminder(DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        if (ExpiryDate < today) return new ReminderInstruction.None();

        var fireAt = ExpiryDate.AddDays(-RemindBefore.Days).ToDateTime(ReminderFireTime);
        return fireAt <= nowLocal ? new ReminderInstruction.Immediate() : new ReminderInstruction.At(fireAt);
    }

    private static string ValidatedName(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainRuleViolationException(DomainRule.DocumentNameRequired);
        if (trimmed.Length > NameMaxLength)
            throw new DomainRuleViolationException(DomainRule.DocumentNameTooLong);

        return trimmed;
    }
}
