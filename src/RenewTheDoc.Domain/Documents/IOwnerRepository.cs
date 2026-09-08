namespace RenewTheDoc.Domain.Documents;

/// <summary>Local-only persistence seam for the Owner aggregate, same shape as
/// <see cref="IDocumentRepository"/> (spec §5.1).</summary>
/// <remarks>
/// <see cref="RemoveAsync"/> exists because the contract is per-aggregate and symmetric; Owner
/// removal itself is not wired to any use case yet. When it is, the "an Owner cannot be removed
/// while a Document references it" rule belongs to the app service, not here (spec §3.3).
/// </remarks>
public interface IOwnerRepository
{
    /// <summary>The stored Owner, or null when nothing is stored under that id.</summary>
    Task<Owner?> GetAsync(OwnerId id);

    Task<IReadOnlyList<Owner>> GetAllAsync();

    /// <summary>Stores the Owner, whether it is brand-new or already persisted.</summary>
    Task SaveAsync(Owner owner);

    Task RemoveAsync(OwnerId id);
}
