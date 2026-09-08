namespace RenewTheDoc.Domain;

/// <summary>
/// A domain invariant was violated. Carries the <see cref="DomainRule"/> code so the presentation
/// layer can pick a localized message; the exception message itself is for logs and test output.
/// </summary>
public sealed class DomainRuleViolationException : Exception
{
    public DomainRuleViolationException(DomainRule rule)
        : base($"Domain rule violated: {rule}.") => Rule = rule;

    public DomainRule Rule { get; }
}
