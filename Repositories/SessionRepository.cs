using System.Text.Json;
using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;

namespace FenixLegalOs.Repositories;

public class SessionRepository
{
    private readonly DbInitializer _db;
    private readonly IMemoryCache? _cache;
    private const string BenchmarkCacheKey = "benchmark_stats_v1";

    public SessionRepository(DbInitializer db, IMemoryCache? cache = null)
    {
        _db = db;
        _cache = cache;
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
        var session = conn.QuerySingleOrDefault<DiagnosticSession>(@"
            SELECT id AS Id, created_at AS CreatedAt, updated_at AS UpdatedAt,
                   answers AS AnswersJson, last_section_id AS LastSectionId,
                   completed_at AS CompletedAt, result AS ResultJson,
                    paid AS Paid, paid_at AS PaidAt, payment_amount AS PaymentAmount,
                    payment_method AS PaymentMethod, user_id AS UserId,
                    terms_accepted AS TermsAccepted, terms_accepted_at AS TermsAcceptedAt,
                    pdf_bytes AS PdfBytes, pdf_generated_at AS PdfGeneratedAt
             FROM sessions WHERE id = @id", new { id });
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
                updated_at = excluded.updated_at
            WHERE sessions.completed_at IS NULL;
        ", new { answersJson, lastSectionId, now, id });
        return rows > 0;
    }

    public bool CompleteSession(string id, string answersJson, ScoreResult result)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        var resultJson = JsonSerializer.Serialize(result);
        int rows = conn.Execute(@"
            INSERT INTO sessions (id, created_at, updated_at, answers, result, completed_at, qb_version, engine_version, risk_version)
            VALUES (@id, @now, @now, @answersJson, @resultJson, @now, @qb, @eng, @risk)
            ON CONFLICT(id) DO UPDATE SET
                answers = excluded.answers,
                result = excluded.result,
                completed_at = excluded.completed_at,
                updated_at = excluded.updated_at,
                qb_version = excluded.qb_version,
                engine_version = excluded.engine_version,
                risk_version = excluded.risk_version
            WHERE sessions.completed_at IS NULL;
        ", new
        {
            answersJson, resultJson, now, id,
            qb = result.Versions.QuestionBank,
            eng = result.Versions.ScoringEngine,
            risk = result.Versions.RiskLibrary
        });

        if (rows > 0)
        {
            // Invalidate cached benchmark stats on session completion
            _cache?.Remove(BenchmarkCacheKey);
        }
        return rows > 0;
    }

    public bool MarkSessionPaid(string id, int amount, string method)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int sRows = conn.Execute("UPDATE sessions SET paid = 1, paid_at = @now, payment_amount = @amount, payment_method = @method WHERE id = @id", new { now, amount, method, id });
        conn.Execute("UPDATE leads SET paid = 1, paid_at = @now, payment_amount = @amount, payment_method = @method WHERE session_id = @id", new { now, amount, method, id });
        return sRows > 0;
    }

    public bool SavePdf(string id, byte[] pdfBytes, bool overwrite = false)
    {
        using var conn = GetConn();
        var now = DateTime.UtcNow.ToString("o");
        int rows = conn.Execute(@"
            UPDATE sessions
            SET pdf_bytes = @pdfBytes,
                pdf_generated_at = @now,
                updated_at = @now
            WHERE id = @id AND (pdf_bytes IS NULL OR length(pdf_bytes) = 0 OR @overwrite = 1)",
            new { pdfBytes, now, id, overwrite = overwrite ? 1 : 0 });
        return rows > 0;
    }

    public byte[]? GetPdf(string id)
    {
        using var conn = GetConn();
        return conn.QuerySingleOrDefault<byte[]?>(
            "SELECT pdf_bytes FROM sessions WHERE id = @id AND pdf_bytes IS NOT NULL",
            new { id });
    }

    public BenchmarkStatsDto GetBenchmarkStats()
    {
        // 1. Return from memory cache if present (instant, 0ms DB load)
        if (_cache != null && _cache.TryGetValue(BenchmarkCacheKey, out BenchmarkStatsDto? cached) && cached != null)
        {
            return cached;
        }

        using var conn = GetConn();
        var rows = conn.Query<DiagnosticSession>(
            "SELECT answers AS AnswersJson, result AS ResultJson FROM sessions WHERE result IS NOT NULL"
        ).ToList();

        int completedCount = rows.Count;
        if (completedCount == 0)
        {
            var empty = new BenchmarkStatsDto
            {
                TotalScreenings = 0,
                CountriesCount = 0,
                AverageScore = 0,
                IpRiskPercentage = 0
            };
            _cache?.Set(BenchmarkCacheKey, empty, TimeSpan.FromMinutes(10));
            return empty;
        }

        var scores = new List<int>();
        int ipRiskCount = 0;
        var countries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!string.IsNullOrWhiteSpace(row.AnswersJson))
            {
                try
                {
                    var ans = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.AnswersJson);
                    if (ans != null)
                    {
                        foreach (var key in new[] { "COR-01", "COR-C01", "c_inc", "jurisdiction", "fnd_jurisdiction" })
                        {
                            if (ans.TryGetValue(key, out var val))
                            {
                                var strVal = val.ToString()?.Trim();
                                if (!string.IsNullOrWhiteSpace(strVal) && strVal != "unknown" && strVal != "in_progress")
                                {
                                    countries.Add(strVal);
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(row.ResultJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(row.ResultJson);
                    if (doc.RootElement.TryGetProperty("Overall", out var ov) && ov.TryGetInt32(out var sc))
                    {
                        scores.Add(sc);
                    }
                    if (doc.RootElement.TryGetProperty("Risks", out var risks) && risks.ValueKind == JsonValueKind.Array)
                    {
                        bool hasIpRisk = false;
                        foreach (var rk in risks.EnumerateArray())
                        {
                            var code = rk.TryGetProperty("Code", out var c) ? c.GetString() ?? "" : "";
                            var sec = rk.TryGetProperty("SectionId", out var s) ? s.GetString() ?? "" : "";
                            if (code.StartsWith("IP_", StringComparison.OrdinalIgnoreCase) || sec.Equals("ip", StringComparison.OrdinalIgnoreCase))
                            {
                                hasIpRisk = true;
                                break;
                            }
                        }
                        if (hasIpRisk) ipRiskCount++;
                    }
                }
                catch { }
            }
        }

        int avgScore = scores.Count > 0 ? (int)Math.Round(scores.Average()) : 0;
        int ipPercent = completedCount > 0 ? (int)Math.Round((double)ipRiskCount / completedCount * 100) : 0;
        int countriesCount = countries.Count > 0 ? countries.Count : 1;

        var stats = new BenchmarkStatsDto
        {
            TotalScreenings = completedCount,
            CountriesCount = countriesCount,
            AverageScore = avgScore,
            IpRiskPercentage = ipPercent
        };

        // Cache for 10 minutes (also invalidated immediately upon CompleteSession)
        _cache?.Set(BenchmarkCacheKey, stats, TimeSpan.FromMinutes(10));
        return stats;
    }
}
