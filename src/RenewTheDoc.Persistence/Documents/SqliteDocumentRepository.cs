using RenewTheDoc.Domain.Documents;
using SQLite;

namespace RenewTheDoc.Persistence.Documents;

/// <summary>
/// The Document aggregate over SQLite. Serves aggregates, not rows: everything crossing this
/// boundary is a <see cref="Document"/>, and the row type never leaves the class.
/// </summary>
public sealed class SqliteDocumentRepository : IDocumentRepository
{
    private readonly SQLiteAsyncConnection _db;

    public SqliteDocumentRepository(SqliteDatabase database) => _db = database.Connection;

    /// <summary>Called by <see cref="SqliteDatabase.InitializeAsync"/>; the row type stays private.</summary>
    internal static Task CreateTablesAsync(SQLiteAsyncConnection connection) =>
        connection.CreateTableAsync<DocumentRow>();

    public async Task<Document?> GetAsync(DocumentId id)
    {
        // .Value because sqlite-net binds the primary key through a type switch over known types.
        var row = await _db.FindAsync<DocumentRow>(Key(id));
        return row?.ToDocument();
    }

    public async Task<IReadOnlyList<Document>> GetAllAsync()
    {
        var rows = await _db.Table<DocumentRow>().ToListAsync();
        return rows.Select(r => r.ToDocument()).ToList();
    }

    /// <summary>Upsert: the same call stores a brand-new Document and an edited one.</summary>
    public Task SaveAsync(Document document)
    {
        _ = Key(document.Id); // an aggregate carrying an empty id never reaches the table
        return _db.InsertOrReplaceAsync(DocumentRow.From(document));
    }

    public Task RemoveAsync(DocumentId id) => _db.DeleteAsync<DocumentRow>(Key(id));

    /// <summary>
    /// Unwraps the id for sqlite-net, refusing an empty Guid on the way out. <c>default(DocumentId)</c>
    /// is a value the struct itself cannot refuse, and an empty id would silently address the wrong
    /// row — or none (spec §5.1, §9.1).
    /// </summary>
    private static Guid Key(DocumentId id) =>
        id.Value == Guid.Empty
            ? throw new ArgumentException("A Document id cannot be empty.", nameof(id))
            : id.Value;

    // Fully settable properties, deliberately: sqlite-net silently skips get-only and private-set
    // ones, producing a table with missing columns and no error at all (spec §5.3). The table name
    // is pinned to the type name the schema was first created under — renaming it would orphan
    // every existing row.
    [Table("DocumentRow")]
    private sealed class DocumentRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty; // ISO yyyy-MM-dd
        public int RemindBeforeDays { get; set; }
        public string? Note { get; set; }
        public string? CountryCode { get; set; }
        public Guid? OwnerId { get; set; }

        // The row is where typed ids unwrap: the columns are plain Guids. It is also the one place
        // Me collapses to a null column — the schema is unchanged by the modelling move (spec §5.5).
        public static DocumentRow From(Document d) => new()
        {
            Id = d.Id.Value,
            Name = d.Name,
            ExpiryDate = d.ExpiryDate.ToString("O"),
            RemindBeforeDays = d.RemindBefore.Days,
            Note = d.Note,
            CountryCode = d.Country?.Code,
            OwnerId = d.Owner is DocumentOwner.Person person ? person.Id.Value : null,
        };

        // Restore, not a constructor: a row that breaks an invariant must fail loud rather than
        // become an invalid aggregate. Nothing here bypasses construction (spec §5.2).
        public Document ToDocument() => Document.Restore(
            new DocumentId(Id),
            Name,
            DateOnly.Parse(ExpiryDate),
            new RemindBefore(RemindBeforeDays),
            OwnerId is { } ownerId
                ? new DocumentOwner.Person(new OwnerId(ownerId))
                : DocumentOwner.Me,
            Note,
            Country.OfNullable(CountryCode));
    }
}
