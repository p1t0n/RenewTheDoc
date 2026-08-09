namespace RenewTheDoc.Core;

/// <summary>A person a Document belongs to. A Document without an Owner belongs to the user. See CONTEXT.md.</summary>
public sealed record Owner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
}
