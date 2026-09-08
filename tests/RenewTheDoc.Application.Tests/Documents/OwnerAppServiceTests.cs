using RenewTheDoc.Application.Documents;
using RenewTheDoc.Domain;
using RenewTheDoc.Domain.Documents;

namespace RenewTheDoc.Application.Tests.Documents;

public class OwnerAppServiceTests
{
    [Fact]
    public async Task Add_stores_an_owner_under_the_given_name_and_returns_it()
    {
        var log = new CallLog();
        var store = new FakeOwnerStore(log);
        var service = new OwnerAppService(store);

        var owner = await service.AddAsync("Ann");

        Assert.Equal(["owners.Add(Ann)"], log.Calls);
        Assert.Equal("Ann", owner.Name);
        Assert.Same(owner, store.LastAdded);
        Assert.NotEqual(Guid.Empty, owner.Id.Value);
    }

    [Fact]
    public async Task Added_owner_is_visible_to_the_next_list()
    {
        var log = new CallLog();
        var service = new OwnerAppService(new FakeOwnerStore(log));

        var owner = await service.AddAsync("Ann");
        var owners = await service.ListAsync();

        Assert.Equal(["owners.Add(Ann)", "owners.GetAll"], log.Calls);
        Assert.Equal(owner.Id, Assert.Single(owners).Id);
    }

    /// <summary>
    /// Uniqueness is deliberately not enforced, and the service adds no check of its own: adding a
    /// name the dictionary already holds stores a second, distinct person (spec §3.3).
    /// </summary>
    [Fact]
    public async Task A_name_the_dictionary_already_holds_is_added_again_as_a_second_person()
    {
        var log = new CallLog();
        var store = new FakeOwnerStore(log);
        var service = new OwnerAppService(store);

        var first = await service.AddAsync("Ann");
        var second = await service.AddAsync("Ann");
        var owners = await service.ListAsync();

        Assert.Equal(["owners.Add(Ann)", "owners.Add(Ann)", "owners.GetAll"], log.Calls);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(["Ann", "Ann"], owners.Select(o => o.Name));
    }

    [Fact]
    public async Task An_owner_name_that_breaks_a_rule_never_reaches_the_store()
    {
        var log = new CallLog();
        var store = new FakeOwnerStore(log);
        var service = new OwnerAppService(store);

        var violation = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => service.AddAsync(new string('a', 101)));

        Assert.Equal(DomainRule.OwnerNameTooLong, violation.Rule);
        Assert.Empty(log.Calls);
    }

    [Fact]
    public async Task List_returns_the_dictionary_as_stored()
    {
        var log = new CallLog();
        var ann = Owner.Create("Ann");
        var bob = Owner.Create("Bob");
        var service = new OwnerAppService(new FakeOwnerStore(log, ann, bob));

        var owners = await service.ListAsync();

        Assert.Equal(["owners.GetAll"], log.Calls);
        Assert.Equal(["Ann", "Bob"], owners.Select(o => o.Name));
    }
}
