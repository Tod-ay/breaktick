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

    public DashboardStats GetDashboardStats()
    {
        using var connection = OpenConnection();
        using var totalCommand = connection.CreateCommand();
        totalCommand.CommandText = "SELECT COUNT(*) FROM completions;";
        var total = Convert.ToInt32(totalCommand.ExecuteScalar());

        using var datesCommand = connection.CreateCommand();
        datesCommand.CommandText = "SELECT DISTINCT date FROM completions ORDER BY date DESC;";
        var dates = new HashSet<DateOnly>();
        using (var reader = datesCommand.ExecuteReader())
        {
            while (reader.Read() && DateOnly.TryParse(reader.GetString(0), out var date))
            {
                dates.Add(date);
            }
        }

        var streak = 0;
        for (var date = DateOnly.FromDateTime(DateTime.Today); dates.Contains(date); date = date.AddDays(-1))
        {
            streak++;
        }

        var recentDays = new List<int>();
        for (var offset = 6; offset >= 0; offset--)
        {
            using var dayCommand = connection.CreateCommand();
            dayCommand.CommandText = "SELECT COUNT(*) FROM completions WHERE date = $date;";
            dayCommand.Parameters.AddWithValue("$date", DateOnly.FromDateTime(DateTime.Today.AddDays(-offset)).ToString("yyyy-MM-dd"));
            recentDays.Add(Convert.ToInt32(dayCommand.ExecuteScalar()));
        }

        return new DashboardStats(total, streak, recentDays);
    }

    public void SaveTimerSnapshot(TimerSnapshot snapshot)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO timer_state (id, phase, resume_phase, deadline, remaining_seconds, session_id, updated_at)
            VALUES (1, $phase, $resumePhase, $deadline, $remainingSeconds, $sessionId, $updatedAt)
            ON CONFLICT(id) DO UPDATE SET
                phase = excluded.phase,
                resume_phase = excluded.resume_phase,
                deadline = excluded.deadline,
                remaining_seconds = excluded.remaining_seconds,
                session_id = excluded.session_id,
                updated_at = excluded.updated_at;
            """;
        command.Parameters.AddWithValue("$phase", snapshot.Phase.ToString());
        command.Parameters.AddWithValue("$resumePhase", snapshot.ResumePhase?.ToString() ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$deadline", snapshot.Deadline?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$remainingSeconds", snapshot.Remaining.TotalSeconds);
        command.Parameters.AddWithValue("$sessionId", snapshot.SessionId ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }

    public TimerSnapshot? LoadTimerSnapshot()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT phase, resume_phase, deadline, remaining_seconds, session_id FROM timer_state WHERE id = 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read() || !Enum.TryParse<TimerPhase>(reader.GetString(0), out var phase))
        {
            return null;
        }

        TimerPhase? resumePhase = null;
        if (!reader.IsDBNull(1) && Enum.TryParse<TimerPhase>(reader.GetString(1), out var parsedResumePhase))
        {
            resumePhase = parsedResumePhase;
        }

        DateTimeOffset? deadline = null;
        if (!reader.IsDBNull(2) && DateTimeOffset.TryParse(reader.GetString(2), out var parsedDeadline))
        {
            deadline = parsedDeadline;
        }

        return new TimerSnapshot(
            phase,
            resumePhase,
            deadline,
            TimeSpan.FromSeconds(reader.GetDouble(3)),
            reader.IsDBNull(4) ? null : reader.GetInt64(4));
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
            CREATE TABLE IF NOT EXISTS timer_state (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                phase TEXT NOT NULL,
                resume_phase TEXT,
                deadline TEXT,
                remaining_seconds REAL NOT NULL,
                session_id INTEGER,
                updated_at TEXT NOT NULL
            );
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
