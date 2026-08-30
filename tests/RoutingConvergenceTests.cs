using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class RoutingConvergenceTests
{
    private readonly ScoringEngine _engine;
    private readonly List<DiagnosticQuestion> _allQuestions;

    public RoutingConvergenceTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_conv_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
        _allQuestions = repo.GetQuestions();
    }

    // ─── 1. Already Stable State ──────────────────────────────────────────
    [Fact(DisplayName = "1. Already stable state converges cleanly and returns exact stable state")]
    public void Already_Stable_State_Converges()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-06"] = "clear",
            ["PROD-07"] = "company"
        };

        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(_allQuestions, answers);

        Assert.Equal(answers.Count, effectiveAnswers.Count);
        foreach (var (k, v) in answers)
        {
            Assert.Equal(v.ToString(), effectiveAnswers[k].ToString());
        }
        Assert.True(visibleQs.Count >= answers.Count);
    }

    // ─── 2. One Stale Child Removed ───────────────────────────────────────
    [Fact(DisplayName = "2. One stale child is cleanly removed in resolution")]
    public void One_Stale_Child_Removed()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-05"] = "yes" // Stale: PROD-05 requires PROD-04 in [current, old, template]
        };

        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(_allQuestions, answers);

        Assert.False(effectiveAnswers.ContainsKey("PROD-05"));
        Assert.DoesNotContain(visibleQs, q => q.Id == "PROD-05");
        Assert.False(factStore.Facts.ContainsKey("product.rulesMatch"));
    }

    // ─── 3. Multi-Level Stale Chain (Parent -> Child -> Grandchild -> Great-Grandchild) ───
    [Fact(DisplayName = "3. Multi-level stale chain is completely eliminated")]
    public void Multi_Level_Stale_Chain_Eliminated()
    {
        // Custom 4-level chain: Q1 -> Q2 -> Q3 -> Q4
        var questions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q1", Order = 1, Type = QuestionType.Single, ShowIf = null },
            new() { Id = "Q2", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q1", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "Q3", Order = 3, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q2", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "Q4", Order = 4, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q3", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

        // User changes Q1 to "no", but session has stale answers for Q2, Q3, Q4
        var rawAnswers = new Dictionary<string, object>
        {
            ["Q1"] = "no",
            ["Q2"] = "yes",
            ["Q3"] = "yes",
            ["Q4"] = "yes"
        };

        var (visibleQs, effectiveAnswers, _) = ScoringEngine.ResolveEffectiveState(questions, rawAnswers);

        Assert.Single(visibleQs);
        Assert.Equal("Q1", visibleQs.First().Id);

        Assert.Single(effectiveAnswers);
        Assert.Equal("no", effectiveAnswers["Q1"].ToString());
        Assert.False(effectiveAnswers.ContainsKey("Q2"));
        Assert.False(effectiveAnswers.ContainsKey("Q3"));
        Assert.False(effectiveAnswers.ContainsKey("Q4"));
    }

    // ─── 4. Product Subscription Self-Resurrection Scenario ───────────────
    [Fact(DisplayName = "4. Product subscription self-resurrection scenario remains strictly fixed")]
    public void Product_Subscription_SelfResurrection_Fixed()
    {
        // PROD-10 free + stale PROD-13 (yes) + stale PROD-13A (clear) + stale PROD-14 (complex)
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "free",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "clear",
            ["PROD-14"] = "complex"
        };

        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(_allQuestions, rawAnswers);

        var visibleIds = visibleQs.Select(q => q.Id).ToHashSet();
        Assert.DoesNotContain("PROD-13", visibleIds);
        Assert.DoesNotContain("PROD-13A", visibleIds);
        Assert.DoesNotContain("PROD-14", visibleIds);

        Assert.False(effectiveAnswers.ContainsKey("PROD-13"));
        Assert.False(effectiveAnswers.ContainsKey("PROD-13A"));
        Assert.False(effectiveAnswers.ContainsKey("PROD-14"));

        Assert.False(factStore.Facts.ContainsKey("product.autoRenew"));
        Assert.False(factStore.Facts.ContainsKey("product.autoRenewDisclosure"));
        Assert.False(factStore.Facts.ContainsKey("product.subscriptionCancellation"));
    }

    // ─── 5. Idempotent / Deterministic Resolution ─────────────────────────
    [Fact(DisplayName = "5. Repeated ResolveEffectiveState with identical input produces identical output")]
    public void Idempotent_Resolution()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "free",
            ["PROD-11"] = "late_fees"
        };

        var (v1, e1, f1) = ScoringEngine.ResolveEffectiveState(_allQuestions, rawAnswers);
        var (v2, e2, f2) = ScoringEngine.ResolveEffectiveState(_allQuestions, rawAnswers);

        Assert.Equal(v1.Select(q => q.Id), v2.Select(q => q.Id));
        Assert.Equal(e1.Count, e2.Count);
        foreach (var (k, v) in e1)
        {
            Assert.Equal(v.ToString(), e2[k].ToString());
        }
    }

    // ─── 6. Pathological ShowIf Oscillation Fails Closed ──────────────────
    [Fact(DisplayName = "6. Pathological circular/oscillating ShowIf dependency fails closed deterministically")]
    public void Pathological_Oscillating_Dependency_Fails_Closed()
    {
        // Construct oscillating ShowIf:
        // Q1 visible if Q2 is answered
        // Q2 visible if Q1 is NOT answered (or Q1 != "yes")
        var questions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q_OSC_1", Order = 1, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q_OSC_2", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "Q_OSC_2", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q_OSC_1", Op = ConditionalOperator.Neq, Value = "yes" } } }
        };

        var rawAnswers = new Dictionary<string, object>
        {
            ["Q_OSC_1"] = "yes",
            ["Q_OSC_2"] = "yes"
        };

        // Must fail closed with InvalidOperationException, never loop infinitely
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ScoringEngine.ResolveEffectiveState(questions, rawAnswers));

        Assert.Contains("Architecture A routing convergence failure", ex.Message);
    }

    // ─── 7. ComputeResult and GetNavigationState Match Exactly ────────────
    [Fact(DisplayName = "7. ComputeResult and GetNavigationState use identical visible and effective state")]
    public void ComputeResult_And_GetNavigationState_Are_Identical()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "one_off",
            ["PROD-11"] = "clear",
            ["PROD-12"] = "published"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var nav = _engine.GetNavigationState(rawAnswers);

        var (expectedVisible, expectedEffective, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, rawAnswers);

        Assert.Equal(expectedVisible.Select(q => q.Id), nav.VisibleQuestionIds);
        Assert.Equal(expectedEffective.Count, result.AnsweredCount);
    }
}
