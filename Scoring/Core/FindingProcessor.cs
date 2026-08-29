using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Core;

public class FindingProcessor
{
    public static List<RiskFinding> CollectRawFindings(
        SharedFactStore facts,
        IReadOnlyList<RiskDefinition> allRisks,
        IEnumerable<IModuleRuleEngine> moduleRuleEngines)
    {
        var rawList = new List<RiskFinding>();
        foreach (var engine in moduleRuleEngines)
        {
            rawList.AddRange(engine.Evaluate(facts, allRisks));
        }
        return rawList;
    }

    public static List<RiskFinding> MergeAndSuppressFindings(List<RiskFinding> rawFindings, SharedFactStore facts)
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

        return rawFindings
            .Where(f => !suppressedCodes.Contains(f.Code))
            .OrderBy(r => GetSeverityOrder(r.Severity))
            .ToList();
    }

    public static int GetSeverityOrder(string sev)
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
