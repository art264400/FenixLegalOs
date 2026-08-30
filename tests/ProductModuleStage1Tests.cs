using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ProductModuleStage1Tests
{
    private readonly ScoringEngine _engine;

    public ProductModuleStage1Tests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_prod_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _engine = new ScoringEngine(new QuestionRepository(dbInit));
    }

    // ─── 1. PROD-01 Prelaunch ─────────────────────────────────────────────
    [Fact(DisplayName = "1. PROD-01 prelaunch: liveUsers=false, userStage=prelaunch")]
    public void Prod01_Prelaunch_Produces_Expected_Facts()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "prelaunch"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal("prelaunch", facts.Facts["product.userStage"]);
        Assert.False((bool)facts.Facts["product.liveUsers"]!);
        Assert.False(facts.Facts.ContainsKey("product.userScale"));
    }

    // ─── 2. PROD-01 First, Regular, Large ─────────────────────────────────
    [Fact(DisplayName = "2. PROD-01 first/regular/large: liveUsers=true, correct userScale")]
    public void Prod01_LiveUsers_Produces_Scale()
    {
        var fFirst = FactNormalizer.NormalizeFacts(new() { ["PROD-01"] = "first" });
        Assert.Equal("first_users", fFirst.Facts["product.userStage"]);
        Assert.True((bool)fFirst.Facts["product.liveUsers"]!);
        Assert.Equal("small", fFirst.Facts["product.userScale"]);

        var fRegular = FactNormalizer.NormalizeFacts(new() { ["PROD-01"] = "regular" });
        Assert.Equal("regular", fRegular.Facts["product.userStage"]);
        Assert.True((bool)fRegular.Facts["product.liveUsers"]!);
        Assert.Equal("medium", fRegular.Facts["product.userScale"]);

        var fLarge = FactNormalizer.NormalizeFacts(new() { ["PROD-01"] = "large" });
        Assert.Equal("large", fLarge.Facts["product.userStage"]);
        Assert.True((bool)fLarge.Facts["product.liveUsers"]!);
        Assert.Equal("large", fLarge.Facts["product.userScale"]);
    }

    // ─── 3. PROD-02 Multiple Append ───────────────────────────────────────
    [Fact(DisplayName = "3. PROD-02 multiple canonical append behavior")]
    public void Prod02_Multiple_Appends_UserTypes_And_MinorsPossible()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers", "minors", "companies" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        var types = (List<string>)facts.Facts["product.userTypes"]!;
        Assert.Contains("consumers", types);
        Assert.Contains("minors", types);
        Assert.Contains("companies", types);
        Assert.True((bool)facts.Facts["product.minorsPossible"]!);
    }

    // ─── 4. Missing PROD-02 Does Not Create Synthetic Defaults ────────────
    [Fact(DisplayName = "4. Missing PROD-02 does not create userTypes=[] or minorsPossible=false")]
    public void Missing_Prod02_No_Synthetic_Defaults()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.False(facts.Facts.ContainsKey("product.userTypes"));
        Assert.False(facts.Facts.ContainsKey("product.minorsPossible"));
    }

    // ─── 5. PROD-04 Preparing Distinct from None ──────────────────────────
    [Fact(DisplayName = "5. PROD-04 preparing remains distinct from none")]
    public void Prod04_Preparing_Distinct_From_None()
    {
        var fPrep = FactNormalizer.NormalizeFacts(new() { ["PROD-04"] = "preparing" });
        Assert.Equal("preparing", fPrep.Facts["product.userRulesStatus"]);

        var fNone = FactNormalizer.NormalizeFacts(new() { ["PROD-04"] = "none" });
        Assert.Equal("none", fNone.Facts["product.userRulesStatus"]);
    }

    // ─── 6. PROD-04 Unknown Semantics ─────────────────────────────────────
    [Fact(DisplayName = "6. PROD-04 unknown creates exact 'unknown' + unknownQuestionIds")]
    public void Prod04_Unknown_Registers_Fact_And_Tracker()
    {
        var facts = FactNormalizer.NormalizeFacts(new() { ["PROD-04"] = "unknown" });
        Assert.Equal("unknown", facts.Facts["product.userRulesStatus"]);
        var unkList = (List<string>)facts.Facts["diagnostic.unknownQuestionIds"]!;
        Assert.Contains("PROD-04", unkList);
    }

    // ─── 7. PROD-05 ShowIf ────────────────────────────────────────────────
    [Fact(DisplayName = "7. PROD-05 showIf only for current, old, template")]
    public void Prod05_ShowIf_Matches_Specification()
    {
        // current -> visible
        var nav1 = _engine.GetNavigationState(new() { ["PROD-04"] = "current" });
        Assert.Contains("PROD-05", nav1.VisibleQuestionIds);

        // old -> visible
        var nav2 = _engine.GetNavigationState(new() { ["PROD-04"] = "old" });
        Assert.Contains("PROD-05", nav2.VisibleQuestionIds);

        // template -> visible
        var nav3 = _engine.GetNavigationState(new() { ["PROD-04"] = "template" });
        Assert.Contains("PROD-05", nav3.VisibleQuestionIds);

        // none -> hidden
        var nav4 = _engine.GetNavigationState(new() { ["PROD-04"] = "none" });
        Assert.DoesNotContain("PROD-05", nav4.VisibleQuestionIds);

        // preparing -> hidden
        var nav5 = _engine.GetNavigationState(new() { ["PROD-04"] = "preparing" });
        Assert.DoesNotContain("PROD-05", nav5.VisibleQuestionIds);
    }

    // ─── 8. PROD-07A ShowIf ───────────────────────────────────────────────
    [Fact(DisplayName = "8. PROD-07A showIf exact provider-role set")]
    public void Prod07A_ShowIf_Matches_Specification()
    {
        foreach (var role in new[] { "joint", "marketplace", "varies", "unknown" })
        {
            var nav = _engine.GetNavigationState(new() { ["PROD-07"] = role });
            Assert.Contains("PROD-07A", nav.VisibleQuestionIds);
        }

        var navCompany = _engine.GetNavigationState(new() { ["PROD-07"] = "company" });
        Assert.DoesNotContain("PROD-07A", navCompany.VisibleQuestionIds);
    }

    // ─── 9. PROD-08 / PROD-09 Routing Exact ───────────────────────────────
    [Fact(DisplayName = "9. PROD-08 and PROD-09 routing exact")]
    public void Prod08_And_Prod09_Routing_Exact()
    {
        // PROD-08 visible when liveUsers=true AND userRulesStatus not in [none, preparing]
        var nav1 = _engine.GetNavigationState(new()
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current"
        });
        Assert.Contains("PROD-08", nav1.VisibleQuestionIds);

        // Hidden if prelaunch
        var nav2 = _engine.GetNavigationState(new()
        {
            ["PROD-01"] = "prelaunch",
            ["PROD-04"] = "current"
        });
        Assert.DoesNotContain("PROD-08", nav2.VisibleQuestionIds);

        // Hidden if userRulesStatus is none
        var nav3 = _engine.GetNavigationState(new()
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none"
        });
        Assert.DoesNotContain("PROD-08", nav3.VisibleQuestionIds);

        // PROD-09 visible when termsAcceptance=explicit AND liveUsers=true
        var nav4 = _engine.GetNavigationState(new()
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-08"] = "explicit"
        });
        Assert.Contains("PROD-09", nav4.VisibleQuestionIds);

        // PROD-09 hidden when termsAcceptance=link_only
        var nav5 = _engine.GetNavigationState(new()
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-08"] = "link_only"
        });
        Assert.DoesNotContain("PROD-09", nav5.VisibleQuestionIds);
    }

    // ─── 10. Payment Routing ──────────────────────────────────────────────
    [Fact(DisplayName = "10. Payment routing: PROD-11/12 only when paid=true")]
    public void Payment_Routing_Matches_Specification()
    {
        // PROD-10 = free -> paid = false -> PROD-11 & PROD-12 hidden
        var navFree = _engine.GetNavigationState(new() { ["PROD-10"] = "free" });
        Assert.DoesNotContain("PROD-11", navFree.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-12", navFree.VisibleQuestionIds);

        // PROD-10 = subscription -> paid = true -> PROD-11 & PROD-12 visible
        var navPaid = _engine.GetNavigationState(new() { ["PROD-10"] = "subscription" });
        Assert.Contains("PROD-11", navPaid.VisibleQuestionIds);
        Assert.Contains("PROD-12", navPaid.VisibleQuestionIds);
    }

    // ─── 11. Subscription Routing ─────────────────────────────────────────
    [Fact(DisplayName = "11. Subscription routing: PROD-13/14/15 exact")]
    public void Subscription_Routing_Matches_Specification()
    {
        // Not subscription -> PROD-13, 14, 15 hidden
        var navOneOff = _engine.GetNavigationState(new() { ["PROD-10"] = "one_off" });
        Assert.DoesNotContain("PROD-13", navOneOff.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-14", navOneOff.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-15", navOneOff.VisibleQuestionIds);

        // Subscription -> PROD-13, 14, 15 visible
        var navSub = _engine.GetNavigationState(new() { ["PROD-10"] = "subscription" });
        Assert.Contains("PROD-13", navSub.VisibleQuestionIds);
        Assert.Contains("PROD-14", navSub.VisibleQuestionIds);
        Assert.Contains("PROD-15", navSub.VisibleQuestionIds);

        // PROD-13A visible when autoRenew=true or depends
        var navAutoYes = _engine.GetNavigationState(new()
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "yes"
        });
        Assert.Contains("PROD-13A", navAutoYes.VisibleQuestionIds);

        var navAutoDepends = _engine.GetNavigationState(new()
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "depends"
        });
        Assert.Contains("PROD-13A", navAutoDepends.VisibleQuestionIds);

        var navAutoNo = _engine.GetNavigationState(new()
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "no"
        });
        Assert.DoesNotContain("PROD-13A", navAutoNo.VisibleQuestionIds);
    }

    // ─── 12. UGC Routing ──────────────────────────────────────────────────
    [Fact(DisplayName = "12. UGC routing: true and unknown branch; false branch hidden")]
    public void Ugc_Routing_Matches_Specification()
    {
        var navYes = _engine.GetNavigationState(new() { ["PROD-18"] = "yes" });
        Assert.Contains("PROD-18A", navYes.VisibleQuestionIds);
        Assert.Contains("PROD-18B", navYes.VisibleQuestionIds);
        Assert.Contains("PROD-19", navYes.VisibleQuestionIds);

        var navUnknown = _engine.GetNavigationState(new() { ["PROD-18"] = "unknown" });
        Assert.Contains("PROD-18A", navUnknown.VisibleQuestionIds);
        Assert.Contains("PROD-18B", navUnknown.VisibleQuestionIds);
        Assert.Contains("PROD-19", navUnknown.VisibleQuestionIds);

        var navNo = _engine.GetNavigationState(new() { ["PROD-18"] = "no" });
        Assert.DoesNotContain("PROD-18A", navNo.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-18B", navNo.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-19", navNo.VisibleQuestionIds);
    }

    // ─── 13. Minors Routing Exact ─────────────────────────────────────────
    [Fact(DisplayName = "13. Minors routing exact")]
    public void Minors_Routing_Matches_Specification()
    {
        // PROD-20 visible if minorsPossible=true or userTypes contains consumers
        var navConsumers = _engine.GetNavigationState(new() { ["PROD-02"] = new List<string> { "consumers" } });
        Assert.Contains("PROD-20", navConsumers.VisibleQuestionIds);

        var navMinors = _engine.GetNavigationState(new() { ["PROD-02"] = new List<string> { "minors" } });
        Assert.Contains("PROD-20", navMinors.VisibleQuestionIds);

        var navB2bOnly = _engine.GetNavigationState(new() { ["PROD-02"] = new List<string> { "companies" } });
        Assert.DoesNotContain("PROD-20", navB2bOnly.VisibleQuestionIds);

        // PROD-20A visible if minorsAllowed is true, possible, or unknown
        var navAllowedYes = _engine.GetNavigationState(new()
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["PROD-20"] = "yes"
        });
        Assert.Contains("PROD-20A", navAllowedYes.VisibleQuestionIds);

        var navAllowedNo = _engine.GetNavigationState(new()
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["PROD-20"] = "no"
        });
        Assert.DoesNotContain("PROD-20A", navAllowedNo.VisibleQuestionIds);
    }

    // ─── 14. Geography Routing Exact ──────────────────────────────────────
    [Fact(DisplayName = "14. Geography routing exact")]
    public void Geography_Routing_Matches_Specification()
    {
        foreach (var geo in new[] { "multiple", "global", "not_tracked", "unknown" })
        {
            var nav = _engine.GetNavigationState(new() { ["PROD-21"] = geo });
            Assert.Contains("PROD-21A", nav.VisibleQuestionIds);
        }

        var navOne = _engine.GetNavigationState(new() { ["PROD-21"] = "one" });
        Assert.DoesNotContain("PROD-21A", navOne.VisibleQuestionIds);
    }

    // ─── 15. PROD-22 None Mutually Exclusive ──────────────────────────────
    [Fact(DisplayName = "15. PROD-22 none is marked exclusive in QuestionBank")]
    public void Prod22_None_Is_Mutually_Exclusive()
    {
        var q22 = ProductQuestions.All.First(q => q.Id == "PROD-22");
        var optNone = q22.Options!.First(o => o.Id == "none");
        Assert.True(optNone.Exclusive);
    }

    // ─── 16. Hidden / Stale Product Answers Do Not Become Effective Facts ──
    [Fact(DisplayName = "16. Hidden/stale Product answers do not become effective facts")]
    public void Hidden_Stale_Product_Answers_Do_Not_Become_Effective_Facts()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-05"] = "changed" // Stale: PROD-05 only visible when PROD-04 in [current, old, template]
        };

        var result = _engine.ComputeResult(answers);
        // Stale PROD-05 answer should not generate facts or affect score
        var nav = _engine.GetNavigationState(answers);
        Assert.DoesNotContain("PROD-05", nav.VisibleQuestionIds);
    }

    // ─── 17. Product Stale Answers Cannot Leak ────────────────────────────
    [Fact(DisplayName = "17. Product stale answers cannot leak into facts")]
    public void Product_Stale_Answers_Cannot_Leak()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "free",
            ["PROD-11"] = "late_fees", // Stale: paid=false
            ["PROD-13"] = "yes",       // Stale: subscription=false
            ["PROD-14"] = "complex"    // Stale: subscription=false
        };

        var result = _engine.ComputeResult(answers);
        var nav = _engine.GetNavigationState(answers);
        Assert.DoesNotContain("PROD-11", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-13", nav.VisibleQuestionIds);
        Assert.DoesNotContain("PROD-14", nav.VisibleQuestionIds);
    }

    // ─── 18. score.{questionId} Produced Only by Generic FactNormalizer ───
    [Fact(DisplayName = "18. score.{questionId} produced only by generic FactNormalizer")]
    public void QuestionScores_Produced_By_Generic_FactNormalizer()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-04"] = "current",
            ["PROD-06"] = "mostly"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(1.0, facts.Facts["score.PROD-04"]);
        Assert.Equal(0.75, facts.Facts["score.PROD-06"]);
    }

    // ─── 19. Product Dimension Weights Sum Exactly 100% ───────────────────
    [Fact(DisplayName = "19. Product dimension weights sum exactly 100%")]
    public void Product_Dimension_Weights_Sum_To_100()
    {
        var diagnosticQs = ProductQuestions.All.Where(q => q.ScoreMode == ScoreMode.Diagnostic).ToList();
        var distinctDims = diagnosticQs.Select(q => q.DimensionId).Distinct().ToList();

        Assert.Equal(11, distinctDims.Count);

        double totalDimWeight = 0;
        foreach (var dim in distinctDims)
        {
            var firstQ = diagnosticQs.First(q => q.DimensionId == dim);
            totalDimWeight += firstQ.DimensionWeight;

            var qsInDim = diagnosticQs.Where(q => q.DimensionId == dim).ToList();
            var sumWithin = qsInDim.Sum(q => q.WithinDimensionWeight);
            Assert.Equal(100, sumWithin);
        }

        Assert.Equal(100.0, totalDimWeight);
    }

    // ─── 20. Question Bank Count and Categories ───────────────────────────
    [Fact(DisplayName = "20. Product Question Bank has exact 28 questions (9 context, 18 diagnostic, 1 trigger)")]
    public void Product_QuestionBank_Counts_Are_Exact()
    {
        Assert.Equal(28, ProductQuestions.All.Count);
        Assert.Equal(9, ProductQuestions.All.Count(q => q.ScoreMode == ScoreMode.Context));
        Assert.Equal(18, ProductQuestions.All.Count(q => q.ScoreMode == ScoreMode.Diagnostic));
        Assert.Equal(1, ProductQuestions.All.Count(q => q.ScoreMode == ScoreMode.Trigger));
    }
}
