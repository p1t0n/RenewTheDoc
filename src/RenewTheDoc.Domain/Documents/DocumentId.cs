namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Identity of a Document, and the sync identity later (spec §9.1) — stable, never regenerated.
/// Unwraps to <see cref="Guid"/> only at the persistence boundary: sqlite-net has no converter
/// seam and cannot store a custom struct.
/// </summary>
public readonly record struct DocumentId(Guid Value)
{
    public static DocumentId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
