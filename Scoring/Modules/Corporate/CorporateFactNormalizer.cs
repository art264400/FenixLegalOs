using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Corporate;

public class CorporateFactNormalizer : IFactNormalizer
{
    public string ModuleId => "corporate";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // ==========================================
        // 2. CORPORATE FACTS (§24 & §22)
        // ==========================================
        if (answers.TryGetValue("COR-C01", out var corC01Raw) && corC01Raw != null)
        {
            var corC01 = corC01Raw.ToString() ?? "";
            f["company.entityStatus"] = corC01 switch
            {
                "one" or "multiple" or "several" or "aifc" => "incorporated",
                "registering" or "process" => "registering",
                "none" => "not_incorporated",
                _ => "unknown"
            };

            var isMultiple = corC01 is "multiple" or "several";
            var corC02B = GetAnswerStr(answers, "COR-C02B");
            int entityCount = corC01 switch
            {
                "one" or "registering" or "aifc" => 1,
                "multiple" or "several" => corC02B switch { "2" => 2, "3" => 3, "4plus" => 4, _ => 2 },
                "none" => 0,
                _ => 1
            };

            f["company.entityCount"] = entityCount;
            f["company.groupStructure"] = isMultiple;

            var primaryJurisdiction = GetAnswerStr(answers, "COR-C02A");
            if (string.IsNullOrEmpty(primaryJurisdiction)) primaryJurisdiction = GetAnswerStr(answers, "COR-C02");
            if (string.IsNullOrEmpty(primaryJurisdiction) && corC01 == "aifc") primaryJurisdiction = "aifc";
            f["company.primaryJurisdiction"] = primaryJurisdiction;
            f["company.jurisdiction"] = primaryJurisdiction;

            var jurisdictionsList = new List<string>();
            if (!string.IsNullOrEmpty(primaryJurisdiction)) jurisdictionsList.Add(primaryJurisdiction);

            var entitiesSummary = new List<string>();
            if (!string.IsNullOrEmpty(primaryJurisdiction))
            {
                entitiesSummary.Add($"Основная компания: {FormatJurisdictionName(primaryJurisdiction)}");
            }

            if (isMultiple && answers.TryGetValue("COR-C02C", out var rawC02C) && rawC02C != null)
            {
                f["company.additionalEntitiesRaw"] = rawC02C;
                if (rawC02C is JsonElement je && je.ValueKind == JsonValueKind.Array)
                {
                    int cIdx = 2;
                    foreach (var item in je.EnumerateArray())
                    {
                        var jVal = item.TryGetProperty("jurisdiction", out var jp) ? jp.GetString() : null;
                        var rList = item.TryGetProperty("roles", out var rp) && rp.ValueKind == JsonValueKind.Array
                            ? rp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                            : new List<string>();

                        if (!string.IsNullOrEmpty(jVal))
                        {
                            jurisdictionsList.Add(jVal);
                            var roleStr = rList.Count > 0 ? string.Join(", ", rList.Select(FormatRoleName)) : "операционная деятельность";
                            entitiesSummary.Add($"Компания {cIdx} ({FormatJurisdictionName(jVal)}): {roleStr}");
                        }
                        cIdx++;
                    }
                }
                else if (rawC02C is string strC02C && !string.IsNullOrWhiteSpace(strC02C))
                {
                    entitiesSummary.Add($"Дополнительные компании: {strC02C}");
                }
            }

            f["company.jurisdictions"] = jurisdictionsList.Distinct().ToList();

            string narrative;
            if (corC01 == "none")
            {
                narrative = "Проект пока работает без зарегистрированного юридического лица.";
            }
            else if (corC01 == "registering")
            {
                narrative = $"Компания находится в процессе регистрации ({FormatJurisdictionName(primaryJurisdiction)}).";
            }
            else if (!isMultiple)
            {
                narrative = $"Бизнес ведет деятельность через одну компанию в юрисдикции {FormatJurisdictionName(primaryJurisdiction)}.";
            }
            else
            {
                narrative = $"В структуре бизнеса используется {entityCount} компаний:\n- " + string.Join("\n- ", entitiesSummary);
            }
            f["company.structureNarrative"] = narrative;
        }

        if (answers.TryGetValue("COR-01", out var cor01Raw) && cor01Raw != null)
        {
            var cor01 = cor01Raw.ToString() ?? "";
            f["capital.ownershipMatch"] = cor01 switch
            {
                "match" => "match",
                "planned_change" => "planned_change",
                "unregistered_holding" => "unregistered_holding",
                "nominal" => "nominal",
                "dispute" => "dispute",
                _ => cor01
            };
            f["capital.ownershipDispute"] = cor01 == "dispute";
        }

