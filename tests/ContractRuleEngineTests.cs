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

public class ContractRuleEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;
    private readonly ContractRuleEngine _ruleEngine;

    public ContractRuleEngineTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_contracts_stage2_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
        _ruleEngine = new ContractRuleEngine();
    }

    // =========================================================================
    // 1. REGISTRY TESTS (§24)
    // =========================================================================

    [Fact(DisplayName = "1. Contract RiskDefinitions count is exactly 6")]
    public void ContractRisks_Count_Is_6()
    {
        Assert.Equal(6, ContractRisks.All.Count);
        var inBank = DataBank.Risks.Where(r => r.SectionId == "contracts").ToList();
        Assert.Equal(6, inBank.Count);
    }

    [Fact(DisplayName = "2. All six exact canonical risk codes exist")]
    public void All_Six_Exact_Codes_Exist()
    {
        var expectedCodes = new HashSet<string>
        {
            "CONTRACTS_NOT_FORMALIZED",
            "CONTRACT_SCOPE_UNCLEAR",
            "CONTRACT_RISK_ALLOCATION_WEAK",
            "CONTRACT_MODEL_MISMATCH",
            "CONTRACT_COUNTERPARTY_DEPENDENCY",
            "CONTRACT_LARGE_DEAL_REVIEW"
        };

        var actualCodes = ContractRisks.All.Select(r => r.Code).ToHashSet();
        Assert.Equal(expectedCodes, actualCodes);
    }

    [Fact(DisplayName = "3. Risk codes are unique in ContractRisks and DataBank")]
    public void RiskCodes_Are_Unique()
    {
        var contractCodes = ContractRisks.All.Select(r => r.Code).ToList();
        Assert.Equal(contractCodes.Count, contractCodes.Distinct().Count());

        var allBankCodes = DataBank.Risks.Select(r => r.Code).ToList();
        Assert.Equal(allBankCodes.Count, allBankCodes.Distinct().Count());
    }

    [Fact(DisplayName = "4. Every AffectedDimension resolves to canonical Contracts dimensions")]
    public void Every_AffectedDimension_Resolves()
    {
        var validDims = ContractDimensions.All.Select(d => d.Id).ToHashSet();
        foreach (var risk in ContractRisks.All)
        {
            Assert.NotEmpty(risk.AffectedDimensions);
            foreach (var dim in risk.AffectedDimensions)
            {
                Assert.Contains(dim, validDims);
            }
        }
    }

    [Fact(DisplayName = "5. Every ServiceCode is canonical CONTRACTS_REVIEW")]
    public void ServiceCodes_Are_Canonical()
    {
        foreach (var risk in ContractRisks.All)
        {
            Assert.Equal("CONTRACTS_REVIEW", risk.ServiceCode);
        }
    }

    [Fact(DisplayName = "6-7. Suppression targets resolve and no self suppression exists")]
    public void Suppression_Targets_Are_Valid()
    {
        var allCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();
        foreach (var risk in ContractRisks.All)
        {
            Assert.DoesNotContain(risk.Code, risk.SuppressCodes);
            foreach (var sup in risk.SuppressCodes)
            {
                Assert.Contains(sup, allCodes);
            }
        }
    }

    [Fact(DisplayName = "8-9. Metadata copied from RiskDefinition and resolves through DataBank")]
    public void Metadata_Copied_From_RiskDefinition()
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.writtenCoverage"] = "material_informal"
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        var finding = Assert.Single(findings);
        var def = DataBank.Risks.First(r => r.Code == "CONTRACTS_NOT_FORMALIZED");

        Assert.Equal(def.Severity, finding.Severity);
        Assert.Equal(def.Priority, finding.Priority);
        Assert.Equal(def.RootCauseGroup, finding.RootCauseGroup);
        Assert.Equal(def.ServiceCode, finding.ServiceCode);
        Assert.Equal(def.AffectedDimensions, finding.AffectedDimensions);
        Assert.NotEmpty(finding.Basis);
    }

    [Fact(DisplayName = "10. No invented seventh Contract risk exists")]
    public void No_Invented_Seventh_Risk()
    {
        var forbiddenCodes = new[]
        {
            "CONTRACT_PAYMENT_TERMINATION_WEAK",
            "CONTRACT_NO_LIABILITY_CAP",
            "CONTRACT_NO_GOVERNING_LAW",
            "CONTRACT_NO_SLA",
            "CONTRACT_TERMINATION_RISK",
            "CONTRACT_CUSTOMER_CONCENTRATION"
        };

        foreach (var code in forbiddenCodes)
        {
            Assert.DoesNotContain(DataBank.Risks, r => r.Code == code);
        }
    }

    // =========================================================================
    // 2. BUSINESS RULES TESTS (§25 & §27.2)
    // =========================================================================

    // --- CONTRACTS_NOT_FORMALIZED ---
    [Theory(DisplayName = "11-13. CONTRACTS_NOT_FORMALIZED trigger, healthy, and boundary")]
    [InlineData("some_in_messages", true)]
    [InlineData("material_informal", true)]
    [InlineData("mostly_informal", true)]
    [InlineData("always", false)]
    [InlineData("unknown", false)]
    public void ContractsNotFormalized_Triggers(string coverage, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.writtenCoverage"] = coverage
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACTS_NOT_FORMALIZED"));
    }

    // --- CONTRACT_SCOPE_UNCLEAR ---
    [Theory(DisplayName = "14-16. CONTRACT_SCOPE_UNCLEAR trigger, clear, mostly boundary")]
    [InlineData("outside", true)]
    [InlineData("generic", true)]
    [InlineData("clear", false)]
    [InlineData("mostly", false)]
    [InlineData("unknown", false)]
    public void ContractScopeUnclear_Triggers(string scope, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.scopeClarity"] = scope
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_SCOPE_UNCLEAR"));
    }

    // --- CONTRACT_RISK_ALLOCATION_WEAK ---
    [Theory(DisplayName = "17-19. CONTRACT_RISK_ALLOCATION_WEAK trigger, healthy, mostly boundary")]
    [InlineData("general", true)]
    [InlineData("weak", true)]
    [InlineData("clear", false)]
    [InlineData("mostly", false)]
    [InlineData("unknown", false)]
    public void ContractRiskAllocationWeak_Triggers(string allocation, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.riskAllocation"] = allocation
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_RISK_ALLOCATION_WEAK"));
    }

    // --- CONTRACT_MODEL_MISMATCH ---
    [Theory(DisplayName = "20-22. CONTRACT_MODEL_MISMATCH trigger, custom/adapted non-trigger")]
    [InlineData("templates", true)]
    [InlineData("copied", true)]
    [InlineData("custom", false)]
    [InlineData("adapted", false)]
    [InlineData("unknown", false)]
    public void ContractModelMismatch_Triggers(string model, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.modelMatch"] = model
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_MODEL_MISMATCH"));
    }

    // --- CONTRACT_LARGE_DEAL_REVIEW ---
    [Theory(DisplayName = "23-26. CONTRACT_LARGE_DEAL_REVIEW trigger, reviewed, unknown non-trigger, not_applicable NEVER triggers")]
    [InlineData("sometimes", true)]
    [InlineData("often_unreviewed", true)]
    [InlineData("unknown", false)]
    [InlineData("reviewed", false)]
    [InlineData("not_applicable", false)]
    public void ContractLargeDealReview_Triggers(string review, bool expectedFinding)
    {
        var facts = new SharedFactStore
        {
            Facts = new Dictionary<string, object?>
            {
                ["contracts.b2bRelevant"] = true,
                ["contracts.largeDealReview"] = review
            }
        };

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_LARGE_DEAL_REVIEW"));
    }

    // --- CONTRACT_COUNTERPARTY_DEPENDENCY (§27.2 Matrix) ---
    [Theory(DisplayName = "27-34. CONTRACT_COUNTERPARTY_DEPENDENCY exact §27.2 matrix")]
    [InlineData("material", "serious", true)]
    [InlineData("material", "unknown", true)]
    [InlineData("near_total", "serious", true)]
    [InlineData("near_total", "unknown", true)]
    [InlineData("noticeable", "serious", false)]
    [InlineData("material", "backup", false)]
    [InlineData("near_total", "protected", false)]
    [InlineData("none", "serious", false)]
    public void ContractCounterpartyDependency_Matrix(string dep, string exitRisk, bool expectedFinding)
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

        var findings = _ruleEngine.Evaluate(facts, DataBank.Risks);
        Assert.Equal(expectedFinding, findings.Any(f => f.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY"));
    }

    // =========================================================================
    // 3. PIPELINE & INTEGRATION TESTS (§26)
    // =========================================================================

    [Fact(DisplayName = "35. Contracts N/A produces ZERO Contract findings")]
    public void Contracts_NotApplicable_Produces_Zero_Findings()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" },
            ["CONTRACT-02"] = "mostly_informal",
            ["CONTRACT-03"] = "generic",
            ["CONTRACT-04"] = "case",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "copied",
            ["CONTRACT-07"] = "often",
            ["CONTRACT-08"] = "near_total",
            ["CONTRACT-08A"] = "serious"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var contractRisks = result.Risks.Where(r => r.Modules.Contains("contracts")).ToList();
        Assert.Empty(contractRisks);
    }

    [Fact(DisplayName = "36. Applicable weak Contracts scenario produces expected canonical findings")]
    public void Applicable_Weak_Contracts_Scenario()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "mostly_informal", // CONTRACTS_NOT_FORMALIZED
            ["CONTRACT-03"] = "generic",         // CONTRACT_SCOPE_UNCLEAR
            ["CONTRACT-04"] = "case",            // No finding (payment_termination)
            ["CONTRACT-05"] = "weak",            // CONTRACT_RISK_ALLOCATION_WEAK
            ["CONTRACT-06"] = "copied",          // CONTRACT_MODEL_MISMATCH
            ["CONTRACT-07"] = "often",           // CONTRACT_LARGE_DEAL_REVIEW
            ["CONTRACT-08"] = "near_total",      // CONTRACT_COUNTERPARTY_DEPENDENCY
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

    [Fact(DisplayName = "37. Switching CONTRACT-01 from clients to none removes all stale findings")]
    public void Switching_Contract01_Removes_Stale_Findings()
    {
        // 1. Initial State: B2B clients with weak answers -> findings present
        var rawAnswers1 = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal",
            ["CONTRACT-05"] = "weak"
        };
        var res1 = _engine.ComputeResult(rawAnswers1);
        Assert.Contains(res1.Risks, r => r.Code == "CONTRACTS_NOT_FORMALIZED");
        Assert.Contains(res1.Risks, r => r.Code == "CONTRACT_RISK_ALLOCATION_WEAK");

        // 2. Switched State: CONTRACT-01 = ["none"]
        var rawAnswers2 = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" },
            ["CONTRACT-02"] = "material_informal",
            ["CONTRACT-05"] = "weak"
        };
        var res2 = _engine.ComputeResult(rawAnswers2);
        Assert.Empty(res2.Risks.Where(r => r.Modules.Contains("contracts")));
    }

    [Fact(DisplayName = "38. Stale hidden CONTRACT-08A cannot trigger dependency finding when CONTRACT-08 becomes 'no'")]
    public void Stale_Hidden_Contract08A_Cannot_Trigger()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-08"] = "no",       // dependency = none
            ["CONTRACT-08A"] = "serious" // Stale hidden answer
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "CONTRACT_COUNTERPARTY_DEPENDENCY");
    }

    [Fact(DisplayName = "39. Product companies makes CONTRACT-07 applicable but does NOT create finding by itself")]
    public void Product_Companies_Only_Makes_Contract07_Applicable()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" },
            ["CONTRACT-07"] = "reviewed" // Healthy answer
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");
    }

    [Fact(DisplayName = "40. CONTRACT-07 = unknown lowers confidence without emitting CONTRACT_LARGE_DEAL_REVIEW")]
    public void Contract07_Unknown_Lowers_Confidence_Without_Finding()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "unknown", // Lowers confidence, but NO finding
            ["CONTRACT-08"] = "no"
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.True(result.Confidence < 100);
        Assert.DoesNotContain(result.Risks, r => r.Code == "CONTRACT_LARGE_DEAL_REVIEW");
    }

    [Fact(DisplayName = "41. Payment_termination can be weak without invented finding")]
    public void PaymentTermination_Weak_Without_Finding()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "case", // Score = 0.20 (weak payment_termination)
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

    [Fact(DisplayName = "42. FindingProcessor handles Contract findings generically without auto-suppression by RootCauseGroup")]
    public void Same_RootCauseGroup_Does_Not_Auto_Suppress()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal", // Root: COMMERCIAL_CONTRACTS
            ["CONTRACT-03"] = "generic",           // Root: COMMERCIAL_CONTRACTS
            ["CONTRACT-05"] = "weak",              // Root: COMMERCIAL_CONTRACTS
            ["CONTRACT-06"] = "copied",            // Root: COMMERCIAL_CONTRACTS
            ["CONTRACT-07"] = "often"              // Root: COMMERCIAL_CONTRACTS
        };

        var result = _engine.ComputeResult(rawAnswers);
        var commercialRisks = result.Risks.Where(r => r.RootCauseGroup == "COMMERCIAL_CONTRACTS").ToList();

        // All 5 commercial contract risks coexist cleanly without auto-suppression
        Assert.Equal(5, commercialRisks.Count);
    }

    [Fact(DisplayName = "43. Deterministic finding order and no duplicate final RiskCodes")]
    public void Deterministic_Order_And_No_Duplicates()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "material_informal",
            ["CONTRACT-03"] = "outside",
            ["CONTRACT-05"] = "general",
            ["CONTRACT-06"] = "templates",
            ["CONTRACT-07"] = "sometimes",
            ["CONTRACT-08"] = "near_total",
            ["CONTRACT-08A"] = "unknown"
        };

        var r1 = _engine.ComputeResult(rawAnswers);
        var r2 = _engine.ComputeResult(rawAnswers);

        var codes1 = r1.Risks.Select(r => r.Code).ToList();
        var codes2 = r2.Risks.Select(r => r.Code).ToList();

        Assert.Equal(codes1, codes2);
        Assert.Equal(codes1.Count, codes1.Distinct().Count());
    }
}
