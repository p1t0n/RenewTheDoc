namespace RenewTheDoc.Domain;

/// <summary>
/// The domain rules a caller can violate. A code, never a message: the message is the UI's job,
/// in the user's language (see docs/architecture/ddd-refactor.md §4 and §7.3).
/// </summary>
public enum DomainRule
{
    DocumentNameRequired,
    DocumentNameTooLong,
    CountryCodeInvalid,
    RemindBeforeNegative,
    OwnerNameRequired,
    OwnerNameTooLong,
}
