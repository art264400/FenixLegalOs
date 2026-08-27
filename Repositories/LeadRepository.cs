using System.Text.Json;
using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class LeadRepository
{
    private readonly DbInitializer _db;

    public LeadRepository(DbInitializer db)
    {
        _db = db;
    }

    private SqliteConnection GetConn()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        return conn;
    }

    public string CreateLead(Lead lead)
    {
        using var conn = GetConn();
        var id = string.IsNullOrEmpty(lead.Id) ? Guid.NewGuid().ToString() : lead.Id;
        var now = DateTime.UtcNow.ToString("o");

        conn.Execute(@"
            INSERT INTO leads (id, session_id, type, name, company, website, email, messenger,
                interest, source_risk_code, heat_score, heat_label, status, paid, paid_at, payment_amount, payment_method, created_at)
            VALUES (@id, @SessionId, @Type, @Name, @Company, @Website, @Email, @Messenger,
                @Interest, @SourceRiskCode, @HeatScore, @HeatLabel, 'new', @Paid, @PaidAt, @PaymentAmount, @PaymentMethod, @now)
        ", new
        {
            id, lead.SessionId, lead.Type, lead.Name, lead.Company, lead.Website, lead.Email,
            lead.Messenger, lead.Interest, lead.SourceRiskCode, lead.HeatScore, lead.HeatLabel,
            lead.Paid, lead.PaidAt, lead.PaymentAmount, lead.PaymentMethod, now
        });

        conn.Execute("INSERT INTO lead_status_history (id, lead_id, status, created_at) VALUES (@histId, @id, 'new', @now)", new { histId = Guid.NewGuid().ToString(), id, now });
        return id;
    }

    public IEnumerable<dynamic> ListLeads()
    {
        using var conn = GetConn();
        return conn.Query(@"
            SELECT 
                l.id AS Id, 
                l.name AS Name, 
                l.company AS Company, 
                l.email AS Email,
                l.messenger AS Messenger, 
                l.type AS Type, 
                l.interest AS Interest,
                l.heat_score AS HeatScore, 
                l.heat_label AS HeatLabel, 
                l.status AS Status,
                l.paid AS Paid, 
                l.paid_at AS PaidAt, 
                l.payment_amount AS PaymentAmount, 
                l.payment_method AS PaymentMethod,
                l.created_at AS CreatedAt, 
                s.result AS SessionResult
            FROM leads l 
            LEFT JOIN sessions s ON s.id = l.session_id

            UNION ALL

            SELECT 
                'session_' || s.id AS Id,
                'Сессия ' || SUBSTR(s.id, 1, 8) AS Name,
                '' AS Company,
                '— (контакт не оставлен)' AS Email,
                '' AS Messenger,
                CASE WHEN s.completed_at IS NOT NULL THEN 'completed_audit' ELSE 'in_progress' END AS Type,
                '' AS Interest,
                CASE WHEN s.completed_at IS NOT NULL THEN 60 ELSE 30 END AS HeatScore,
                CASE WHEN s.completed_at IS NOT NULL THEN 'warm' ELSE 'cold' END AS HeatLabel,
                'new' AS Status,
                s.paid AS Paid,
                s.paid_at AS PaidAt,
                s.payment_amount AS PaymentAmount,
                s.payment_method AS PaymentMethod,
                s.created_at AS CreatedAt,
                s.result AS SessionResult
            FROM sessions s
            WHERE s.id NOT IN (SELECT session_id FROM leads WHERE session_id IS NOT NULL)
              AND (s.completed_at IS NOT NULL OR LENGTH(s.answers) > 2)

            ORDER BY CreatedAt DESC
        ");
    }

    public dynamic? GetLead(string id)
    {
        using var conn = GetConn();
        if (id.StartsWith("session_"))
        {
            var sessId = id.Substring("session_".Length);
            return conn.QuerySingleOrDefault(@"
                SELECT 
                    'session_' || s.id AS Id, 
                    s.id AS SessionId, 
                    'completed_audit' AS Type, 
                    'Сессия ' || SUBSTR(s.id, 1, 8) AS Name, 
                    '' AS Company,
                    '' AS Website, 
                    '— (контакт не оставлен)' AS Email, 
                    '' AS Messenger, 
                    '' AS Interest,
                    '' AS SourceRiskCode, 
                    60 AS HeatScore, 
                    'warm' AS HeatLabel,
                    'new' AS Status, 
                    s.paid AS Paid, 
                    s.paid_at AS PaidAt, 
                    s.payment_amount AS PaymentAmount,
                    s.payment_method AS PaymentMethod, 
                    s.created_at AS CreatedAt,
                    s.answers AS SessionAnswers, 
                    s.result AS SessionResult, 
                    s.created_at AS SessionCreatedAt
                FROM sessions s
                WHERE s.id = @sessId
            ", new { sessId });
        }

        return conn.QuerySingleOrDefault(@"
            SELECT l.id AS Id, l.session_id AS SessionId, l.type AS Type, l.name AS Name, l.company AS Company,
                   l.website AS Website, l.email AS Email, l.messenger AS Messenger, l.interest AS Interest,
                   l.source_risk_code AS SourceRiskCode, l.heat_score AS HeatScore, l.heat_label AS HeatLabel,
                   l.status AS Status, l.paid AS Paid, l.paid_at AS PaidAt, l.payment_amount AS PaymentAmount,
                   l.payment_method AS PaymentMethod, l.created_at AS CreatedAt,
                   s.answers AS SessionAnswers, s.result AS SessionResult, s.created_at AS SessionCreatedAt
            FROM leads l LEFT JOIN sessions s ON s.id = l.session_id
            WHERE l.id = @id
        ", new { id });
    }

    public bool UpdateStatus(string id, string status)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int rows = conn.Execute("UPDATE leads SET status = @status WHERE id = @id", new { status, id });
        if (rows > 0)
        {
            conn.Execute("INSERT INTO lead_status_history (id, lead_id, status, created_at) VALUES (@histId, @id, @status, @now)", new { histId = Guid.NewGuid().ToString(), id, status, now });
        }
        return rows > 0;
    }

    public void AddNote(string leadId, string note)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        conn.Execute("INSERT INTO lead_notes (id, lead_id, note, created_at) VALUES (@id, @leadId, @note, @now)", new { id = Guid.NewGuid().ToString(), leadId, note, now });
    }

    public IEnumerable<dynamic> GetLeadNotes(string leadId)
    {
        using var conn = GetConn();
        return conn.Query("SELECT id AS Id, note AS Note, created_at AS CreatedAt FROM lead_notes WHERE lead_id = @leadId ORDER BY created_at DESC", new { leadId });
    }

    public IEnumerable<dynamic> FindLeadsBySession(string sessionId)
    {
        using var conn = GetConn();
        return conn.Query("SELECT * FROM leads WHERE session_id = @sessionId ORDER BY created_at", new { sessionId });
    }

    public void RecordEvent(string name, string? sessionId, object? payload)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        var payloadJson = payload != null ? JsonSerializer.Serialize(payload) : null;
        conn.Execute("INSERT INTO events (id, session_id, name, payload, created_at) VALUES (@id, @sessionId, @name, @payloadJson, @now)", new { id = Guid.NewGuid().ToString(), sessionId, name, payloadJson, now });
    }

    public void AuditLog(string actor, string action, string? detail)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        conn.Execute("INSERT INTO audit_log (id, actor, action, detail, created_at) VALUES (@id, @actor, @action, @detail, @now)", new { id = Guid.NewGuid().ToString(), actor, action, detail, now });
    }

    public dynamic GetOverviewStats()
    {
        using var conn = GetConn();
        int started = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sessions");
        int completed = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sessions WHERE completed_at IS NOT NULL");
        int leads = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM leads");
        int hot = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM leads WHERE heat_label IN ('hot','priority')");
        int consultations = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM leads WHERE type = 'consultation'");
        int paidSessions = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM sessions WHERE paid = 1");
        int totalRevenue = conn.ExecuteScalar<int?>("SELECT SUM(payment_amount) FROM sessions WHERE paid = 1") ?? 0;

        return new
        {
            diagnosticsStarted = started,
            diagnosticsCompleted = completed,
            leadsCaptured = leads,
            hotLeads = hot,
            consultationRequests = consultations,
            paidSessions = paidSessions,
            totalRevenue = totalRevenue
        };
    }
}
