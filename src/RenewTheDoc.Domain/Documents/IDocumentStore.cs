namespace RenewTheDoc.Domain.Documents;

/// <summary>Local-only persistence seam. No backend exists — see the map's privacy stance.</summary>
public interface IDocumentStore
{
    Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Document document, CancellationToken ct = default);
    Task UpdateAsync(Document document, CancellationToken ct = default);
    Task DeleteAsync(DocumentId documentId, CancellationToken ct = default);
}
