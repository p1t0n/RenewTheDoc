using RenewTheDoc.Persistence;

namespace RenewTheDoc.Persistence.Tests;

/// <summary>
/// A real SQLite file per test, initialized the way the app initializes it, deleted afterwards.
/// These tests run against sqlite-net itself — mapping and schema are exactly what a fake cannot
/// tell us (spec §7.1).
/// </summary>
public sealed class SqliteTestDatabase : IAsyncDisposable
{
    private readonly string _path;

    private SqliteTestDatabase(string path, SqliteDatabase database)
    {
        _path = path;
        Database = database;
    }

    public SqliteDatabase Database { get; }

    public static async Task<SqliteTestDatabase> CreateAsync()
    {
        var path = Path.Combine(Path.GetTempPath(), $"renewthedoc-tests-{Guid.NewGuid():N}.db3");
        var database = new SqliteDatabase(path);
        await database.InitializeAsync();
        return new SqliteTestDatabase(path, database);
    }

    /// <summary>
    /// The same file, closed and opened again through a fresh initializer — an app restart. Both
    /// handles are safe to dispose; the second delete of the file is a no-op.
    /// </summary>
    public async Task<SqliteTestDatabase> ReopenAsync()
    {
        await Database.Connection.CloseAsync();
        var database = new SqliteDatabase(_path);
        await database.InitializeAsync();
        return new SqliteTestDatabase(_path, database);
    }

    /// <summary>The column names of a table, in declaration order, straight out of SQLite.</summary>
    public async Task<IReadOnlyList<string>> ColumnsOfAsync(string table)
    {
        var columns = await Database.Connection.QueryAsync<TableInfoRow>($"PRAGMA table_info({table})");
        return columns.Select(c => c.Name).ToList();
    }

    public async ValueTask DisposeAsync()
    {
        await Database.Connection.CloseAsync();
        if (File.Exists(_path)) File.Delete(_path);
    }

    /// <summary>One row of <c>PRAGMA table_info</c>; sqlite-net matches the column names case-insensitively.</summary>
    private sealed class TableInfoRow
    {
        public string Name { get; set; } = string.Empty;
    }
}
