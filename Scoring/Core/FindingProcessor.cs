using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
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

    public static List<RiskFinding> MergeAndSuppressFindings(List<RiskFinding> rawFindings, SharedFactStore facts, IReadOnlyList<RiskDefinition>? allRisks = null)
    {
        var suppressedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var risksLookup = allRisks ?? Data.DataBank.Risks;

        foreach (var finding in rawFindings)
        {
            var def = risksLookup.FirstOrDefault(r => string.Equals(r.Code, finding.Code, StringComparison.OrdinalIgnoreCase))
                      ?? Data.DataBank.Risks.FirstOrDefault(r => string.Equals(r.Code, finding.Code, StringComparison.OrdinalIgnoreCase));
            if (def == null)
            {
                throw new InvalidOperationException($"Unknown RiskCode '{finding.Code}' encountered in candidate findings.");
            }

            if (def.SuppressCodes.Count > 0)
            {
                foreach (var code in def.SuppressCodes)
                {
                    suppressedCodes.Add(code);
                }
            }
        }

        return rawFindings
            .Where(f => !suppressedCodes.Contains(f.Code))
            .OrderBy(r => GetSeverityOrder(r.Severity))
            .ThenBy(r => r.Code, StringComparer.Ordinal)
            .ToList();
    }

    public static int GetSeverityOrder(RiskSeverity sev)
    {
        return sev switch
        {
            RiskSeverity.Blocker => 1,
            RiskSeverity.Critical => 2,
            RiskSeverity.High => 3,
            RiskSeverity.Medium => 4,
            RiskSeverity.Info => 5,
            _ => 6
        };
    }
}
