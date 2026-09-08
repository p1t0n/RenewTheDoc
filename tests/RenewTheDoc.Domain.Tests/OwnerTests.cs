using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

/// <summary>
/// Owner's own invariants — and the uniqueness rule it deliberately does not have (spec §3.3).
/// </summary>
public class OwnerTests
{
    private static DomainRule RuleFrom(Action act) =>
        Assert.Throws<DomainRuleViolationException>(act).Rule;

    [Fact]
    public void No_public_constructor_exists_so_an_invalid_owner_cannot_be_built_from_outside() =>
        Assert.Empty(typeof(Owner).GetConstructors());

    [Fact]
    public void Create_mints_a_new_identity()
    {
        var one = Owner.Create("Ann");
        var two = Owner.Create("Ann");

        Assert.NotEqual(default, one.Id);
        Assert.NotEqual(one.Id, two.Id);
    }

    [Fact]
    public void Restore_keeps_the_stored_identity()
    {
        var id = OwnerId.New();

        Assert.Equal(id, Owner.Restore(id, "Ann").Id);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_name_that_is_empty_after_trimming_is_refused(string name)
    {
        Assert.Equal(DomainRule.OwnerNameRequired, RuleFrom(() => Owner.Create(name)));
        Assert.Equal(DomainRule.OwnerNameRequired, RuleFrom(() => Owner.Restore(OwnerId.New(), name)));
    }

    [Fact]
    public void A_name_over_100_characters_is_refused()
    {
        var tooLong = new string('a', 101);

        Assert.Equal(DomainRule.OwnerNameTooLong, RuleFrom(() => Owner.Create(tooLong)));
        Assert.Equal(
            DomainRule.OwnerNameTooLong,
            RuleFrom(() => Owner.Restore(OwnerId.New(), tooLong)));
    }

    [Fact]
    public void Exactly_100_characters_is_still_a_name() =>
        Assert.Equal(100, Owner.Create(new string('a', 100)).Name.Length);

    [Fact]
    public void The_name_is_stored_trimmed() =>
        Assert.Equal("Ann", Owner.Create("  Ann  ").Name);

    /// <summary>
    /// Two people may share a name; enforcing uniqueness would need a repository query before every
    /// save, dragging persistence into the domain.
    /// </summary>
    [Fact]
    public void Two_owners_may_share_a_name_and_stay_distinct_people()
    {
        var one = Owner.Create("Ann");
        var two = Owner.Create("Ann");

        Assert.Equal(one.Name, two.Name);
        Assert.NotEqual(one.Id, two.Id);
        Assert.NotEqual(one, two);
    }

    /// <summary>
    /// Identity is independent of any Document, so nothing on the aggregate rewrites it — and there
    /// is no Rename until something calls one.
    /// </summary>
    [Fact]
    public void An_owner_exposes_no_way_to_change_itself() =>
        Assert.DoesNotContain(
            typeof(Owner).GetMethods().Select(m => m.Name),
            name => name is "Rename" or "set_Name" or "set_Id");
}
