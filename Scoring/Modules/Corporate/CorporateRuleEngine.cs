using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Corporate;

public class CorporateRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "corporate";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

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
