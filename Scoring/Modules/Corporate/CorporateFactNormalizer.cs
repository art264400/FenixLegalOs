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
            var status = corC01 switch
            {
                "one" or "multiple" or "several" or "aifc" => "incorporated",
                "registering" or "process" => "registering",
                "none" => "not_incorporated",
                _ => null
            };

            if (status != null)
            {
                f["company.entityStatus"] = status;

                var isMultiple = corC01 is "multiple" or "several";
                var corC02B = GetAnswerStr(answers, "COR-C02B");
                int? entityCount = corC01 switch
                {
                    "one" or "registering" or "aifc" => 1,
                    "multiple" or "several" => corC02B switch { "2" => 2, "3" => 3, "4plus" => 4, _ => (int?)null },
                    "none" => 0,
                    _ => null
                };

                if (entityCount.HasValue)
                {
                    f["company.entityCount"] = entityCount.Value;
                }
                f["company.groupStructure"] = isMultiple;

                var primaryJurisdiction = GetAnswerStr(answers, "COR-C02A");
                if (string.IsNullOrEmpty(primaryJurisdiction)) primaryJurisdiction = GetAnswerStr(answers, "COR-C02");
                if (string.IsNullOrEmpty(primaryJurisdiction) && corC01 == "aifc") primaryJurisdiction = "aifc";

                if (!string.IsNullOrEmpty(primaryJurisdiction))
                {
                    f["company.primaryJurisdiction"] = primaryJurisdiction;
                    f["company.jurisdiction"] = primaryJurisdiction;
                }

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

                if (jurisdictionsList.Count > 0)
                {
                    f["company.jurisdictions"] = jurisdictionsList.Distinct().ToList();
                }

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
                    narrative = $"В структуре бизнеса используется {entityCount ?? 2} компаний:\n- " + string.Join("\n- ", entitiesSummary);
                }
                f["company.structureNarrative"] = narrative;
            }
        }

        if (answers.TryGetValue("COR-01", out var cor01Raw) && cor01Raw != null)
        {
            var cor01 = cor01Raw.ToString() ?? "";
            if (cor01 is "match" or "planned_change" or "unregistered_holding" or "nominal" or "dispute")
            {
                f["capital.ownershipMatch"] = cor01;
                f["capital.ownershipDispute"] = cor01 == "dispute";
            }
        }

        if (answers.TryGetValue("COR-02", out var cor02Raw) && cor02Raw != null)
        {
            var cor02 = cor02Raw.ToString() ?? "";
            var capStatus = cor02 switch
            {
                "complete" or "registered" => "complete",
                "current_plus_separate" => "current_plus_separate",
                "irregular" => "irregular",
                "fragmented" => "fragmented",
                "none" => "unreliable",
                _ => null
            };
            if (capStatus != null)
            {
                f["capital.capTableStatus"] = capStatus;
            }
        }

        if (answers.TryGetValue("COR-03", out var cor03Raw) && cor03Raw != null)
        {
            var cor03 = cor03Raw.ToString() ?? "";
            var eqProm = cor03 switch
            {
                "none" => "none",
                "documented_included" or "signed" => "documented_included",
                "documented_not_included" => "documented_not_included",
                "informal" => "informal",
                "unclear_terms" => "unclear_terms",
                _ => null
            };
            if (eqProm != null)
            {
                f["capital.equityPromises"] = eqProm;
            }
        }

        if (answers.TryGetValue("COR-04", out var cor04Raw) && cor04Raw != null)
        {
            var cor04 = cor04Raw.ToString() ?? "";
            var histStatus = cor04 switch
            {
                "none" => "none",
                "complete" => "complete",
                "main_docs" => "main_docs",
                "partial" => "partial",
                "missing" => "missing",
                _ => null
            };
            if (histStatus != null)
            {
                f["capital.historyChanges"] = cor04 is "complete" or "main_docs" or "partial" or "missing";
                f["capital.historyStatus"] = histStatus;
            }
        }

        if (answers.TryGetValue("COR-04A", out var cor04ARaw) && cor04ARaw != null)
        {
            var cor04A = cor04ARaw.ToString() ?? "";
            var histTrace = cor04A switch
            {
                "yes" => "complete",
                "partial" => "partial",
                "no" => "missing",
                _ => null
            };
            if (histTrace != null)
            {
                f["capital.historyTrace"] = histTrace;
            }
        }

        if (answers.TryGetValue("COR-05", out var cor05Raw) && cor05Raw != null)
        {
            var cor05 = cor05Raw.ToString() ?? "";
            if (cor05 is "systematic" or "main" or "inconsistent" or "often_missing" or "no_events")
            {
                f["corporate.approvals"] = cor05;
            }
        }

        if (answers.TryGetValue("COR-06", out var cor06Raw) && cor06Raw != null)
        {
            var cor06 = cor06Raw.ToString() ?? "";
            if (cor06 is "clear_limits" or "clear_no_limits" or "multiple_partial" or "unclear")
            {
                f["corporate.authority"] = cor06;
            }
        }

        string? cor07 = null;
        if (answers.TryGetValue("COR-07", out var c07) && c07 != null) cor07 = c07.ToString();
        else if (answers.TryGetValue("COR-07_GROUP", out var c07g) && c07g != null) cor07 = c07g.ToString();
        else if (answers.TryGetValue("COR-07_AIFC", out var c07a) && c07a != null) cor07 = c07a.ToString();

        if (!string.IsNullOrEmpty(cor07))
        {
            var align = cor07 switch
            {
                "aligned" or "clear" or "clean" => "aligned",
                "minor_exceptions" or "in_progress" => "minor_exceptions",
                "material_outside" or "formal_only" => "material_outside",
                _ => null
            };
            if (align != null)
            {
                f["company.entityAlignment"] = align;
            }
        }

        if (answers.TryGetValue("COR-08", out var cor08Raw) && cor08Raw != null)
        {
            var cor08 = cor08Raw.ToString() ?? "";
            if (cor08 is "organized" or "scattered" or "reconstruct" or "missing" or "partial" or "disorganized")
            {
                f["corporate.records"] = cor08;
            }
        }

        if (answers.TryGetValue("COR-T01", out var corT01Raw) && corT01Raw != null)
        {
            var corT01 = corT01Raw.ToString() ?? "";
            switch (corT01)
            {
                case "none":
                    f["company.hiddenBeneficiary"] = false;
                    break;
                case "formal":
                    f["company.hiddenBeneficiary"] = false;
                    f["company.holdingStructure"] = true;
                    break;
                case "indirect":
                    f["company.hiddenControl"] = "indirect";
                    break;
                case "informal":
                    f["company.hiddenControl"] = "informal";
                    break;
                case "unknown":
                    f["company.hiddenControl"] = "unknown";
                    break;
            }
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

    private static string FormatRoleName(string role)
    {
        return role switch
        {
            "holding" => "холдинг",
            "ip_holder" => "владение IP",
            "operating" => "операционная деятельность",
            "rnd" => "R&D / разработка",
            "fintech" => "финансовая деятельность",
            "crypto" => "крипто-лицензия",
            "other" => "другое",
            _ => role
        };
    }

    private static string GetAnswerStr(IReadOnlyDictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return "";
        return val.ToString() ?? "";
    }
}
