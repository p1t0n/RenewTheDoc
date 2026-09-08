namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// Who a Document belongs to: either the user themselves (<see cref="Me"/>) or a named
/// <see cref="Person"/> from the Owner dictionary. See CONTEXT.md.
/// </summary>
/// <remarks>
/// A closed two-case hierarchy — the base constructor is private, so no third case can be added
/// from outside. It replaces the old <c>OwnerId?</c> where <c>null</c> meant Me: the model really
/// has three cases at the filter level (all Documents / mine / that person's), and one nullable id
/// could only carry two, which forced call sites to pass a second disambiguating flag alongside it
/// (spec §3.4). Me is a value, not an absence, so it is never an identity the app issues; storage
/// still keeps it as a null column (spec §5.5).
/// </remarks>
public abstract record DocumentOwner
{
    private DocumentOwner() { }

    /// <summary>
    /// The user themselves. A single shared instance rather than one allocation per row — there is
    /// only ever one "me", and Me is deliberately not a dictionary entry (CONTEXT.md).
    /// </summary>
    public static DocumentOwner Me { get; } = new TheUser();

    /// <summary>
    /// Someone from the Owner dictionary, referenced by id across the aggregate boundary. The
    /// referenced <see cref="Owner"/> may be unknown locally and the Document is still valid
    /// (spec §3.3).
    /// </summary>
    public sealed record Person(OwnerId Id) : DocumentOwner
    {
        public override string ToString() => Id.ToString();
    }

    private sealed record TheUser : DocumentOwner
    {
        public override string ToString() => "Me";
    }
}
