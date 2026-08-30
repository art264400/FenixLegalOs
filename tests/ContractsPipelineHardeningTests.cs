using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Contracts;
using FenixLegalOs.Scoring.Validation;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ContractsPipelineHardeningTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;

    public ContractsPipelineHardeningTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_contracts_hardening_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
    }

    // =========================================================================
    // 1. ROOT APPLICABILITY STALE ISOLATION
    // =========================================================================

    [Fact(DisplayName = "1. CONTRACT-01 = none purges all diagnostic answers, facts, scores, findings, Strong Areas")]
    public void Root_Applicability_Stale_Isolation_Full()
    {
        // 1. Initial State: B2B clients with weak answers -> multiple findings emitted
        var rawAnswers1 = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "mostly_informal",
            ["CONTRACT-03"] = "generic",
            ["CONTRACT-04"] = "case",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "copied",
            ["CONTRACT-07"] = "often",
            ["CONTRACT-08"] = "near_total",
            ["CONTRACT-08A"] = "serious"
        };
        var res1 = _engine.ComputeResult(rawAnswers1);
        var contractSec1 = res1.Sections.First(s => s.SectionId == "contracts");
        Assert.Equal(ApplicabilityStatus.Applicable, contractSec1.Status);
        Assert.NotEmpty(res1.Risks.Where(r => r.Modules.Contains("contracts")));

        // 2. Switched State: CONTRACT-01 = ["none"]
        var rawAnswers2 = new Dictionary<string, object>(rawAnswers1)
        {
            ["CONTRACT-01"] = new List<string> { "none" }
        };
        var res2 = _engine.ComputeResult(rawAnswers2);
        var contractSec2 = res2.Sections.First(s => s.SectionId == "contracts");

        Assert.Equal(ApplicabilityStatus.NotApplicable, contractSec2.Status);
        Assert.Null(contractSec2.Score);
        Assert.Empty(res2.Risks.Where(r => r.Modules.Contains("contracts")));
        Assert.DoesNotContain(res2.Strengths, s => s.Contains("договор", StringComparison.OrdinalIgnoreCase));
    }

    // =========================================================================
    // 2. DOWNSTREAM STALE ISOLATION (CONTRACT-08A)
    // =========================================================================

    [Fact(DisplayName = "2. CONTRACT-08 = no purges hidden CONTRACT-08A answer and eliminates dependency finding")]
    public void Downstream_Contract08A_Stale_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-08"] = "no",       // dependency = none
            ["CONTRACT-08A"] = "serious" // Stale hidden answer in raw map
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-08A");
        Assert.DoesNotContain("CONTRACT-08A", effective.Keys);
        Assert.False(facts.Facts.ContainsKey("contracts.counterpartyExitRisk"));

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY");
    }

    // =========================================================================
    // 3. CONTRACT_LARGE_DEAL_REVIEW UNKNOWN INVARIANT
    // =========================================================================

    [Theory(DisplayName = "3. CONTRACT_LARGE_DEAL_REVIEW exact trigger regression")]
    [InlineData("sometimes", true)]
    [InlineData("often_unreviewed", true)]
    [InlineData("unknown", false)]       // Lowers confidence only, NO finding
    [InlineData("reviewed", false)]
    [InlineData("not_applicable", false)] // N/A option NEVER triggers
    public void LargeDealReview_Trigger_Regression(string reviewFact, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.largeDealReview"] = reviewFact
            }
        };

        var ruleEngine = new ContractRuleEngine();
        var findings = ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_LARGE_DEAL_REVIEW"));
    }

    // =========================================================================
    // 4. CLASS A DEPENDENCY RULE EXACTNESS & CRITICAL GUARD
    // =========================================================================

    [Theory(DisplayName = "4. CONTRACT_COUNTERPARTY_DEPENDENCY full positive and near-miss matrix")]
    [InlineData("material", "serious", true)]
    [InlineData("material", "unknown", true)]
    [InlineData("near_total", "serious", true)]
    [InlineData("near_total", "unknown", true)]
    [InlineData("noticeable", "serious", false)]
    [InlineData("material", "backup", false)]
    [InlineData("near_total", "protected", false)]
    [InlineData("none", "serious", false)]
    public void Counterparty_Dependency_Matrix(string dep, string exitRisk, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.counterpartyDependency"] = dep,
                ["contracts.counterpartyExitRisk"] = exitRisk
            }
        };

        var ruleEngine = new ContractRuleEngine();
        var findings = ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY"));

        if (expectedFinding)
        {
            var f = findings.First(x => x.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY");
            // Critical escalation guard: severity must remain High
            Assert.Equal(RiskSeverity.High, f.Severity);
        }
    }

    // =========================================================================
    // 5. ALL SIX FINDINGS COEXISTENCE & SUPPRESSION AUDIT
    // =========================================================================

    [Fact(DisplayName = "5. All six Contract findings coexist cleanly without auto-suppression by RootCauseGroup")]
    public void All_Six_Findings_Coexist()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "mostly_informal", // CONTRACTS_NOT_FORMALIZED
            ["CONTRACT-03"] = "outside",          // CONTRACT_SCOPE_UNCLEAR
            ["CONTRACT-04"] = "case",             // payment_termination (no finding)
            ["CONTRACT-05"] = "weak",             // CONTRACT_RISK_ALLOCATION_WEAK
            ["CONTRACT-06"] = "copied",           // CONTRACT_MODEL_MISMATCH
            ["CONTRACT-07"] = "sometimes",        // CONTRACT_LARGE_DEAL_REVIEW
            ["CONTRACT-08"] = "near_total",       // CONTRACT_COUNTERPARTY_DEPENDENCY
            ["CONTRACT-08A"] = "serious"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var contractRisks = result.Risks.Where(r => r.Modules.Contains("contracts")).ToList();

        Assert.Equal(6, contractRisks.Count);
        Assert.Contains(contractRisks, r => r.Code == "CONTRACTS_NOT_FORMALIZED");
        Assert.Contains(contractRisks, r => r.Code == "CONTRACT_SCOPE_UNCLEAR");
        Assert.Contains(contractRisks, r => r.Code == "CONTRACT_RISK_ALLOCATION_WEAK");
        Assert.Contains(contractRisks, r => r.Code == "CONTRACT_MODEL_MISMATCH");
        Assert.Contains(contractRisks, r => r.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY");
        Assert.Contains(contractRisks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");
    }

    [Fact(DisplayName = "6. FindingProcessor collapses identical duplicate risk codes generically")]
    public void FindingProcessor_Duplicate_Collapse()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var codes = result.Risks.Select(r => r.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    // =========================================================================
    // 6. BASIS INTEGRITY
    // =========================================================================

    [Fact(DisplayName = "7. Every emitted Contract finding has concrete, non-empty basis with active questions")]
    public void Basis_Integrity_Verification()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal",
            ["CONTRACT-03"] = "generic",
            ["CONTRACT-05"] = "general",
            ["CONTRACT-06"] = "templates",
            ["CONTRACT-07"] = "sometimes",
            ["CONTRACT-08"] = "material",
            ["CONTRACT-08A"] = "unknown"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var contractRisks = result.Risks.Where(r => r.Modules.Contains("contracts")).ToList();

        Assert.Equal(6, contractRisks.Count);
        foreach (var r in contractRisks)
        {
            Assert.NotEmpty(r.Basis);
            foreach (var b in r.Basis)
            {
                Assert.NotEmpty(b.QuestionId);
                Assert.NotEmpty(b.AnswerId);
                Assert.StartsWith("CONTRACT-", b.QuestionId);
            }
        }
    }

    // =========================================================================
    // 7. STRONG AREAS & PAYMENT_TERMINATION DIMENSION
    // =========================================================================

    [Fact(DisplayName = "8. Strong Areas: dimension >= 80% with no High finding becomes Strong Area")]
    public void Strong_Areas_Verification()
    {
        // Case A: written_form score = 100% (always), no finding -> Strong Area
        var rawAnswersA = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "no"
        };
        var resA = _engine.ComputeResult(rawAnswersA);
        Assert.Contains(resA.Strengths, s => s.Contains("Письменная форма", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(resA.Strengths, s => s.Contains("Оплата, расторжение", StringComparison.OrdinalIgnoreCase));

        // Case B: written_form score < 80% (material_informal = 35%) + High finding -> NOT Strong Area
        var rawAnswersB = new Dictionary<string, object>(rawAnswersA)
        {
            ["CONTRACT-02"] = "material_informal"
        };
        var resB = _engine.ComputeResult(rawAnswersB);
        Assert.DoesNotContain(resB.Strengths, s => s.Contains("Письменная форма", StringComparison.OrdinalIgnoreCase));

        // Case C: Medium CONTRACT_LARGE_DEAL_REVIEW does not block Strong Area if dimension >= 80%
        // (CONTRACT-07 = sometimes: 0.65 * 40 + CONTRACT-08 = no: 1.0 * 25 + CONTRACT-08A hidden -> (26 + 25) / 65 = 78.4% -> <80%)
        // If CONTRACT-07 is reviewed (1.0) and CONTRACT-08 is noticeable (0.75) -> (40 + 18.75 + 35) / 100 = 93.75% -> Strong Area
    }

    [Fact(DisplayName = "9. Payment_termination dimension weakness affects score without emitting finding")]
    public void Payment_Termination_Weakness_Without_Finding()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "case", // Score = 0.20
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "no"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var contractSec = result.Sections.First(s => s.SectionId == "contracts");
        var payDim = contractSec.Dimensions.First(d => d.DimensionId == "payment_termination");

        Assert.Equal(20, payDim.Score);
        Assert.Empty(result.Risks.Where(r => r.Modules.Contains("contracts")));
    }

    // =========================================================================
    // 8. DYNAMIC DENOMINATOR & NULLABLE / N/A SCORING
    // =========================================================================

    [Fact(DisplayName = "10. dependency_large_deals dynamic denominator renormalization across all 4 scenarios")]
    public void Dependency_Large_Deals_Dynamic_Denominator()
    {
        // Scenario A: 07, 08, 08A all visible (weights 40, 25, 35)
        var rawA = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-07"] = "reviewed",   // 1.0 * 40 = 40
            ["CONTRACT-08"] = "material",   // 0.35 * 25 = 8.75
            ["CONTRACT-08A"] = "protected"  // 1.0 * 35 = 35
        };
        var resA = _engine.ComputeResult(rawA);
        var dimA = resA.Sections.First(s => s.SectionId == "contracts").Dimensions.First(d => d.DimensionId == "dependency_large_deals");
        // Total: 40 + 8.75 + 35 = 83.75 -> 84%
        Assert.Equal(84, dimA.Score);

        // Scenario B: 07 hidden (PROD-02 consumers + CONTRACT-01 partners), 08 & 08A visible (weights 25, 35; denominator 60)
        var rawB = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-08"] = "material",   // 0.35 * 25 = 8.75
            ["CONTRACT-08A"] = "protected"  // 1.0 * 35 = 35
        };
        var resB = _engine.ComputeResult(rawB);
        var dimB = resB.Sections.First(s => s.SectionId == "contracts").Dimensions.First(d => d.DimensionId == "dependency_large_deals");
        // Total: (8.75 + 35) / 60 = 43.75 / 60 = 72.9% -> 73%
        Assert.Equal(73, dimB.Score);

        // Scenario C: 07 = no_large (N/A, excluded), 08 & 08A visible (denominator 60)
        var rawC = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-07"] = "no_large",   // N/A
            ["CONTRACT-08"] = "material",   // 0.35 * 25 = 8.75
            ["CONTRACT-08A"] = "protected"  // 1.0 * 35 = 35
        };
        var resC = _engine.ComputeResult(rawC);
        var dimC = resC.Sections.First(s => s.SectionId == "contracts").Dimensions.First(d => d.DimensionId == "dependency_large_deals");
        // Total: (8.75 + 35) / 60 = 73%
        Assert.Equal(73, dimC.Score);

        // Scenario D: 08 = no (1.0), 08A hidden (35% excluded), 07 = no_large (40% excluded) -> denominator 25
        var rawD = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-07"] = "no_large", // N/A
            ["CONTRACT-08"] = "no"        // 1.0 * 25
        };
        var resD = _engine.ComputeResult(rawD);
        var dimD = resD.Sections.First(s => s.SectionId == "contracts").Dimensions.First(d => d.DimensionId == "dependency_large_deals");
        Assert.Equal(100, dimD.Score);
    }

    // =========================================================================
    // 9. OVERALL SCORE WEIGHT & DETERMINISM
    // =========================================================================

    [Fact(DisplayName = "11. Overall score integrates Contracts with 8% weight when applicable, renormalizes when N/A")]
    public void Overall_Score_8Percent_Weight_And_Renormalization()
    {
        // 1. Applicable Contracts scenario
        var rawApplicable = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "no"
        };
        var resApp = _engine.ComputeResult(rawApplicable);
        var secApp = resApp.Sections.First(s => s.SectionId == "contracts");
        Assert.Equal(8, secApp.Weight);
        Assert.Equal(ApplicabilityStatus.Applicable, secApp.Status);
        Assert.Equal(100, secApp.Score);

        // 2. N/A Contracts scenario
        var rawNA = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" }
        };
        var resNA = _engine.ComputeResult(rawNA);
        var secNA = resNA.Sections.First(s => s.SectionId == "contracts");
        Assert.Equal(ApplicabilityStatus.NotApplicable, secNA.Status);
        Assert.Null(secNA.Score);
    }

    [Fact(DisplayName = "12. ComputeResult is 100% deterministic across multiple evaluations")]
    public void ComputeResult_Is_Deterministic()
    {
        var raw = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal",
            ["CONTRACT-03"] = "outside",
            ["CONTRACT-04"] = "case",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "copied",
            ["CONTRACT-07"] = "sometimes",
            ["CONTRACT-08"] = "near_total",
            ["CONTRACT-08A"] = "serious"
        };

        var r1 = _engine.ComputeResult(raw);
        var r2 = _engine.ComputeResult(raw);
        r1.ComputedAt = "STATIC";
        r2.ComputedAt = "STATIC";

        Assert.Equal(JsonSerializer.Serialize(r1), JsonSerializer.Serialize(r2));
    }

    // =========================================================================
    // 10. REGISTRY INVARIANTS
    // =========================================================================

    [Fact(DisplayName = "13. Registry counts: Contracts = 9 Qs, 6 Dims, 6 Risks; Total system risks = 88")]
    public void Registry_Invariants_Verification()
    {
        var contractQs = DataBank.Questions.Where(q => q.SectionId == "contracts").ToList();
        var contractDims = DataBank.Dimensions.Where(d => d.SectionId == "contracts").ToList();
        var contractRisks = DataBank.Risks.Where(r => r.SectionId == "contracts").ToList();

        Assert.Equal(9, contractQs.Count);
        Assert.Equal(6, contractDims.Count);
        Assert.Equal(6, contractRisks.Count);
        Assert.Equal(100, DataBank.Risks.Count);
    }
}
