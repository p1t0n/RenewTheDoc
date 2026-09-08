using RenewTheDoc.Domain.Documents;
using SQLite;

namespace RenewTheDoc.Persistence.Documents;

/// <summary>The Owner dictionary over SQLite, in the same file as the Documents.</summary>
public sealed class SqliteOwnerRepository : IOwnerRepository
{
    private readonly SQLiteAsyncConnection _db;

    public SqliteOwnerRepository(SqliteDatabase database) => _db = database.Connection;

    /// <summary>Called by <see cref="SqliteDatabase.InitializeAsync"/>; the row type stays private.</summary>
    internal static Task CreateTablesAsync(SQLiteAsyncConnection connection) =>
        connection.CreateTableAsync<OwnerRow>();

    public async Task<Owner?> GetAsync(OwnerId id)
    {
        var row = await _db.FindAsync<OwnerRow>(Key(id));
        return row?.ToOwner();
    }

    /// <summary>By name, as the picker and the filter chips have always shown them.</summary>
    public async Task<IReadOnlyList<Owner>> GetAllAsync()
    {
        var rows = await _db.Table<OwnerRow>().ToListAsync();
        return rows.Select(r => r.ToOwner())
            .OrderBy(o => o.Name, StringComparer.CurrentCulture).ToList();
    }

    /// <summary>Upsert: the same call stores a brand-new Owner and an already-stored one.</summary>
    public Task SaveAsync(Owner owner)
    {
        _ = Key(owner.Id); // an aggregate carrying an empty id never reaches the table
        return _db.InsertOrReplaceAsync(OwnerRow.From(owner));
    }

    public Task RemoveAsync(OwnerId id) => _db.DeleteAsync<OwnerRow>(Key(id));

    /// <summary>Unwraps the id for sqlite-net, refusing the empty Guid the struct cannot refuse.</summary>
    private static Guid Key(OwnerId id) =>
        id.Value == Guid.Empty
            ? throw new ArgumentException("An Owner id cannot be empty.", nameof(id))
            : id.Value;

    // Fully settable properties: sqlite-net silently skips get-only and private-set ones (spec §5.3).
    // Table name pinned to the name the schema was first created under.
    [Table("OwnerRow")]
    private sealed class OwnerRow
    {
        [PrimaryKey]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public static OwnerRow From(Owner owner) => new() { Id = owner.Id.Value, Name = owner.Name };

        // Restore re-runs the Owner's invariants: a bad row throws rather than circulating (spec §5.2).
        public Owner ToOwner() => Owner.Restore(new OwnerId(Id), Name);
    }
}
