using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Contracts;

public class ContractRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "contracts";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        // ─── Contracts Normalized Facts ───────────────────────────────────────
        bool b2bRelevant = f.TryGetValue("contracts.b2bRelevant", out var b2bObj) && b2bObj is true;
        if (!b2bRelevant)
        {
            return list;
        }

        var writtenCoverage = (string?)f.GetValueOrDefault("contracts.writtenCoverage");
        var scopeClarity = (string?)f.GetValueOrDefault("contracts.scopeClarity");
        var riskAllocation = (string?)f.GetValueOrDefault("contracts.riskAllocation");
        var modelMatch = (string?)f.GetValueOrDefault("contracts.modelMatch");
        var largeDealReview = (string?)f.GetValueOrDefault("contracts.largeDealReview");
        var counterpartyDependency = (string?)f.GetValueOrDefault("contracts.counterpartyDependency");
        var counterpartyExitRisk = (string?)f.GetValueOrDefault("contracts.counterpartyExitRisk");

        // ─── 1. CONTRACTS_NOT_FORMALIZED (§25) ───────────────────────────────
        // contracts.writtenCoverage in [some_in_messages, material_informal, mostly_informal] -> HIGH
        if (writtenCoverage is "some_in_messages" or "material_informal" or "mostly_informal")
        {
            AddFinding(list, allRisks, "CONTRACTS_NOT_FORMALIZED", "CONTRACT-02", writtenCoverage, RiskSeverity.High);
        }

        // ─── 2. CONTRACT_SCOPE_UNCLEAR (§25) ─────────────────────────────────
        // contracts.scopeClarity in [outside, generic] -> HIGH
        if (scopeClarity is "outside" or "generic")
        {
            AddFinding(list, allRisks, "CONTRACT_SCOPE_UNCLEAR", "CONTRACT-03", scopeClarity, RiskSeverity.High);
        }

        // ─── 3. CONTRACT_RISK_ALLOCATION_WEAK (§25) ──────────────────────────
        // contracts.riskAllocation in [general, weak] -> HIGH
        if (riskAllocation is "general" or "weak")
        {
            AddFinding(list, allRisks, "CONTRACT_RISK_ALLOCATION_WEAK", "CONTRACT-05", riskAllocation, RiskSeverity.High);
        }

        // ─── 4. CONTRACT_MODEL_MISMATCH (§25) ────────────────────────────────
        // contracts.modelMatch in [templates, copied] -> HIGH
        if (modelMatch is "templates" or "copied")
        {
            AddFinding(list, allRisks, "CONTRACT_MODEL_MISMATCH", "CONTRACT-06", modelMatch, RiskSeverity.High);
        }

        // ─── 5. CONTRACT_COUNTERPARTY_DEPENDENCY (§27.2) ─────────────────────
        // contracts.counterpartyDependency in [material, near_total] AND contracts.counterpartyExitRisk in [serious, unknown] -> HIGH
        if (counterpartyDependency is "material" or "near_total" &&
            counterpartyExitRisk is "serious" or "unknown")
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY");
            if (def != null)
            {
                list.Add(new RiskFinding
                {
                    Code = def.Code,
                    SectionId = def.SectionId,
                    Modules = new(def.Modules),
                    Severity = RiskSeverity.High,
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
                        new() { QuestionId = "CONTRACT-08", AnswerId = counterpartyDependency },
                        new() { QuestionId = "CONTRACT-08A", AnswerId = counterpartyExitRisk }
                    }
                });
            }
        }

        // ─── 6. CONTRACT_LARGE_DEAL_REVIEW (§25) ─────────────────────────────
        // contracts.largeDealReview in [sometimes, often_unreviewed] -> MEDIUM
        // (CRITICAL INVARIANT: not_applicable NEVER triggers; unknown lowers confidence only)
        if (largeDealReview is "sometimes" or "often_unreviewed")
        {
            AddFinding(list, allRisks, "CONTRACT_LARGE_DEAL_REVIEW", "CONTRACT-07", largeDealReview, RiskSeverity.Medium);
        }

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
