using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public sealed class ReportRevisionRepository
{
    private readonly DbInitializer _db;
    private const string SelectColumns = @"id AS Id, session_id AS SessionId, base_revision_id AS BaseRevisionId,
        number AS Number, version AS Version, status AS Status, answers AS AnswersJson,
        base_answers AS BaseAnswersJson, questions AS QuestionsJson, sections AS SectionsJson,
        bank_fingerprint AS BankFingerprint, reason AS Reason, created_by AS CreatedBy,
        created_at AS CreatedAt, updated_at AS UpdatedAt, published_at AS PublishedAt,
        result AS ResultJson, pdf_bytes AS PdfBytes, error AS Error";

    public ReportRevisionRepository(DbInitializer db) => _db = db;

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("PRAGMA busy_timeout = 10000;");
        return conn;
    }

    public ReportRevision? Get(string sessionId, string revisionId)
    {
        using var conn = Open();
        return conn.QuerySingleOrDefault<ReportRevision>($"SELECT {SelectColumns} FROM report_revisions WHERE session_id=@sessionId AND id=@revisionId", new { sessionId, revisionId });
    }

    public List<ReportRevision> List(string sessionId)
    {
        using var conn = Open();
        return conn.Query<ReportRevision>($"SELECT {SelectColumns} FROM report_revisions WHERE session_id=@sessionId ORDER BY number DESC", new { sessionId }).ToList();
    }

    public ReportRevision? GetDraft(string sessionId)
    {
        using var conn = Open();
        // A crashed generator cannot hold the editor forever. The version invalidates its late result.
        conn.Execute(@"UPDATE report_revisions SET status='failed', error='Генерация прервана. Повторите пересчёт.', version=version+1, updated_at=@now
            WHERE session_id=@sessionId AND status='generating' AND updated_at < @expiry",
            new { sessionId, now = DateTime.UtcNow.ToString("o"), expiry = DateTime.UtcNow.AddMinutes(-20).ToString("o") });
        return conn.QuerySingleOrDefault<ReportRevision>($"SELECT {SelectColumns} FROM report_revisions WHERE session_id=@sessionId AND status IN ('draft','generating','ready','failed')", new { sessionId });
    }

    public bool Create(ReportRevision revision)
    {
        using var conn = Open();
        try
        {
            return conn.Execute(@"INSERT INTO report_revisions
                (id,session_id,base_revision_id,number,version,status,answers,base_answers,questions,sections,bank_fingerprint,reason,created_by,created_at,updated_at)
                SELECT @Id,@SessionId,@BaseRevisionId,COALESCE((SELECT MAX(number) FROM report_revisions WHERE session_id=@SessionId),0)+1,
                    1,'draft',@AnswersJson,@BaseAnswersJson,@QuestionsJson,@SectionsJson,@BankFingerprint,@Reason,@CreatedBy,@CreatedAt,@UpdatedAt
                FROM sessions WHERE id=@SessionId AND completed_at IS NOT NULL AND published_revision_id IS @BaseRevisionId", revision) == 1;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) { return false; }
    }

    public bool Save(string sessionId, string revisionId, int version, string answersJson, string reason, string actor)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        int rows = conn.Execute(@"UPDATE report_revisions SET answers=@answersJson, reason=@reason, status='draft',
                result=NULL,pdf_bytes=NULL,error=NULL,version=version+1,updated_at=@now
            WHERE id=@revisionId AND session_id=@sessionId AND version=@version AND status IN ('draft','ready','failed')
                AND EXISTS (SELECT 1 FROM sessions WHERE id=@sessionId AND published_revision_id IS report_revisions.base_revision_id)",
            new { sessionId, revisionId, version, answersJson, reason, now = DateTime.UtcNow.ToString("o") }, tx);
        if (rows == 1) Audit(conn, tx, actor, "report_revision_saved", revisionId);
        tx.Commit();
        return rows == 1;
    }

    public bool BeginGeneration(string sessionId, string revisionId, int version)
    {
        using var conn = Open();
        return conn.Execute(@"UPDATE report_revisions SET status='generating',error=NULL,result=NULL,pdf_bytes=NULL,version=version+1,updated_at=@now
            WHERE id=@revisionId AND session_id=@sessionId AND version=@version AND status IN ('draft','failed','ready')
                AND EXISTS (SELECT 1 FROM sessions WHERE id=@sessionId AND published_revision_id IS report_revisions.base_revision_id)",
            new { sessionId, revisionId, version, now = DateTime.UtcNow.ToString("o") }) == 1;
    }

    public bool FinishGeneration(string sessionId, string revisionId, int version, string resultJson, byte[] pdfBytes)
    {
        if (pdfBytes.Length == 0 || string.IsNullOrWhiteSpace(resultJson)) return false;
        using var conn = Open();
        return conn.Execute(@"UPDATE report_revisions SET status='ready',result=@resultJson,pdf_bytes=@pdfBytes,error=NULL,version=version+1,updated_at=@now
            WHERE id=@revisionId AND session_id=@sessionId AND version=@version AND status='generating'",
            new { sessionId, revisionId, version, resultJson, pdfBytes, now = DateTime.UtcNow.ToString("o") }) == 1;
    }

    public void FailGeneration(string sessionId, string revisionId, int version)
    {
        using var conn = Open();
        conn.Execute(@"UPDATE report_revisions SET status='failed',error='Не удалось сформировать PDF. Повторите пересчёт.',version=version+1,updated_at=@now
            WHERE id=@revisionId AND session_id=@sessionId AND version=@version AND status='generating'",
            new { sessionId, revisionId, version, now = DateTime.UtcNow.ToString("o") });
    }

    public bool Discard(string sessionId, string revisionId, int version, string actor)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        int rows = conn.Execute(@"UPDATE report_revisions SET status='discarded',version=version+1,updated_at=@now
            WHERE id=@revisionId AND session_id=@sessionId AND version=@version AND status IN ('draft','ready','failed')",
            new { sessionId, revisionId, version, now = DateTime.UtcNow.ToString("o") }, tx);
        if (rows == 1) Audit(conn, tx, actor, "report_revision_discarded", revisionId);
        tx.Commit();
        return rows == 1;
    }

    public bool Publish(string sessionId, string revisionId, int version, string actor)
    {
        using var conn = Open();
        using var tx = conn.BeginTransaction();
        var revision = conn.QuerySingleOrDefault<ReportRevision>($"SELECT {SelectColumns} FROM report_revisions WHERE session_id=@sessionId AND id=@revisionId", new { sessionId, revisionId }, tx);
        if (revision == null || revision.Version != version || revision.Status != "ready" ||
            revision.PdfBytes is not { Length: > 0 } || string.IsNullOrWhiteSpace(revision.ResultJson) || string.IsNullOrWhiteSpace(revision.Reason)) return false;
        var now = DateTime.UtcNow.ToString("o");
        int rows = conn.Execute(@"UPDATE sessions SET published_revision_id=@revisionId,updated_at=@now
            WHERE id=@sessionId AND completed_at IS NOT NULL AND published_revision_id IS @baseRevisionId",
            new { sessionId, revisionId, now, baseRevisionId = revision.BaseRevisionId }, tx);
        if (rows != 1) return false;
        conn.Execute("UPDATE report_revisions SET status='superseded' WHERE id=@baseRevisionId AND session_id=@sessionId AND status='published'", new { sessionId, baseRevisionId = revision.BaseRevisionId }, tx);
        conn.Execute("UPDATE report_revisions SET status='published',published_at=@now,updated_at=@now,version=version+1 WHERE id=@revisionId", new { revisionId, now }, tx);
        Audit(conn, tx, actor, "report_revision_published", $"{sessionId}: {revisionId}, revision {revision.Number}; {revision.Reason}");
        tx.Commit();
        return true;
    }

    private static void Audit(SqliteConnection conn, SqliteTransaction tx, string actor, string action, string detail)
        => conn.Execute("INSERT INTO audit_log(id,actor,action,detail,created_at) VALUES(@id,@actor,@action,@detail,@now)",
            new { id = Guid.NewGuid().ToString(), actor, action, detail, now = DateTime.UtcNow.ToString("o") }, tx);
}
