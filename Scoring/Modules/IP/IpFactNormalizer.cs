using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.IP;

public class IpFactNormalizer : IFactNormalizer
{
    public string ModuleId => "ip";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // ==========================================
        // 3. IP FACTS (§24 & §23.3)
        // ==========================================
        if (answers.TryGetValue("IP-01", out var ip01Raw) && ip01Raw != null)
        {
            var ip01 = ip01Raw.ToString() ?? "";
            var stage = ip01 switch
            {
                "idea" => "idea",
                "prototype" => "prototype",
                "ready" => "live_or_ready",
                "multiple" => "multiple_products",
                _ => null
            };

            if (stage != null)
            {
                f["ip.coreProductExists"] = stage != "idea";
                f["product.stage"] = stage;
                if (stage == "idea")
                {
                    f["ip.normativeModuleScore"] = 100;
                }
            }
        }

        if (answers.TryGetValue("IP-02", out var ip02Raw) && ip02Raw != null)
        {
            var list = GetAnswerList(answers, "IP-02");
            if (list.Count > 0)
            {
                f["ip.assets"] = list;
            }
        }

        if (answers.TryGetValue("IP-03", out var ip03Raw) && ip03Raw != null)
        {
            var list = GetAnswerList(answers, "IP-03");
            if (list.Count > 0)
            {
                f["ip.creators"] = list;
            }
        }

        if (answers.TryGetValue("IP-04", out var ip04Raw) && ip04Raw != null)
        {
            var ip04 = ip04Raw.ToString() ?? "";
            if (ip04 is "all" or "main" or "some" or "informal" or "none")
            {
                f["ip.overallRightsEvidence"] = ip04;
            }
        }

        if (answers.TryGetValue("IP-05", out var ip05Raw) && ip05Raw != null)
        {
            var ip05 = ip05Raw.ToString() ?? "";
            var fRights = ip05 switch
            {
                "assigned" => "assigned",
                "covered" => "covered",
                "partial" => "partial",
                "agreed" => "agreed_not_completed",
                "founder_owned" => "founder_owned",
                _ => null
            };
            if (fRights != null)
            {
                f["ip.founderRights"] = fRights;
            }
        }

        if (answers.TryGetValue("IP-06", out var ip06Raw) && ip06Raw != null)
        {
            var ip06 = ip06Raw.ToString() ?? "";
            if (ip06 is "all" or "key_gaps" or "not_reviewed" or "missing_some")
            {
                f["ip.employeeRights"] = ip06;
            }
        }

        if (answers.TryGetValue("IP-07", out var ip07Raw) && ip07Raw != null)
        {
            var ip07 = ip07Raw.ToString() ?? "";
            if (ip07 is "all" or "most" or "unclear_clause" or "payment_only" or "no_contract")
            {
                f["ip.contractorRights"] = ip07;
            }
        }

        if (answers.TryGetValue("IP-08", out var ip08Raw) && ip08Raw != null)
        {
            var ip08 = ip08Raw.ToString() ?? "";
            if (ip08 is "none" or "complete" or "partial" or "unresolved" or "dispute")
            {
                f["ip.formerCreatorStatus"] = ip08;
            }
        }

        if (answers.TryGetValue("IP-09", out var ip09Raw) && ip09Raw != null)
        {
            var ip09 = ip09Raw.ToString() ?? "";
            if (ip09 is "confirmed" or "agency_only" or "subcontractors_unchecked" or "unknown_chain")
            {
                f["ip.studioRights"] = ip09;
            }
        }

        if (answers.TryGetValue("IP-10", out var ip10Raw) && ip10Raw != null)
        {
            var ip10 = ip10Raw.ToString() ?? "";
            if (ip10 is "no" or "unrelated" or "lawyer_checked" or "not_reviewed" or "unknown")
            {
                f["ip.externalEmployerCreation"] = ip10;
            }
        }

        if (answers.TryGetValue("IP-10A", out var ip10ARaw) && ip10ARaw != null)
        {
            var ip10A = ip10ARaw.ToString() ?? "";
            object? resUsed = ip10A switch
            {
                "yes" => true,
                "no" => false,
                "possible" => "possible",
                "unknown" => "unknown",
                _ => null
            };
            if (resUsed != null)
            {
                f["ip.employerResourcesUsed"] = resUsed;
            }
        }

        if (answers.TryGetValue("IP-11", out var ip11Raw) && ip11Raw != null)
        {
            var ip11 = ip11Raw.ToString() ?? "";
            object? tpUsed = ip11 switch
            {
                "yes" or "likely" => true,
                "no" => false,
                _ => null
            };
            if (tpUsed != null)
            {
                f["ip.thirdPartyComponentsUsed"] = tpUsed;
            }
        }

        if (answers.TryGetValue("IP-11A", out var ip11ARaw) && ip11ARaw != null)
        {
            var ip11A = ip11ARaw.ToString() ?? "";
            var tpTerms = ip11A switch
            {
                "yes" => "systematic",
                "main" => "main",
                "developers_only" => "developers_only",
                "no" => "none",
                "unknown" => "unknown",
                _ => null
            };
            if (tpTerms != null)
            {
                f["ip.thirdPartyTermsReview"] = tpTerms;
            }
        }

        if (answers.TryGetValue("IP-12", out var ip12Raw) && ip12Raw != null)
        {
            var ip12 = ip12Raw.ToString() ?? "";
            var extDep = ip12 switch
            {
                "no" => "none",
                "known" => "known",
                "unchecked" => "unchecked",
                "critical" => "critical",
                _ => null
            };
            if (extDep != null)
            {
                f["ip.externalDependency"] = extDep;
            }
        }

        if (answers.TryGetValue("IP-13", out var ip13Raw) && ip13Raw != null)
        {
            var ip13 = ip13Raw.ToString() ?? "";
            if (ip13 is "company" or "mixed" or "one_founder" or "worker")
            {
                f["ip.criticalAccountsControl"] = ip13;
            }
        }

        if (answers.TryGetValue("IP-14", out var ip14Raw) && ip14Raw != null)
        {
            var ip14 = ip14Raw.ToString() ?? "";
            if (ip14 is "company" or "mixed" or "founder" or "worker" or "brand_not_registered")
            {
                f["ip.brandDomainControl"] = ip14;
                f["ip.brandRegistration"] = ip14 == "brand_not_registered" ? "not_registered" : "registered";
            }
        }

        if (answers.TryGetValue("IP-15", out var ip15Raw) && ip15Raw != null)
        {
            var ip15 = ip15Raw.ToString() ?? "";
            if (ip15 is "clear" or "mostly" or "some_unknown" or "external_unchecked" or "unknown" or "licensed" or "unchecked" or "risk")
            {
                f["ip.contentProvenance"] = ip15;
            }
        }
    }

    private static List<string> GetAnswerList(IReadOnlyDictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return new List<string>();
        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var res = new List<string>();
            foreach (var item in je.EnumerateArray())
            {
                var s = item.ToString();
                if (!string.IsNullOrWhiteSpace(s)) res.Add(s);
            }
            return res;
        }
        if (val is IEnumerable<string> list) return list.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
        var single = val.ToString() ?? "";
        return string.IsNullOrWhiteSpace(single) ? new List<string>() : new List<string> { single };
    }
}
