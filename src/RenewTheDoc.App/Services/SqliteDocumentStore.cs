using RenewTheDoc.Core;
using SQLite;

namespace RenewTheDoc.App.Services;

public sealed class SqliteDocumentStore : IDocumentStore
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public SqliteDocumentStore(string dbPath) => _db = new SQLiteAsyncConnection(dbPath);

    public async Task<IReadOnlyList<Document>> GetAllAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        var rows = await _db.Table<DocumentRow>().ToListAsync();
        return rows.Select(r => r.ToDocument()).ToList();
    }

    public async Task AddAsync(Document document, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(DocumentRow.From(document));
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<DocumentRow>();
        _initialized = true;
    }

    private sealed class DocumentRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ExpiryDate { get; set; } = string.Empty; // ISO yyyy-MM-dd
        public int RemindBeforeDays { get; set; }
        public string? Note { get; set; }

        public static DocumentRow From(Document d) => new()
        {
            Id = d.Id,
            Name = d.Name,
            ExpiryDate = d.ExpiryDate.ToString("O"),
            RemindBeforeDays = d.RemindBefore.Days,
            Note = d.Note,
        };

        public Document ToDocument() => new()
        {
            Id = Id,
            Name = Name,
            ExpiryDate = DateOnly.Parse(ExpiryDate),
            RemindBefore = new RemindBefore(RemindBeforeDays),
            Note = Note,
        };
    }
}
