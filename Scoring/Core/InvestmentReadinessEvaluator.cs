using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Core;

public class InvestmentReadinessEvaluator
{
    public static InvestmentReadinessOverlay Calculate(
        Dictionary<string, object> answers,
        SharedFactStore facts,
        List<RiskFinding> findings)
    {
        bool applicable = (string)facts.Facts["investment.timing"]! != "none" || (bool)facts.Facts["investment.priorInvestment"]!;
        if (!applicable) return new InvestmentReadinessOverlay { Applicable = false, ReadinessScore = 100 };

        var blockers = findings
            .Where(f => f.Severity is "CRITICAL" or "BLOCKER")
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
