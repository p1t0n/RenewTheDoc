using RenewTheDoc.Persistence.Documents;
using SQLite;

namespace RenewTheDoc.Persistence;

/// <summary>
/// Owns the one SQLite connection for the whole app and creates the Documents context's tables
/// once, at startup.
/// </summary>
/// <remarks>
/// Replaces the per-call <c>EnsureInitializedAsync</c> guard that used to sit in front of every
/// store method: six guards, and two concurrent first calls could both run the table creation
/// (spec §5.4). <see cref="InitializeAsync"/> is awaited once during startup instead, before any
/// page appears, so the repositories can just query.
/// </remarks>
public sealed class SqliteDatabase
{
    public SqliteDatabase(string databasePath) => Connection = new SQLiteAsyncConnection(databasePath);

    /// <summary>
    /// The shared connection. Exposed so a second bounded context — Cloud Sync, later — can create
    /// its own tables in the same file, after this initializer has run (spec §9.6).
    /// </summary>
    public SQLiteAsyncConnection Connection { get; }

    /// <summary>Creates the Documents context's tables. Idempotent, but meant to be called once.</summary>
    /// <remarks>
    /// <c>ConfigureAwait(false)</c> is load-bearing, not decoration. Startup calls this from the UI
    /// thread and has to wait for it, so a continuation posted back to that thread would never run:
    /// verified on an Android emulator, where the app froze on the splash screen with the first
    /// table created and the second one not.
    /// </remarks>
    public async Task InitializeAsync()
    {
        await SqliteDocumentRepository.CreateTablesAsync(Connection).ConfigureAwait(false);
        await SqliteOwnerRepository.CreateTablesAsync(Connection).ConfigureAwait(false);
    }
}
