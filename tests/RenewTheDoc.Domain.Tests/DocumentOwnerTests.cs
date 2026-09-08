using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Domain.Tests;

/// <summary>
/// The two cases a Document's owner can be, and the equality every call site relies on: the list
/// filter compares owners rather than unwrapping ids (spec §3.4).
/// </summary>
public class DocumentOwnerTests
{
    [Fact]
    public void Me_is_one_shared_instance_not_an_allocation_per_document() =>
        Assert.Same(DocumentOwner.Me, DocumentOwner.Me);

    [Fact]
    public void A_person_carries_the_owner_id()
    {
        var id = OwnerId.New();

        Assert.Equal(id, new DocumentOwner.Person(id).Id);
    }

    [Fact]
    public void The_same_person_twice_is_the_same_owner()
    {
        var id = OwnerId.New();

        Assert.Equal(new DocumentOwner.Person(id), new DocumentOwner.Person(id));
    }

    [Fact]
    public void Two_different_people_are_different_owners() =>
        Assert.NotEqual(
            new DocumentOwner.Person(OwnerId.New()),
            new DocumentOwner.Person(OwnerId.New()));

    [Fact]
    public void Me_is_not_a_person() =>
        Assert.NotEqual(DocumentOwner.Me, (DocumentOwner)new DocumentOwner.Person(OwnerId.New()));

    [Fact]
    public void Me_is_a_value_of_its_own_so_no_id_has_to_stand_in_for_it() =>
        Assert.IsNotType<DocumentOwner.Person>(DocumentOwner.Me);

    /// <summary>
    /// Only Me and Person exist: the base constructor is private, so nothing outside the file can
    /// introduce a third case an exhaustive call site would silently miss.
    /// </summary>
    [Fact]
    public void The_hierarchy_is_closed_to_new_cases() =>
        Assert.Empty(typeof(DocumentOwner).GetConstructors());
}
