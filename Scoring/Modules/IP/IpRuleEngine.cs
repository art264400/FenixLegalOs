using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.IP;

public class IpRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "ip";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        var entityStatus = (string?)f.GetValueOrDefault("company.entityStatus");
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

    private static bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }

    private static void AddFinding(List<RiskFinding> list, IReadOnlyList<RiskDefinition> allRisks, string code, string qId, string ansId, string severity)
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
}
