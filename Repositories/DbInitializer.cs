using System.Text.Json;
using Dapper;
using FenixLegalOs.Data;
using FenixLegalOs.Scoring.Core;
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
        // Fail closed at startup if questionnaire dependency graph has cycles or invalid authority
        RoutingDependencyValidator.Validate(DataBank.Questions);

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

            -- Question Bank Tables
            CREATE TABLE IF NOT EXISTS sections (
                id TEXT PRIMARY KEY,
                order_num INTEGER NOT NULL,
                title TEXT NOT NULL,
                short_title TEXT NOT NULL,
                weight INTEGER NOT NULL,
                enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS questions (
                id TEXT PRIMARY KEY,
                section_id TEXT NOT NULL,
                dimension_id TEXT,
                order_num INTEGER NOT NULL,
                question TEXT NOT NULL,
                explanation TEXT,
                type TEXT NOT NULL DEFAULT 'single',
                score_mode TEXT NOT NULL DEFAULT 'diagnostic',
                weight REAL NOT NULL DEFAULT 1.0,
                dimension_weight REAL NOT NULL DEFAULT 1.0,
                within_dimension_weight REAL NOT NULL DEFAULT 1.0,
                options_json TEXT,
                tags_json TEXT,
                show_if_json TEXT,
                skip_if_json TEXT,
                enabled INTEGER NOT NULL DEFAULT 1
            );

            CREATE TABLE IF NOT EXISTS risks (
                code TEXT PRIMARY KEY,
                root_cause_group TEXT NOT NULL DEFAULT 'GENERAL',
                severity TEXT NOT NULL DEFAULT 'MEDIUM',
                priority TEXT NOT NULL DEFAULT 'LATER',
                section_id TEXT NOT NULL,
                modules_json TEXT,
                title TEXT NOT NULL,
                finding TEXT NOT NULL,
                why_it_matters TEXT NOT NULL,
                recommendations_json TEXT,
                recommendation TEXT NOT NULL,
                lawyer_required INTEGER NOT NULL DEFAULT 0,
                resolution TEXT NOT NULL DEFAULT 'self',
                service_code TEXT,
                suppress_codes_json TEXT,
                cta TEXT
            );

            CREATE TABLE IF NOT EXISTS knowledge_versions (
                key TEXT PRIMARY KEY,
                version TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS system_settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );
        ");

        // Seed initial pricing
        conn.Execute(@"
            INSERT OR IGNORE INTO system_settings (key, value, updated_at)
            VALUES ('report_price_kzt', '19999', datetime('now')),
                   ('report_old_price_kzt', '49990', datetime('now'));
        ");

        // Safe migrations
        TryAddColumn(conn, "sessions", "paid", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "sessions", "paid_at", "TEXT");
        TryAddColumn(conn, "sessions", "payment_amount", "INTEGER");
        TryAddColumn(conn, "sessions", "payment_method", "TEXT");

        TryAddColumn(conn, "leads", "paid", "INTEGER NOT NULL DEFAULT 0");
        TryAddColumn(conn, "leads", "paid_at", "TEXT");
        TryAddColumn(conn, "leads", "payment_amount", "INTEGER");
        TryAddColumn(conn, "leads", "payment_method", "TEXT");

        // Seed or update Question Bank in DB
        SeedQuestionBank(conn);
    }

    private void SeedQuestionBank(SqliteConnection conn)
    {
        // 1. Seed Sections
        foreach (var s in DataBank.Sections)
        {
            conn.Execute(@"
                INSERT INTO sections (id, order_num, title, short_title, weight, enabled)
                VALUES (@Id, @Order, @Title, @ShortTitle, @Weight, 1)
                ON CONFLICT(id) DO UPDATE SET
                    order_num = excluded.order_num,
                    title = excluded.title,
                    short_title = excluded.short_title,
                    weight = excluded.weight,
                    enabled = 1;
            ", s);
        }

        // 2. Seed Questions
        foreach (var q in DataBank.Questions)
        {
            conn.Execute(@"
                INSERT INTO questions (
                    id, section_id, dimension_id, order_num, question, explanation,
                    type, score_mode, weight, dimension_weight, within_dimension_weight,
                    options_json, tags_json, show_if_json, skip_if_json, enabled
                ) VALUES (
                    @Id, @SectionId, @DimensionId, @Order, @Question, @Explanation,
                    @Type, @ScoreMode, @Weight, @DimensionWeight, @WithinDimensionWeight,
                    @OptionsJson, @TagsJson, @ShowIfJson, @SkipIfJson, @Enabled
                )
                ON CONFLICT(id) DO UPDATE SET
                    section_id = excluded.section_id,
                    dimension_id = excluded.dimension_id,
                    order_num = excluded.order_num,
                    question = excluded.question,
                    explanation = excluded.explanation,
                    type = excluded.type,
                    score_mode = excluded.score_mode,
                    weight = excluded.weight,
                    dimension_weight = excluded.dimension_weight,
                    within_dimension_weight = excluded.within_dimension_weight,
                    options_json = excluded.options_json,
                    tags_json = excluded.tags_json,
                    show_if_json = excluded.show_if_json,
                    skip_if_json = excluded.skip_if_json,
                    enabled = excluded.enabled;
            ", new
            {
                q.Id,
                q.SectionId,
                q.DimensionId,
                Order = q.Order,
                q.Question,
                q.Explanation,
                Type = JsonSerializer.Serialize(q.Type).Trim('"'),
                ScoreMode = JsonSerializer.Serialize(q.ScoreMode).Trim('"'),
                q.Weight,
                q.DimensionWeight,
                q.WithinDimensionWeight,
                OptionsJson = q.Options != null ? JsonSerializer.Serialize(q.Options) : null,
                TagsJson = q.Tags != null ? JsonSerializer.Serialize(q.Tags) : null,
                ShowIfJson = q.ShowIf != null ? JsonSerializer.Serialize(q.ShowIf) : null,
                SkipIfJson = q.SkipIf != null ? JsonSerializer.Serialize(q.SkipIf) : null,
                Enabled = q.Enabled ? 1 : 0
            });
        }

        // 3. Seed Risks
        foreach (var r in DataBank.Risks)
        {
            conn.Execute(@"
                INSERT INTO risks (
                    code, root_cause_group, severity, priority, section_id, modules_json,
                    title, finding, why_it_matters, recommendations_json, recommendation,
                    lawyer_required, resolution, service_code, suppress_codes_json, cta
                ) VALUES (
                    @Code, @RootCauseGroup, @Severity, @Priority, @SectionId, @ModulesJson,
                    @Title, @Finding, @WhyItMatters, @RecommendationsJson, @Recommendation,
                    @LawyerRequired, @Resolution, @ServiceCode, @SuppressCodesJson, @Cta
                )
                ON CONFLICT(code) DO UPDATE SET
                    root_cause_group = excluded.root_cause_group,
                    severity = excluded.severity,
                    priority = excluded.priority,
                    section_id = excluded.section_id,
                    modules_json = excluded.modules_json,
                    title = excluded.title,
                    finding = excluded.finding,
                    why_it_matters = excluded.why_it_matters,
                    recommendations_json = excluded.recommendations_json,
                    recommendation = excluded.recommendation,
                    lawyer_required = excluded.lawyer_required,
                    resolution = excluded.resolution,
                    service_code = excluded.service_code,
                    suppress_codes_json = excluded.suppress_codes_json,
                    cta = excluded.cta;
            ", new
            {
                r.Code,
                r.RootCauseGroup,
                Severity = JsonSerializer.Serialize(r.Severity).Trim('"'),
                Priority = JsonSerializer.Serialize(r.Priority).Trim('"'),
                r.SectionId,
                ModulesJson = JsonSerializer.Serialize(r.Modules),
                r.Title,
                r.Finding,
                r.WhyItMatters,
                RecommendationsJson = JsonSerializer.Serialize(r.Recommendations),
                r.Recommendation,
                LawyerRequired = r.LawyerRequired ? 1 : 0,
                Resolution = JsonSerializer.Serialize(r.Resolution).Trim('"'),
                r.ServiceCode,
                SuppressCodesJson = JsonSerializer.Serialize(r.SuppressCodes),
                r.Cta
            });
        }

        // 3b. Remove obsolete questions, risks, sections not present in canonical DataBank
        var validQuestionIds = DataBank.Questions.Select(q => q.Id).ToList();
        conn.Execute("DELETE FROM questions WHERE id NOT IN @validQuestionIds", new { validQuestionIds });

        var validRiskCodes = DataBank.Risks.Select(r => r.Code).ToList();
        conn.Execute("DELETE FROM risks WHERE code NOT IN @validRiskCodes", new { validRiskCodes });

        var validSectionIds = DataBank.Sections.Select(s => s.Id).ToList();
        conn.Execute("DELETE FROM sections WHERE id NOT IN @validSectionIds", new { validSectionIds });

        // 4. Record Versions
        conn.Execute(@"
            INSERT INTO knowledge_versions (key, version, updated_at)
            VALUES ('question_bank', @qbVersion, @now),
                   ('scoring_engine', @engineVersion, @now),
                   ('risk_library', @riskVersion, @now)
            ON CONFLICT(key) DO UPDATE SET
                version = excluded.version,
                updated_at = excluded.updated_at;
        ", new
        {
            qbVersion = DataBank.QuestionBankVersion,
            engineVersion = DataBank.ScoringEngineVersion,
            riskVersion = DataBank.RiskLibraryVersion,
            now = DateTime.UtcNow.ToString("o")
        });
    }

    private void TryAddColumn(SqliteConnection conn, string table, string column, string type)
    {
        try { conn.Execute($"ALTER TABLE {table} ADD COLUMN {column} {type};"); } catch { }
    }
}
