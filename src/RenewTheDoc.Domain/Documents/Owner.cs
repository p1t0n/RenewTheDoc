namespace RenewTheDoc.Domain.Documents;

/// <summary>A person a Document belongs to, living in the user-managed dictionary. See CONTEXT.md.</summary>
/// <remarks>
/// Aggregate root in its own right: identity is independent of any Document, so renaming the person
/// leaves every Document still pointing at them. Creation only — no <c>Rename</c> until something
/// calls one (spec §3.3). Name uniqueness is deliberately not an invariant: two people may share a
/// name, and enforcing it would need a repository query before every save.
/// </remarks>
public sealed record Owner
{
    private const int NameMaxLength = 100;

    private Owner(OwnerId id, string name)
    {
        Id = id;
        Name = name;
    }

    public OwnerId Id { get; }

    /// <summary>Non-empty after trimming, at most 100 characters, stored trimmed.</summary>
    public string Name { get; }

    /// <summary>A brand-new Owner, with a fresh identity.</summary>
    public static Owner Create(string name) => new(OwnerId.New(), ValidatedName(name));

    /// <summary>
    /// Rebuilds a stored Owner under its existing identity, running the same invariants as
    /// <see cref="Create"/> so a bad row fails loud rather than circulating (spec §5.2).
    /// </summary>
    public static Owner Restore(OwnerId id, string name) => new(id, ValidatedName(name));

    private static string ValidatedName(string name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new DomainRuleViolationException(DomainRule.OwnerNameRequired);
        if (trimmed.Length > NameMaxLength)
            throw new DomainRuleViolationException(DomainRule.OwnerNameTooLong);

        return trimmed;
    }
}
