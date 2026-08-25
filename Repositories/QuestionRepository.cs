using System.Text.Json;
using Dapper;
using FenixLegalOs.Models;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class QuestionRepository
{
    private readonly string _connectionString;

    public QuestionRepository(DbInitializer dbInit)
    {
        _connectionString = dbInit.ConnectionString;
    }

    public List<DiagnosticSection> GetSections(bool enabledOnly = true)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        string sql = enabledOnly
            ? "SELECT id, order_num, title, short_title, weight FROM sections WHERE enabled = 1 ORDER BY order_num ASC"
            : "SELECT id, order_num, title, short_title, weight FROM sections ORDER BY order_num ASC";

        var rows = conn.Query(sql);
        return rows.Select(r => new DiagnosticSection(
            (string)r.id,
            Convert.ToInt32(r.order_num),
            (string)r.title,
            (string)r.short_title,
            Convert.ToInt32(r.weight)
        )).ToList();
    }

    public List<DiagnosticQuestion> GetQuestions(bool enabledOnly = true)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        string sql = enabledOnly
            ? "SELECT * FROM questions WHERE enabled = 1 ORDER BY order_num ASC"
            : "SELECT * FROM questions ORDER BY order_num ASC";

        var rows = conn.Query(sql);
        var list = new List<DiagnosticQuestion>();

        foreach (var r in rows)
        {
            var q = new DiagnosticQuestion
            {
                Id = (string)r.id,
                SectionId = (string)r.section_id,
                DimensionId = r.dimension_id as string,
                Order = Convert.ToInt32(r.order_num),
                Question = (string)r.question,
                Explanation = r.explanation as string,
                Type = (string)r.type,
                ScoreMode = (string)r.score_mode,
                Weight = Convert.ToDouble(r.weight),
                DimensionWeight = Convert.ToDouble(r.dimension_weight),
                WithinDimensionWeight = Convert.ToDouble(r.within_dimension_weight),
                Options = !string.IsNullOrEmpty((string)r.options_json)
                    ? JsonSerializer.Deserialize<List<AnswerOption>>((string)r.options_json)
                    : new List<AnswerOption>(),
                Tags = !string.IsNullOrEmpty((string)r.tags_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.tags_json) ?? new()
                    : new List<string>(),
                ShowIf = !string.IsNullOrEmpty((string)r.show_if_json)
                    ? JsonSerializer.Deserialize<List<ConditionalRule>>((string)r.show_if_json)
                    : null,
                SkipIf = !string.IsNullOrEmpty((string)r.skip_if_json)
                    ? JsonSerializer.Deserialize<List<ConditionalRule>>((string)r.skip_if_json)
                    : null,
                Enabled = Convert.ToInt32(r.enabled) == 1
            };
            list.Add(q);
        }
        return list;
    }

    public List<RiskDefinition> GetRisks()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var rows = conn.Query("SELECT * FROM risks");
        var list = new List<RiskDefinition>();

        foreach (var r in rows)
        {
            var risk = new RiskDefinition
            {
                Code = (string)r.code,
                RootCauseGroup = (string)r.root_cause_group,
                Severity = (string)r.severity,
                Priority = (string)r.priority,
                SectionId = (string)r.section_id,
                Modules = !string.IsNullOrEmpty((string)r.modules_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.modules_json) ?? new()
                    : new List<string>(),
                Title = (string)r.title,
                Finding = (string)r.finding,
                WhyItMatters = (string)r.why_it_matters,
                Recommendations = !string.IsNullOrEmpty((string)r.recommendations_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.recommendations_json) ?? new()
                    : new List<string>(),
                Recommendation = (string)r.recommendation,
                LawyerRequired = Convert.ToInt32(r.lawyer_required) == 1,
                Resolution = (string)r.resolution,
                ServiceCode = r.service_code as string,
                SuppressCodes = !string.IsNullOrEmpty((string)r.suppress_codes_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.suppress_codes_json) ?? new()
                    : new List<string>(),
                Cta = r.cta as string
            };
            list.Add(risk);
        }
        return list;
    }

    public Dictionary<string, string> GetVersions()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        var rows = conn.Query("SELECT key, version FROM knowledge_versions");
        return rows.ToDictionary(r => (string)r.key, r => (string)r.version);
    }
}
