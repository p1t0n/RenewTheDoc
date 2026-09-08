namespace RenewTheDoc.Domain.Documents;

/// <summary>Derived, never stored. Declared in glossary order — the order the list groups in.</summary>
public enum DocumentState
{
    Expired,
    ExpiringSoon,
    Ok,
}