        if (answers.TryGetValue("COR-02", out var cor02Raw) && cor02Raw != null)
        {
            var cor02 = cor02Raw.ToString() ?? "";
            f["capital.capTableStatus"] = cor02 switch
            {
                "complete" or "registered" => "complete",
                "current_plus_separate" => "current_plus_separate",
                "irregular" => "irregular",
                "fragmented" => "fragmented",
                "none" => "unreliable",
                _ => cor02
            };
        }

        if (answers.TryGetValue("COR-03", out var cor03Raw) && cor03Raw != null)
        {
            var cor03 = cor03Raw.ToString() ?? "";
            f["capital.equityPromises"] = cor03 switch
            {
                "none" => "none",
                "documented_included" or "signed" => "documented_included",
                "documented_not_included" => "documented_not_included",
                "informal" => "informal",
                "unclear_terms" => "unclear_terms",
                _ => cor03
            };
        }

        if (answers.TryGetValue("COR-04", out var cor04Raw) && cor04Raw != null)
        {
            var cor04 = cor04Raw.ToString() ?? "";
            f["capital.historyChanges"] = cor04 is "complete" or "main_docs" or "partial" or "missing";
            f["capital.historyStatus"] = cor04 switch
            {
                "none" => "none",
                "complete" => "complete",
                "main_docs" => "main_docs",
                "partial" => "partial",
                "missing" => "missing",
                _ => cor04
            };
        }

        if (answers.TryGetValue("COR-04A", out var cor04ARaw) && cor04ARaw != null)
        {
            var cor04A = cor04ARaw.ToString() ?? "";
            f["capital.historyTrace"] = cor04A switch
            {
                "yes" => "complete",
                "partial" => "partial",
                "no" => "missing",
                _ => cor04A
            };
        }

        if (answers.TryGetValue("COR-05", out var cor05Raw) && cor05Raw != null)
        {
            var cor05 = cor05Raw.ToString() ?? "";
            f["corporate.approvals"] = cor05 switch
            {
                "systematic" => "systematic",
                "main" => "main",
                "inconsistent" => "inconsistent",
                "often_missing" => "often_missing",
                "no_events" => "no_events",
                _ => cor05
            };
        }

        if (answers.TryGetValue("COR-06", out var cor06Raw) && cor06Raw != null)
        {
            var cor06 = cor06Raw.ToString() ?? "";
            f["corporate.authority"] = cor06 switch
            {
                "clear_limits" => "clear_limits",
                "clear_no_limits" => "clear_no_limits",
                "multiple_partial" => "multiple_partial",
                "unclear" => "unclear",
                _ => cor06
            };
        }

        string? cor07 = null;
        if (answers.TryGetValue("COR-07", out var c07) && c07 != null) cor07 = c07.ToString();
        else if (answers.TryGetValue("COR-07_GROUP", out var c07g) && c07g != null) cor07 = c07g.ToString();
        else if (answers.TryGetValue("COR-07_AIFC", out var c07a) && c07a != null) cor07 = c07a.ToString();

        if (!string.IsNullOrEmpty(cor07))
        {
            f["company.entityAlignment"] = cor07 switch
            {
                "aligned" or "clear" or "clean" => "aligned",
                "minor_exceptions" or "in_progress" => "minor_exceptions",
                "material_outside" or "formal_only" => "material_outside",
                _ => cor07
            };
        }

        if (answers.TryGetValue("COR-08", out var cor08Raw) && cor08Raw != null)
        {
            var cor08 = cor08Raw.ToString() ?? "";
            f["corporate.records"] = cor08 switch
            {
                "organized" => "organized",
                "partial" => "partial",
                "disorganized" => "disorganized",
                _ => cor08
            };
        }
    }

    public static string FormatJurisdictionName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Не указана";
        return code.ToLowerInvariant() switch
        {
            "kz" => "Казахстан (ТОО)",
            "aifc" => "МФЦА (AIFC)",
            "us" => "США (Delaware / C-Corp)",
            "uae" => "ОАЭ (Free Zone / Mainland)",
            "cy" => "Кипр (Ltd)",
            "sg" => "Сингапур (Pte Ltd)",
            "uk" => "Великобритания (Ltd)",
            "eu" => "Европейский союз",
            "kg" => "Кыргызстан (ОсОО / ПВТ)",
            "uz" => "Узбекистан (ООО / IT Park)",
            "other" => "Другая юрисдикция",
            _ => code
        };
    }

    public static string FormatRoleName(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "операционная деятельность";
        return role.ToLowerInvariant() switch
        {
            "ip_holder" => "Владелец IP и технологий",
            "operating" => "Операционная компания",
            "holding" => "Холдинговая компания",
            "fundraising" => "Инвестиционная компания",
            "rnd" => "Центр разработки (R&D)",
            _ => role
        };
    }

    private static string GetAnswerStr(IReadOnlyDictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return "";
        return val.ToString() ?? "";
    }
}
