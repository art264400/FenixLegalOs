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
using FenixLegalOs.Scoring.Modules.Investment;
using FenixLegalOs.Scoring.Validation;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentModuleStage1Tests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;
    private readonly InvestmentFactNormalizer _normalizer;

    public InvestmentModuleStage1Tests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_investment_stage1_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
        _normalizer = new InvestmentFactNormalizer();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDb)) File.Delete(_tempDb);
        }
        catch { }
    }

    // =========================================================================
    // 1. CANONICAL QUESTIONS REGISTRY (§23.8 / §24)
    // =========================================================================

    [Fact(DisplayName = "1. Exactly 17 canonical Investment questions registered with unique IDs")]
    public void Investment_Questions_Count_And_Uniqueness()
    {
        Assert.Equal(17, InvestmentQuestions.All.Count);
        var qIds = InvestmentQuestions.All.Select(q => q.Id).ToList();
        Assert.Equal(qIds.Count, qIds.Distinct().Count());

        var expectedIds = new[]
        {
            "INVEST-01", "INVEST-02", "INVEST-02A", "INVEST-03", "INVEST-04",
            "INVEST-05", "INVEST-06", "INVEST-06A", "INVEST-07", "INVEST-08",
            "INVEST-09", "INVEST-10", "INVEST-11", "INVEST-12", "INVEST-13",
            "INVEST-14", "INVEST-15"
        };
        Assert.Equal(expectedIds.OrderBy(x => x), qIds.OrderBy(x => x));
    }

    [Fact(DisplayName = "2. ScoreMode distribution: 1 Context, 15 Diagnostic, 1 Trigger")]
    public void Investment_Question_Modes()
    {
        var contextQs = InvestmentQuestions.All.Where(q => q.ScoreMode == ScoreMode.Context).Select(q => q.Id).ToList();
        var diagnosticQs = InvestmentQuestions.All.Where(q => q.ScoreMode == ScoreMode.Diagnostic).Select(q => q.Id).ToList();
        var triggerQs = InvestmentQuestions.All.Where(q => q.ScoreMode == ScoreMode.Trigger).Select(q => q.Id).ToList();

        Assert.Single(contextQs);
        Assert.Contains("INVEST-01", contextQs);

        Assert.Equal(15, diagnosticQs.Count);
        Assert.Single(triggerQs);
        Assert.Contains("INVEST-10", triggerQs);
    }

    [Fact(DisplayName = "3. Nullable / N/A score options are correctly registered")]
    public void Nullable_Score_Options()
    {
        // INVEST-01: all null
        var q01 = InvestmentQuestions.All.First(q => q.Id == "INVEST-01");
        Assert.NotNull(q01.Options);
        Assert.All(q01.Options, opt => Assert.Null(opt.Score));

        // INVEST-02: "no" is null
        var q02 = InvestmentQuestions.All.First(q => q.Id == "INVEST-02");
        Assert.NotNull(q02.Options);
        Assert.Null(q02.Options.First(o => o.Id == "no").Score);
        Assert.NotNull(q02.Options.First(o => o.Id == "formal").Score);

        // INVEST-10: all null
        var q10 = InvestmentQuestions.All.First(q => q.Id == "INVEST-10");
        Assert.NotNull(q10.Options);
        Assert.All(q10.Options, opt => Assert.Null(opt.Score));

        // INVEST-14: "not_discussed" is null
        var q14 = InvestmentQuestions.All.First(q => q.Id == "INVEST-14");
        Assert.NotNull(q14.Options);
        Assert.Null(q14.Options.First(o => o.Id == "not_discussed").Score);
        Assert.NotNull(q14.Options.First(o => o.Id == "yes").Score);
    }

    // =========================================================================
    // 2. CANONICAL DIMENSIONS REGISTRY (§23.8)
    // =========================================================================

    [Fact(DisplayName = "4. Exactly 10 canonical Investment dimensions with weights summing to 100%")]
    public void Investment_Dimensions_Integrity()
    {
        Assert.Equal(10, InvestmentDimensions.All.Count);

        var expectedWeights = new Dictionary<string, double>
        {
            ["prior_investments"] = 15,
            ["future_ownership"] = 12,
            ["dilution"] = 8,
            ["round_definition"] = 10,
            ["runway"] = 8,
            ["financial_model"] = 10,
            ["metrics_evidence"] = 10,
            ["dd_documents"] = 15,
            ["deal_terms"] = 7,
            ["deal_review"] = 5
        };

        var actualDimIds = InvestmentDimensions.All.Select(d => d.Id).ToHashSet();
        Assert.Equal(expectedWeights.Keys.ToHashSet(), actualDimIds);

        // Verify question dimension weights match dimension weights
        var diagQs = InvestmentQuestions.All.Where(q => q.ScoreMode == ScoreMode.Diagnostic).ToList();
        var dimGroupWeights = diagQs
            .Where(q => !string.IsNullOrEmpty(q.DimensionId))
            .GroupBy(q => q.DimensionId!)
            .ToDictionary(g => g.Key, g => g.First().DimensionWeight);

        foreach (var (dimId, expectedWeight) in expectedWeights)
        {
            Assert.Equal(expectedWeight, dimGroupWeights[dimId]);
        }

        Assert.Equal(100.0, expectedWeights.Values.Sum());
    }

    // =========================================================================
    // 3. FACT NORMALIZATION & NAMESPACE OWNERSHIP (§24)
    // =========================================================================

    [Fact(DisplayName = "5. InvestmentFactNormalizer writes strictly to investment.* and diagnostic.unknownQuestionIds")]
    public void InvestmentFactNormalizer_Namespace_Ownership()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "terms",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes",
            ["INVEST-03"] = "exact",
            ["INVEST-04"] = "yes",
            ["INVEST-05"] = "clear",
            ["INVEST-06"] = "regular",
            ["INVEST-06A"] = "gt12",
            ["INVEST-07"] = "current",
            ["INVEST-08"] = "yes",
            ["INVEST-09"] = "organized",
            ["INVEST-10"] = "none",
            ["INVEST-11"] = "current",
            ["INVEST-12"] = "yes",
            ["INVEST-13"] = "reserved_only",
            ["INVEST-14"] = "yes",
            ["INVEST-15"] = "specialist"
        };

        var store = new SharedFactStore();
        _normalizer.Normalize(rawAnswers, store);

        var allowedPrefixes = new[] { "investment.", "diagnostic." };
        foreach (var key in store.Facts.Keys)
        {
            Assert.True(allowedPrefixes.Any(p => key.StartsWith(p)), $"Unexpected key: {key}");
        }
    }

    [Fact(DisplayName = "6. Unknown question options correctly populate diagnostic.unknownQuestionIds")]
    public void Unknown_Questions_Tracking()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-02"] = "unknown",
            ["INVEST-02A"] = "unknown",
            ["INVEST-03"] = "unknown",
            ["INVEST-04"] = "unknown",
            ["INVEST-05"] = "unknown",
            ["INVEST-06"] = "unknown",
            ["INVEST-06A"] = "unknown",
            ["INVEST-07"] = "unknown",
            ["INVEST-08"] = "unknown",
            ["INVEST-09"] = "unknown",
            ["INVEST-10"] = "unknown",
            ["INVEST-13"] = "unknown",
            ["INVEST-14"] = "unknown",
            ["INVEST-15"] = "unknown"
        };

        var store = new SharedFactStore();
        _normalizer.Normalize(rawAnswers, store);

        Assert.True(store.Facts.TryGetValue("diagnostic.unknownQuestionIds", out var obj));
        var unknowns = Assert.IsType<List<string>>(obj);
        Assert.Equal(14, unknowns.Count);
        Assert.Contains("INVEST-02", unknowns);
        Assert.Contains("INVEST-15", unknowns);
    }

    // =========================================================================
    // 4. MODULE APPLICABILITY & ROUTING (§23.8 / §22)
    // =========================================================================

    [Fact(DisplayName = "7. INVEST-01 = none AND INVEST-02 = no -> Module NotApplicable (excluded from denominator)")]
    public void Investment_NotApplicable_When_No_Timing_And_No_Prior()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var invSec = result.Sections.First(s => s.SectionId == "investment");

        Assert.Equal(ApplicabilityStatus.NotApplicable, invSec.Status);
        Assert.Null(invSec.Score);
    }

    [Fact(DisplayName = "8. INVEST-01 = none AND INVEST-02 = formal -> Module Applicable with prior investment branch only")]
    public void Investment_Applicable_With_Prior_Investment_Only()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes",
            ["INVEST-03"] = "exact"
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        var visibleIds = visible.Select(q => q.Id).ToList();

        Assert.Contains("INVEST-01", visibleIds);
        Assert.Contains("INVEST-02", visibleIds);
        Assert.Contains("INVEST-02A", visibleIds);
        Assert.Contains("INVEST-03", visibleIds);

        // Future round questions remain hidden
        Assert.DoesNotContain("INVEST-04", visibleIds);
        Assert.DoesNotContain("INVEST-05", visibleIds);
        Assert.DoesNotContain("INVEST-06", visibleIds);
        Assert.DoesNotContain("INVEST-07", visibleIds);
        Assert.DoesNotContain("INVEST-08", visibleIds);
        Assert.DoesNotContain("INVEST-09", visibleIds);
        Assert.DoesNotContain("INVEST-10", visibleIds);
        Assert.DoesNotContain("INVEST-11", visibleIds);
        Assert.DoesNotContain("INVEST-12", visibleIds);

        var result = _engine.ComputeResult(rawAnswers);
        var invSec = result.Sections.First(s => s.SectionId == "investment");
        Assert.Equal(ApplicabilityStatus.Applicable, invSec.Status);
        Assert.Equal(100, invSec.Score);
    }

    [Fact(DisplayName = "9. INVEST-01 = searching -> INVEST-04..11 visible, INVEST-12..15 hidden")]
    public void Investment_Searching_Visibility()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no"
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        var visibleIds = visible.Select(q => q.Id).ToList();

        Assert.Contains("INVEST-04", visibleIds);
        Assert.Contains("INVEST-05", visibleIds);
        Assert.Contains("INVEST-06", visibleIds);
        Assert.Contains("INVEST-07", visibleIds);
        Assert.Contains("INVEST-08", visibleIds);
        Assert.Contains("INVEST-09", visibleIds);
        Assert.Contains("INVEST-10", visibleIds);
        Assert.Contains("INVEST-11", visibleIds);

        // Deal terms questions hidden
        Assert.DoesNotContain("INVEST-12", visibleIds);
        Assert.DoesNotContain("INVEST-13", visibleIds);
        Assert.DoesNotContain("INVEST-14", visibleIds);
        Assert.DoesNotContain("INVEST-15", visibleIds);
    }

    [Fact(DisplayName = "10. INVEST-01 = specific or terms -> INVEST-12..15 visible")]
    public void Investment_Specific_Or_Terms_Visibility()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "terms",
            ["INVEST-02"] = "no"
        };

        var (visible, effective, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, rawAnswers);
        var visibleIds = visible.Select(q => q.Id).ToList();

        Assert.Contains("INVEST-12", visibleIds);
        Assert.Contains("INVEST-13", visibleIds);
        Assert.Contains("INVEST-14", visibleIds);
        Assert.Contains("INVEST-15", visibleIds);
        Assert.True(facts.Facts.TryGetValue("investment.termSheetOrTerms", out var val) && val is true);
    }

    [Fact(DisplayName = "11. INVEST-06 = no hides INVEST-06A; INVEST-06 = regular enables INVEST-06A")]
    public void Runway_Question_Visibility()
    {
        // 1. INVEST-06 = no -> INVEST-06A hidden
        var raw1 = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-06"] = "no"
        };
        var (vis1, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw1);
        Assert.DoesNotContain(vis1, q => q.Id == "INVEST-06A");

        // 2. INVEST-06 = regular -> INVEST-06A visible
        var raw2 = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-06"] = "regular"
        };
        var (vis2, _, _) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw2);
        Assert.Contains(vis2, q => q.Id == "INVEST-06A");
    }

    [Fact(DisplayName = "12. Stale deal terms answers are purged when timing changes from specific to none")]
    public void Stale_Deal_Terms_Answers_Purged()
    {
        // 1. Initial State: timing = specific, answers for 12..15 provided
        var raw1 = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = "yes",
            ["INVEST-13"] = "reserved_only",
            ["INVEST-14"] = "yes",
            ["INVEST-15"] = "specialist"
        };
        var (vis1, eff1, facts1) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw1);
        Assert.Contains("INVEST-12", eff1.Keys);
        Assert.True(facts1.Facts.ContainsKey("investment.dealTermsUnderstanding"));

        // 2. Switched State: timing = none
        var raw2 = new Dictionary<string, object>(raw1)
        {
            ["INVEST-01"] = "none"
        };
        var (vis2, eff2, facts2) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw2);
        Assert.DoesNotContain("INVEST-12", eff2.Keys);
        Assert.False(facts2.Facts.ContainsKey("investment.dealTermsUnderstanding"));
    }

    // =========================================================================
    // 5. SCORING INVARIANTS (§23.8)
    // =========================================================================

    [Fact(DisplayName = "13. Runway dimension score: INVEST-06 (60%) + INVEST-06A (40%) = exact weighted sum")]
    public void Runway_Dimension_Scoring()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-06"] = "regular", // Score = 1.0 * 60% = 60
            ["INVEST-06A"] = "lt3"     // Score = 0.20 * 40% = 8
        };

        var result = _engine.ComputeResult(raw);
        var invSec = result.Sections.First(s => s.SectionId == "investment");
        var runwayDim = invSec.Dimensions.First(d => d.DimensionId == "runway");

        // 60 + 8 = 68%
        Assert.Equal(68, runwayDim.Score);
    }

    [Fact(DisplayName = "14. DD Documents dimension score: INVEST-09 (85%) + INVEST-11 (15%) = exact weighted sum")]
    public void Dd_Documents_Dimension_Scoring()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-09"] = "organized", // Score = 1.0 * 85% = 85
            ["INVEST-11"] = "none"        // Score = 0.20 * 15% = 3
        };

        var result = _engine.ComputeResult(raw);
        var invSec = result.Sections.First(s => s.SectionId == "investment");
        var ddDim = invSec.Dimensions.First(d => d.DimensionId == "dd_documents");

        // 85 + 3 = 88%
        Assert.Equal(88, ddDim.Score);
    }

    [Fact(DisplayName = "15. Deal Terms dimension renormalizes across INVEST-12 and INVEST-13 when INVEST-14 is not_discussed (N/A)")]
    public void Deal_Terms_Renormalization_When_14_Not_Discussed()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "terms",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = "yes",           // Score = 1.0 * 40 = 40
            ["INVEST-13"] = "extra_known",   // Score = 0.75 * 30 = 22.5
            ["INVEST-14"] = "not_discussed", // Score = null (N/A, 30% excluded)
            ["INVEST-15"] = "specialist"
        };

        var result = _engine.ComputeResult(raw);
        var invSec = result.Sections.First(s => s.SectionId == "investment");
        var termsDim = invSec.Dimensions.First(d => d.DimensionId == "deal_terms");

        // (40 + 22.5) / (40 + 30) = 62.5 / 70 = 89.28% -> 89%
        Assert.Equal(89, termsDim.Score);
    }

    [Fact(DisplayName = "16. INVEST-02 = no makes prior_investments dimension N/A (excluded from denominator)")]
    public void Prior_Investments_Dimension_Excluded_When_No_Prior()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-04"] = "yes", // 1.0 @ 8%
            ["INVEST-05"] = "clear" // 1.0 @ 10%
        };

        var result = _engine.ComputeResult(raw);
        var invSec = result.Sections.First(s => s.SectionId == "investment");

        Assert.DoesNotContain(invSec.Dimensions, d => d.DimensionId == "prior_investments");
    }

    // =========================================================================
    // 6. ROUTING DAG VALIDATION & OVERALL SCORE
    // =========================================================================

    [Fact(DisplayName = "17. RoutingDependencyValidator validates all 150 questions including Investment DAG with 0 errors")]
    public void RoutingDag_Validation()
    {
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }

    [Fact(DisplayName = "18. Investment participates with 12% weight in Overall Score when applicable, renormalizes when N/A")]
    public void Overall_Score_12Percent_Weight()
    {
        // 1. Applicable
        var rawApp = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes"
        };
        var resApp = _engine.ComputeResult(rawApp);
        var secApp = resApp.Sections.First(s => s.SectionId == "investment");
        Assert.Equal(12, secApp.Weight);
        Assert.Equal(ApplicabilityStatus.Applicable, secApp.Status);

        // 2. NotApplicable
        var rawNA = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "no"
        };
        var resNA = _engine.ComputeResult(rawNA);
        var secNA = resNA.Sections.First(s => s.SectionId == "investment");
        Assert.Equal(ApplicabilityStatus.NotApplicable, secNA.Status);
        Assert.Null(secNA.Score);
    }
}
