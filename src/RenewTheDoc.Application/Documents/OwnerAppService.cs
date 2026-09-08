using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Documents;

/// <summary>Use cases over the Owner dictionary: list and add.</summary>
public sealed class OwnerAppService
{
    private readonly IOwnerRepository _owners;

    public OwnerAppService(IOwnerRepository owners) => _owners = owners;

    public Task<IReadOnlyList<Owner>> ListAsync() => _owners.GetAllAsync();

    /// <summary>
    /// Adds an Owner to the dictionary and returns it, so the caller can select it. A name already
    /// in the dictionary is accepted: two people may share one (spec §3.3).
    /// </summary>
    public async Task<Owner> AddAsync(string name)
    {
        var owner = Owner.Create(name);
        await _owners.SaveAsync(owner);
        return owner;
    }
}
