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

public class ContractsCrossModuleIntegrationTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;
    private readonly ContractRuleEngine _ruleEngine;
    private readonly ContractFactNormalizer _normalizer;

    public ContractsCrossModuleIntegrationTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_contracts_integration_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
        _ruleEngine = new ContractRuleEngine();
        _normalizer = new ContractFactNormalizer();
    }

    // =========================================================================
    // 1. NAMESPACE OWNERSHIP
    // =========================================================================

    [Fact(DisplayName = "1. ContractFactNormalizer writes strictly to contracts.* and diagnostic.unknownQuestionIds")]
    public void ContractFactNormalizer_Namespace_Ownership()
    {
        var allAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients", "partners" },
            ["CONTRACT-02"] = "unknown",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "material",
            ["CONTRACT-08A"] = "serious"
        };

        var facts = new SharedFactStore();
        _normalizer.Normalize(allAnswers, facts);

        var allowedKeys = new HashSet<string>
        {
            "contracts.b2bRelevant",
            "contracts.counterpartyTypes",
            "contracts.writtenCoverage",
            "contracts.scopeClarity",
            "contracts.paymentTermination",
            "contracts.riskAllocation",
            "contracts.modelMatch",
            "contracts.largeDealReview",
            "contracts.counterpartyDependency",
            "contracts.counterpartyExitRisk",
            "diagnostic.unknownQuestionIds"
        };

        foreach (var key in facts.Facts.Keys)
        {
            Assert.Contains(key, allowedKeys);
            Assert.False(key.StartsWith("product."));
            Assert.False(key.StartsWith("team."));
            Assert.False(key.StartsWith("ip."));
            Assert.False(key.StartsWith("corporate."));
            Assert.False(key.StartsWith("founders."));
            Assert.False(key.StartsWith("data."));
            Assert.False(key.StartsWith("ai."));
            Assert.False(key.StartsWith("investment."));
        }
    }

    // =========================================================================
    // 2. RULE ENGINE IMMUTABILITY
    // =========================================================================

    [Fact(DisplayName = "2. ContractRuleEngine does NOT mutate SharedFactStore during evaluation")]
    public void ContractRuleEngine_Is_Pure_And_Immutable()
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.writtenCoverage"] = "material_informal",
                ["contracts.scopeClarity"] = "generic",
                ["contracts.riskAllocation"] = "weak",
                ["contracts.modelMatch"] = "copied",
                ["contracts.largeDealReview"] = "sometimes",
                ["contracts.counterpartyDependency"] = "near_total",
                ["contracts.counterpartyExitRisk"] = "serious"
            }
        };

        string snapshotBefore = JsonSerializer.Serialize(facts.Facts);
        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        string snapshotAfter = JsonSerializer.Serialize(facts.Facts);

        Assert.Equal(snapshotBefore, snapshotAfter);
        Assert.NotEmpty(findings);
    }

    // =========================================================================
    // 3. PRODUCT -> CONTRACTS ROUTING & STALE ISOLATION
    // =========================================================================

    [Fact(DisplayName = "3. Product userTypes companies makes CONTRACT-07 visible when CONTRACT-01 has partners (not clients)")]
    public void Product_Companies_Enables_Contract07()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" }
        };

        var (visible, _, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.Contains(visible, q => q.Id == "CONTRACT-07");
    }

    [Fact(DisplayName = "4. CONTRACT-01 clients makes CONTRACT-07 visible even if Product has only consumers")]
    public void Contract_Clients_Enables_Contract07()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["CONTRACT-01"] = new List<string> { "clients" }
        };

        var (visible, _, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.Contains(visible, q => q.Id == "CONTRACT-07");
    }

    [Fact(DisplayName = "5. Neither clients nor companies hides CONTRACT-07")]
    public void Neither_Clients_Nor_Companies_Hides_Contract07()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["CONTRACT-01"] = new List<string> { "partners", "suppliers" }
        };

        var (visible, _, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-07");
    }

    [Fact(DisplayName = "6. Stale Product companies removal hides CONTRACT-07 and drops stale largeDealReview fact")]
    public void Stale_Product_Removal_Hides_Contract07()
    {
        // 1. Initial State: Product has companies, CONTRACT-07 is answered "sometimes" (produces finding)
        var rawAnswers1 = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-07"] = "sometimes"
        };
        var res1 = _engine.ComputeResult(rawAnswers1);
        Assert.Contains(res1.Risks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");

        // 2. Switched State: PROD-02 changes to ["consumers"], stale "sometimes" remains in raw dictionary
        var rawAnswers2 = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-07"] = "sometimes"
        };
        var res2 = _engine.ComputeResult(rawAnswers2);
        Assert.DoesNotContain(res2.Risks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");
    }

    // =========================================================================
    // 4. PRODUCT SIGNAL != CONTRACT FINDING
    // =========================================================================

    [Fact(DisplayName = "7. Product companies signal makes CONTRACT-07 applicable but does NOT create finding directly")]
    public void Product_Signal_Does_Not_Create_Contract_Finding()
    {
        // Case A: Companies + CONTRACT-07 reviewed -> NO finding
        var rawAnswersA = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-07"] = "reviewed"
        };
        var resA = _engine.ComputeResult(rawAnswersA);
        Assert.DoesNotContain(resA.Risks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");

        // Case B: Companies + CONTRACT-07 sometimes -> finding emitted from CONTRACT-07 answer, not from Product
        var rawAnswersB = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-07"] = "sometimes"
        };
        var resB = _engine.ComputeResult(rawAnswersB);
        var finding = Assert.Single(resB.Risks.Where(r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW"));
        Assert.Contains(finding.Basis, b => b.QuestionId == "CONTRACT-07" && b.AnswerId == "sometimes");
        Assert.DoesNotContain(finding.Basis, b => b.QuestionId == "PROD-02");
    }

    // =========================================================================
    // 5. NAVIGATION / COMPUTATION CONSISTENCY
    // =========================================================================

    [Fact(DisplayName = "8. ResolveEffectiveState produces consistent visible and effective answers")]
    public void Navigation_And_Scoring_Consistency()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "no",
            ["CONTRACT-08A"] = "serious" // Stale hidden answer (CONTRACT-08 is "no")
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);

        // CONTRACT-08A is hidden and removed from effective
        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-08A");
        Assert.DoesNotContain("CONTRACT-08A", effective.Keys);

        // CONTRACT-07 is visible via PROD-02 companies and retained in effective
        Assert.Contains(visible, q => q.Id == "CONTRACT-07");
        Assert.Contains("CONTRACT-07", effective.Keys);

        var result = _engine.ComputeResult(rawAnswers);
        Assert.Equal(9, result.AnsweredCount); // 1 Product (PROD-02) + 8 Contracts (CONTRACT-01..08, excluding stale CONTRACT-08A)
    }

    // =========================================================================
    // 6. ROUTING DAG VALIDATION
    // =========================================================================

    [Fact(DisplayName = "9. RoutingDependencyValidator validates all 133 questions including Contracts DAG with 0 errors")]
    public void RoutingDag_Complete_Validation()
    {
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }
}
