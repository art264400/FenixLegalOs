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
    }

    private static List<string> GetAnswerList(IReadOnlyDictionary<string, object> answers, string key)
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
