using SQLite;

namespace RenewTheDoc.Persistence.Tests;

/// <summary>
/// The load-bearing test of this layer. sqlite-net silently skips get-only and <c>private set</c>
/// properties: a row type mixing them produces a table with missing columns, no exception and no
/// warning, and the data simply vanishes (REN-53, spec §5.3). That failure is invisible in code
/// review, so these tests read the real schema back out of SQLite and assert it.
/// </summary>
public class SchemaGuardTests
{
    [Fact]
    public async Task The_document_table_has_exactly_the_columns_the_row_declares()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var columns = await db.ColumnsOfAsync("DocumentRow");

        Assert.Equal(
            ["Id", "Name", "ExpiryDate", "RemindBeforeDays", "Note", "CountryCode", "OwnerId"],
            columns);
    }

    [Fact]
    public async Task The_owner_table_has_exactly_the_columns_the_row_declares()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var columns = await db.ColumnsOfAsync("OwnerRow");

        Assert.Equal(["Id", "Name"], columns);
    }

    /// <summary>
    /// The notification number lives in its own table rather than as a column on
    /// <c>DocumentRow</c> — the repository upserts a whole aggregate, and the aggregate has no
    /// number to write back (REN-54).
    /// </summary>
    [Fact]
    public async Task The_notification_number_table_has_exactly_the_columns_the_row_declares()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var columns = await db.ColumnsOfAsync("NotificationNumberRow");

        Assert.Equal(["DocumentId", "Number"], columns);
    }

    /// <summary>The unique index is the guard that a number can never be handed out twice.</summary>
    [Fact]
    public async Task The_notification_number_column_is_unique_in_the_database_itself()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();
        await db.Database.Connection.ExecuteAsync(
            "INSERT INTO NotificationNumberRow (DocumentId, Number) VALUES (?, 1)", Guid.NewGuid());

        await Assert.ThrowsAsync<SQLiteException>(() => db.Database.Connection.ExecuteAsync(
            "INSERT INTO NotificationNumberRow (DocumentId, Number) VALUES (?, 1)", Guid.NewGuid()));
    }

    /// <summary>
    /// Table names are part of the schema too: renaming a row type would orphan every existing row
    /// while the app still built and started cleanly.
    /// </summary>
    [Fact]
    public async Task Initialization_creates_those_three_tables_and_nothing_else()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        var tables = await db.Database.Connection.QueryScalarsAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");

        Assert.Equal(["DocumentRow", "NotificationNumberRow", "OwnerRow"], tables);
    }

    /// <summary>
    /// The single concrete thing Cloud Sync asks of this refactor: its own tables, in the same file,
    /// created through the shared connection after the Documents initializer has run (spec §9.6).
    /// </summary>
    [Fact]
    public async Task A_second_context_can_create_its_own_tables_in_the_same_database()
    {
        await using var db = await SqliteTestDatabase.CreateAsync();

        await db.Database.Connection.CreateTableAsync<OtherContextRow>();

        Assert.Equal(["Id", "Revision"], await db.ColumnsOfAsync("OtherContextRow"));
        var tables = await db.Database.Connection.QueryScalarsAsync<string>(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
        Assert.Equal(
            ["DocumentRow", "NotificationNumberRow", "OtherContextRow", "OwnerRow"], tables);
    }

    /// <summary>Stands in for a Cloud Sync row: a foreign context's table, not the Documents context's.</summary>
    private sealed class OtherContextRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public long Revision { get; set; }
    }
}
