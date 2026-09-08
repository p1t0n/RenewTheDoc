namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// What a Reminder is about, as the inputs an alert is rendered from — never rendered text. The
/// wording and the language belong to the adapter (docs/architecture/ddd-refactor.md §3.2), so the
/// domain hands over the Document's name and expiry date and nothing else.
/// </summary>
public sealed record ReminderContent(string DocumentName, DateOnly ExpiryDate)
{
    public static ReminderContent Of(Document document) => new(document.Name, document.ExpiryDate);
}
