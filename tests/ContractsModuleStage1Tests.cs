using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
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

public class ContractsModuleStage1Tests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;

    public ContractsModuleStage1Tests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_contracts_stage1_{Guid.NewGuid():N}.db");
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
    // A. REGISTRY
    // =========================================================================

    [Fact(DisplayName = "A.1 Contracts question count is exactly 9")]
    public void QuestionCount_Is_9()
    {
        var contractQs = ContractQuestions.All;
        Assert.Equal(9, contractQs.Count);

        var bankQs = DataBank.Questions.Where(q => q.SectionId == "contracts").ToList();
        Assert.Equal(9, bankQs.Count);
    }

    [Fact(DisplayName = "A.2 Contracts dimensions count is exactly 6 and weights total 100%")]
    public void DimensionCount_Is_6_And_Weights_Total_100()
    {
        var contractDims = ContractDimensions.All;
        Assert.Equal(6, contractDims.Count);

        var expectedWeights = new Dictionary<string, double>
        {
            ["written_form"] = 20,
            ["scope"] = 20,
            ["payment_termination"] = 15,
            ["risk_allocation"] = 20,
            ["model_match"] = 15,
            ["dependency_large_deals"] = 10
        };

        var contractQs = ContractQuestions.All.Where(q => q.ScoreMode == ScoreMode.Diagnostic).ToList();
        var groupedWeights = contractQs.GroupBy(q => q.DimensionId).ToDictionary(g => g.Key!, g => g.First().DimensionWeight);

        Assert.Equal(expectedWeights.Count, groupedWeights.Count);
        double totalWeight = 0;
        foreach (var (dim, w) in expectedWeights)
        {
            Assert.True(groupedWeights.ContainsKey(dim), $"Missing dimension: {dim}");
            Assert.Equal(w, groupedWeights[dim]);
            totalWeight += w;
        }
        Assert.Equal(100.0, totalWeight);
    }

    [Fact(DisplayName = "A.3 Contracts section has canonical 8% weight and Stage 1 risks count = 0")]
    public void SectionWeight_Is_8_And_Stage1_Risks_Count_Is_0()
    {
        var section = DataBank.Sections.FirstOrDefault(s => s.Id == "contracts");
        Assert.NotNull(section);
        Assert.Equal(8, section.Weight);

        var contractRisks = DataBank.Risks.Where(r => r.SectionId == "contracts").ToList();
        Assert.Empty(contractRisks);
    }

    // =========================================================================
    // B. APPLICABILITY
    // =========================================================================

    [Fact(DisplayName = "B.1 CONTRACT-01 = none sets b2bRelevant = false and module is NotApplicable")]
    public void Contract01_None_Sets_B2BRelevant_False()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" }
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        Assert.True(facts.Facts.TryGetValue("contracts.b2bRelevant", out var val) && val is false);

        var result = _engine.ComputeResult(rawAnswers);
        var contractSec = result.Sections.FirstOrDefault(s => s.SectionId == "contracts");
        Assert.NotNull(contractSec);
        Assert.Equal(ApplicabilityStatus.NotApplicable, contractSec.Status);
        Assert.Null(contractSec.Score);
    }

    [Theory(DisplayName = "B.2 Canonical B2B counterparty options set b2bRelevant = true and module is Applicable")]
    [InlineData("clients")]
    [InlineData("partners")]
    [InlineData("suppliers")]
    [InlineData("some")]
    public void Contract01_B2B_Options_Set_Applicable(string option)
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { option }
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        Assert.True(facts.Facts.TryGetValue("contracts.b2bRelevant", out var val) && val is true);

        var result = _engine.ComputeResult(rawAnswers);
        var contractSec = result.Sections.FirstOrDefault(s => s.SectionId == "contracts");
        Assert.NotNull(contractSec);
        Assert.Equal(ApplicabilityStatus.Applicable, contractSec.Status);
    }

    [Fact(DisplayName = "B.3 Unanswered CONTRACT-01 leaves contracts.b2bRelevant fact absent (not false)")]
    public void Unanswered_Contract01_Leaves_Fact_Absent()
    {
        var rawAnswers = new Dictionary<string, object>();
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);

        Assert.False(facts.Facts.ContainsKey("contracts.b2bRelevant"));
    }

    // =========================================================================
    // C. MULTIPLE-SELECT VALIDATION
    // =========================================================================

    [Fact(DisplayName = "C.1 CONTRACT-01 'none' is mutually exclusive")]
    public void Contract01_None_Is_Mutually_Exclusive()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none", "clients" }
        };

        var allQs = DataBank.Questions;
        var validation = AnswerValidator.Validate(rawAnswers, allQs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.QuestionId == "CONTRACT-01" && e.ErrorCode == ValidationErrorCode.MutuallyExclusiveConflict);
    }

    [Fact(DisplayName = "C.2 Unknown answer IDs for CONTRACT-01 are rejected by AnswerValidator")]
    public void Contract01_Unknown_Option_Rejected()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "invalid_option_xyz" }
        };

        var allQs = DataBank.Questions;
        var validation = AnswerValidator.Validate(rawAnswers, allQs);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.QuestionId == "CONTRACT-01" && e.ErrorCode == ValidationErrorCode.InvalidOption);
    }

    // =========================================================================
    // D. ROUTING
    // =========================================================================

    [Fact(DisplayName = "D.1 CONTRACT-02..06 and CONTRACT-08 are visible iff b2bRelevant = true")]
    public void Contract02_to_06_And_08_Visibility()
    {
        // When b2bRelevant = false (none)
        var rawAnswersNone = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" }
        };
        var (visibleNone, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswersNone);
        Assert.DoesNotContain(visibleNone, q => q.Id is "CONTRACT-02" or "CONTRACT-03" or "CONTRACT-04" or "CONTRACT-05" or "CONTRACT-06" or "CONTRACT-08");

        // When b2bRelevant = true (clients)
        var rawAnswersClients = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" }
        };
        var (visibleClients, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswersClients);
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-02");
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-03");
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-04");
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-05");
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-06");
        Assert.Contains(visibleClients, q => q.Id == "CONTRACT-08");
    }

    [Fact(DisplayName = "D.2 CONTRACT-07 visible via CONTRACT-01 contains clients")]
    public void Contract07_Visible_Via_Clients()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" }
        };
        var (visible, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.Contains(visible, q => q.Id == "CONTRACT-07");
    }

    [Fact(DisplayName = "D.3 CONTRACT-07 visible via Product userTypes contains companies")]
    public void Contract07_Visible_Via_Product_Companies()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "companies" },
            ["CONTRACT-01"] = new List<string> { "partners" } // Not clients, but Product has companies
        };
        var (visible, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.Contains(visible, q => q.Id == "CONTRACT-07");
    }

    [Fact(DisplayName = "D.4 CONTRACT-07 hidden when neither clients nor companies are present")]
    public void Contract07_Hidden_Without_Clients_Or_Companies()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "b2c" },
            ["CONTRACT-01"] = new List<string> { "partners", "suppliers" }
        };
        var (visible, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-07");
    }

    [Theory(DisplayName = "D.5 CONTRACT-08A visible for noticeable, material, near_total, unknown; hidden for none")]
    [InlineData("noticeable", true)]
    [InlineData("material", true)]
    [InlineData("near_total", true)]
    [InlineData("unknown", true)]
    [InlineData("no", false)]
    public void Contract08A_Dependency_Routing(string answer08, bool expectedVisible)
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-08"] = answer08
        };

        var (visible, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        bool actualVisible = visible.Any(q => q.Id == "CONTRACT-08A");
        Assert.Equal(expectedVisible, actualVisible);
    }

    // =========================================================================
    // E. FACTS MAPPINGS
    // =========================================================================

    [Fact(DisplayName = "E.1 All CONTRACT-02..08A answer options produce exact canonical facts")]
    public void Facts_Mappings_Exactness()
    {
        // 1. CONTRACT-02
        AssertFact("CONTRACT-02", "always", "contracts.writtenCoverage", "always");
        AssertFact("CONTRACT-02", "some_in_messages", "contracts.writtenCoverage", "some_in_messages");
        AssertFact("CONTRACT-02", "material_informal", "contracts.writtenCoverage", "material_informal");
        AssertFact("CONTRACT-02", "mostly_informal", "contracts.writtenCoverage", "mostly_informal");
        AssertFact("CONTRACT-02", "unknown", "contracts.writtenCoverage", "unknown");

        // 2. CONTRACT-03
        AssertFact("CONTRACT-03", "clear", "contracts.scopeClarity", "clear");
        AssertFact("CONTRACT-03", "mostly", "contracts.scopeClarity", "mostly");
        AssertFact("CONTRACT-03", "outside", "contracts.scopeClarity", "outside");
        AssertFact("CONTRACT-03", "generic", "contracts.scopeClarity", "generic");
        AssertFact("CONTRACT-03", "unknown", "contracts.scopeClarity", "unknown");

        // 3. CONTRACT-04
        AssertFact("CONTRACT-04", "clear", "contracts.paymentTermination", "clear");
        AssertFact("CONTRACT-04", "mostly", "contracts.paymentTermination", "mostly");
        AssertFact("CONTRACT-04", "some_unclear", "contracts.paymentTermination", "some_unclear");
        AssertFact("CONTRACT-04", "case", "contracts.paymentTermination", "case");
        AssertFact("CONTRACT-04", "unknown", "contracts.paymentTermination", "unknown");

        // 4. CONTRACT-05
        AssertFact("CONTRACT-05", "clear", "contracts.riskAllocation", "clear");
        AssertFact("CONTRACT-05", "mostly", "contracts.riskAllocation", "mostly");
        AssertFact("CONTRACT-05", "general", "contracts.riskAllocation", "general");
        AssertFact("CONTRACT-05", "weak", "contracts.riskAllocation", "weak");
        AssertFact("CONTRACT-05", "unknown", "contracts.riskAllocation", "unknown");

        // 5. CONTRACT-06
        AssertFact("CONTRACT-06", "custom", "contracts.modelMatch", "custom");
        AssertFact("CONTRACT-06", "adapted", "contracts.modelMatch", "adapted");
        AssertFact("CONTRACT-06", "templates", "contracts.modelMatch", "templates");
        AssertFact("CONTRACT-06", "copied", "contracts.modelMatch", "copied");
        AssertFact("CONTRACT-06", "unknown", "contracts.modelMatch", "unknown");

        // 6. CONTRACT-07
        AssertFact("CONTRACT-07", "reviewed", "contracts.largeDealReview", "reviewed");
        AssertFact("CONTRACT-07", "sometimes", "contracts.largeDealReview", "sometimes");
        AssertFact("CONTRACT-07", "often", "contracts.largeDealReview", "often_unreviewed");
        AssertFact("CONTRACT-07", "no_large", "contracts.largeDealReview", "not_applicable");
        AssertFact("CONTRACT-07", "unknown", "contracts.largeDealReview", "unknown");

        // 7. CONTRACT-08
        AssertFact("CONTRACT-08", "no", "contracts.counterpartyDependency", "none");
        AssertFact("CONTRACT-08", "noticeable", "contracts.counterpartyDependency", "noticeable");
        AssertFact("CONTRACT-08", "material", "contracts.counterpartyDependency", "material");
        AssertFact("CONTRACT-08", "near_total", "contracts.counterpartyDependency", "near_total");
        AssertFact("CONTRACT-08", "unknown", "contracts.counterpartyDependency", "unknown");

        // 8. CONTRACT-08A
        AssertFact("CONTRACT-08A", "protected", "contracts.counterpartyExitRisk", "protected");
        AssertFact("CONTRACT-08A", "backup", "contracts.counterpartyExitRisk", "backup");
        AssertFact("CONTRACT-08A", "serious", "contracts.counterpartyExitRisk", "serious");
        AssertFact("CONTRACT-08A", "unknown", "contracts.counterpartyExitRisk", "unknown");
    }

    private void AssertFact(string questionId, string answerVal, string factKey, string expectedFactVal)
    {
        var facts = FactNormalizer.NormalizeFacts(new Dictionary<string, object> { [questionId] = answerVal });
        Assert.True(facts.Facts.TryGetValue(factKey, out var actualVal), $"Fact '{factKey}' not found for {questionId}='{answerVal}'.");
        Assert.Equal(expectedFactVal, actualVal?.ToString());
    }

    // =========================================================================
    // F. N/A & DENOMINATOR EXCLUSIONS
    // =========================================================================

    [Fact(DisplayName = "F.1 CONTRACT-07 = no_large score is null/N/A and excluded from denominator")]
    public void Contract07_NoLarge_Is_Excluded_From_Denominator()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-07"] = "no_large", // N/A
            ["CONTRACT-08"] = "no"        // score = 1.0, hides CONTRACT-08A
        };

        var result = _engine.ComputeResult(rawAnswers);
        var contractSec = result.Sections.First(s => s.SectionId == "contracts");
        var depDim = contractSec.Dimensions.First(d => d.DimensionId == "dependency_large_deals");

        // CONTRACT-07 is N/A (excluded), CONTRACT-08A is hidden (excluded).
        // Only CONTRACT-08 participates (score 1.0 => 100%).
        Assert.Equal(100, depDim.Score);
    }

    // =========================================================================
    // G. UNKNOWN SEMANTICS
    // =========================================================================

    [Fact(DisplayName = "G.1 Explicit unknown writes 'unknown' and records question in diagnostic.unknownQuestionIds")]
    public void Explicit_Unknown_Semantics()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-02"] = "unknown",
            ["CONTRACT-05"] = "unknown"
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        Assert.Equal("unknown", facts.Facts["contracts.writtenCoverage"]);
        Assert.Equal("unknown", facts.Facts["contracts.riskAllocation"]);

        Assert.True(facts.Facts.TryGetValue("diagnostic.unknownQuestionIds", out var obj));
        var unkList = Assert.IsType<List<string>>(obj);
        Assert.Contains("CONTRACT-02", unkList);
        Assert.Contains("CONTRACT-05", unkList);
    }

    // =========================================================================
    // H. EFFECTIVE ANSWERS & STALE ISOLATION
    // =========================================================================

    [Fact(DisplayName = "H.1 Switching CONTRACT-01 to 'none' removes all Contracts diagnostic answers from EffectiveAnswers")]
    public void Stale_Contracts_Diagnostics_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "none" },
            ["CONTRACT-02"] = "mostly_informal", // Stale
            ["CONTRACT-03"] = "generic",         // Stale
            ["CONTRACT-04"] = "case",            // Stale
            ["CONTRACT-05"] = "weak",            // Stale
            ["CONTRACT-06"] = "copied",          // Stale
            ["CONTRACT-07"] = "often",           // Stale
            ["CONTRACT-08"] = "near_total",      // Stale
            ["CONTRACT-08A"] = "serious"         // Stale
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);

        Assert.DoesNotContain("CONTRACT-02", effective.Keys);
        Assert.DoesNotContain("CONTRACT-08", effective.Keys);
        Assert.DoesNotContain("CONTRACT-08A", effective.Keys);

        Assert.False(facts.Facts.ContainsKey("contracts.writtenCoverage"));
        Assert.False(facts.Facts.ContainsKey("contracts.counterpartyDependency"));
        Assert.False(facts.Facts.ContainsKey("contracts.counterpartyExitRisk"));

        var result = _engine.ComputeResult(rawAnswers);
        var contractSec = result.Sections.First(s => s.SectionId == "contracts");
        Assert.Equal(ApplicabilityStatus.NotApplicable, contractSec.Status);
        Assert.Null(contractSec.Score);
    }

    [Fact(DisplayName = "H.2 Stale CONTRACT-08A answer removed when CONTRACT-08 becomes 'no' (dependency = none)")]
    public void Stale_Contract08A_Answer_Removed()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-08"] = "no",       // dependency = none
            ["CONTRACT-08A"] = "serious" // Stale answer to hidden question
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);

        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-08A");
        Assert.DoesNotContain("CONTRACT-08A", effective.Keys);
        Assert.False(facts.Facts.ContainsKey("contracts.counterpartyExitRisk"));
    }

    [Fact(DisplayName = "H.3 Stale CONTRACT-07 answer removed when CONTRACT-01 changes from clients to partners")]
    public void Stale_Contract07_Answer_Removed()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" }, // No companies
            ["CONTRACT-01"] = new List<string> { "partners" }, // Clients removed
            ["CONTRACT-07"] = "reviewed" // Stale answer to hidden question
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);

        Assert.DoesNotContain(visible, q => q.Id == "CONTRACT-07");
        Assert.DoesNotContain("CONTRACT-07", effective.Keys);
        Assert.False(facts.Facts.ContainsKey("contracts.largeDealReview"));
    }

    // =========================================================================
    // I. GENERIC PIPELINE INTEGRATION
    // =========================================================================

    [Fact(DisplayName = "I.1 RoutingDependencyValidator validates complete question bank including Contracts with 0 errors")]
    public void RoutingDag_Validation_Clean()
    {
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }

    [Fact(DisplayName = "I.2 ComputeResult is deterministic across multiple evaluations")]
    public void ComputeResult_Is_Deterministic()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "no",
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "always",
            ["CONTRACT-03"] = "clear",
            ["CONTRACT-04"] = "clear",
            ["CONTRACT-05"] = "clear",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "reviewed",
            ["CONTRACT-08"] = "noticeable",
            ["CONTRACT-08A"] = "protected"
        };

        var r1 = _engine.ComputeResult(rawAnswers);
        var r2 = _engine.ComputeResult(rawAnswers);
        r1.ComputedAt = "STATIC";
        r2.ComputedAt = "STATIC";

        Assert.Equal(JsonSerializer.Serialize(r1), JsonSerializer.Serialize(r2));
    }
}
