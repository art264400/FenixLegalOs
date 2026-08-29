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
            }
        }

        if (answers.TryGetValue("FND-C03", out var fndC03Raw) && fndC03Raw != null)
        {
            var fndC03 = fndC03Raw.ToString() ?? "";
            switch (fndC03)
            {
                case "departed_clean" or "resolved":
                    f["founders.departedFounderExists"] = true;
                    f["founders.departedFounderStatus"] = "clean";
                    break;
                case "departed_unresolved" or "unresolved":
                    f["founders.departedFounderExists"] = true;
                    f["founders.departedFounderStatus"] = "unresolved";
                    f["founders.inactiveExists"] = true;
                    break;
                case "conflict" or "dispute":
                    f["founders.departedFounderExists"] = true;
                    f["founders.departedFounderStatus"] = "dispute";
                    f["founders.inactiveExists"] = true;
                    break;
                case "formal_only":
                    f["founders.departedFounderExists"] = false;
                    f["founders.departedFounderStatus"] = "formal_only";
                    f["founders.inactiveExists"] = true;
                    break;
                case "none":
                    f["founders.departedFounderExists"] = false;
                    f["founders.departedFounderStatus"] = "none";
                    break;
            }
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
        }

        if (answers.TryGetValue("FND-C04", out var fndC04Raw) && fndC04Raw != null)
        {
            var fndC04 = fndC04Raw.ToString() ?? "";
            if (fndC04 is "signed" or "multiple_docs" or "draft" or "informal" or "none" or "unknown" or "aifc_sha")
            {
                f["founders.founderAgreementStatus"] = fndC04;
            }
        }

        if (answers.TryGetValue("FND-01", out var fnd01Raw) && fnd01Raw != null)
        {
            var fnd01 = fnd01Raw.ToString() ?? "";
            f["founders.activeDispute"] = fnd01 is "material" or "active_conflict" or "formal_dispute";
            var dispLevel = fnd01 switch
            {
                "none" => "none",
                "minor" => "minor",
                "material" => "material",
                "active_conflict" => "active",
                "formal_dispute" => "formal",
                _ => null
            };
            if (dispLevel != null)
            {
                f["founders.disputeLevel"] = dispLevel;
            }
        }

        if (answers.TryGetValue("FND-02", out var fnd02Raw) && fnd02Raw != null)
        {
            var fnd02 = fnd02Raw.ToString() ?? "";
            if (fnd02 is "written" or "clear_oral" or "overlap" or "disputed" or "clear" or "partial" or "ambiguous" or "conflict")
            {
                f["founders.roleClarity"] = fnd02;
            }
        }

        if (answers.TryGetValue("FND-03", out var fnd03Raw) && fnd03Raw != null)
        {
            var fnd03 = fnd03Raw.ToString() ?? "";
            if (fnd03 is "aligned" or "temporary_part_time" or "different_accepted" or "below_expected" or "stopped" or "full_time" or "part_time_aligned" or "part_time_mismatch")
            {
                f["founders.commitmentStatus"] = fnd03;
                if (fnd03 is "stopped" or "below_expected")
                {
                    if (fnd03 == "stopped")
                    {
                        f["founders.inactiveExists"] = true;
                        if (!f.ContainsKey("founders.departedFounderStatus") || (string?)f["founders.departedFounderStatus"] == "none")
                        {
                            f["founders.departedFounderStatus"] = "stopped";
                        }
                    }
                }
            }
        }

        if (answers.TryGetValue("FND-04", out var fnd04Raw) && fnd04Raw != null)
        {
            var fnd04 = fnd04Raw.ToString() ?? "";
            if (fnd04 is "registered" or "written_agreed" or "preliminary" or "verbal" or "ambiguous" or "dispute")
            {
                f["founders.equityClarity"] = fnd04;
            }
        }

        if (answers.TryGetValue("FND-05", out var fnd05Raw) && fnd05Raw != null)
        {
            var fnd05 = fnd05Raw.ToString() ?? "";
            var vStatus = fnd05 switch
            {
                "vesting" or "reverse_vesting" or "cliff_only" => "vesting_signed",
                "repurchase" => "repurchase_signed",
                "verbal_rule" => "verbal_rule",
                "informal" => "informal",
                "none" or "not_discussed" or "retains_all" => "none",
                _ => null
            };
            if (vStatus != null)
            {
                f["founders.vestingStatus"] = vStatus;
            }
        }

        if (answers.TryGetValue("FND-05A", out var fnd05aRaw) && fnd05aRaw != null)
        {
            var fnd05a = fnd05aRaw.ToString() ?? "";
            if (fnd05a is "defined" or "partial" or "oral" or "none" or "unknown" or "detailed" or "general")
            {
                f["founders.leaverRules"] = fnd05a;
            }
        }

        if (answers.TryGetValue("FND-06", out var fnd06Raw) && fnd06Raw != null)
        {
            var fnd06 = fnd06Raw.ToString() ?? "";
            if (fnd06 is "written" or "verbal" or "partial" or "all_together" or "none" or "unknown" or "unanimous" or "majority" or "supermajority" or "ceo_veto")
            {
                f["founders.governanceClarity"] = fnd06;
            }
        }

        if (answers.TryGetValue("FND-06A", out var fnd06aRaw) && fnd06aRaw != null)
        {
            var fnd06a = fnd06aRaw.ToString() ?? "";
            if (fnd06a is "different_thresholds" or "majority" or "material_unanimity" or "broad_unanimity" or "undefined" or "unknown" or "unanimous_all" or "majority_simple" or "qualified_75" or "sole_ceo" or "no_formal_rule")
            {
                f["founders.keyDecisionMode"] = fnd06a;
            }
        }

        if (answers.TryGetValue("FND-07", out var fnd07Raw) && fnd07Raw != null)
        {
            var fnd07 = fnd07Raw.ToString() ?? "";
            if (fnd07 is "full" or "staged" or "casting_vote" or "mediator_only" or "only_agree" or "none" or "unknown" or "buyout_formula" or "escalation" or "external_vote" or "russian_roulette")
            {
                f["founders.deadlockMechanism"] = fnd07;
            }
        }

        if (answers.TryGetValue("FND-08", out var fnd08Raw) && fnd08Raw != null)
        {
            var fnd08 = fnd08Raw.ToString() ?? "";
            var exStatus = fnd08 switch
            {
                "full" or "partial" or "oral" or "none" or "clear_procedure" or "general_clause" => fnd08,
                "already_unresolved" => "unresolved_departure",
                _ => null
            };
            if (exStatus != null)
            {
                f["founders.exitRules"] = exStatus;
            }
        }

        if (answers.TryGetValue("FND-09", out var fnd09Raw) && fnd09Raw != null)
        {
            var fnd09 = fnd09Raw.ToString() ?? "";
            if (fnd09 is "none" or "documented" or "small_partial" or "material_unclear" or "dispute" or "unknown" or "documented_equal" or "documented_unbalanced" or "informal_valued")
            {
                f["founders.personalContributions"] = fnd09;
            }
        }

        if (answers.TryGetValue("FND-10", out var fnd10Raw) && fnd10Raw != null)
        {
            var fnd10 = fnd10Raw.ToString() ?? "";
            var extAct = fnd10 switch
            {
                "none" => "none",
                "unrelated" or "no_overlap" => "unrelated",
                "overlap_rules" or "settled" => "overlap_rules",
                "potential_competitor" or "competing" => "potential_competitor",
                "employer_same_field" or "employer" => "employer_same_field",
                "active_competition" => "active_competition",
                "unknown" => "unknown",
                _ => null
            };
            if (extAct != null)
            {
                f["founders.externalActivity"] = extAct;
                f["founders.externalEmployerSameField"] = fnd10 is "employer_same_field" or "employer" or "active_competition";
                f["founders.hasConflictOfInterest"] = fnd10 is "potential_competitor" or "competing" or "employer_same_field" or "employer" or "active_competition";
            }
        }

        if (answers.TryGetValue("FND-11", out var fnd11Raw) && fnd11Raw != null)
        {
            var fnd11 = fnd11Raw.ToString() ?? "";
            if (fnd11 is "aligned" or "differences_discussed" or "not_discussed" or "material_difference" or "conflict" or "partial" or "divergent")
            {
                f["founders.strategicAlignment"] = fnd11;
            }
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
