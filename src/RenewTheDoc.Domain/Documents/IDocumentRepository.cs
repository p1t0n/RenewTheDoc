namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Local-only persistence seam for the Document aggregate. No backend exists — see the map's
/// privacy stance.
/// </summary>
/// <remarks>
/// One repository per aggregate, serving aggregates rather than rows. <see cref="SaveAsync"/> is an
/// upsert: the split into add-versus-update carried two unhandled asymmetries — updating a missing
/// row silently affected zero rows, inserting an existing one threw — and the app service already
/// knows create from edit (spec §5.1). No <c>CancellationToken</c> parameters: sqlite-net's async
/// API accepts none, and a token no implementation can honour is a lie the compiler endorses (§5.6).
/// </remarks>
public interface IDocumentRepository
{
    /// <summary>The stored Document, or null when nothing is stored under that id.</summary>
    Task<Document?> GetAsync(DocumentId id);

    Task<IReadOnlyList<Document>> GetAllAsync();

    /// <summary>Stores the Document, whether it is brand-new or already persisted.</summary>
    Task SaveAsync(Document document);

    Task RemoveAsync(DocumentId id);
}
