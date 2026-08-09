namespace RenewTheDoc.Core;

/// <summary>Local-only persistence for the Owner dictionary.</summary>
public interface IOwnerStore
{
    Task<IReadOnlyList<Owner>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Owner owner, CancellationToken ct = default);
}
