using RenewTheDoc.Application.Documents;
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
        Assert.NotEqual(Guid.Empty, owner.Id);
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

    [Fact]
    public async Task List_returns_the_dictionary_as_stored()
    {
        var log = new CallLog();
        var ann = new Owner { Name = "Ann" };
        var bob = new Owner { Name = "Bob" };
        var service = new OwnerAppService(new FakeOwnerStore(log, ann, bob));

        var owners = await service.ListAsync();

        Assert.Equal(["owners.GetAll"], log.Calls);
        Assert.Equal(["Ann", "Bob"], owners.Select(o => o.Name));
    }
}
