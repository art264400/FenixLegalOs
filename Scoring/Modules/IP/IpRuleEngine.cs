using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
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
            AddFinding(list, allRisks, "IP_PRODUCT_RIGHTS_UNCONFIRMED", "IP-04", overallRights ?? "none", RiskSeverity.Critical);
        }

        // IP_FOUNDER_RIGHTS_NOT_TRANSFERRED
        var ipCreators = f.GetValueOrDefault("ip.creators") as List<string> ?? new List<string>();
        var founderRights = (string?)f.GetValueOrDefault("ip.founderRights");
        if (ipCreators.Contains("founders") && founderRights is "founder_owned" or "agreed_not_completed" or "partial")
        {
            RiskSeverity sev = founderRights == "founder_owned" ? RiskSeverity.Critical : RiskSeverity.High;
            AddFinding(list, allRisks, "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", "IP-05", founderRights, sev);
        }

        // IP_CONTRACTOR_RIGHTS_GAP
        var contractorRights = (string?)f.GetValueOrDefault("ip.contractorRights");
        var employeeRights = (string?)f.GetValueOrDefault("ip.employeeRights");
        if ((ipCreators.Contains("contractors") && contractorRights is "payment_only" or "no_contract" or "unclear_clause") ||
            (ipCreators.Contains("employees") && employeeRights is "missing_some" or "not_reviewed"))
        {
            AddFinding(list, allRisks, "IP_CONTRACTOR_RIGHTS_GAP", "IP-07", contractorRights ?? employeeRights ?? "unclear", RiskSeverity.High);
        }

        // IP_FORMER_DEVELOPER_GAP
        var formerStatus = (string?)f.GetValueOrDefault("ip.formerCreatorStatus");
        if (formerStatus is "unresolved" or "dispute" || (ipCreators.Contains("former") && contractorRights is "payment_only" or "no_contract" or "unclear_clause"))
        {
            AddFinding(list, allRisks, "IP_FORMER_DEVELOPER_GAP", "IP-08", formerStatus ?? "unresolved", RiskSeverity.Critical);
        }

        // IP_STUDIO_RIGHTS_GAP
        var studioRights = (string?)f.GetValueOrDefault("ip.studioRights");
        if (ipCreators.Contains("studio") && studioRights is "unknown_chain" or "subcontractors_unchecked")
        {
            AddFinding(list, allRisks, "IP_STUDIO_RIGHTS_GAP", "IP-09", studioRights, RiskSeverity.High);
        }

        // IP_EMPLOYER_RISK strictly according to rule contract:
        var extEmployer = (string?)f.GetValueOrDefault("ip.externalEmployerCreation");
        var resUsed = f.GetValueOrDefault("ip.employerResourcesUsed");
        bool resUsedTrue = resUsed is true || (resUsed is string sTrue && sTrue.Equals("yes", StringComparison.OrdinalIgnoreCase));
        bool resUsedPossibleOrUnknown = resUsed is "possible" or "unknown";
        bool extEmployerRisky = extEmployer is "not_reviewed" or "unknown";

        if (resUsedTrue)
        {
            AddFinding(list, allRisks, "IP_EMPLOYER_RISK", "IP-10A", "yes", RiskSeverity.Critical);
        }
        else if (extEmployerRisky && resUsedPossibleOrUnknown)
        {
            AddFinding(list, allRisks, "IP_EMPLOYER_RISK", "IP-10A", resUsed?.ToString() ?? extEmployer ?? "unknown", RiskSeverity.High);
        }

        // IP_THIRD_PARTY_COMPONENTS
        var tpComponentsUsed = f.GetValueOrDefault("ip.thirdPartyComponentsUsed");
        var tpReview = (string?)f.GetValueOrDefault("ip.thirdPartyTermsReview");
        if (tpComponentsUsed is true && tpReview is "developers_only" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "IP_THIRD_PARTY_COMPONENTS", "IP-11A", tpReview ?? "none", RiskSeverity.Medium);
        }

        // IP_EXTERNAL_DEPENDENCY
        var extDep = (string?)f.GetValueOrDefault("ip.externalDependency");
        if (extDep is "critical" or "unchecked")
        {
            RiskSeverity sev = extDep == "critical" ? RiskSeverity.High : RiskSeverity.Medium;
            AddFinding(list, allRisks, "IP_EXTERNAL_DEPENDENCY", "IP-12", extDep, sev);
        }

        // IP_ACCESS_CONTROL (§27.2: ip.criticalAccountsControl in [worker,one_founder] AND (founders.activeDispute == true OR team.formerPersonConflict == true OR personDeparting == true))
        var accControl = (string?)f.GetValueOrDefault("ip.criticalAccountsControl");
        bool founderDispute = GetBoolFact(f, "founders.activeDispute") || GetBoolFact(f, "founders.dispute");
        bool teamFormerConflict = GetBoolFact(f, "team.formerPersonConflict");
        if (accControl is "worker" or "one_founder" && (founderDispute || teamFormerConflict))
        {
            AddFinding(list, allRisks, "IP_ACCESS_CONTROL", "IP-13", accControl ?? "worker", RiskSeverity.Critical);
        }

        // IP_DOMAIN_BRAND_CONTROL
        var brandDomain = (string?)f.GetValueOrDefault("ip.brandDomainControl");
        if (brandDomain is "worker" or "founder")
        {
            RiskSeverity sev = brandDomain == "worker" ? RiskSeverity.High : RiskSeverity.Medium;
            AddFinding(list, allRisks, "IP_DOMAIN_BRAND_CONTROL", "IP-14", brandDomain, sev);
        }

        // IP_CONTENT_RIGHTS
        var contentProv = (string?)f.GetValueOrDefault("ip.contentProvenance");
        if (contentProv is "some_unknown" or "external_unchecked" or "unchecked" or "risk")
        {
            RiskSeverity sev = contentProv is "external_unchecked" or "risk" ? RiskSeverity.High : RiskSeverity.Medium;
            AddFinding(list, allRisks, "IP_CONTENT_RIGHTS", "IP-15", contentProv, sev);
        }

        // IP_BRAND_REGISTRATION_INFO
        var brandReg = (string?)f.GetValueOrDefault("ip.brandRegistration");
        if (brandReg == "not_registered")
        {
            AddFinding(list, allRisks, "IP_BRAND_REGISTRATION_INFO", "IP-14", "brand_not_registered", RiskSeverity.Info);
        }

        return list;
    }

    private static bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }

    private static void AddFinding(List<RiskFinding> list, IReadOnlyList<RiskDefinition> allRisks, string code, string qId, string ansId, RiskSeverity severity)
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
            Cta = def.Cta,
            AffectedDimensions = def.AffectedDimensions
        });
    }
}
