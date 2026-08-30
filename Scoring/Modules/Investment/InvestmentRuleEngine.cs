using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Investment;

public class InvestmentRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "investment";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        var timing = (string?)f.GetValueOrDefault("investment.timing");
        bool priorInvestment = f.TryGetValue("investment.priorInvestment", out var piObj) && piObj is true;
        var priorInvestmentStatus = (string?)f.GetValueOrDefault("investment.priorInvestmentStatus");
        var priorRightsClarity = (string?)f.GetValueOrDefault("investment.priorRightsClarity");
        var futureOwnershipClarity = (string?)f.GetValueOrDefault("investment.futureOwnershipClarity");
        var dilutionModel = (string?)f.GetValueOrDefault("investment.dilutionModel");
        var roundDefinition = (string?)f.GetValueOrDefault("investment.roundDefinition");
        var runwayKnown = (string?)f.GetValueOrDefault("investment.runwayKnown");
        var runwayMonthsBucket = (string?)f.GetValueOrDefault("investment.runwayMonthsBucket");
        var financialModel = (string?)f.GetValueOrDefault("investment.financialModel");
        var metricsEvidence = (string?)f.GetValueOrDefault("investment.metricsEvidence");
        var documentFolder = (string?)f.GetValueOrDefault("investment.documentFolder");
        var dealTermsUnderstanding = (string?)f.GetValueOrDefault("investment.dealTermsUnderstanding");
        var dealReview = (string?)f.GetValueOrDefault("investment.dealReview");

        // ─── 1. INVEST_PRIOR_INVESTMENT_UNCLEAR (§27.2 Class A) ──────────────
        // investment.priorInvestment == true AND (investment.priorInvestmentStatus in [partial, informal] OR investment.priorRightsClarity in [unclear, none, unknown])
        if (priorInvestment &&
            (priorInvestmentStatus is "partial" or "informal" ||
             priorRightsClarity is "unclear" or "none" or "unknown"))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR");
            if (def != null)
            {
                var basis = new List<RiskFindingBasis>();
                if (priorInvestmentStatus is "partial" or "informal")
                {
                    basis.Add(new() { QuestionId = "INVEST-02", AnswerId = priorInvestmentStatus });
                }
                if (priorRightsClarity is "unclear" or "none" or "unknown")
                {
                    basis.Add(new() { QuestionId = "INVEST-02A", AnswerId = priorRightsClarity });
                }
                if (basis.Count == 0)
                {
                    basis.Add(new() { QuestionId = "INVEST-02", AnswerId = "true" });
                }

                list.Add(new RiskFinding
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
                    Basis = basis
                });
            }
        }

        // ─── 2. INVEST_FUTURE_CAP_TABLE_UNCLEAR (§25 Class B) ────────────────
        // investment.futureOwnershipClarity in [current_only, none] -> HIGH
        if (futureOwnershipClarity is "current_only" or "none")
        {
            AddFinding(list, allRisks, "INVEST_FUTURE_CAP_TABLE_UNCLEAR", "INVEST-03", futureOwnershipClarity, RiskSeverity.High);
        }

        // ─── 3. INVEST_DILUTION_NOT_MODELED (§25 Class B) ────────────────────
        // investment.dilutionModel in [rough, none] -> MEDIUM
        if (dilutionModel is "rough" or "none")
        {
            AddFinding(list, allRisks, "INVEST_DILUTION_NOT_MODELED", "INVEST-04", dilutionModel, RiskSeverity.Medium);
        }

        // ─── 4. INVEST_ROUND_NOT_DEFINED (§25 Class B) ───────────────────────
        // investment.roundDefinition in [max_possible, none] -> MEDIUM
        if (roundDefinition is "max_possible" or "none")
        {
            AddFinding(list, allRisks, "INVEST_ROUND_NOT_DEFINED", "INVEST-05", roundDefinition, RiskSeverity.Medium);
        }

        // ─── 5. INVEST_RUNWAY_WARNING (§25 Class B) ──────────────────────────
        // investment.timing != "none" AND (investment.runwayMonthsBucket == "lt3" OR investment.runwayKnown in [none, old]) -> HIGH
        if (!string.IsNullOrEmpty(timing) && timing != "none" &&
            (runwayMonthsBucket == "lt3" || runwayKnown is "none" or "old"))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "INVEST_RUNWAY_WARNING");
            if (def != null)
            {
                var basis = new List<RiskFindingBasis>();
                if (runwayMonthsBucket == "lt3")
                {
                    basis.Add(new() { QuestionId = "INVEST-06A", AnswerId = "lt3" });
                }
                if (runwayKnown is "none" or "old")
                {
                    basis.Add(new() { QuestionId = "INVEST-06", AnswerId = runwayKnown });
                }

                list.Add(new RiskFinding
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
                    Basis = basis
                });
            }
        }

        // ─── 6. INVEST_FIN_MODEL_WEAK (§25 Class B) ──────────────────────────
        // investment.financialModel in [old, fragments, none] -> HIGH
        if (financialModel is "old" or "fragments" or "none")
        {
            AddFinding(list, allRisks, "INVEST_FIN_MODEL_WEAK", "INVEST-07", financialModel, RiskSeverity.High);
        }

        // ─── 7. INVEST_METRICS_UNVERIFIABLE (§25 Class B) ────────────────────
        // investment.metricsEvidence in [approx, hard] -> HIGH
        if (metricsEvidence is "approx" or "hard")
        {
            AddFinding(list, allRisks, "INVEST_METRICS_UNVERIFIABLE", "INVEST-08", metricsEvidence, RiskSeverity.High);
        }

        // ─── 8. INVEST_DD_DOCS_NOT_READY (§27.2 Class A) ─────────────────────
        // investment.timing != "none" AND investment.documentFolder in [scattered, reconstruct, missing, unknown] -> HIGH
        if (!string.IsNullOrEmpty(timing) && timing != "none" &&
            documentFolder is "scattered" or "reconstruct" or "missing" or "unknown")
        {
            AddFinding(list, allRisks, "INVEST_DD_DOCS_NOT_READY", "INVEST-09", documentFolder, RiskSeverity.High);
        }

        // ─── 9. INVEST_TERMS_NOT_UNDERSTOOD (§27.2 Class A) ──────────────────
        // investment.timing in [specific_investor, terms_received] AND investment.dealTermsUnderstanding in [price_only, unclear, not_reviewed] -> CRITICAL
        if (timing is "specific_investor" or "terms_received" &&
            dealTermsUnderstanding is "price_only" or "unclear" or "not_reviewed")
        {
            AddFinding(list, allRisks, "INVEST_TERMS_NOT_UNDERSTOOD", "INVEST-12", dealTermsUnderstanding, RiskSeverity.Critical);
        }

        // ─── 10. INVEST_DEAL_UNREVIEWED (§25 Class B) ────────────────────────
        // investment.timing in [specific_investor, terms_received] AND investment.dealReview in [lawyer_unclear, self, none] -> HIGH
        if (timing is "specific_investor" or "terms_received" &&
            dealReview is "lawyer_unclear" or "self" or "none")
        {
            AddFinding(list, allRisks, "INVEST_DEAL_UNREVIEWED", "INVEST-15", dealReview, RiskSeverity.High);
        }

        // ─── 11. INVEST_SELF_AWARENESS_GAP (§27.2 Class A) ───────────────────
        // Deferred to Stage 3 cross-module / finalFindings stage.

        // ─── 12. INVEST_ROUND_BLOCKER (§27.2 Class A) ────────────────────────
        // Deferred to Stage 3 cross-module / readiness overlay stage.

        return list;
    }

    private static void AddFinding(
        List<RiskFinding> list,
        IReadOnlyList<RiskDefinition> allRisks,
        string riskCode,
        string questionId,
        string answerId,
        RiskSeverity severity)
    {
        var def = allRisks.FirstOrDefault(r => r.Code == riskCode);
        if (def == null) return;

        list.Add(new RiskFinding
        {
            Code = def.Code,
            SectionId = def.SectionId,
            Modules = new(def.Modules),
            Severity = severity,
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
            Basis = new List<RiskFindingBasis>
            {
                new() { QuestionId = questionId, AnswerId = answerId }
            }
        });
    }
}
