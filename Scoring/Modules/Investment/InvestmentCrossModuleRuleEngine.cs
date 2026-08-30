using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Modules.Investment;

public static class InvestmentCrossModuleRuleEngine
{
    public static List<RiskFinding> Evaluate(
        List<RiskFinding> existingMergedFindings,
        SharedFactStore facts,
        IReadOnlyList<RiskDefinition> allRisks)
    {
        var crossFindings = new List<RiskFinding>();

        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        bool priorInvestment = facts.Facts.TryGetValue("investment.priorInvestment", out var pi) && (pi is true || pi?.ToString() == "unknown");
        bool isInvestmentApplicable = timing != "none" || priorInvestment;

        if (!isInvestmentApplicable)
        {
            return crossFindings;
        }

        // ─── 1. INVEST_SELF_AWARENESS_GAP (§27.2 Class A) ────────────────────
        // investment.selfReportedIssues == "none" AND count(finalFindings where severity in [HIGH, CRITICAL, BLOCKER] and module != INVESTMENT) > 0
        var selfReportedIssues = (string?)facts.Facts.GetValueOrDefault("investment.selfReportedIssues");
        if (selfReportedIssues == "none")
        {
            var externalSevereFindings = existingMergedFindings
                .Where(f => !f.Modules.Contains("investment") &&
                            (f.Severity is RiskSeverity.High or RiskSeverity.Critical or RiskSeverity.Blocker))
                .ToList();

            if (externalSevereFindings.Count > 0)
            {
                var def = allRisks.FirstOrDefault(r => r.Code == "INVEST_SELF_AWARENESS_GAP")
                          ?? DataBank.Risks.FirstOrDefault(r => r.Code == "INVEST_SELF_AWARENESS_GAP");

                if (def != null)
                {
                    var basisList = new List<RiskFindingBasis>
                    {
                        new() { QuestionId = "INVEST-10", AnswerId = "none" }
                    };

                    foreach (var extFinding in externalSevereFindings)
                    {
                        foreach (var b in extFinding.Basis)
                        {
                            if (!basisList.Any(existing => existing.QuestionId == b.QuestionId && existing.AnswerId == b.AnswerId))
                            {
                                basisList.Add(new() { QuestionId = b.QuestionId, AnswerId = b.AnswerId });
                            }
                        }
                    }

                    crossFindings.Add(new RiskFinding
                    {
                        Code = def.Code,
                        SectionId = def.SectionId,
                        Modules = new(def.Modules),
                        Severity = def.Severity,
                        Priority = def.Priority,
                        RootCauseGroup = def.RootCauseGroup,
                        Resolution = def.Resolution,
                        ServiceCode = def.ServiceCode,
                        Title = def.Title,
                        Finding = def.Finding,
                        WhyItMatters = def.WhyItMatters,
                        Recommendation = def.Recommendation,
                        Recommendations = new(def.Recommendations),
                        AffectedDimensions = new(def.AffectedDimensions),
                        LawyerRequired = def.LawyerRequired,
                        Basis = basisList
                    });
                }
            }
        }

        // ─── 2. INVEST_ROUND_BLOCKER (§27.2 Class A) ─────────────────────────
        // investment.timing in [3_6m, active_search, specific_investor, terms_received] AND materialDDIssue == true
        var materialResult = MaterialDdIssueEvaluator.Evaluate(existingMergedFindings, facts);
        if (materialResult.HasMaterialDDIssue)
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "INVEST_ROUND_BLOCKER")
                      ?? DataBank.Risks.FirstOrDefault(r => r.Code == "INVEST_ROUND_BLOCKER");

            if (def != null)
            {
                var basisList = new List<RiskFindingBasis>();
                if (!string.IsNullOrEmpty(timing))
                {
                    basisList.Add(new() { QuestionId = "INVEST-01", AnswerId = timing });
                }
                foreach (var b in materialResult.CombinedBasis)
                {
                    if (!basisList.Any(existing => existing.QuestionId == b.QuestionId && existing.AnswerId == b.AnswerId))
                    {
                        basisList.Add(new() { QuestionId = b.QuestionId, AnswerId = b.AnswerId });
                    }
                }

                crossFindings.Add(new RiskFinding
                {
                    Code = def.Code,
                    SectionId = def.SectionId,
                    Modules = new(def.Modules),
                    Severity = def.Severity,
                    Priority = def.Priority,
                    RootCauseGroup = def.RootCauseGroup,
                    Resolution = def.Resolution,
                    ServiceCode = def.ServiceCode,
                    Title = def.Title,
                    Finding = def.Finding,
                    WhyItMatters = def.WhyItMatters,
                    Recommendation = def.Recommendation,
                    Recommendations = new(def.Recommendations),
                    AffectedDimensions = new(def.AffectedDimensions),
                    LawyerRequired = def.LawyerRequired,
                    Basis = basisList
                });
            }
        }

        return crossFindings;
    }
}
