using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Product;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ProductCrossModuleAndPipelineHardeningTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;

    public ProductCrossModuleAndPipelineHardeningTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_prod_stage3_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
    }

    // ─── 1. Namespace Ownership ───────────────────────────────────────────
    [Fact(DisplayName = "1. ProductFactNormalizer strictly writes product.* and shared diagnostic keys")]
    public void ProductFactNormalizer_Namespace_Ownership()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "consumers", "companies" },
            ["PROD-04"] = "unknown",
            ["PROD-10"] = "subscription",
            ["PROD-18"] = "yes",
            ["PROD-20"] = "yes",
            ["PROD-21"] = "global",
            ["PROD-22"] = new List<string> { "crypto" }
        };

        var facts = new SharedFactStore();
        var normalizer = new ProductFactNormalizer();
        normalizer.Normalize(answers, facts);

        var forbiddenPrefixes = new[] { "founders.", "corporate.", "ip.", "team.", "data.", "ai.", "contracts.", "investment." };

        foreach (var key in facts.Facts.Keys)
        {
            Assert.True(key.StartsWith("product.") || key == "diagnostic.unknownQuestionIds",
                $"ProductFactNormalizer wrote unexpected key: {key}");

            foreach (var forbidden in forbiddenPrefixes)
            {
                Assert.False(key.StartsWith(forbidden), $"ProductFactNormalizer violated namespace boundary: {key}");
            }
        }
    }

    // ─── 2. Scenario A: paid -> free ──────────────────────────────────────
    [Fact(DisplayName = "2. Scenario A: paid -> free completely isolates stale PROD-11/12 answers")]
    public void ScenarioA_Paid_To_Free_Stale_Answers_Have_Zero_Effect()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "free",
            ["PROD-11"] = "late_fees", // Stale: paid=false
            ["PROD-12"] = "unclear"    // Stale: paid=false
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-11", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-12", nav.VisibleQuestionIds);
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_PAYMENT_TRANSPARENCY");
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_REFUND_RULES");

        var (_, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(_repo.GetQuestions(), answers);
        Assert.False(effectiveAnswers.ContainsKey("PROD-11"));
        Assert.False(effectiveAnswers.ContainsKey("PROD-12"));
        Assert.False(factStore.Facts.ContainsKey("product.priceTransparency"));
        Assert.False(factStore.Facts.ContainsKey("product.refundRules"));
    }

    // ─── 3. Scenario B: subscription -> free ──────────────────────────────
    [Fact(DisplayName = "3. Scenario B: subscription -> free isolates stale PROD-13/13A/14/15 answers")]
    public void ScenarioB_Subscription_To_Free_Stale_Answers_Have_Zero_Effect()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "free",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "no",
            ["PROD-14"] = "undefined",
            ["PROD-15"] = "terms_only"
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-13", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-13A", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-14", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-15", nav.VisibleQuestionIds);
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_SUBSCRIPTION_RULES");
    }

    // ─── 4. Scenario C: UGC true -> false ─────────────────────────────────
    [Fact(DisplayName = "4. Scenario C: UGC true -> false isolates stale PROD-18A/18B/19 answers")]
    public void ScenarioC_Ugc_True_To_False_Stale_Answers_Have_Zero_Effect()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-18"] = "no",
            ["PROD-18A"] = "no",
            ["PROD-18B"] = "no",
            ["PROD-19"] = "no"
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-18A", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-18B", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-19", nav.VisibleQuestionIds);
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_USER_CONTENT_RULES");
    }

    // ─── 5. Scenario D: explicit -> link_only ─────────────────────────────
    [Fact(DisplayName = "5. Scenario D: explicit -> link_only isolates stale PROD-09 answer")]
    public void ScenarioD_Explicit_To_LinkOnly_Stale_PROD09_Isolated()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-08"] = "link_only",
            ["PROD-09"] = "none" // Stale
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-09", nav.VisibleQuestionIds);
        var (_, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(_repo.GetQuestions(), answers);
        Assert.False(effectiveAnswers.ContainsKey("PROD-09"));
        Assert.False(factStore.Facts.ContainsKey("product.acceptanceEvidence"));

        // PROD_ACCEPTANCE_WEAK emitted from PROD-08 (link_only), basis is PROD-08
        var finding = Assert.Single(result.Risks, r => r.Code == "PROD_ACCEPTANCE_WEAK");
        Assert.Equal("PROD-08", finding.Basis.First().QuestionId);
    }

    // ─── 6. Scenario E: global -> one country ─────────────────────────────
    [Fact(DisplayName = "6. Scenario E: global -> one country isolates stale PROD-21A answer")]
    public void ScenarioE_Global_To_OneCountry_Stale_PROD21A_Isolated()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-21"] = "one",
            ["PROD-21A"] = "no" // Stale
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-21A", nav.VisibleQuestionIds);
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_MULTI_COUNTRY_REVIEW");
    }

    // ─── 7. Self-Resurrecting Routing Test (Architecture A Trust Boundary) ─
    [Fact(DisplayName = "7. Stale child answer cannot resurrect itself or downstream questions (PROD-10 -> PROD-13 -> PROD-13A)")]
    public void Self_Resurrecting_Routing_Test_PROD13A()
    {
        // User previously answered subscription + autoRenew=yes + autoRenewDisclosure=no
        // Then changed PROD-10 to free
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "free",
            ["PROD-13"] = "yes", // Stale: subscription is false
            ["PROD-13A"] = "no"  // Stale: depends on autoRenew == true
        };

        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-13", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-13A", nav.VisibleQuestionIds);
    }

    // ─── 8. Multi-Level Self-Resurrecting Test (PROD-04 -> PROD-08 -> PROD-09) ─
    [Fact(DisplayName = "8. Multi-level child visibility is cleanly suppressed when grandparent changes")]
    public void Multi_Level_Grandparent_Change_Suppresses_Descendants()
    {
        // PROD-04 changed to none -> PROD-08 hidden -> PROD-09 must be hidden
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-08"] = "explicit", // Stale: PROD-08 requires userRulesStatus not in [none, preparing]
            ["PROD-09"] = "versioned" // Stale: PROD-09 requires termsAcceptance == explicit
        };

        var nav = _engine.GetNavigationState(answers);

        Assert.DoesNotContain("PROD-08", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-09", nav.VisibleQuestionIds);
    }

    // ─── 9. Finding Pipeline Invariants ───────────────────────────────────
    [Fact(DisplayName = "9. Product risk library maintains exact 13 definitions and pipeline invariants")]
    public void Product_Risk_Library_Invariants()
    {
        Assert.Equal(13, ProductRisks.All.Count);
        var prodDims = ProductDimensions.All.Select(d => d.Id).ToHashSet();

        foreach (var risk in ProductRisks.All)
        {
            Assert.StartsWith("PROD_", risk.Code);
            Assert.NotEmpty(risk.Title);
            Assert.NotEmpty(risk.Finding);
            Assert.NotEmpty(risk.WhyItMatters);
            Assert.NotEmpty(risk.Recommendation);
            Assert.NotEmpty(risk.Recommendations);
            Assert.NotEmpty(risk.RootCauseGroup);
            Assert.Equal("PRODUCT_LEGAL_REVIEW", risk.ServiceCode);
            Assert.NotEmpty(risk.AffectedDimensions);
            Assert.All(risk.AffectedDimensions, dim => Assert.Contains(dim, prodDims));

            // No self-suppression
            if (risk.SuppressCodes != null)
            {
                Assert.DoesNotContain(risk.Code, risk.SuppressCodes);
                Assert.Equal(risk.SuppressCodes.Distinct().Count(), risk.SuppressCodes.Count);
            }
        }
    }

    // ─── 10. Strong Areas Product Tests ───────────────────────────────────
    [Fact(DisplayName = "10. Strong Areas correctly identifies high-scoring dimensions without blocking risks")]
    public void StrongAreas_Product_Integration()
    {
        // Perfect Product answers
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-06"] = "clear",
            ["PROD-07"] = "company",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "one_off",
            ["PROD-11"] = "clear",
            ["PROD-12"] = "published",
            ["PROD-16"] = "clear",
            ["PROD-17"] = "clear",
            ["PROD-18"] = "yes",
            ["PROD-18A"] = "yes",
            ["PROD-18B"] = "yes",
            ["PROD-19"] = "yes",
            ["PROD-21"] = "one",
            ["PROD-22"] = new List<string> { "none" }
        };

        var result = _engine.ComputeResult(answers);
        Assert.Contains("Наличие правил для пользователей", result.Strengths);
        Assert.Contains("Соответствие правил реальной работе продукта", result.Strengths);
        Assert.Contains("Ясность предложения и условий до оплаты", result.Strengths);
    }

    [Fact(DisplayName = "10B. Dimension with HIGH risk cannot become a Strong Area")]
    public void StrongAreas_Blocked_By_High_Risk()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-05"] = "changed" // Emits PROD_RULES_MISMATCH (HIGH)
        };

        var result = _engine.ComputeResult(answers);
        Assert.Contains(result.Risks, r => r.Code == "PROD_RULES_MISMATCH");
        Assert.DoesNotContain("Соответствие правил реальной работе продукта", result.Strengths);
    }

    // ─── 11. Prelaunch End-to-End Hardening ────────────────────────────────
    [Fact(DisplayName = "11. Prelaunch end-to-end scenario is APPLICABLE and emits 0 live-user findings")]
    public void Prelaunch_EndToEnd_Hardening()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "prelaunch",
            ["PROD-04"] = "none",
            ["PROD-06"] = "clear",
            ["PROD-10"] = "free",
            ["PROD-18"] = "no",
            ["PROD-21"] = "one",
            ["PROD-22"] = new List<string> { "none" }
        };

        var result = _engine.ComputeResult(answers);
        var prodSection = Assert.Single(result.Sections, s => s.SectionId == "product");

        Assert.Equal(ApplicabilityStatus.Applicable, prodSection.Status);
        Assert.DoesNotContain(result.Risks, r => r.SectionId == "product");
    }

    // ─── 12. Regulated Functions Hardening ─────────────────────────────────
    [Fact(DisplayName = "12. Regulated functions trigger legal review HIGH without becoming Critical or Blocker")]
    public void Regulated_Functions_Hardening()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-22"] = new List<string> { "crypto", "payments" }
        };

        var result = _engine.ComputeResult(answers);
        var finding = Assert.Single(result.Risks, r => r.Code == "PROD_REGULATORY_REVIEW");

        Assert.Equal(RiskSeverity.High, finding.Severity);
        Assert.NotEqual(RiskSeverity.Critical, finding.Severity);
        Assert.NotEqual(RiskSeverity.Blocker, finding.Severity);
    }
}
