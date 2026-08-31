using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Dapper;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

/// <summary>
/// Репозиторий для динамического управления каталогом юридических рисков в SQLite fenix.db.
/// </summary>
public class RiskRepository
{
    private readonly string _connectionString;
    private static readonly object _cacheLock = new();
    private static List<RiskDefinition>? _cachedRisks;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public RiskRepository(DbInitializer dbInit)
    {
        _connectionString = dbInit.ConnectionString;
    }

    /// <summary>
    /// Получить все риски с возможностью фильтрации по секции, опасности и поисковому запросу.
    /// </summary>
    public List<RiskDefinition> GetAllRisks(string? sectionId = null, string? severity = null, string? priority = null, string? search = null)
    {
        var all = GetCachedOrLoadRisks();

        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(sectionId) && !sectionId.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => string.Equals(r.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(severity) && !severity.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Severity.ToString().Equals(severity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(priority) && !priority.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(r => r.Priority.ToString().Equals(priority, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(r =>
                r.Code.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Title.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Finding.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.WhyItMatters.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.Recommendation.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                r.RootCauseGroup.Contains(s, StringComparison.OrdinalIgnoreCase));
        }

        return query
            .OrderBy(r => GetSeverityRank(r.Severity))
            .ThenBy(r => r.SectionId, StringComparer.Ordinal)
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Получить конкретный риск по коду.
    /// </summary>
    public RiskDefinition? GetRiskByCode(string code)
    {
        var all = GetCachedOrLoadRisks();
        return all.FirstOrDefault(r => string.Equals(r.Code, code, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Обновить данные риска в БД.
    /// </summary>
    public bool UpdateRisk(RiskDefinition updated)
    {
        if (updated == null || string.IsNullOrWhiteSpace(updated.Code)) return false;

        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        const string sql = @"
            UPDATE risks SET
                title = @Title,
                severity = @Severity,
                priority = @Priority,
                section_id = @SectionId,
                root_cause_group = @RootCauseGroup,
                finding = @Finding,
                why_it_matters = @WhyItMatters,
                recommendation = @Recommendation,
                recommendations_json = @RecommendationsJson,
                suppress_codes_json = @SuppressCodesJson,
                modules_json = @ModulesJson,
                lawyer_required = @LawyerRequired,
                resolution = @Resolution,
                service_code = @ServiceCode,
                cta = @Cta
            WHERE code = @Code;
        ";

        int affected = conn.Execute(sql, new
        {
            updated.Code,
            updated.Title,
            Severity = JsonSerializer.Serialize(updated.Severity).Trim('"'),
            Priority = JsonSerializer.Serialize(updated.Priority).Trim('"'),
            updated.SectionId,
            updated.RootCauseGroup,
            updated.Finding,
            updated.WhyItMatters,
            updated.Recommendation,
            RecommendationsJson = JsonSerializer.Serialize(updated.Recommendations ?? new()),
            SuppressCodesJson = JsonSerializer.Serialize(updated.SuppressCodes ?? new()),
            ModulesJson = JsonSerializer.Serialize(updated.Modules ?? new()),
            LawyerRequired = updated.LawyerRequired ? 1 : 0,
            Resolution = JsonSerializer.Serialize(updated.Resolution).Trim('"'),
            updated.ServiceCode,
            updated.Cta
        });

        if (affected > 0)
        {
            InvalidateCache();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Сбросить все риски в базе к эталонным настройкам DataBank.Risks.
    /// </summary>
    public void ResetToDefaults()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();
        conn.Execute("DELETE FROM risks;");

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
                );
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

        InvalidateCache();
    }

    /// <summary>
    /// Сбросить кэш определений рисков.
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedRisks = null;
        }
    }

    private List<RiskDefinition> GetCachedOrLoadRisks()
    {
        lock (_cacheLock)
        {
            if (_cachedRisks != null) return _cachedRisks;

            _cachedRisks = LoadRisksFromDb();
            return _cachedRisks;
        }
    }

    private List<RiskDefinition> LoadRisksFromDb()
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

        const string sql = "SELECT * FROM risks ORDER BY code ASC";
        var rows = conn.Query(sql);
        var list = new List<RiskDefinition>();

        foreach (var r in rows)
        {
            var def = new RiskDefinition
            {
                Code = (string)r.code,
                RootCauseGroup = (string)(r.root_cause_group ?? "GENERAL"),
                Severity = ParseWireEnum<RiskSeverity>((string)r.severity),
                Priority = ParseWireEnum<RiskPriority>((string)r.priority),
                SectionId = (string)r.section_id,
                Title = (string)r.title,
                Finding = (string)r.finding,
                WhyItMatters = (string)r.why_it_matters,
                Recommendation = (string)r.recommendation,
                LawyerRequired = Convert.ToInt32(r.lawyer_required) == 1,
                Resolution = ParseWireEnum<ResolutionType>((string)(r.resolution ?? "self")),
                ServiceCode = r.service_code as string,
                Cta = r.cta as string,
                Modules = !string.IsNullOrEmpty((string)r.modules_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.modules_json, JsonOpts) ?? new()
                    : new(),
                Recommendations = !string.IsNullOrEmpty((string)r.recommendations_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.recommendations_json, JsonOpts) ?? new()
                    : new(),
                SuppressCodes = !string.IsNullOrEmpty((string)r.suppress_codes_json)
                    ? JsonSerializer.Deserialize<List<string>>((string)r.suppress_codes_json, JsonOpts) ?? new()
                    : new()
            };
            list.Add(def);
        }

        // Если в таблице пусто, загружаем из DataBank
        if (list.Count == 0)
        {
            return DataBank.Risks.ToList();
        }

        return list;
    }

    private static T ParseWireEnum<T>(string wireVal) where T : struct, Enum
    {
        if (Enum.TryParse<T>(wireVal, true, out var exact))
            return exact;

        // Попытка сопоставления через JSON десериализацию
        try
        {
            var json = $"\"{wireVal}\"";
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }

    private static int GetSeverityRank(RiskSeverity sev) => sev switch
    {
        RiskSeverity.Blocker => 1,
        RiskSeverity.Critical => 2,
        RiskSeverity.High => 3,
        RiskSeverity.Medium => 4,
        RiskSeverity.Info => 5,
        _ => 6
    };
}
