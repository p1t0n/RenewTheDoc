using RenewTheDoc.Core;
using SQLite;

namespace RenewTheDoc.App.Services;

public sealed class SqliteDocumentStore : IDocumentStore, IOwnerStore
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

    public async Task UpdateAsync(Document document, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _db.UpdateAsync(DocumentRow.From(document));
    }

    public async Task DeleteAsync(Guid documentId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _db.DeleteAsync<DocumentRow>(documentId);
    }

    async Task<IReadOnlyList<Owner>> IOwnerStore.GetAllAsync(CancellationToken ct)
    {
        await EnsureInitializedAsync();
        var rows = await _db.Table<OwnerRow>().ToListAsync();
        return rows.Select(r => new Owner { Id = r.Id, Name = r.Name })
            .OrderBy(o => o.Name, StringComparer.CurrentCulture).ToList();
    }

    public async Task AddAsync(Owner owner, CancellationToken ct = default)
    {
        await EnsureInitializedAsync();
        await _db.InsertAsync(new OwnerRow { Id = owner.Id, Name = owner.Name });
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        await _db.CreateTableAsync<DocumentRow>();
        await _db.CreateTableAsync<OwnerRow>();
        _initialized = true;
    }

    private sealed class OwnerRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

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

        public static DocumentRow From(Document d) => new()
        {
            Id = d.Id,
            Name = d.Name,
            ExpiryDate = d.ExpiryDate.ToString("O"),
            RemindBeforeDays = d.RemindBefore.Days,
            Note = d.Note,
            CountryCode = d.CountryCode,
            OwnerId = d.OwnerId,
        };

        public Document ToDocument() => new()
        {
            Id = Id,
            Name = Name,
            ExpiryDate = DateOnly.Parse(ExpiryDate),
            RemindBefore = new RemindBefore(RemindBeforeDays),
            Note = Note,
            CountryCode = CountryCode,
            OwnerId = OwnerId,
        };
    }
}
