namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Identity of an Owner, independent of any Document. Distinct from <see cref="DocumentId"/> so
/// an owner id cannot reach a document parameter when both flow through the same service.
/// </summary>
public readonly record struct OwnerId(Guid Value)
{
    public static OwnerId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
