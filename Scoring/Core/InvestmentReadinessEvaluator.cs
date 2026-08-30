using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

public class InvestmentReadinessEvaluator
{
    public static InvestmentReadinessOverlay Calculate(
        Dictionary<string, object> answers,
        SharedFactStore facts,
        List<RiskFinding> findings)
    {
        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        var priorInv = facts.Facts.GetValueOrDefault("investment.priorInvestment");
        bool applicable = timing is not (null or "none") || priorInv is true;

        if (!applicable) return new InvestmentReadinessOverlay { Applicable = false, ReadinessScore = 100 };

        var blockers = findings
            .Where(f => f.Severity is RiskSeverity.Critical or RiskSeverity.Blocker)
            .Select(f => f.Title)
            .ToList();

        int readiness = 85;
        if (blockers.Count >= 2) readiness = 35;
        else if (blockers.Count == 1) readiness = 55;

        return new InvestmentReadinessOverlay
        {
            Applicable = true,
            ReadinessScore = readiness,
            Blockers = blockers
        };
    }
}
