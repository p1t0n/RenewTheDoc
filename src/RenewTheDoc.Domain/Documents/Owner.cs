namespace RenewTheDoc.Domain.Documents;

/// <summary>A person a Document belongs to. A Document without an Owner belongs to the user. See CONTEXT.md.</summary>
public sealed record Owner
{
    public OwnerId Id { get; init; } = OwnerId.New();
    public required string Name { get; init; }
}
