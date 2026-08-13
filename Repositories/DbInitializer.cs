using Dapper;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class DbInitializer
{
    private readonly string _connectionString;

    public DbInitializer(IConfiguration config)
    {
        var dbPath = config["FENIX_DB_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "fenix.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public string ConnectionString => _connectionString;

    public void Initialize()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        conn.Execute("PRAGMA journal_mode = WAL;");

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS sessions (
                id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                updated_at TEXT NOT NULL,
                answers TEXT NOT NULL DEFAULT '{}',
                last_section_id TEXT,
                completed_at TEXT,
                result TEXT,
                qb_version TEXT,
                engine_version TEXT,
                risk_version TEXT,
                paid INTEGER NOT NULL DEFAULT 0,
                paid_at TEXT,
                payment_amount INTEGER,
                payment_method TEXT
            );

            CREATE TABLE IF NOT EXISTS leads (
                id TEXT PRIMARY KEY,
                session_id TEXT NOT NULL,
                type TEXT NOT NULL,
                name TEXT NOT NULL,
                company TEXT,
                website TEXT,
                email TEXT NOT NULL,
                messenger TEXT,
                interest TEXT,
                source_risk_code TEXT,
                heat_score INTEGER NOT NULL DEFAULT 0,
                heat_label TEXT NOT NULL DEFAULT 'cold',
                status TEXT NOT NULL DEFAULT 'new',
                paid INTEGER NOT NULL DEFAULT 0,
                paid_at TEXT,
                payment_amount INTEGER,
                payment_method TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lead_notes (
                id TEXT PRIMARY KEY,
                lead_id TEXT NOT NULL,
                note TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS lead_status_history (
                id TEXT PRIMARY KEY,
                lead_id TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS events (
                id TEXT PRIMARY KEY,
                session_id TEXT,
                name TEXT NOT NULL,
                payload TEXT,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS email_log (
                id TEXT PRIMARY KEY,
                to_addr TEXT NOT NULL,
                subject TEXT NOT NULL,
                body TEXT NOT NULL,
                created_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS audit_log (
                id TEXT PRIMARY KEY,
                actor TEXT NOT NULL,
                action TEXT NOT NULL,
                detail TEXT,
                created_at TEXT NOT NULL
            );
        ");

        // Safe migrations for extra columns
        TryAddColumn(conn, "sessions", "paid", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "sessions", "paid_at", "TEXT");
        TryAddColumn(conn, "sessions", "payment_amount", "INTEGER");
        TryAddColumn(conn, "sessions", "payment_method", "TEXT");

        TryAddColumn(conn, "leads", "paid", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "leads", "paid_at", "TEXT");
        TryAddColumn(conn, "leads", "payment_amount", "INTEGER");
        TryAddColumn(conn, "leads", "payment_method", "TEXT");
    }

    private void TryAddColumn(SqliteConnection conn, string table, string column, string type)
    {
        try { conn.Execute($"ALTER TABLE {table} ADD COLUMN {column} {type};"); } catch { }
    }
}
