namespace RenewTheDoc.Domain.Documents;

/// <summary>A run of Documents sharing a state. Carries domain objects only.</summary>
public sealed record DocumentGroup(DocumentState State, IReadOnlyList<Document> Documents);

/// <summary>How the user's Documents are presented as a list: grouped by urgency, ordered inside.</summary>
public static class DocumentList
{
    /// <summary>
    /// Groups by state in glossary order — Expired, Expiring Soon, Ok — with expired first and then
    /// nearest expiry inside each group. A state nobody is in yields no group, so the list shows no
    /// empty headings.
    /// </summary>
    public static IReadOnlyList<DocumentGroup> Grouped(IEnumerable<Document> documents, DateOnly today) =>
        documents
            .Select(document => (Document: document, State: document.StateOn(today)))
            .OrderBy(entry => entry.State == DocumentState.Expired ? 0 : 1)
            .ThenBy(entry => entry.Document.ExpiryDate)
            .GroupBy(entry => entry.State)
            .OrderBy(group => group.Key)
            .Select(group => new DocumentGroup(group.Key, group.Select(entry => entry.Document).ToList()))
            .ToList();
}
