using System.Text.Json;
using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class SessionRepository
{
    private readonly DbInitializer _db;

    public SessionRepository(DbInitializer db)
    {
        _db = db;
    }

    private SqliteConnection GetConn()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        return conn;
    }

    public string CreateSession()
    {
        using var conn = GetConn();
        var id = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow.ToString("o");
        conn.Execute("INSERT INTO sessions (id, created_at, updated_at) VALUES (@id, @now, @now)", new { id, now });
        return id;
    }

    public DiagnosticSession? GetSession(string id)
    {
        using var conn = GetConn();
        var session = conn.QuerySingleOrDefault<DiagnosticSession>("SELECT id AS Id, created_at AS CreatedAt, updated_at AS UpdatedAt, answers AS AnswersJson, last_section_id AS LastSectionId, completed_at AS CompletedAt, result AS ResultJson, paid AS Paid, paid_at AS PaidAt, payment_amount AS PaymentAmount, payment_method AS PaymentMethod FROM sessions WHERE id = @id", new { id });
        return session;
    }

    public bool SaveAnswers(string id, string answersJson, string? lastSectionId)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int rows = conn.Execute(@"
            INSERT INTO sessions (id, created_at, updated_at, answers, last_section_id)
            VALUES (@id, @now, @now, @answersJson, @lastSectionId)
            ON CONFLICT(id) DO UPDATE SET
                answers = excluded.answers,
                last_section_id = excluded.last_section_id,
                updated_at = excluded.updated_at;
        ", new { answersJson, lastSectionId, now, id });
        return rows > 0;
    }

    public void CompleteSession(string id, string answersJson, ScoreResult result)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        var resultJson = JsonSerializer.Serialize(result);
        conn.Execute(@"
            INSERT INTO sessions (id, created_at, updated_at, answers, result, completed_at, qb_version, engine_version, risk_version)
            VALUES (@id, @now, @now, @answersJson, @resultJson, @now, @qb, @eng, @risk)
            ON CONFLICT(id) DO UPDATE SET
                answers = excluded.answers,
                result = excluded.result,
                completed_at = excluded.completed_at,
                updated_at = excluded.updated_at,
                qb_version = excluded.qb_version,
                engine_version = excluded.engine_version,
                risk_version = excluded.risk_version;
        ", new
        {
            answersJson, resultJson, now, id,
            qb = result.Versions.QuestionBank,
            eng = result.Versions.ScoringEngine,
            risk = result.Versions.RiskLibrary
        });
    }

    public bool MarkSessionPaid(string id, int amount, string method)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int sRows = conn.Execute("UPDATE sessions SET paid = 1, paid_at = @now, payment_amount = @amount, payment_method = @method WHERE id = @id", new { now, amount, method, id });
        conn.Execute("UPDATE leads SET paid = 1, paid_at = @now, payment_amount = @amount, payment_method = @method WHERE session_id = @id", new { now, amount, method, id });
        return sRows > 0;
    }
}
