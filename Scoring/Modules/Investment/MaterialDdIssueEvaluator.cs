using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Modules.Investment;

public class MaterialDdEvaluationResult
{
    public bool HasMaterialDDIssue { get; set; }
    public string DerivedByRule { get; set; } = "MATERIAL_DD_ISSUE_EVALUATOR";
    public List<string> SourceRiskCodes { get; set; } = new();
    public List<RiskFindingBasis> CombinedBasis { get; set; } = new();
    public List<RiskFinding> BlockerFindings { get; set; } = new();
    public int UnresolvedBlockersCount { get; set; }
}

public static class MaterialDdIssueEvaluator
{
    // Canonical 5 core material DD problem categories that reach Blocker status in active/close rounds (§17.3)
    private static readonly HashSet<string> CoreMaterialRiskCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "INVEST_PRIOR_INVESTMENT_UNCLEAR",
        "FND_EQUITY_DISPUTE",
        "COR_OWNERSHIP_DISPUTE",
        "COR_OWNERSHIP_MISMATCH",
        "IP_PRODUCT_RIGHTS_UNCONFIRMED",
        "FND_DEPARTED_UNRESOLVED"
    };

    public static MaterialDdEvaluationResult Evaluate(
        List<RiskFinding> findings,
        SharedFactStore facts)
    {
        var result = new MaterialDdEvaluationResult();

        bool isCloseOrActive = InvestmentTimingClassifier.IsCloseOrActiveRound(facts);
        if (!isCloseOrActive)
        {
            return result;
        }

        var blockerFindings = findings
            .Where(f => CoreMaterialRiskCodes.Contains(f.Code) && (f.Severity is RiskSeverity.Critical or RiskSeverity.Blocker or RiskSeverity.High))
            .ToList();

        if (blockerFindings.Count > 0)
        {
            result.HasMaterialDDIssue = true;
            result.BlockerFindings = blockerFindings;
            result.SourceRiskCodes = blockerFindings.Select(f => f.Code).Distinct().ToList();

            // Deduplicate blockers by RootCauseGroup to avoid double-counting merged roots
            result.UnresolvedBlockersCount = blockerFindings
                .Select(f => f.RootCauseGroup)
                .Distinct()
                .Count();

            var basisList = new List<RiskFindingBasis>();
            foreach (var bf in blockerFindings)
            {
                foreach (var b in bf.Basis)
                {
                    if (!basisList.Any(existing => existing.QuestionId == b.QuestionId && existing.AnswerId == b.AnswerId))
                    {
                        basisList.Add(new() { QuestionId = b.QuestionId, AnswerId = b.AnswerId });
                    }
                }
            }
            result.CombinedBasis = basisList;
        }

        return result;
    }
}
