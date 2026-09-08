using RenewTheDoc.Domain;
using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence.Documents;

namespace RenewTheDoc.Persistence.Tests;

public class SqliteDocumentRepositoryTests
{
    [Fact]
    public async Task A_saved_document_comes_back_whole()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var document = Document.Create(
            "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth,
            new DocumentOwner.Person(OwnerId.New()), "in the drawer", Country.Of("pl"));

        await repository.SaveAsync(document);
        var loaded = await repository.GetAsync(document.Id);

        Assert.Equal(document, loaded);
    }

    /// <summary>
    /// The upsert, which is the whole reason add and update collapsed into one method: a brand-new
    /// aggregate and an existing one both go through this call (spec §5.1).
    /// </summary>
    [Fact]
    public async Task Saving_a_brand_new_document_and_saving_it_again_edited_both_work()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var document = Document.Create(
            "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth, DocumentOwner.Me);

        await repository.SaveAsync(document);
        await repository.SaveAsync(document.Edit(
            "Passport (renewed)", new DateOnly(2032, 3, 14), RemindBefore.ThreeMonths,
            DocumentOwner.Me));

        var all = await repository.GetAllAsync();
        var stored = Assert.Single(all);
        Assert.Equal(document.Id, stored.Id);
        Assert.Equal("Passport (renewed)", stored.Name);
        Assert.Equal(new DateOnly(2032, 3, 14), stored.ExpiryDate);
        Assert.Equal(RemindBefore.ThreeMonths.Days, stored.RemindBefore.Days);
    }

    [Fact]
    public async Task Me_round_trips_as_Me_and_a_person_as_that_person()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var ownerId = OwnerId.New();
        await repository.SaveAsync(Document.Create(
            "Mine", new DateOnly(2027, 1, 1), RemindBefore.OneWeek, DocumentOwner.Me));
        await repository.SaveAsync(Document.Create(
            "Theirs", new DateOnly(2027, 1, 1), RemindBefore.OneWeek,
            new DocumentOwner.Person(ownerId)));

        var byName = (await repository.GetAllAsync()).ToDictionary(d => d.Name, d => d.Owner);

        Assert.Same(DocumentOwner.Me, byName["Mine"]);
        Assert.Equal(new DocumentOwner.Person(ownerId), byName["Theirs"]);
    }

    [Fact]
    public async Task An_unknown_id_reads_as_nothing_rather_than_throwing()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);

        Assert.Null(await repository.GetAsync(DocumentId.New()));
    }

    [Fact]
    public async Task Removing_a_document_takes_it_out_of_the_list()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var document = Document.Create(
            "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth, DocumentOwner.Me);
        await repository.SaveAsync(document);

        await repository.RemoveAsync(document.Id);

        Assert.Empty(await repository.GetAllAsync());
        Assert.Null(await repository.GetAsync(document.Id));
    }

    /// <summary>Removing what is not there is not an error — the delete path is idempotent.</summary>
    [Fact]
    public async Task Removing_an_unknown_id_is_no_error()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);

        await repository.RemoveAsync(DocumentId.New());
    }

    /// <summary>
    /// <c>default(DocumentId)</c> is an empty Guid the struct cannot refuse, so the repository does
    /// — rather than addressing whatever row an empty key happens to hit (spec §5.1, §9.1).
    /// </summary>
    [Fact]
    public async Task An_empty_id_is_refused_on_every_call_that_takes_one()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.RemoveAsync(default));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(Document.Restore(
            default, "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth,
            DocumentOwner.Me)));
    }

    /// <summary>
    /// Rows in the shape the previous store wrote — same table, same columns, dates as ISO strings,
    /// the owner as a plain nullable Guid — still load. The schema was not reshaped (spec §5.5), so
    /// an installed app's database keeps working.
    /// </summary>
    [Fact]
    public async Task Rows_written_the_way_the_previous_store_wrote_them_still_load()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO DocumentRow (Id, Name, ExpiryDate, RemindBeforeDays, Note, CountryCode, OwnerId) " +
            "VALUES (?, ?, ?, ?, ?, ?, ?)",
            id, "Passport", "2027-03-14", 90, "in the drawer", "PL", ownerId);

        var document = Assert.Single(await repository.GetAllAsync());

        Assert.Equal(new DocumentId(id), document.Id);
        Assert.Equal("Passport", document.Name);
        Assert.Equal(new DateOnly(2027, 3, 14), document.ExpiryDate);
        Assert.Equal(90, document.RemindBefore.Days);
        Assert.Equal("in the drawer", document.Note);
        Assert.Equal("PL", document.Country?.Code);
        Assert.Equal(new DocumentOwner.Person(new OwnerId(ownerId)), document.Owner);
    }

    /// <summary>
    /// Reconstitution fails loud. A row whose name is blank breaks an invariant, and rebuilding it
    /// throws the same rule violation the UI would have shown — no repair, no clamping, no
    /// construction bypass (spec §5.2).
    /// </summary>
    [Fact]
    public async Task A_row_that_breaks_an_invariant_throws_when_it_is_rebuilt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var id = Guid.NewGuid();
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO DocumentRow (Id, Name, ExpiryDate, RemindBeforeDays, Note, CountryCode, OwnerId) " +
            "VALUES (?, ?, ?, ?, NULL, NULL, NULL)",
            id, "   ", "2027-03-14", 30);

        var fromGet = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => repository.GetAsync(new DocumentId(id)));
        var fromGetAll = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => repository.GetAllAsync());

        Assert.Equal(DomainRule.DocumentNameRequired, fromGet.Rule);
        Assert.Equal(DomainRule.DocumentNameRequired, fromGetAll.Rule);
    }

    /// <summary>A negative remind-before is the other way a hand-written row can be wrong.</summary>
    [Fact]
    public async Task A_row_with_a_negative_remind_before_throws_when_it_is_rebuilt()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO DocumentRow (Id, Name, ExpiryDate, RemindBeforeDays, Note, CountryCode, OwnerId) " +
            "VALUES (?, ?, ?, ?, NULL, NULL, NULL)",
            Guid.NewGuid(), "Passport", "2027-03-14", -1);

        var violation = await Assert.ThrowsAsync<DomainRuleViolationException>(
            () => repository.GetAllAsync());

        Assert.Equal(DomainRule.RemindBeforeNegative, violation.Rule);
    }
}
