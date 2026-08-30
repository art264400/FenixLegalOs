using System.Collections.Generic;
using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Contracts;

public class ContractFactNormalizer : IFactNormalizer
{
    public string ModuleId => "contracts";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // ─── CONTRACT-01: B2B Counterparty Context ────────────────────────────
        if (answers.TryGetValue("CONTRACT-01", out var c01Raw) && c01Raw != null)
        {
            var types = ExtractList(c01Raw);
            if (types.Contains("none"))
            {
                f["contracts.b2bRelevant"] = false;
            }
            else if (types.Count > 0)
            {
                f["contracts.b2bRelevant"] = true;
                var validCounterparties = new List<string>();
                foreach (var t in types)
                {
                    if (t is "clients" or "partners" or "suppliers" or "some")
                    {
                        validCounterparties.Add(t);
                    }
                }
                if (validCounterparties.Count > 0)
                {
                    f["contracts.counterpartyTypes"] = validCounterparties;
                }
            }
        }

        // ─── CONTRACT-02: Written Form ────────────────────────────────────────
        if (answers.TryGetValue("CONTRACT-02", out var c02Raw) && c02Raw != null)
        {
            var str = c02Raw.ToString();
            var val = str switch
            {
                "always" => "always",
                "some_in_messages" => "some_in_messages",
                "material_informal" => "material_informal",
                "mostly_informal" => "mostly_informal",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.writtenCoverage"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-02");
            }
        }

        // ─── CONTRACT-03: Scope Clarity ───────────────────────────────────────
        if (answers.TryGetValue("CONTRACT-03", out var c03Raw) && c03Raw != null)
        {
            var str = c03Raw.ToString();
            var val = str switch
            {
                "clear" => "clear",
                "mostly" => "mostly",
                "outside" => "outside",
                "generic" => "generic",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.scopeClarity"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-03");
            }
        }

        // ─── CONTRACT-04: Payment & Termination ───────────────────────────────
        if (answers.TryGetValue("CONTRACT-04", out var c04Raw) && c04Raw != null)
        {
            var str = c04Raw.ToString();
            var val = str switch
            {
                "clear" => "clear",
                "mostly" => "mostly",
                "some_unclear" => "some_unclear",
                "case" => "case",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.paymentTermination"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-04");
            }
        }

        // ─── CONTRACT-05: Risk Allocation & Liability Cap ────────────────────
        if (answers.TryGetValue("CONTRACT-05", out var c05Raw) && c05Raw != null)
        {
            var str = c05Raw.ToString();
            var val = str switch
            {
                "clear" => "clear",
                "mostly" => "mostly",
                "general" => "general",
                "weak" => "weak",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.riskAllocation"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-05");
            }
        }

        // ─── CONTRACT-06: Model Match ─────────────────────────────────────────
        if (answers.TryGetValue("CONTRACT-06", out var c06Raw) && c06Raw != null)
        {
            var str = c06Raw.ToString();
            var val = str switch
            {
                "custom" => "custom",
                "adapted" => "adapted",
                "templates" => "templates",
                "copied" => "copied",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.modelMatch"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-06");
            }
        }

        // ─── CONTRACT-07: Large Deal Review ───────────────────────────────────
        if (answers.TryGetValue("CONTRACT-07", out var c07Raw) && c07Raw != null)
        {
            var str = c07Raw.ToString();
            var val = str switch
            {
                "reviewed" => "reviewed",
                "sometimes" => "sometimes",
                "often" => "often_unreviewed",
                "no_large" => "not_applicable",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.largeDealReview"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-07");
            }
        }

        // ─── CONTRACT-08: Counterparty Dependency ─────────────────────────────
        if (answers.TryGetValue("CONTRACT-08", out var c08Raw) && c08Raw != null)
        {
            var str = c08Raw.ToString();
            var val = str switch
            {
                "no" => "none",
                "noticeable" => "noticeable",
                "material" => "material",
                "near_total" => "near_total",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.counterpartyDependency"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-08");
            }
        }

        // ─── CONTRACT-08A: Counterparty Exit Risk ─────────────────────────────
        if (answers.TryGetValue("CONTRACT-08A", out var c08aRaw) && c08aRaw != null)
        {
            var str = c08aRaw.ToString();
            var val = str switch
            {
                "protected" => "protected",
                "backup" => "backup",
                "serious" => "serious",
                "unknown" => "unknown",
                _ => null
            };
            if (val != null)
            {
                f["contracts.counterpartyExitRisk"] = val;
                if (val == "unknown") AddUnknown(f, "CONTRACT-08A");
            }
        }
    }

    private static List<string> ExtractList(object raw)
    {
        var list = new List<string>();
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in je.EnumerateArray())
            {
                var str = el.GetString();
                if (!string.IsNullOrEmpty(str)) list.Add(str);
            }
        }
        else if (raw is IEnumerable<string> strEnum)
        {
            foreach (var s in strEnum)
            {
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
        }
        else if (raw != null)
        {
            var str = raw.ToString();
            if (!string.IsNullOrEmpty(str)) list.Add(str);
        }
        return list;
    }

    private static void AddUnknown(Dictionary<string, object?> f, string questionId)
    {
        if (!f.TryGetValue("diagnostic.unknownQuestionIds", out var obj) || obj is not List<string> list)
        {
            list = new List<string>();
            f["diagnostic.unknownQuestionIds"] = list;
        }
        if (!list.Contains(questionId))
        {
            list.Add(questionId);
        }
    }
}
