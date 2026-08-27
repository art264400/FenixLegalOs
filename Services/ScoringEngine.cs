using System.Text.Json;
using System.Text.RegularExpressions;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;

namespace FenixLegalOs.Services;

public class ConditionsEvaluator
{
    public static bool IsVisible(List<ConditionalRule>? rules, Dictionary<string, object> answers, SharedFactStore? factStore = null)
    {
        if (rules == null || rules.Count == 0) return true;
        return rules.All(r => EvaluateRule(r, answers, factStore));
    }

    public static bool EvaluateRule(ConditionalRule rule, Dictionary<string, object> answers, SharedFactStore? factStore = null)
    {
        if (rule.All != null && rule.All.Count > 0)
            return rule.All.All(r => EvaluateRule(r, answers, factStore));

        if (rule.Any != null && rule.Any.Count > 0)
            return rule.Any.Any(r => EvaluateRule(r, answers, factStore));

        if (string.IsNullOrEmpty(rule.QuestionId)) return true;

        // Check if QuestionId refers to a FactStore key
        if (factStore != null && (rule.QuestionId.Contains('.') || factStore.Facts.ContainsKey(rule.QuestionId)))
        {
            if (factStore.Facts.TryGetValue(rule.QuestionId, out var factVal))
            {
                if (factVal is bool b && bool.TryParse(rule.Value?.ToString(), out var targetBool))
                {
                    return rule.Op switch
                    {
                        "eq" or null => b == targetBool,
                        "neq" => b != targetBool,
                        _ => throw new InvalidOperationException($"Unsupported boolean conditional operator: '{rule.Op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal is IEnumerable<string> strList)
                {
                    var targetStr = rule.Value?.ToString() ?? "";
                    return rule.Op switch
                    {
                        "contains" or "eq" or "in" => strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        "notContains" or "neq" => !strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        _ => throw new InvalidOperationException($"Unsupported collection conditional operator: '{rule.Op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal != null)
                {
                    return EvaluateOp(rule.Op, factVal.ToString() ?? "", rule.Value, factVal, rule.QuestionId);
                }
            }
        }

        if (!answers.TryGetValue(rule.QuestionId, out var rawVal) || rawVal == null)
            return rule.Op == "neq" || rule.Op == "notIn" || rule.Op == "notContains";

        if (rawVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var arrVals = new List<string>();
            foreach (var el in je.EnumerateArray()) arrVals.Add(el.ToString());
            var targetStr = rule.Value?.ToString() ?? "";
            return rule.Op switch
            {
                "contains" or "eq" or "in" => arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                "notContains" or "neq" => !arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported array conditional operator: '{rule.Op}' for question '{rule.QuestionId}'")
            };
        }

        if (rawVal is IEnumerable<string> listVal)
        {
            var targetStr = rule.Value?.ToString() ?? "";
            return rule.Op switch
            {
                "contains" or "eq" or "in" => listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                "notContains" or "neq" => !listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported list conditional operator: '{rule.Op}' for question '{rule.QuestionId}'")
            };
        }

        var valStr = rawVal.ToString() ?? "";
        return EvaluateOp(rule.Op, valStr, rule.Value, rawVal, rule.QuestionId);
    }

    private static bool EvaluateOp(string? op, string valStr, object? ruleValue, object? rawVal, string questionId)
    {
        return op switch
        {
            "eq" => valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            "neq" => !valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            "in" => RuleValueContains(ruleValue, valStr),
            "notIn" => !RuleValueContains(ruleValue, valStr),
            "contains" => valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            "notContains" => !valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            "answered" => !string.IsNullOrEmpty(valStr),
            "gte" => double.TryParse(valStr, out var v1) && double.TryParse(ruleValue?.ToString(), out var v2) && v1 >= v2,
            "lte" => double.TryParse(valStr, out var v3) && double.TryParse(ruleValue?.ToString(), out var v4) && v3 <= v4,
            _ => throw new InvalidOperationException($"Unsupported conditional operator: '{op}' for question '{questionId}'")
        };
    }

    private static bool RuleValueContains(object? ruleVal, string val)
    {
        if (ruleVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ToString().Equals(val, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                if (str.Contains(','))
                {
                    return str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
                }
                return str.Equals(val, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (ruleVal is List<string> list)
        {
            return list.Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
        }
        if (ruleVal is string s)
        {
            if (s.Contains(','))
            {
                return s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
            }
            return s.Equals(val, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}

public class FactNormalizer
{
    public static SharedFactStore NormalizeFacts(Dictionary<string, object> answers)
    {
        var store = new SharedFactStore();
        var f = store.Facts;

        // ==========================================
        // 1. FOUNDERS FACTS (§24 & §22)
        // ==========================================
        if (answers.TryGetValue("FND-C01", out var fndC01Raw) && fndC01Raw != null)
        {
            var fndC01 = fndC01Raw.ToString() ?? "";
            switch (fndC01)
            {
                case "solo":
                    f["founders.count"] = 1;
                    f["founders.activeCount"] = 1;
                    f["founders.isSolo"] = true;
                    f["founders.inactiveExists"] = false;
                    break;
                case "2":
                    f["founders.count"] = 2;
                    f["founders.activeCount"] = 2;
                    f["founders.isSolo"] = false;
                    f["founders.inactiveExists"] = false;
                    break;
                case "3":
                    f["founders.count"] = 3;
                    f["founders.activeCount"] = 3;
                    f["founders.isSolo"] = false;
                    f["founders.inactiveExists"] = false;
                    break;
                case "4plus":
                    f["founders.count"] = 4;
                    f["founders.activeCount"] = 4;
                    f["founders.isSolo"] = false;
                    f["founders.inactiveExists"] = false;
                    break;
                case "inactive_exist":
                    f["founders.count"] = "multiple";
                    f["founders.activeCount"] = "unknown";
                    f["founders.isSolo"] = false;
                    f["founders.inactiveExists"] = true;
                    break;
                default:
                    f["founders.count"] = "unknown";
                    f["founders.activeCount"] = "unknown";
                    break;
            }
        }
        else
        {
            f["founders.count"] = "unknown";
            f["founders.activeCount"] = "unknown";
            f["founders.inactiveExists"] = false;
        }

        if (answers.TryGetValue("FND-C03", out var fndC03Raw) && fndC03Raw != null)
        {
            var fndC03 = fndC03Raw.ToString() ?? "";
            if (fndC03 is "formal_only" or "departed_unresolved" or "unresolved" or "conflict" or "dispute")
            {
                f["founders.inactiveExists"] = true;
            }
            f["founders.departedFounderExists"] = fndC03 is "departed_clean" or "resolved" or "departed_unresolved" or "unresolved" or "conflict" or "dispute";
            f["founders.departedFounderStatus"] = fndC03 switch
            {
                "departed_clean" or "resolved" => "clean",
                "departed_unresolved" or "unresolved" => "unresolved",
                "conflict" or "dispute" => "dispute",
                "formal_only" => "formal_only",
                "none" => "none",
                _ => fndC03
            };
        }
        else
        {
            f["founders.departedFounderExists"] = false;
            f["founders.departedFounderStatus"] = "none";
        }

        if (answers.TryGetValue("FND-C02", out var fndC02Raw) && fndC02Raw != null)
        {
            var shares = new List<double>();
            if (fndC02Raw is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in je.EnumerateArray())
                    {
                        if (item.TryGetDouble(out var d)) shares.Add(d);
                        else if (double.TryParse(item.GetString(), out var ps)) shares.Add(ps);
                    }
                }
                else if (je.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in je.EnumerateObject())
                    {
                        if (prop.Value.TryGetDouble(out var d)) shares.Add(d);
                        else if (double.TryParse(prop.Value.GetString(), out var ps)) shares.Add(ps);
                    }
                }
            }
            else if (fndC02Raw is IEnumerable<double> enumDouble)
            {
                shares.AddRange(enumDouble);
            }
            else if (fndC02Raw is IEnumerable<int> enumInt)
            {
                shares.AddRange(enumInt.Select(i => (double)i));
            }
            else if (fndC02Raw is IDictionary<string, double> dictDouble)
            {
                shares.AddRange(dictDouble.Values);
            }
            else if (fndC02Raw is IDictionary<string, object> dictObj)
            {
                foreach (var v in dictObj.Values)
                {
                    if (double.TryParse(v?.ToString(), out var d)) shares.Add(d);
                }
            }
            else
            {
                var str = fndC02Raw.ToString() ?? "";
                var matches = Regex.Matches(str, @"\b\d+(?:\.\d+)?\b");
                foreach (Match m in matches)
                {
                    if (double.TryParse(m.Value, out var val)) shares.Add(val);
                }
            }

            if (shares.Count >= 2)
            {
                var maxShare = shares.Max();
                bool is5050 = shares.Count == 2 && Math.Abs(shares[0] - shares[1]) < 0.01;
                bool nearEqual = shares.Count == 2 ? Math.Abs(shares[0] - shares[1]) <= 10.0 : maxShare <= 50.0;
                f["founders.equityShares"] = shares;
                f["founders.isEqual5050"] = is5050;
                f["founders.nearEqualControl"] = nearEqual;
            }
            else
            {
                f["founders.isEqual5050"] = false;
                f["founders.nearEqualControl"] = false;
            }
        }
        else
        {
            f["founders.isEqual5050"] = false;
            f["founders.nearEqualControl"] = false;
        }

        if (answers.TryGetValue("FND-C04", out var fndC04Raw) && fndC04Raw != null)
        {
            f["founders.founderAgreementStatus"] = fndC04Raw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-01", out var fnd01Raw) && fnd01Raw != null)
        {
            var fnd01 = fnd01Raw.ToString() ?? "";
            f["founders.activeDispute"] = fnd01 is "material" or "active_conflict" or "formal_dispute";
            f["founders.disputeLevel"] = fnd01 switch
            {
                "none" => "none",
                "minor" => "minor",
                "material" => "material",
                "active_conflict" => "active",
                "formal_dispute" => "formal",
                _ => fnd01
            };
        }

        if (answers.TryGetValue("FND-02", out var fnd02Raw) && fnd02Raw != null)
        {
            f["founders.roleClarity"] = fnd02Raw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-03", out var fnd03Raw) && fnd03Raw != null)
        {
            var fnd03 = fnd03Raw.ToString() ?? "";
            f["founders.commitmentStatus"] = fnd03;
            if (fnd03 == "stopped")
            {
                f["founders.inactiveExists"] = true;
                if ((string?)f["founders.departedFounderStatus"] == "none")
                {
                    f["founders.departedFounderStatus"] = "stopped";
                }
            }
        }

        if (answers.TryGetValue("FND-04", out var fnd04Raw) && fnd04Raw != null)
        {
            var fnd04 = fnd04Raw.ToString() ?? "";
            f["founders.equityClarity"] = fnd04 switch
            {
                "registered" => "registered",
                "written_agreed" => "written_agreed",
                "preliminary" => "preliminary",
                "verbal" => "verbal",
                "ambiguous" => "ambiguous",
                "dispute" => "dispute",
                _ => fnd04
            };
        }

        if (answers.TryGetValue("FND-05", out var fnd05Raw) && fnd05Raw != null)
        {
            var fnd05 = fnd05Raw.ToString() ?? "";
            f["founders.vestingStatus"] = fnd05 switch
            {
                "vesting" or "reverse_vesting" or "cliff_only" => "vesting_signed",
                "repurchase" => "repurchase_signed",
                "verbal_rule" => "verbal_rule",
                "informal" => "informal",
                "none" or "not_discussed" or "retains_all" => "none",
                _ => fnd05
            };
        }

        if (answers.TryGetValue("FND-05A", out var fnd05aRaw) && fnd05aRaw != null)
        {
            f["founders.leaverRules"] = fnd05aRaw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-06", out var fnd06Raw) && fnd06Raw != null)
        {
            f["founders.governanceClarity"] = fnd06Raw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-06A", out var fnd06aRaw) && fnd06aRaw != null)
        {
            f["founders.keyDecisionMode"] = fnd06aRaw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-07", out var fnd07Raw) && fnd07Raw != null)
        {
            f["founders.deadlockMechanism"] = fnd07Raw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-08", out var fnd08Raw) && fnd08Raw != null)
        {
            var fnd08 = fnd08Raw.ToString() ?? "";
            f["founders.exitRules"] = fnd08 == "already_unresolved" ? "unresolved_departure" : fnd08;
        }

        if (answers.TryGetValue("FND-09", out var fnd09Raw) && fnd09Raw != null)
        {
            f["founders.personalContributions"] = fnd09Raw.ToString() ?? "";
        }

        if (answers.TryGetValue("FND-10", out var fnd10Raw) && fnd10Raw != null)
        {
            var fnd10 = fnd10Raw.ToString() ?? "";
            f["founders.externalActivity"] = fnd10 switch
            {
                "none" => "none",
                "unrelated" or "no_overlap" => "unrelated",
                "overlap_rules" or "settled" => "overlap_rules",
                "potential_competitor" or "competing" => "potential_competitor",
                "employer_same_field" or "employer" => "employer_same_field",
                "active_competition" => "active_competition",
                _ => "unknown"
            };
            f["founders.externalEmployerSameField"] = fnd10 is "employer_same_field" or "employer" or "active_competition";
            f["founders.hasConflictOfInterest"] = fnd10 is "potential_competitor" or "competing" or "employer_same_field" or "employer" or "active_competition";
        }

        if (answers.TryGetValue("FND-11", out var fnd11Raw) && fnd11Raw != null)
        {
            f["founders.strategicAlignment"] = fnd11Raw.ToString() ?? "";
        }

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

        // ==========================================
        // 3. IP FACTS (§24 & §23.3)
        // ==========================================
        if (answers.TryGetValue("IP-01", out var ip01Raw) && ip01Raw != null)
        {
            var ip01 = ip01Raw.ToString() ?? "";
            bool coreProductExists = ip01 != "idea" && !string.IsNullOrEmpty(ip01);
            f["ip.coreProductExists"] = coreProductExists;
            f["product.stage"] = ip01 switch
            {
                "idea" => "idea",
                "prototype" => "prototype",
                "ready" => "live_or_ready",
                "multiple" => "multiple_products",
                _ => ip01
            };
        }
        else
        {
            f["ip.coreProductExists"] = false;
        }

        if (answers.TryGetValue("IP-02", out var ip02Raw) && ip02Raw != null)
        {
            f["ip.assets"] = GetAnswerList(answers, "IP-02");
        }

        if (answers.TryGetValue("IP-03", out var ip03Raw) && ip03Raw != null)
        {
            f["ip.creators"] = GetAnswerList(answers, "IP-03");
        }

        if (answers.TryGetValue("IP-04", out var ip04Raw) && ip04Raw != null)
        {
            var ip04 = ip04Raw.ToString() ?? "";
            f["ip.overallRightsEvidence"] = ip04 switch
            {
                "all" => "all",
                "main" => "main",
                "some" => "some",
                "informal" => "informal",
                "none" => "none",
                _ => ip04
            };
        }

        if (answers.TryGetValue("IP-05", out var ip05Raw) && ip05Raw != null)
        {
            var ip05 = ip05Raw.ToString() ?? "";
            f["ip.founderRights"] = ip05 switch
            {
                "assigned" => "assigned",
                "covered" => "covered",
                "partial" => "partial",
                "agreed" => "agreed_not_completed",
                "founder_owned" => "founder_owned",
                _ => ip05
            };
        }

        if (answers.TryGetValue("IP-06", out var ip06Raw) && ip06Raw != null)
        {
            var ip06 = ip06Raw.ToString() ?? "";
            f["ip.employeeRights"] = ip06 switch
            {
                "all" => "all",
                "key_gaps" => "key_gaps",
                "not_reviewed" => "not_reviewed",
                "missing_some" => "missing_some",
                _ => ip06
            };
        }

        if (answers.TryGetValue("IP-07", out var ip07Raw) && ip07Raw != null)
        {
            var ip07 = ip07Raw.ToString() ?? "";
            f["ip.contractorRights"] = ip07 switch
            {
                "all" => "all",
                "most" => "most",
                "unclear_clause" => "unclear_clause",
                "payment_only" => "payment_only",
                "no_contract" => "no_contract",
                _ => ip07
            };
        }

        if (answers.TryGetValue("IP-08", out var ip08Raw) && ip08Raw != null)
        {
            var ip08 = ip08Raw.ToString() ?? "";
            f["ip.formerCreatorStatus"] = ip08 switch
            {
                "none" => "none",
                "complete" => "complete",
                "partial" => "partial",
                "unresolved" => "unresolved",
                "dispute" => "dispute",
                _ => ip08
            };
        }

        if (answers.TryGetValue("IP-09", out var ip09Raw) && ip09Raw != null)
        {
            var ip09 = ip09Raw.ToString() ?? "";
            f["ip.studioRights"] = ip09 switch
            {
                "confirmed" => "confirmed",
                "agency_only" => "agency_only",
                "subcontractors_unchecked" => "subcontractors_unchecked",
                "unknown_chain" => "unknown_chain",
                _ => ip09
            };
        }

        if (answers.TryGetValue("IP-10", out var ip10Raw) && ip10Raw != null)
        {
            var ip10 = ip10Raw.ToString() ?? "";
            f["ip.externalEmployerCreation"] = ip10 switch
            {
                "no" => "no",
                "unrelated" => "unrelated",
                "lawyer_checked" => "lawyer_checked",
                "not_reviewed" => "not_reviewed",
                "unknown" => "unknown",
                _ => ip10
            };
        }

        if (answers.TryGetValue("IP-10A", out var ip10ARaw) && ip10ARaw != null)
        {
            var ip10A = ip10ARaw.ToString() ?? "";
            f["ip.employerResourcesUsed"] = ip10A switch
            {
                "yes" => true,
                "no" => false,
                "possible" => "possible",
                "unknown" => "unknown",
                _ => ip10A
            };
        }

        if (answers.TryGetValue("IP-11", out var ip11Raw) && ip11Raw != null)
        {
            var ip11 = ip11Raw.ToString() ?? "";
            f["ip.thirdPartyComponentsUsed"] = ip11 switch
            {
                "yes" or "likely" => true,
                "no" => false,
                _ => "unknown"
            };
        }

        if (answers.TryGetValue("IP-11A", out var ip11ARaw) && ip11ARaw != null)
        {
            var ip11A = ip11ARaw.ToString() ?? "";
            f["ip.thirdPartyTermsReview"] = ip11A switch
            {
                "yes" => "systematic",
                "main" => "main",
                "developers_only" => "developers_only",
                "no" => "none",
                "unknown" => "unknown",
                _ => ip11A
            };
        }

        if (answers.TryGetValue("IP-12", out var ip12Raw) && ip12Raw != null)
        {
            var ip12 = ip12Raw.ToString() ?? "";
            f["ip.externalDependency"] = ip12 switch
            {
                "no" => "none",
                "known" => "known",
                "unchecked" => "unchecked",
                "critical" => "critical",
                _ => ip12
            };
        }

        if (answers.TryGetValue("IP-13", out var ip13Raw) && ip13Raw != null)
        {
            var ip13 = ip13Raw.ToString() ?? "";
            f["ip.criticalAccountsControl"] = ip13 switch
            {
                "company" => "company",
                "mixed" => "mixed",
                "one_founder" => "one_founder",
                "worker" => "worker",
                _ => ip13
            };
        }

        if (answers.TryGetValue("IP-14", out var ip14Raw) && ip14Raw != null)
        {
            var ip14 = ip14Raw.ToString() ?? "";
            f["ip.brandDomainControl"] = ip14 switch
            {
                "company" => "company",
                "mixed" => "mixed",
                "founder" => "founder",
                "worker" => "worker",
                _ => ip14
            };
            f["ip.brandRegistration"] = ip14 == "brand_not_registered" ? "not_registered" : "registered";
        }

        if (answers.TryGetValue("IP-15", out var ip15Raw) && ip15Raw != null)
        {
            var ip15 = ip15Raw.ToString() ?? "";
            f["ip.contentProvenance"] = ip15 switch
            {
                "clear" => "clear",
                "licensed" => "licensed",
                "unchecked" => "unchecked",
                "risk" => "risk",
                _ => ip15
            };
        }

        // ==========================================
        // 4. TEAM & OTHER ACTIVITY SIGNALS (§24)
        // ==========================================
        var teamC01 = GetAnswerStr(answers, "TEAM-C01");
        f["team.hasNonFounderTeam"] = teamC01 != "founders_only" && teamC01 != "solo_only" && !string.IsNullOrEmpty(teamC01) && teamC01 != "none";

        var rev01 = GetAnswerStr(answers, "REV-01");
        var revC01 = GetAnswerStr(answers, "REV-C01");
        bool hasRev = (rev01 != "none" && !string.IsNullOrEmpty(rev01)) || (revC01 != "none" && !string.IsNullOrEmpty(revC01));
        f["company.hasRevenue"] = hasRev;
        f["revenue.exists"] = hasRev;

        var data01 = GetAnswerStr(answers, "DATA-01");
        var data02 = GetAnswerStr(answers, "DATA-02");
        f["data.personalDataProcessed"] = data01 == "yes" || (!string.IsNullOrEmpty(data02) && data02 != "none");

        var ai01 = GetAnswerStr(answers, "AI-01");
        f["ai.used"] = ai01 is "external" or "own" or "both";

        var ai02 = GetAnswerStr(answers, "AI-02");
        f["ai.sensitiveDataSent"] = ai02 == "sensitive";

        var contract01 = GetAnswerStr(answers, "CONTRACT-01");
        f["contracts.b2bRelevant"] = contract01 != "none" && !string.IsNullOrEmpty(contract01);

        var invest01 = GetAnswerStr(answers, "INVEST-01");
        f["investment.timing"] = invest01 switch
        {
            "m3" or "m3_6" => "near_term",
            "m6_12" => "mid_term",
            "looking" or "discussing" or "terms" => "active",
            _ => "none"
        };

        var invest02 = GetAnswerStr(answers, "INVEST-02");
        var invC01 = GetAnswerStr(answers, "INV-C01");
        f["investment.priorInvestment"] = (invest02 != "none" && !string.IsNullOrEmpty(invest02)) || invC01 == "yes";

        return store;
    }

    private static string FormatJurisdictionName(string? code)
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

    private static string FormatRoleName(string? role)
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

    private static string GetAnswerStr(Dictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return "";
        return val.ToString() ?? "";
    }

    private static List<string> GetAnswerList(Dictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return new List<string>();
        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var res = new List<string>();
            foreach (var item in je.EnumerateArray()) res.Add(item.ToString());
            return res;
        }
        if (val is IEnumerable<string> list) return list.ToList();
        return new List<string> { val.ToString() ?? "" };
    }
}

public class ScoringEngine
{
    private readonly QuestionRepository _repository;

    public ScoringEngine(QuestionRepository repository)
    {
        _repository = repository;
    }

    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var allSections = _repository.GetSections().OrderBy(s => s.Order).ToList();
        var allQuestions = _repository.GetQuestions();
        var allRisks = _repository.GetRisks();

        // 1. Fact Normalization
        var factStore = FactNormalizer.NormalizeFacts(answers);

        // 2. Visible Questions
        var visibleQs = allQuestions.Where(q => ConditionsEvaluator.IsVisible(q.ShowIf, answers, factStore)).ToList();

        var sections = new List<SectionScore>();
        double totalApplicableModuleWeight = 0;
        double weightedModuleScoreSum = 0;

        double totalDiagnosticQuestionWeight = 0;
        double weightedConfidenceSum = 0;

        var allDimensionScores = new List<DimensionScore>();

        foreach (var sec in allSections)
        {
            var sectionQs = visibleQs.Where(q => q.SectionId == sec.Id).ToList();
            bool isApplicable = IsModuleApplicable(sec.Id, factStore, sectionQs);

            if (!isApplicable)
            {
                sections.Add(new SectionScore
                {
                    SectionId = sec.Id,
                    Title = sec.Title,
                    Score = null,
                    Weight = sec.Weight,
                    Status = "N_A",
                    Confidence = 100,
                    Findings = new List<string>(),
                    Dimensions = new List<DimensionScore>()
                });
                continue;
            }

            var diagnosticQs = sectionQs.Where(q => q.ScoreMode == "diagnostic").ToList();
            var dimensionGroups = diagnosticQs.GroupBy(q => !string.IsNullOrEmpty(q.DimensionId) ? q.DimensionId : q.Id).ToList();

            var sectionDimensions = new List<DimensionScore>();
            double totalApplicableDimWeight = 0;
            double weightedDimScoreSum = 0;

            foreach (var dimGroup in dimensionGroups)
            {
                var dimId = dimGroup.Key;
                var dimQuestions = dimGroup.ToList();
                double firstDimWeight = dimQuestions.First().DimensionWeight;
                if (firstDimWeight <= 0) firstDimWeight = dimQuestions.First().Weight;

                double applicableWithinDimWeightSum = 0;
                double weightedQuestionScoreSum = 0;

                foreach (var q in dimQuestions)
                {
                    if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null) continue;
                    var opt = q.Options?.FirstOrDefault(o => o.Id == ansVal.ToString());
                    if (opt == null) continue;

                    double withinWeight = q.WithinDimensionWeight > 0 ? q.WithinDimensionWeight : 100.0;
                    applicableWithinDimWeightSum += withinWeight;
                    weightedQuestionScoreSum += opt.Score * withinWeight;

                    // Question-level confidence tracking
                    double confFactor = opt.ConfidenceClass switch
                    {
                        "known" => 1.0,
                        "partial" => 0.5,
                        "unknown" => 0.0,
                        _ => 1.0
                    };
                    double effectiveQWeight = (firstDimWeight * withinWeight) / 100.0;
                    totalDiagnosticQuestionWeight += effectiveQWeight;
                    weightedConfidenceSum += confFactor * effectiveQWeight;
                }

                if (applicableWithinDimWeightSum > 0)
                {
                    int dimScore = (int)Math.Round((weightedQuestionScoreSum / applicableWithinDimWeightSum) * 100.0);
                    var dimModel = new DimensionScore
                    {
                        DimensionId = dimId,
                        Score = dimScore,
                        Weight = firstDimWeight,
                        IsApplicable = true
                    };
                    sectionDimensions.Add(dimModel);
                    allDimensionScores.Add(dimModel);

                    totalApplicableDimWeight += firstDimWeight;
                    weightedDimScoreSum += dimScore * firstDimWeight;
                }
            }

            int? sectionScore = null;
            if (totalApplicableDimWeight > 0)
            {
                sectionScore = (int)Math.Round(weightedDimScoreSum / totalApplicableDimWeight);
                totalApplicableModuleWeight += sec.Weight;
                weightedModuleScoreSum += sectionScore.Value * sec.Weight;
            }
            else if (sec.Id == "founders" && answers.TryGetValue("FND-C01", out var fndAns) && fndAns?.ToString() == "solo" && !GetBoolFact(factStore.Facts, "founders.inactiveExists"))
            {
                // Solo founder: zero diagnostic questions shown, normative score is 100 per §22.1 & §23.1
                sectionScore = 100;
                totalApplicableModuleWeight += sec.Weight;
                weightedModuleScoreSum += 100 * sec.Weight;
            }

            sections.Add(new SectionScore
            {
                SectionId = sec.Id,
                Title = sec.Title,
                Score = sectionScore,
                Weight = sec.Weight,
                Status = "APPLICABLE",
                Confidence = 100,
                Dimensions = sectionDimensions
            });
        }

        int overallScore = 0;
        if (totalApplicableModuleWeight > 0)
        {
            overallScore = (int)Math.Round(weightedModuleScoreSum / totalApplicableModuleWeight);
        }

        int overallConfidence = 0;
        if (totalDiagnosticQuestionWeight > 0)
        {
            overallConfidence = (int)Math.Round((weightedConfidenceSum / totalDiagnosticQuestionWeight) * 100.0);
        }

        // 3. Merged & Suppressed Findings (Generated STRICTLY from Rule Engine evaluating SharedFactStore)
        var rawFindings = CollectRawFindings(factStore, allRisks);
        var mergedFindings = MergeAndSuppressFindings(rawFindings, factStore);

        // 4. Dimension-Level Strong Areas (§20 Acceptance Criteria)
        // Finding -> affected DimensionId(s) deterministic mapping
        var strongAreas = new List<string>();
        foreach (var dim in allDimensionScores)
        {
            if (dim.Score >= 80)
            {
                bool hasSevereRisk = mergedFindings.Any(r =>
                    GetAffectedDimensions(r.Code).Contains(dim.DimensionId) &&
                    r.Severity is "CRITICAL" or "HIGH" or "BLOCKER");

                if (!hasSevereRisk)
                {
                    strongAreas.Add(GetDimensionDisplayName(dim.DimensionId));
                }
            }
        }

        // 5. Investment Readiness Overlay
        var investmentOverlay = CalculateInvestmentReadiness(answers, factStore, mergedFindings);

        // 6. Consulting Recommendation
        var consulting = CalculateConsultingRecommendation(mergedFindings, factStore, overallScore);

        var level = GetLevel(overallScore);

        return new ScoreResult
        {
            Overall = overallScore,
            Confidence = overallConfidence,
            ConfidenceText = GetConfidenceText(overallConfidence),
            Level = level,
            LevelTitle = GetLevelTitle(level),
            LevelText = GetLevelText(level),
            Sections = sections,
            Risks = mergedFindings,
            CriticalCount = mergedFindings.Count(r => r.Severity is "CRITICAL" or "BLOCKER"),
            HighCount = mergedFindings.Count(r => r.Severity == "HIGH"),
            MediumCount = mergedFindings.Count(r => r.Severity == "MEDIUM"),
            Strengths = strongAreas.Distinct().ToList(),
            AnsweredCount = visibleQs.Count(q => answers.ContainsKey(q.Id)),
            InvestmentReadiness = investmentOverlay,
            Consulting = consulting,
            Versions = new ScoreVersions(),
            ComputedAt = DateTime.UtcNow.ToString("o")
        };
    }

    public static List<string> GetAffectedDimensions(string riskCode)
    {
        return riskCode switch
        {
                        // Founders (Canonical §25 — 18 Findings)
            "FND_ACTIVE_DISPUTE" => new() { "existing_dispute" },
            "FND_EQUITY_DISPUTE" => new() { "equity_clarity" },
            "FND_DEAD_EQUITY" => new() { "early_exit_equity", "commitment" },
            "FND_DEADLOCK" => new() { "deadlock" },
            "FND_DEPARTED_UNRESOLVED" => new() { "early_exit_equity", "exit_continuity" },
            "FND_CONFLICT_OF_INTEREST" => new() { "conflict_of_interest" },
            "FND_ROLE_AMBIGUITY" => new() { "roles" },
            "FND_COMMITMENT_MISMATCH" => new() { "commitment" },
            "FND_EQUITY_NOT_FORMALIZED" => new() { "equity_clarity" },
            "FND_EQUITY_AMBIGUITY" => new() { "equity_clarity" },
            "FND_NO_VESTING" => new() { "early_exit_equity" },
            "FND_INCOMPLETE_LEAVER_RULES" => new() { "early_exit_equity" },
            "FND_GOVERNANCE_AMBIGUITY" => new() { "governance" },
            "FND_NO_DEADLOCK_PROTECTION" => new() { "deadlock" },
            "FND_EXIT_RULES_MISSING" => new() { "exit_continuity" },
            "FND_CONTRIBUTION_AMBIGUITY" => new() { "founder_contributions" },
            "FND_STRATEGIC_MISALIGNMENT" => new() { "strategic_alignment" },
            "FND_DOCUMENTATION_GAP" => new() { "governance" },

// Corporate
            "COR_NO_ENTITY_FOR_ACTIVITY" => new() { "ownership_accuracy", "entity_alignment" },
            "COR_OWNERSHIP_DISPUTE" or "COR_OWNERSHIP_MISMATCH" => new() { "ownership_accuracy" },
            "COR_CAP_TABLE_GAP" or "COR_CAP_TABLE_UNRELIABLE" => new() { "cap_table" },
            "COR_UNDOCUMENTED_EQUITY" => new() { "equity_commitments" },
            "COR_CORPORATE_HISTORY_GAP" => new() { "corporate_history" },
            "COR_APPROVAL_GAP" => new() { "corporate_approvals" },
            "COR_AUTHORITY_GAP" => new() { "authority" },
            "COR_ENTITY_MISMATCH" => new() { "entity_alignment" },
            "COR_RECORDS_GAP" => new() { "records" },
            "COR_HIDDEN_CONTROL" => new() { "ownership_accuracy" },

            // IP
            "IP_PRODUCT_RIGHTS_UNCONFIRMED" => new() { "overall_rights" },
            "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" => new() { "founder_rights" },
            "IP_CONTRACTOR_RIGHTS_GAP" => new() { "external_creators", "employee_rights" },
            "IP_FORMER_DEVELOPER_GAP" => new() { "external_creators" },
            "IP_STUDIO_RIGHTS_GAP" => new() { "external_creators" },
            "IP_EMPLOYER_RISK" => new() { "external_employer" },
            "IP_THIRD_PARTY_COMPONENTS" or "IP_EXTERNAL_DEPENDENCY" => new() { "third_party_dependencies" },
            "IP_ACCESS_CONTROL" => new() { "technical_control" },
            "IP_BRAND_DOMAIN_CONTROL" or "IP_DOMAIN_BRAND_CONTROL" or "IP_BRAND_REGISTRATION_INFO" => new() { "brand_domain" },
            "IP_CONTENT_RIGHTS" => new() { "content_provenance" },

            _ => throw new InvalidOperationException($"Unmapped severe risk code '{riskCode}' in GetAffectedDimensions mapping invariant.")
        };
    }

    private bool IsModuleApplicable(string sectionId, SharedFactStore facts, List<DiagnosticQuestion> sectionQs)
    {
        var f = facts.Facts;
        return sectionId switch
        {
            "founders" => true,
            "corporate" => (string?)f.GetValueOrDefault("company.entityStatus") is "incorporated" or "single" or "multiple" or "registering",
            "ip" => true,
            "team" => GetBoolFact(f, "team.hasNonFounderTeam"),
            "data" => GetBoolFact(f, "data.personalDataProcessed") || GetBoolFact(f, "ai.used"),
            "contracts" => GetBoolFact(f, "contracts.b2bRelevant"),
            "investment" => (string?)f.GetValueOrDefault("investment.timing") != "none" || GetBoolFact(f, "investment.priorInvestment"),
            _ => sectionQs.Count > 0
        };
    }

    private bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }

    private List<RiskFinding> CollectRawFindings(SharedFactStore facts, List<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

                        // ========================================================
        // BLOCK 1: FOUNDERS RULE DEFINITIONS (CANONICAL §25 & §27.2)
        // ========================================================
        var activeCountObj = f.GetValueOrDefault("founders.activeCount");
        int? activeCount = activeCountObj is int ac ? ac : (int?)null;
        var founderAgree = (string?)f.GetValueOrDefault("founders.founderAgreementStatus");
        var equityClarity = (string?)f.GetValueOrDefault("founders.equityClarity");
        bool activeDispute = GetBoolFact(f, "founders.activeDispute");
        var disputeLevel = (string?)f.GetValueOrDefault("founders.disputeLevel");
        bool isEqual5050 = GetBoolFact(f, "founders.isEqual5050");
        bool nearEqualControl = GetBoolFact(f, "founders.nearEqualControl") || isEqual5050;
        var keyDecisionMode = (string?)f.GetValueOrDefault("founders.keyDecisionMode");
        var deadlockMech = (string?)f.GetValueOrDefault("founders.deadlockMechanism");
        var vestingStatus = (string?)f.GetValueOrDefault("founders.vestingStatus");
        var leaverRules = (string?)f.GetValueOrDefault("founders.leaverRules");
        bool inactiveExists = GetBoolFact(f, "founders.inactiveExists");
        var departedStatus = (string?)f.GetValueOrDefault("founders.departedFounderStatus");
        var roleClarity = (string?)f.GetValueOrDefault("founders.roleClarity");
        var commitmentStatus = (string?)f.GetValueOrDefault("founders.commitmentStatus");
        var extActivity = (string?)f.GetValueOrDefault("founders.externalActivity");
        var govClarity = (string?)f.GetValueOrDefault("founders.governanceClarity");
        var exitRules = (string?)f.GetValueOrDefault("founders.exitRules");
        var personalContribs = (string?)f.GetValueOrDefault("founders.personalContributions");
        var stratAlign = (string?)f.GetValueOrDefault("founders.strategicAlignment");

        // FND_DEPARTED_UNRESOLVED (CRITICAL) — §27.2: departedFounderStatus in [unresolved, dispute] OR exitRules == "unresolved_departure"
        if (departedStatus is "unresolved" or "dispute" || exitRules == "unresolved_departure" || (inactiveExists && departedStatus is "stopped" or "formal_only"))
        {
            AddFinding(list, allRisks, "FND_DEPARTED_UNRESOLVED", "FND-C03", departedStatus ?? exitRules ?? "unresolved", "CRITICAL");
        }

        if (activeCount.HasValue && activeCount.Value >= 2)
        {
            // FND_ACTIVE_DISPUTE (CRITICAL) — §24: disputeLevel in [active, formal]
            if (disputeLevel is "active" or "formal")
            {
                AddFinding(list, allRisks, "FND_ACTIVE_DISPUTE", "FND-01", disputeLevel, "CRITICAL");
            }

            // FND_EQUITY_DISPUTE (CRITICAL)
            if (equityClarity == "dispute" || departedStatus == "dispute")
            {
                AddFinding(list, allRisks, "FND_EQUITY_DISPUTE", "FND-04", "dispute", "CRITICAL");
            }
            // FND_EQUITY_AMBIGUITY (HIGH)
            else if (equityClarity == "ambiguous")
            {
                AddFinding(list, allRisks, "FND_EQUITY_AMBIGUITY", "FND-04", "ambiguous", "HIGH");
            }
            // FND_EQUITY_NOT_FORMALIZED (MEDIUM)
            else if (equityClarity is "verbal" or "preliminary" || founderAgree is "oral" or "none" or "in_progress" or "draft" or "informal")
            {
                AddFinding(list, allRisks, "FND_EQUITY_NOT_FORMALIZED", "FND-04", equityClarity ?? founderAgree ?? "unformalized", "MEDIUM");
            }

            // FND_DEADLOCK (CRITICAL) — Strict §27.2: activeCount == 2 AND nearEqualControl AND keyDecisionMode in [material_unanimity, broad_unanimity] AND score(FND-07) <= 0.15
            if (activeCount.Value == 2 && nearEqualControl && keyDecisionMode is "material_unanimity" or "broad_unanimity" && deadlockMech is "none" or "only_agree" or "unknown")
            {
                AddFinding(list, allRisks, "FND_DEADLOCK", "FND-07", deadlockMech ?? "none", "CRITICAL");
            }
            // FND_NO_DEADLOCK_PROTECTION (HIGH)
            else if (deadlockMech is "none" or "only_agree" or "unknown")
            {
                AddFinding(list, allRisks, "FND_NO_DEADLOCK_PROTECTION", "FND-07", deadlockMech ?? "only_agree", "HIGH");
            }

            // FND_DEAD_EQUITY (CRITICAL)
            if ((vestingStatus is "none" or "informal" or "verbal_rule") && (departedStatus is "unresolved" or "stopped" || commitmentStatus == "stopped" || inactiveExists))
            {
                AddFinding(list, allRisks, "FND_DEAD_EQUITY", "FND-03", commitmentStatus ?? departedStatus ?? "stopped", "CRITICAL");
            }

            // FND_NO_VESTING (HIGH)
            if (vestingStatus is "none" or "informal" or "not_discussed" or "verbal_rule")
            {
                AddFinding(list, allRisks, "FND_NO_VESTING", "FND-05", vestingStatus ?? "none", "HIGH");
            }

            // FND_INCOMPLETE_LEAVER_RULES (MEDIUM)
            if (leaverRules is "oral" or "none" or "partial")
            {
                AddFinding(list, allRisks, "FND_INCOMPLETE_LEAVER_RULES", "FND-05A", leaverRules, "MEDIUM");
            }

            // FND_ROLE_AMBIGUITY (MEDIUM / HIGH)
            if (roleClarity is "overlap" or "disputed")
            {
                string sev = roleClarity == "disputed" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_ROLE_AMBIGUITY", "FND-02", roleClarity, sev);
            }

            // FND_COMMITMENT_MISMATCH (HIGH)
            if (commitmentStatus is "below_expected")
            {
                AddFinding(list, allRisks, "FND_COMMITMENT_MISMATCH", "FND-03", commitmentStatus, "HIGH");
            }

            // FND_CONFLICT_OF_INTEREST (HIGH / CRITICAL)
            if (extActivity is "potential_competitor" or "employer_same_field" or "active_competition")
            {
                string sev = extActivity == "active_competition" ? "CRITICAL" : "HIGH";
                AddFinding(list, allRisks, "FND_CONFLICT_OF_INTEREST", "FND-10", extActivity, sev);
            }

            // FND_GOVERNANCE_AMBIGUITY (MEDIUM / HIGH)
            if (govClarity is "none" or "all_together" or "partial" || keyDecisionMode is "broad_unanimity" or "undefined")
            {
                string sev = govClarity == "none" || keyDecisionMode == "undefined" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_GOVERNANCE_AMBIGUITY", "FND-06", govClarity ?? keyDecisionMode ?? "none", sev);
            }

            // FND_EXIT_RULES_MISSING (MEDIUM)
            if (exitRules is "none" or "oral")
            {
                AddFinding(list, allRisks, "FND_EXIT_RULES_MISSING", "FND-08", exitRules, "MEDIUM");
            }

            // FND_CONTRIBUTION_AMBIGUITY (MEDIUM / HIGH)
            if (personalContribs is "material_unclear" or "dispute")
            {
                string sev = personalContribs == "dispute" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_CONTRIBUTION_AMBIGUITY", "FND-09", personalContribs, sev);
            }

            // FND_STRATEGIC_MISALIGNMENT (MEDIUM / HIGH)
            if (stratAlign is "material_difference" or "conflict")
            {
                string sev = stratAlign == "conflict" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_STRATEGIC_MISALIGNMENT", "FND-11", stratAlign, sev);
            }

            // FND_DOCUMENTATION_GAP (MEDIUM)
            if (founderAgree is "draft" or "none" or "oral" or "informal" || disputeLevel == "material")
            {
                AddFinding(list, allRisks, "FND_DOCUMENTATION_GAP", "FND-C04", founderAgree ?? "informal", "MEDIUM");
            }
        }

        // ========================================================
        // BLOCK 2: CORPORATE RULE DEFINITIONS (§27.2 & §25)
        // ========================================================
        var entityStatus = (string?)f.GetValueOrDefault("company.entityStatus");
        bool hasRevenue = GetBoolFact(f, "company.hasRevenue");
        bool hasNonFounderTeam = GetBoolFact(f, "team.hasNonFounderTeam");
        bool priorInvestment = GetBoolFact(f, "investment.priorInvestment");

        // COR_NO_ENTITY_FOR_ACTIVITY
        if (entityStatus == "not_incorporated" && (hasRevenue || hasNonFounderTeam || priorInvestment))
        {
            AddFinding(list, allRisks, "COR_NO_ENTITY_FOR_ACTIVITY", "COR-C01", "none", "HIGH");
        }

        // COR_OWNERSHIP_DISPUTE & COR_OWNERSHIP_MISMATCH
        var ownershipMatch = (string?)f.GetValueOrDefault("capital.ownershipMatch");
        bool ownershipDispute = GetBoolFact(f, "capital.ownershipDispute");
        if (ownershipDispute || ownershipMatch == "dispute")
        {
            AddFinding(list, allRisks, "COR_OWNERSHIP_DISPUTE", "COR-01", "dispute", "CRITICAL");
        }
        else if (ownershipMatch is "planned_change" or "unregistered_holding" or "nominal")
        {
            AddFinding(list, allRisks, "COR_OWNERSHIP_MISMATCH", "COR-01", ownershipMatch, "HIGH");
        }

        // COR_CAP_TABLE_UNRELIABLE
        var capTableStatus = (string?)f.GetValueOrDefault("capital.capTableStatus");
        if (entityStatus is "incorporated" or "single" or "multiple" && capTableStatus is "fragmented" or "unreliable")
        {
            AddFinding(list, allRisks, "COR_CAP_TABLE_UNRELIABLE", "COR-02", capTableStatus, "HIGH");
        }

        // COR_UNDOCUMENTED_EQUITY
        var equityPromises = (string?)f.GetValueOrDefault("capital.equityPromises");
        if (equityPromises is "informal" or "unclear_terms" or "documented_not_included")
        {
            string sev = equityPromises is "informal" or "unclear_terms" ? "HIGH" : "MEDIUM";
            AddFinding(list, allRisks, "COR_UNDOCUMENTED_EQUITY", "COR-03", equityPromises, sev);
        }

        // COR_CORPORATE_HISTORY_GAP
        var historyStatus = (string?)f.GetValueOrDefault("capital.historyStatus");
        var historyTrace = (string?)f.GetValueOrDefault("capital.historyTrace");
        if (historyStatus is "partial" or "missing" || historyTrace is "partial" or "missing")
        {
            AddFinding(list, allRisks, "COR_CORPORATE_HISTORY_GAP", "COR-04", historyStatus ?? "partial", "HIGH");
        }

        // COR_APPROVAL_GAP
        var approvals = (string?)f.GetValueOrDefault("corporate.approvals");
        if (approvals is "inconsistent" or "often_missing")
        {
            AddFinding(list, allRisks, "COR_APPROVAL_GAP", "COR-05", approvals, "MEDIUM");
        }

        // COR_AUTHORITY_GAP
        var authority = (string?)f.GetValueOrDefault("corporate.authority");
        if (authority is "multiple_partial" or "unclear")
        {
            string sev = authority == "unclear" ? "HIGH" : "MEDIUM";
            AddFinding(list, allRisks, "COR_AUTHORITY_GAP", "COR-06", authority, sev);
        }

        // COR_ENTITY_MISMATCH
        var entityAlign = (string?)f.GetValueOrDefault("company.entityAlignment");
        if (entityAlign == "material_outside")
        {
            AddFinding(list, allRisks, "COR_ENTITY_MISMATCH", "COR-07", entityAlign, "HIGH");
        }

        // COR_RECORDS_GAP
        var records = (string?)f.GetValueOrDefault("corporate.records");
        if (records is "partial" or "disorganized")
        {
            string sev = records == "disorganized" ? "MEDIUM" : "LOW";
            AddFinding(list, allRisks, "COR_RECORDS_GAP", "COR-08", records, sev);
        }

        // ========================================================
        // BLOCK 3: IP RULE DEFINITIONS (§27.2 & §25)
        // ========================================================
        bool coreProductExists = GetBoolFact(f, "ip.coreProductExists");
        var overallRights = (string?)f.GetValueOrDefault("ip.overallRightsEvidence");

        // IP_PRODUCT_RIGHTS_UNCONFIRMED
        if (coreProductExists && entityStatus is "incorporated" or "single" or "multiple" && overallRights is "none" or "informal")
        {
            AddFinding(list, allRisks, "IP_PRODUCT_RIGHTS_UNCONFIRMED", "IP-04", overallRights ?? "none", "CRITICAL");
        }

        // IP_FOUNDER_RIGHTS_NOT_TRANSFERRED
        var ipCreators = f.GetValueOrDefault("ip.creators") as List<string> ?? new List<string>();
        var founderRights = (string?)f.GetValueOrDefault("ip.founderRights");
        if (ipCreators.Contains("founders") && founderRights is "founder_owned" or "agreed_not_completed" or "partial")
        {
            string sev = founderRights == "founder_owned" ? "CRITICAL" : "HIGH";
            AddFinding(list, allRisks, "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", "IP-05", founderRights, sev);
        }

        // IP_CONTRACTOR_RIGHTS_GAP
        var contractorRights = (string?)f.GetValueOrDefault("ip.contractorRights");
        var employeeRights = (string?)f.GetValueOrDefault("ip.employeeRights");
        if ((ipCreators.Contains("contractors") && contractorRights is "payment_only" or "no_contract" or "unclear_clause") ||
            (ipCreators.Contains("employees") && employeeRights is "missing_some" or "not_reviewed"))
        {
            AddFinding(list, allRisks, "IP_CONTRACTOR_RIGHTS_GAP", "IP-07", contractorRights ?? employeeRights ?? "unclear", "HIGH");
        }

        // IP_FORMER_DEVELOPER_GAP
        var formerStatus = (string?)f.GetValueOrDefault("ip.formerCreatorStatus");
        if (formerStatus is "unresolved" or "dispute" || (ipCreators.Contains("former") && contractorRights is "payment_only" or "no_contract" or "unclear_clause"))
        {
            AddFinding(list, allRisks, "IP_FORMER_DEVELOPER_GAP", "IP-08", formerStatus ?? "unresolved", "CRITICAL");
        }

        // IP_STUDIO_RIGHTS_GAP
        var studioRights = (string?)f.GetValueOrDefault("ip.studioRights");
        if (ipCreators.Contains("studio") && studioRights is "unknown_chain" or "subcontractors_unchecked")
        {
            AddFinding(list, allRisks, "IP_STUDIO_RIGHTS_GAP", "IP-09", studioRights, "HIGH");
        }

        // IP_EMPLOYER_RISK strictly according to rule contract:
        // 1. ip.employerResourcesUsed == true -> CRITICAL regardless of externalEmployerCreation
        // 2. externalEmployerCreation in [not_reviewed, unknown] AND employerResourcesUsed in [possible, unknown] -> HIGH
        // 3. lawyer_checked + true -> CRITICAL
        // 4. unrelated + false -> finding absent
        var extEmployer = (string?)f.GetValueOrDefault("ip.externalEmployerCreation");
        var resUsed = f.GetValueOrDefault("ip.employerResourcesUsed");
        bool resUsedTrue = resUsed is true || (resUsed is string sTrue && sTrue.Equals("yes", StringComparison.OrdinalIgnoreCase));
        bool resUsedPossibleOrUnknown = resUsed is "possible" or "unknown";
        bool extEmployerRisky = extEmployer is "not_reviewed" or "unknown";

        if (resUsedTrue)
        {
            AddFinding(list, allRisks, "IP_EMPLOYER_RISK", "IP-10A", "yes", "CRITICAL");
        }
        else if (extEmployerRisky && resUsedPossibleOrUnknown)
        {
            AddFinding(list, allRisks, "IP_EMPLOYER_RISK", "IP-10A", resUsed?.ToString() ?? extEmployer ?? "unknown", "HIGH");
        }

        // IP_THIRD_PARTY_COMPONENTS
        var tpComponentsUsed = f.GetValueOrDefault("ip.thirdPartyComponentsUsed");
        var tpReview = (string?)f.GetValueOrDefault("ip.thirdPartyTermsReview");
        if (tpComponentsUsed is true && tpReview is "developers_only" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "IP_THIRD_PARTY_COMPONENTS", "IP-11A", tpReview ?? "none", "MEDIUM");
        }

        // IP_EXTERNAL_DEPENDENCY
        var extDep = (string?)f.GetValueOrDefault("ip.externalDependency");
        if (extDep is "critical" or "unchecked")
        {
            string sev = extDep == "critical" ? "HIGH" : "MEDIUM";
            AddFinding(list, allRisks, "IP_EXTERNAL_DEPENDENCY", "IP-12", extDep, sev);
        }

        // IP_ACCESS_CONTROL
        var accControl = (string?)f.GetValueOrDefault("ip.criticalAccountsControl");
        bool founderDispute = GetBoolFact(f, "founders.activeDispute") || GetBoolFact(f, "founders.dispute");
        if (accControl is "worker" or "one_founder" && founderDispute)
        {
            AddFinding(list, allRisks, "IP_ACCESS_CONTROL", "IP-13", accControl ?? "worker", "CRITICAL");
        }

        // IP_BRAND_DOMAIN_CONTROL
        var brandDomain = (string?)f.GetValueOrDefault("ip.brandDomainControl");
        if (brandDomain is "worker" or "founder")
        {
            string sev = brandDomain == "worker" ? "HIGH" : "MEDIUM";
            AddFinding(list, allRisks, "IP_BRAND_DOMAIN_CONTROL", "IP-14", brandDomain, sev);
        }

        // IP_BRAND_REGISTRATION_INFO
        var brandReg = (string?)f.GetValueOrDefault("ip.brandRegistration");
        if (brandReg == "not_registered")
        {
            AddFinding(list, allRisks, "IP_BRAND_REGISTRATION_INFO", "IP-14", "brand_not_registered", "INFO");
        }

        return list;
    }

    private void AddFinding(List<RiskFinding> list, List<RiskDefinition> allRisks, string code, string qId, string ansId, string severity)
    {
        var def = allRisks.FirstOrDefault(r => r.Code == code);
        if (def == null) return;

        var existing = list.FirstOrDefault(f => f.Code == code);
        if (existing != null)
        {
            existing.Severity = severity;
            if (!existing.Basis.Any(b => b.QuestionId == qId))
            {
                existing.Basis.Add(new RiskFindingBasis { QuestionId = qId, AnswerId = ansId });
            }
            return;
        }

        list.Add(new RiskFinding
        {
            Code = def.Code,
            RootCauseGroup = def.RootCauseGroup,
            Severity = severity,
            Priority = def.Priority,
            SectionId = def.SectionId,
            Title = def.Title,
            Finding = def.Finding,
            WhyItMatters = def.WhyItMatters,
            Recommendation = def.Recommendation.Length > 0 ? def.Recommendation : (def.Recommendations.FirstOrDefault() ?? ""),
            Recommendations = def.Recommendations.Count > 0 ? def.Recommendations : new List<string> { def.Recommendation },
            Basis = new List<RiskFindingBasis> { new() { QuestionId = qId, AnswerId = ansId } },
            LawyerRequired = def.LawyerRequired,
            Resolution = def.Resolution,
            ServiceCode = def.ServiceCode,
            Cta = def.Cta
        });
    }

    private List<RiskFinding> MergeAndSuppressFindings(List<RiskFinding> rawFindings, SharedFactStore facts)
    {
        var suppressedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Canonical Founders Cross-Finding Suppressions (§25)
        if (rawFindings.Any(f => f.Code == "FND_ACTIVE_DISPUTE"))
        {
            suppressedCodes.Add("FND_ROLE_AMBIGUITY");
            suppressedCodes.Add("FND_DOCUMENTATION_GAP");
        }

        if (rawFindings.Any(f => f.Code == "FND_EQUITY_DISPUTE"))
        {
            suppressedCodes.Add("FND_EQUITY_NOT_FORMALIZED");
            suppressedCodes.Add("FND_EQUITY_AMBIGUITY");
        }

        if (rawFindings.Any(f => f.Code == "FND_DEAD_EQUITY"))
        {
            suppressedCodes.Add("FND_NO_VESTING");
            suppressedCodes.Add("FND_COMMITMENT_MISMATCH");
            suppressedCodes.Add("FND_EXIT_RULES_MISSING");
        }

        if (rawFindings.Any(f => f.Code == "FND_DEADLOCK"))
        {
            suppressedCodes.Add("FND_GOVERNANCE_AMBIGUITY");
            suppressedCodes.Add("FND_NO_DEADLOCK_PROTECTION");
        }

        if (rawFindings.Any(f => f.Code == "FND_DEPARTED_UNRESOLVED"))
        {
            suppressedCodes.Add("FND_EXIT_RULES_MISSING");
        }

        if (rawFindings.Any(f => f.Code == "FND_EQUITY_AMBIGUITY"))
        {
            suppressedCodes.Add("FND_EQUITY_NOT_FORMALIZED");
        }

        // Canonical IP Cross-Finding Suppressions (§25)
        if (rawFindings.Any(f => f.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED"))
        {
            suppressedCodes.Add("IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");
            suppressedCodes.Add("IP_CONTRACTOR_RIGHTS_GAP");
            suppressedCodes.Add("IP_STUDIO_RIGHTS_GAP");
        }

        if (rawFindings.Any(f => f.Code == "IP_FORMER_DEVELOPER_GAP"))
        {
            suppressedCodes.Add("IP_CONTRACTOR_RIGHTS_GAP");
            suppressedCodes.Add("TEAM_FORMER_ACCESS_RISK");
        }

        return rawFindings.Where(f => !suppressedCodes.Contains(f.Code)).OrderBy(r => GetSeverityOrder(r.Severity)).ToList();
    }

    private string GetDimensionDisplayName(string dimId)
    {
        return dimId switch
        {
            "equity_split" or "equity_clarity" => "Распределение долей основателей",
            "vesting" or "early_exit_equity" => "Вестинг и фиксация участия",
            "roles" => "Роли и ответственность",
            "ip_transfer" or "ip_assignment" => "Передача прав от основателей",
            "governance" => "Корпоративное управление сооснователей",
            "deadlock" => "Механизмы разрешения тупиков",
            "ownership_accuracy" => "Соответствие долей и реестра",
            "cap_table" => "Таблица долей и история",
            "equity_commitments" => "Фиксация опционов и обещаний",
            "corporate_history" => "История изменений капитала",
            "corporate_approvals" => "Корпоративные решения и одобрения",
            "authority" => "Полномочия и лимиты сделок",
            "entity_alignment" => "Оформление активов на компанию",
            "records" => "Корпоративный архив и учет",
            "overall_rights" => "Права на продукт в целом",
            "founder_rights" => "Передача прав от создателей-основателей",
            "employee_rights" => "Служебные произведения сотрудников",
            "external_creators" => "Оформление прав с подрядчиками",
            "external_employer" => "Чистота от прав прошлых работодателей",
            "third_party_dependencies" => "Лицензии сторонних библиотек",
            "technical_control" => "Контроль технических аккаунтов",
            "brand_domain" => "Права на домен и бренд",
            "content_provenance" => "Происхождение данных и контента",
            _ => dimId
        };
    }

    private InvestmentReadinessOverlay CalculateInvestmentReadiness(Dictionary<string, object> answers, SharedFactStore facts, List<RiskFinding> findings)
    {
        bool applicable = (string)facts.Facts["investment.timing"]! != "none" || (bool)facts.Facts["investment.priorInvestment"]!;
        if (!applicable) return new InvestmentReadinessOverlay { Applicable = false, ReadinessScore = 100 };

        var blockers = findings
            .Where(f => f.Severity is "CRITICAL" or "BLOCKER")
            .Select(f => f.Title)
            .ToList();

        int readiness = 85;
        if (blockers.Count >= 2) readiness = 35;
        else if (blockers.Count == 1) readiness = 55;

        return new InvestmentReadinessOverlay
        {
            Applicable = true,
            ReadinessScore = readiness,
            Blockers = blockers
        };
    }

    private ConsultingRecommendation CalculateConsultingRecommendation(List<RiskFinding> findings, SharedFactStore facts, int overallScore)
    {
        int opportunityScore = 30;
        if (findings.Any(f => f.Severity == "BLOCKER")) opportunityScore += 25;
        if (findings.Any(f => f.Severity == "CRITICAL")) opportunityScore += 20;

        string primary = "FULL_LEGAL_ARCHITECTURE";
        string primaryCta = "Провести полный юридический аудит компании";
        string? secondary = null;
        string? secondaryCta = null;

        var topFinding = findings.FirstOrDefault(f => !string.IsNullOrEmpty(f.ServiceCode));
        if (topFinding != null && !string.IsNullOrEmpty(topFinding.ServiceCode))
        {
            primary = topFinding.ServiceCode;
            primaryCta = topFinding.Cta ?? GetServiceCta(topFinding.ServiceCode);
            secondary = "FULL_LEGAL_ARCHITECTURE";
            secondaryCta = "Провести полный юридический аудит компании";
        }

        return new ConsultingRecommendation
        {
            PrimaryServiceCode = primary,
            PrimaryCta = primaryCta,
            SecondaryServiceCode = secondary,
            SecondaryCta = secondaryCta,
            ConsultingOpportunityScore = opportunityScore
        };
    }

    private static string GetServiceCta(string serviceCode)
    {
        return serviceCode switch
        {
            "CORP_STRUCT_KZ" => "Заказать разработку устава и документов для ТОО в Казахстане",
            "FOUNDERS_AGREEMENT" => "Разработать соглашение сооснователей (Founders' Agreement / SHA)",
            "IP_ASSIGNMENT_PACK" => "Оформить пакет документов по передаче интеллектуальной собственности (IP Assignment)",
            "IP_AUDIT" => "Провести аудит интеллектуальной собственности и цепочек прав",
            _ => "Получить консультацию юриста по устранению выявленных рисков"
        };
    }

    public static string GetLevel(int score)
    {
        if (score >= 80) return "strong";
        if (score >= 60) return "attention";
        if (score >= 40) return "material_gaps";
        return "structural_risks";
    }

    public static string GetLevelTitle(string level)
    {
        return level switch
        {
            "strong" => "Сильная основа",
            "attention" => "Есть вопросы, требующие внимания",
            "material_gaps" => "Существенные пробелы",
            _ => "Структурные вопросы"
        };
    }

    public static string GetLevelText(string level)
    {
        return level switch
        {
            "strong" => "Базовый юридический контур сформирован на высоком уровне. Выявлены точечные зоны для усиления.",
            "attention" => "Ключевые элементы структуры присутствуют, однако есть существенные моменты, требующие юридической доработки.",
            "material_gaps" => "Обнаружены пробелы в защите прав или оформлении структуры, создающие уязвимости для бизнеса.",
            _ => "Юридическая основа бизнеса пока сформирована фрагментарно. Рекомендуется первоочередное закрытие критических рисков."
        };
    }

    public static string GetConfidenceText(int conf)
    {
        if (conf >= 80) return "Высокая определенность ответов.";
        if (conf >= 50) return "Умеренная определенность (часть ответов требует проверки фактов).";
        return "Низкая определенность (много ответов «Не уверен»). Рекомендуется уточнить факты.";
    }

    private static int GetSeverityOrder(string sev)
    {
        return sev switch
        {
            "BLOCKER" => 1,
            "CRITICAL" => 2,
            "HIGH" => 3,
            "MEDIUM" => 4,
            "INFO" => 5,
            _ => 6
        };
    }
}
