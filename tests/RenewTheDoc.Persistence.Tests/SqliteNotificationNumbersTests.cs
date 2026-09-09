using RenewTheDoc.Domain.Documents;
using RenewTheDoc.Persistence.Documents;
using RenewTheDoc.Persistence.Notifications;

namespace RenewTheDoc.Persistence.Tests;

/// <summary>
/// The REN-54 fix: a Document's notification number is handed out once and stored, so two Documents
/// cannot share one and cancel each other's Reminder.
/// </summary>
public class SqliteNotificationNumbersTests
{
    [Fact]
    public async Task A_document_keeps_the_number_it_was_first_given()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);
        var id = DocumentId.New();

        var first = await numbers.ForAsync(id);
        var again = await numbers.ForAsync(id);

        Assert.Equal(first.Value, again.Value);
    }

    /// <summary>
    /// The failure the derived number made permanent, as a property rather than an argument: every
    /// Document gets its own number, however many there are.
    /// </summary>
    [Fact]
    public async Task No_two_documents_are_given_the_same_number()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);

        var assigned = new List<int>();
        for (var i = 0; i < 500; i++)
            assigned.Add((await numbers.ForAsync(DocumentId.New())).Value);

        Assert.Equal(500, assigned.Distinct().Count());
    }

    /// <summary>
    /// Ids that fold to the same derived number — the exact pair pinned in
    /// <see cref="DerivedNotificationNumberTests"/> — are told apart now.
    /// </summary>
    [Fact]
    public async Task The_two_ids_that_used_to_collide_get_different_numbers()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);
        var one = new DocumentId(Guid.Parse("82692553-6eed-464a-b35d-c4b9645f10fe"));
        var other = new DocumentId(Guid.Parse("32fec112-db2c-4686-994a-995dce1916aa"));

        Assert.Equal(
            DerivedNotificationNumber.For(one),
            DerivedNotificationNumber.For(other)); // still true of the old mapping

        Assert.NotEqual(
            (await numbers.ForAsync(one)).Value,
            (await numbers.ForAsync(other)).Value);
    }

    /// <summary>Restart: a new connection over the same file, the same numbers.</summary>
    [Fact]
    public async Task Numbers_survive_a_restart()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var first = DocumentId.New();
        var second = DocumentId.New();
        var before = new SqliteNotificationNumbers(db.Database);
        var firstNumber = (await before.ForAsync(first)).Value;
        var secondNumber = (await before.ForAsync(second)).Value;

        await using var reopened = await db.ReopenAsync();
        var after = new SqliteNotificationNumbers(reopened.Database);

        Assert.Equal(firstNumber, (await after.ForAsync(first)).Value);
        Assert.Equal(secondNumber, (await after.ForAsync(second)).Value);
    }

    /// <summary>
    /// Editing a Document rewrites its whole row through the upsert. The number is in its own table
    /// precisely so that write cannot touch it — an edit that moved a Reminder to a fresh number
    /// would strand the notification standing under the old one.
    /// </summary>
    [Fact]
    public async Task Editing_a_document_leaves_its_number_alone()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var numbers = new SqliteNotificationNumbers(db.Database);
        var document = Document.Create(
            "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth, DocumentOwner.Me);
        await repository.SaveAsync(document);
        var assigned = (await numbers.ForAsync(document.Id)).Value;

        await repository.SaveAsync(document.Edit(
            "Passport (renewed)", new DateOnly(2032, 3, 14), RemindBefore.ThreeMonths,
            DocumentOwner.Me));

        Assert.Equal(assigned, (await numbers.ForAsync(document.Id)).Value);
    }

    /// <summary>
    /// A number is never handed out twice, even after the Document that held it is gone: its row
    /// stays. Reuse would let a Document inherit a pending notification from a deleted one.
    /// </summary>
    [Fact]
    public async Task A_deleted_documents_number_is_not_handed_to_the_next_one()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var repository = new SqliteDocumentRepository(db.Database);
        var numbers = new SqliteNotificationNumbers(db.Database);
        var document = Document.Create(
            "Passport", new DateOnly(2027, 3, 14), RemindBefore.OneMonth, DocumentOwner.Me);
        await repository.SaveAsync(document);
        var assigned = (await numbers.ForAsync(document.Id)).Value;

        await repository.RemoveAsync(document.Id);

        Assert.NotEqual(assigned, (await numbers.ForAsync(DocumentId.New())).Value);
    }

    /// <summary>
    /// The upgrade path. The first look-up after this change names the number the Document was
    /// scheduled under before it, so the adapter can clear that stale notification; no later
    /// look-up does, because by then there is nothing left standing under it.
    /// </summary>
    [Fact]
    public async Task The_first_lookup_names_the_derived_number_it_replaces_and_no_later_one_does()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);
        var id = DocumentId.New();

        var first = await numbers.ForAsync(id);
        var second = await numbers.ForAsync(id);

        Assert.Equal(DerivedNotificationNumber.For(id), first.Superseded);
        Assert.Null(second.Superseded);
    }

    /// <summary>Not even across a restart: the row is what says the clearing already happened.</summary>
    [Fact]
    public async Task A_restart_does_not_re_report_a_superseded_number()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var id = DocumentId.New();
        await new SqliteNotificationNumbers(db.Database).ForAsync(id);

        await using var reopened = await db.ReopenAsync();

        Assert.Null((await new SqliteNotificationNumbers(reopened.Database).ForAsync(id)).Superseded);
    }

    /// <summary>
    /// A database written before this change has no number table; opening it creates one and every
    /// Document already in there is served normally (spec §5.5 — sqlite-net adds what is missing).
    /// </summary>
    [Fact]
    public async Task Documents_from_a_database_written_before_this_change_get_numbers()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.Database.Connection.ExecuteAsync("DROP TABLE NotificationNumberRow");
        var existing = Guid.NewGuid();
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO DocumentRow (Id, Name, ExpiryDate, RemindBeforeDays, Note, CountryCode, OwnerId) " +
            "VALUES (?, ?, ?, ?, NULL, NULL, NULL)",
            existing, "Passport", "2027-03-14", 30);

        await using var reopened = await db.ReopenAsync(); // startup runs InitializeAsync again
        var repository = new SqliteDocumentRepository(reopened.Database);
        var numbers = new SqliteNotificationNumbers(reopened.Database);

        var document = Assert.Single(await repository.GetAllAsync());
        var number = await numbers.ForAsync(document.Id);
        Assert.Equal(1, number.Value);
        Assert.Equal(DerivedNotificationNumber.For(document.Id), number.Superseded);
    }

    /// <summary>
    /// An empty id is refused rather than served, as in the repositories: it would hand every
    /// defaulted Document the same number, which is the collision this class exists to prevent
    /// (spec §5.1, §9.1).
    /// </summary>
    [Fact]
    public async Task An_empty_id_is_refused()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);

        await Assert.ThrowsAsync<ArgumentException>(() => numbers.ForAsync(default));
    }

    /// <summary>
    /// Concurrent first look-ups are the way two Documents could still end up sharing a number:
    /// both read the same maximum, both insert. They are serialized, and the unique column would
    /// throw rather than let it pass silently.
    /// </summary>
    [Fact]
    public async Task Concurrent_first_lookups_still_produce_distinct_numbers()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        var numbers = new SqliteNotificationNumbers(db.Database);
        var ids = Enumerable.Range(0, 50).Select(_ => DocumentId.New()).ToList();

        var assigned = await Task.WhenAll(ids.Select(async id => (await numbers.ForAsync(id)).Value));

        Assert.Equal(50, assigned.Distinct().Count());
    }
}
