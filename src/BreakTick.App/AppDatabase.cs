using System.IO;
using Microsoft.Data.Sqlite;

namespace BreakTick.App;

public sealed class AppDatabase
{
    private readonly string _connectionString;

    public AppDatabase()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BreakTick");
        Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(directory, "data.db"),
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        Initialize();
    }

    public long StartSession(AppSettings settings)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO sessions (date, started_at, work_minutes, break_seconds)
            VALUES ($date, $startedAt, $workMinutes, $breakSeconds);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("$date", Today());
        command.Parameters.AddWithValue("$startedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$workMinutes", settings.WorkMinutes);
        command.Parameters.AddWithValue("$breakSeconds", settings.BreakSeconds);
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public void MarkBreakStarted(long sessionId) => UpdateSession(sessionId, "break_started_at = $now");

    public int CompleteBreak(long sessionId)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        using (var session = connection.CreateCommand())
        {
            session.Transaction = transaction;
            session.CommandText = "UPDATE sessions SET break_ended_at = $now, ended_at = $now, completed = 1 WHERE id = $sessionId;";
            session.Parameters.AddWithValue("$now", now);
            session.Parameters.AddWithValue("$sessionId", sessionId);
            session.ExecuteNonQuery();
        }

        using (var completion = connection.CreateCommand())
        {
            completion.Transaction = transaction;
            completion.CommandText = "INSERT OR IGNORE INTO completions (session_id, completed_at, date) VALUES ($sessionId, $now, $date);";
            completion.Parameters.AddWithValue("$sessionId", sessionId);
            completion.Parameters.AddWithValue("$now", now);
            completion.Parameters.AddWithValue("$date", Today());
            completion.ExecuteNonQuery();
        }

        transaction.Commit();
        return GetTodayCompletionCount();
    }

    public void MarkSkipped(long sessionId) => UpdateSession(sessionId, "ended_at = $now, skipped = 1");

    public void EndSession(long sessionId) => UpdateSession(sessionId, "ended_at = $now");

    public int GetTodayCompletionCount()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM completions WHERE date = $date;";
        command.Parameters.AddWithValue("$date", Today());
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                date TEXT NOT NULL,
                started_at TEXT NOT NULL,
                break_started_at TEXT,
                break_ended_at TEXT,
                ended_at TEXT,
                work_minutes INTEGER NOT NULL,
                break_seconds INTEGER NOT NULL,
                completed INTEGER NOT NULL DEFAULT 0,
                skipped INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS completions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL UNIQUE,
                completed_at TEXT NOT NULL,
                date TEXT NOT NULL,
                FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE INDEX IF NOT EXISTS idx_completions_date ON completions(date);
            """;
        command.ExecuteNonQuery();
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void UpdateSession(long sessionId, string setClause)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE sessions SET {setClause} WHERE id = $sessionId;";
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static string Today() => DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
}
