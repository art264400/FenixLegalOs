using System.Text.Json;
using System.Text.RegularExpressions;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Founders;

public class FoundersFactNormalizer : IFactNormalizer
{
    public string ModuleId => "founders";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

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

        // Module-specific normative scoring policy (§22.1 & §23.1):
        // Solo founder with no inactive co-founders has normative module score = 100.
        bool isSolo = f.TryGetValue("founders.isSolo", out var sVal) && sVal is bool isS && isS;
        bool inactiveEx = f.TryGetValue("founders.inactiveExists", out var inVal) && inVal is bool inE && inE;
        if (isSolo && !inactiveEx)
        {
            f["founders.normativeModuleScore"] = 100;
        }
    }
}
