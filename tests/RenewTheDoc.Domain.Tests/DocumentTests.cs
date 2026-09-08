using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

/// <summary>
/// The invariants the aggregate enforces — and, just as deliberately, the ones it does not. The
/// "not enforced" cases are tests because they are decisions (spec §3.1), not omissions.
/// </summary>
public class DocumentTests
{
    private static readonly DateOnly Expiry = new(2027, 1, 1);
    private static readonly RemindBefore Month = new(30);

    private static DomainRule RuleFrom(Action act) =>
        Assert.Throws<DomainRuleViolationException>(act).Rule;

    // ---- construction is closed ----

    [Fact]
    public void No_public_constructor_exists_so_an_invalid_document_cannot_be_built_from_outside() =>
        Assert.Empty(typeof(Document).GetConstructors());

    [Fact]
    public void Create_mints_a_new_identity()
    {
        var one = Document.Create("Passport", Expiry, Month);
        var two = Document.Create("Passport", Expiry, Month);

        Assert.NotEqual(default, one.Id);
        Assert.NotEqual(one.Id, two.Id);
    }

    [Fact]
    public void Restore_keeps_the_stored_identity()
    {
        var id = DocumentId.New();

        Assert.Equal(id, Document.Restore(id, "Passport", Expiry, Month).Id);
    }

    // ---- enforced: name ----

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_that_is_empty_after_trimming_is_refused(string name)
    {
        Assert.Equal(DomainRule.DocumentNameRequired, RuleFrom(() => Document.Create(name, Expiry, Month)));
        Assert.Equal(
            DomainRule.DocumentNameRequired,
            RuleFrom(() => Document.Restore(DocumentId.New(), name, Expiry, Month)));
        Assert.Equal(
            DomainRule.DocumentNameRequired,
            RuleFrom(() => Document.Create("Passport", Expiry, Month).Edit(name, Expiry, Month)));
    }

    [Fact]
    public void A_name_over_200_characters_is_refused()
    {
        var tooLong = new string('a', 201);

        Assert.Equal(DomainRule.DocumentNameTooLong, RuleFrom(() => Document.Create(tooLong, Expiry, Month)));
        Assert.Equal(
            DomainRule.DocumentNameTooLong,
            RuleFrom(() => Document.Restore(DocumentId.New(), tooLong, Expiry, Month)));
        Assert.Equal(
            DomainRule.DocumentNameTooLong,
            RuleFrom(() => Document.Create("Passport", Expiry, Month).Edit(tooLong, Expiry, Month)));
    }

    [Fact]
    public void Exactly_200_characters_is_still_a_name() =>
        Assert.Equal(200, Document.Create(new string('a', 200), Expiry, Month).Name.Length);

    [Fact]
    public void The_name_is_stored_trimmed() =>
        Assert.Equal("Passport", Document.Create("  Passport  ", Expiry, Month).Name);

    // ---- enforced: country ----

    [Fact]
    public void A_country_code_is_stored_uppercase() =>
        Assert.Equal("PL", Document.Create("Passport", Expiry, Month, country: Country.Of("pl")).Country?.Code);

    [Theory]
    [InlineData("")]
    [InlineData("P")]
    [InlineData("POL")]
    [InlineData("P1")]
    [InlineData("--")]
    [InlineData("PŁ")]
    public void A_country_code_that_is_not_two_ascii_letters_is_refused(string code) =>
        Assert.Equal(DomainRule.CountryCodeInvalid, RuleFrom(() => Country.Of(code)));

    [Fact]
    public void No_country_is_a_legal_country()
    {
        Assert.Null(Country.OfNullable(null));
        Assert.Null(Document.Create("Passport", Expiry, Month).Country);
    }

    [Fact]
    public void Two_countries_with_the_same_code_are_the_same_country() =>
        Assert.Equal(Country.Of("PL"), Country.Of("pl"));

    // ---- deliberately not enforced ----

    [Fact]
    public void A_country_code_outside_the_ISO_registry_is_accepted_shape_is_the_only_rule() =>
        Assert.Equal("ZZ", Document.Create("Passport", Expiry, Month, country: Country.Of("zz")).Country?.Code);

    [Fact]
    public void An_absurdly_early_remind_before_is_legal_intent() =>
        Assert.Equal(10_000, Document.Create("Passport", Expiry, new RemindBefore(10_000)).RemindBefore.Days);

    [Fact]
    public void An_already_expired_expiry_date_is_legal()
    {
        var document = Document.Create("Passport", new DateOnly(1999, 1, 1), Month);

        Assert.Equal(new DateOnly(1999, 1, 1), document.ExpiryDate);
        Assert.Equal(DocumentState.Expired, document.StateOn(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void A_remind_before_longer_than_the_time_to_expiry_is_legal() =>
        Assert.Equal(
            DocumentState.ExpiringSoon,
            Document.Create("Passport", new DateOnly(2026, 1, 2), new RemindBefore(10_000))
                .StateOn(new DateOnly(2026, 1, 1)));

    [Fact]
    public void An_owner_the_dictionary_does_not_know_is_still_a_valid_document()
    {
        var stranger = OwnerId.New();

        Assert.Equal(stranger, Document.Create("Passport", Expiry, Month, ownerId: stranger).OwnerId);
    }

    [Fact]
    public void A_note_has_no_rules() =>
        Assert.Equal("  anything at all  ",
            Document.Create("Passport", Expiry, Month, note: "  anything at all  ").Note);

    // ---- edit ----

    [Fact]
    public void Edit_returns_a_new_instance_under_the_same_identity()
    {
        var original = Document.Create("Passport", Expiry, Month, note: "old", country: Country.Of("PL"));

        var edited = original.Edit("ID card", new DateOnly(2028, 5, 5), new RemindBefore(7), "new",
            Country.Of("DE"), OwnerId.New());

        Assert.NotSame(original, edited);
        Assert.Equal(original.Id, edited.Id);
        Assert.Equal("ID card", edited.Name);
        Assert.Equal(new DateOnly(2028, 5, 5), edited.ExpiryDate);
        Assert.Equal(7, edited.RemindBefore.Days);
        Assert.Equal("new", edited.Note);
        Assert.Equal("DE", edited.Country?.Code);
        Assert.NotNull(edited.OwnerId);
    }

    [Fact]
    public void A_rejected_edit_leaves_the_original_untouched()
    {
        var original = Document.Create("Passport", Expiry, Month, country: Country.Of("PL"));

        Assert.Throws<DomainRuleViolationException>(() => original.Edit("", Expiry, Month));

        Assert.Equal("Passport", original.Name);
        Assert.Equal("PL", original.Country?.Code);
    }

    [Fact]
    public void Edit_clears_the_fields_it_is_not_given_there_is_no_partial_save()
    {
        var original = Document.Create("Passport", Expiry, Month, note: "old", country: Country.Of("PL"),
            ownerId: OwnerId.New());

        var edited = original.Edit("Passport", Expiry, Month);

        Assert.Null(edited.Note);
        Assert.Null(edited.Country);
        Assert.Null(edited.OwnerId);
    }
}
