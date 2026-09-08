using RenewTheDoc.Domain;

namespace RenewTheDoc.App.Localization;

/// <summary>
/// Turns a <see cref="DomainRule"/> code into the localized message the user sees. Lives in App
/// because App is the only layer that knows <see cref="L"/> exists (spec §7.3).
/// </summary>
public static class DomainRuleMessages
{
    /// <summary>The alert text for a violated rule, in the user's language.</summary>
    public static string Localized(DomainRule rule) => L.T(ResourceKey(rule));

    // Deliberately no discard arm: a new DomainRule without a resource key must not compile.
    // CS8509 (missing named value) is escalated to an error in RenewTheDoc.App.csproj; CS8524 is
    // the unnamed-enum-value case only, which a cast can always reach and no key can cover.
#pragma warning disable CS8524
    private static string ResourceKey(DomainRule rule) => rule switch
    {
        DomainRule.DocumentNameRequired => "NameRequired",
        DomainRule.DocumentNameTooLong => "DocumentNameTooLong",
        DomainRule.CountryCodeInvalid => "CountryCodeInvalid",
        DomainRule.RemindBeforeNegative => "InvalidCustomDays",
        DomainRule.OwnerNameRequired => "OwnerNameRequired",
        DomainRule.OwnerNameTooLong => "OwnerNameTooLong",
    };
#pragma warning restore CS8524
}
