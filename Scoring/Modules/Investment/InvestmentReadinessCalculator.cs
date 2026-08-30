using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Modules.Investment;

public static class InvestmentReadinessCalculator
{
    public static InvestmentReadinessOverlay Calculate(
        int? investmentSectionScore,
        List<RiskFinding> findings,
        SharedFactStore facts)
    {
        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        bool priorInvestment = facts.Facts.TryGetValue("investment.priorInvestment", out var pi) && (pi is true || pi?.ToString() == "unknown");
        bool isApplicable = (timing != null && timing != "none") || priorInvestment;

        if (!isApplicable || !investmentSectionScore.HasValue)
        {
            return new InvestmentReadinessOverlay
            {
                Applicable = false,
                ReadinessScore = null,
                Blockers = new List<string>()
            };
        }

        int baseReadiness = investmentSectionScore.Value;

        // Evaluate material DD blockers in close/active fundraising contexts (§17.3)
        var materialResult = MaterialDdIssueEvaluator.Evaluate(findings, facts);
        int unresolvedBlockers = materialResult.UnresolvedBlockersCount;

        int finalReadiness;
        if (unresolvedBlockers >= 2)
        {
            finalReadiness = Math.Min(baseReadiness, 39);
        }
        else if (unresolvedBlockers == 1)
        {
            finalReadiness = Math.Min(baseReadiness, 59);
        }
        else
        {
            finalReadiness = baseReadiness;
        }

        var blockerTitles = materialResult.BlockerFindings
            .Select(f => f.Title)
            .Distinct()
            .ToList();

        return new InvestmentReadinessOverlay
        {
            Applicable = true,
            ReadinessScore = finalReadiness,
            Blockers = blockerTitles
        };
    }
}
