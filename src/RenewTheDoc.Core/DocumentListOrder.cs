namespace RenewTheDoc.Core;

public static class DocumentListOrder
{
    /// <summary>Expired first, then by nearest expiry date.</summary>
    public static IReadOnlyList<Document> Sorted(IEnumerable<Document> documents, DateOnly today) =>
        documents
            .OrderBy(d => d.GetState(today) == DocumentState.Expired ? 0 : 1)
            .ThenBy(d => d.ExpiryDate)
            .ToList();
}
