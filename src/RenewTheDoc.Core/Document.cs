namespace RenewTheDoc.Core;

/// <summary>A thing the user wants to renew before it stops being valid. See CONTEXT.md.</summary>
public sealed record Document
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public required DateOnly ExpiryDate { get; init; }
    public required RemindBefore RemindBefore { get; init; }
    public string? Note { get; init; }
}
