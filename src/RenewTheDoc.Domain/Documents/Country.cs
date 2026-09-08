namespace RenewTheDoc.Domain.Documents;

/// <summary>
/// The Country a Document is issued or valid in, as an ISO 3166-1 alpha-2 code stored uppercase.
/// A sealed class rather than a struct so absence is a plain <c>null</c> and no <c>default</c> can
/// slip past the factory carrying an empty code (spec §6).
/// </summary>
/// <remarks>
/// Shape only. Membership of the ISO registry is deliberately not checked: the code set a
/// <c>RegionInfo</c> lookup accepts is whatever ICU on that OS version lists, so the same Document
/// would be valid on Android and invalid on iOS (spec §3.1).
/// </remarks>
public sealed record Country
{
    private Country(string code) => Code = code;

    /// <summary>Two uppercase ASCII letters.</summary>
    public string Code { get; }

    public static Country Of(string code)
    {
        if (code is not { Length: 2 } || !char.IsAsciiLetter(code[0]) || !char.IsAsciiLetter(code[1]))
            throw new DomainRuleViolationException(DomainRule.CountryCodeInvalid);

        return new Country(code.ToUpperInvariant());
    }

    /// <summary>
    /// Null in, null out — a Document without a Country is legal. Anything else is validated, so an
    /// empty string is a bad code rather than a quietly-accepted absence.
    /// </summary>
    public static Country? OfNullable(string? code) => code is null ? null : Of(code);

    public override string ToString() => Code;
}
