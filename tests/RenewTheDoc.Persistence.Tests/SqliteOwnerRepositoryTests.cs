using RenewTheDoc.Domain;
using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence.Documents;

namespace RenewTheDoc.Persistence.Tests;

public class SqliteOwnerRepositoryTests
{
    [Fact]
    public async Task A_saved_owner_comes_back_whole()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        var owner = Owner.Create("Ann");

        await repository.SaveAsync(owner);

        Assert.Equal(owner, await repository.GetAsync(owner.Id));
    }

    [Fact]
    public async Task Saving_a_brand_new_owner_and_saving_the_same_one_again_both_work()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        var owner = Owner.Create("Ann");

        await repository.SaveAsync(owner);
        await repository.SaveAsync(owner);

        Assert.Equal(owner, Assert.Single(await repository.GetAllAsync()));
    }

    /// <summary>The dictionary reads back by name, as the picker and the filter chips show it.</summary>
    [Fact]
    public async Task The_dictionary_reads_back_in_name_order()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        await repository.SaveAsync(Owner.Create("Zoe"));
        await repository.SaveAsync(Owner.Create("Ann"));

        Assert.Equal(["Ann", "Zoe"], (await repository.GetAllAsync()).Select(o => o.Name));
    }

    /// <summary>Two people may share a name: distinct ids, two rows (spec §3.3).</summary>
    [Fact]
    public async Task Two_owners_sharing_a_name_are_two_rows()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        await repository.SaveAsync(Owner.Create("Ann"));
        await repository.SaveAsync(Owner.Create("Ann"));

        Assert.Equal(["Ann", "Ann"], (await repository.GetAllAsync()).Select(o => o.Name));
    }

    [Fact]
    public async Task An_unknown_id_reads_as_nothing_rather_than_throwing()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);

        Assert.Null(await repository.GetAsync(OwnerId.New()));
    }

    [Fact]
    public async Task Removing_an_owner_takes_it_out_of_the_dictionary()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        var owner = Owner.Create("Ann");
        await repository.SaveAsync(owner);

        await repository.RemoveAsync(owner.Id);

        Assert.Empty(await repository.GetAllAsync());
    }

    [Fact]
    public async Task An_empty_id_is_refused_on_every_call_that_takes_one()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RemoveAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(
            () => repository.SaveAsync(Owner.Restore(default, "Ann")));
    }

    /// <summary>Reconstitution re-runs the Owner's invariants and throws on a bad row (spec §5.2).</summary>
    [Fact]
    public async Task A_row_that_breaks_an_invariant_throws_when_it_is_rebuilt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteOwnerRepository(db.Database);
        var id = Guid.NewGuid();
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO OwnerRow (Id, Name) VALUES (?, ?)", id, "");

        var fromGet = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => repository.GetAsync(new OwnerId(id)));
        var fromGetAll = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => repository.GetAllAsync());

        Assert.Equal(DomainRule.OwnerNameRequired, fromGet.Rule);
        Assert.Equal(DomainRule.OwnerNameRequired, fromGetAll.Rule);
    }
}
