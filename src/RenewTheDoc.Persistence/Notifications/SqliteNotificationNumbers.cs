using RenewTheDoc.Domain.Documents;
using SQLite;

namespace RenewTheDoc.Persistence.Notifications;

/// <summary>
/// The int Plugin.LocalNotification schedules under, one per Document: handed out once, stored, and
/// never handed out again — replacing the lossy fold of the Document's id (REN-54,
/// <see cref="DerivedNotificationNumber"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own table, not a column on <c>DocumentRow</c>.</b> The repository upserts a whole
/// <see cref="Document"/>, and the aggregate has no notification number to carry (spec §3.1, §4.1 —
/// nothing reminder-shaped lives in it), so a column on that row would be overwritten with a default
/// on every edit. Keeping it out also keeps it out of what Cloud Sync mirrors: the number is a
/// device's own platform bookkeeping, meaningless on another device, and REN-29 diffs
/// <c>GetAllAsync</c> (§9.2). sqlite-net creates the extra table on <c>CreateTable</c>, so no
/// migration framework is needed and an existing database keeps its rows untouched.
/// </para>
/// <para>
/// Numbers are the count so far plus one, and rows outlive the Document they belong to, so a number
/// is never reused: a delete that raced a pending notification cannot hand the slot to a Document
/// added afterwards. A restored Document — same id, back from a cloud mirror — gets its old number
/// back for the same reason.
/// </para>
/// <para>
/// Assignment is serialized by a semaphore. Two concurrent first look-ups would otherwise read the
/// same maximum and insert the same number; the <see cref="UniqueAttribute"/> on the column is the
/// second line of defence, turning any such bug into an exception instead of the silent collision
/// this class exists to end.
/// </para>
/// </remarks>
public sealed class SqliteNotificationNumbers
{
    private readonly SQLiteAsyncConnection _db;
    private readonly SemaphoreSlim _assigning = new(1, 1);

    public SqliteNotificationNumbers(SqliteDatabase database) => _db = database.Connection;

    /// <summary>Called by <see cref="SqliteDatabase.InitializeAsync"/>; the row type stays private.</summary>
    internal static Task CreateTablesAsync(SQLiteAsyncConnection connection) =>
        connection.CreateTableAsync<NotificationNumberRow>();

    /// <summary>
    /// This Document's number, assigning one the first time it is asked for. The answer also names
    /// the derived number the Document used to schedule under, but only on that first call — see
    /// <see cref="NotificationNumber.Superseded"/>.
    /// </summary>
    public async Task<NotificationNumber> ForAsync(DocumentId documentId)
    {
        var key = Key(documentId);

        var known = await _db.FindAsync<NotificationNumberRow>(key);
        if (known is not null) return new NotificationNumber(known.Number, Superseded: null);

        await _assigning.WaitAsync();
        try
        {
            // Re-read inside the gate: another caller may have assigned while this one waited.
            known = await _db.FindAsync<NotificationNumberRow>(key);
            if (known is not null) return new NotificationNumber(known.Number, Superseded: null);

            var highest = await _db.ExecuteScalarAsync<int>(
                "SELECT COALESCE(MAX(Number), 0) FROM NotificationNumberRow");
            var assigned = highest + 1;
            await _db.InsertAsync(new NotificationNumberRow { DocumentId = key, Number = assigned });

            return new NotificationNumber(assigned, DerivedNotificationNumber.For(documentId));
        }
        finally
        {
            _assigning.Release();
        }
    }

    /// <summary>
    /// Refuses an empty Guid on the way out, exactly as the repositories do: an empty id addresses
    /// whatever row it happens to hit, and here that would mean two Documents sharing a number
    /// (spec §5.1, §9.1).
    /// </summary>
    private static Guid Key(DocumentId documentId) =>
        documentId.Value == Guid.Empty
            ? throw new ArgumentException("A Document id cannot be empty.", nameof(documentId))
            : documentId.Value;

    // Fully settable properties, as everywhere in this layer: sqlite-net silently skips get-only and
    // private-set ones and creates the table without those columns (spec §5.3).
    [Table("NotificationNumberRow")]
    private sealed class NotificationNumberRow
    {
        [PrimaryKey]
        public Guid DocumentId { get; set; }

        [Unique]
        public int Number { get; set; }
    }
}

/// <summary>The number a Document's notification is scheduled under.</summary>
/// <param name="Value">What to pass to the platform.</param>
/// <param name="Superseded">
/// The number this Document was scheduled under by a build from before REN-54, or <c>null</c>. Only
/// the call that assigns <paramref name="Value"/> reports it, because only that call can be the
/// first one after the upgrade. A notification standing under it belongs to this Document but can no
/// longer be cancelled by id, so the caller must clear it — otherwise an existing install fires that
/// Reminder twice and keeps firing the stale one after the Document is deleted.
/// </param>
public readonly record struct NotificationNumber(int Value, int? Superseded);
