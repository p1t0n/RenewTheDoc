using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Documents;

/// <summary>Use cases over the Owner dictionary: list and add.</summary>
public sealed class OwnerAppService
{
    private readonly IOwnerStore _owners;

    public OwnerAppService(IOwnerStore owners) => _owners = owners;

    public Task<IReadOnlyList<Owner>> ListAsync() => _owners.GetAllAsync();

    /// <summary>Adds an Owner to the dictionary and returns it, so the caller can select it.</summary>
    public async Task<Owner> AddAsync(string name)
    {
        var owner = new Owner { Name = name };
        await _owners.AddAsync(owner);
        return owner;
    }
}
