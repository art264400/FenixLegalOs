using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.QuestionBank;
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

    // ─── 1. Canonical Questionnaire Dependency Graph Validation ────────────
    [Fact(DisplayName = "1. Canonical QuestionBank is validated as an acyclic topological DAG at startup")]
    public void QuestionBank_Dependency_Graph_Is_Acyclic_And_Valid()
    {
        // Must succeed without throwing
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }

    // ─── 2. Direct Self-Dependency Rejected ────────────────────────────────
    [Fact(DisplayName = "2. Direct self-dependency in ShowIf is rejected during validation")]
    public void Direct_Self_Dependency_Rejected()
    {
        var invalidQuestions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q_SELF", Order = 1, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q_SELF", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => RoutingDependencyValidator.Validate(invalidQuestions));
        Assert.Contains("Self-dependency detected", ex.Message);
    }

    // ─── 3. Indirect QA <-> QB Cycle Rejected ──────────────────────────────
    [Fact(DisplayName = "3. Indirect cyclic ShowIf dependency between QA and QB is rejected")]
    public void Indirect_Cycle_Rejected()
    {
        var cyclicQuestions = new List<DiagnosticQuestion>
        {
            new() { Id = "QA", Order = 1, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "QB", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "QB", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "QA", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => RoutingDependencyValidator.Validate(cyclicQuestions));
        Assert.Contains("Forward/backwards dependency detected", ex.Message);
    }

    // ─── 4. Mutually-Supporting Stale Answers Eliminated in Runtime ────────
    [Fact(DisplayName = "4. Mutually-supporting stale answers cannot establish visibility")]
    public void Mutually_Supporting_Stale_Answers_Eliminated()
    {
        var cyclicQuestions = new List<DiagnosticQuestion>
        {
            new() { Id = "QA", Order = 1, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "QB", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "QB", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "QA", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

        var rawAnswers = new Dictionary<string, object>
        {
            ["QA"] = "yes",
            ["QB"] = "yes"
        };

        var (visibleQs, effectiveAnswers, _) = ScoringEngine.ResolveEffectiveState(cyclicQuestions, rawAnswers);

        // Neither QA nor QB can establish visibility because neither has upstream authority
        Assert.Empty(visibleQs);
        Assert.Empty(effectiveAnswers);
    }

    // ─── 5. Self-Supporting Stale Answer Eliminated in Runtime ─────────────
    [Fact(DisplayName = "5. Self-supporting stale answer cannot establish own visibility")]
    public void Self_Supporting_Stale_Answer_Eliminated()
    {
        var questions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q_SELF", Order = 1, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q_SELF", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

        var rawAnswers = new Dictionary<string, object>
        {
            ["Q_SELF"] = "yes"
        };

        var (visibleQs, effectiveAnswers, _) = ScoringEngine.ResolveEffectiveState(questions, rawAnswers);

        Assert.Empty(visibleQs);
        Assert.Empty(effectiveAnswers);
    }

    // ─── 6. Negative Upstream Condition Works Deterministically ───────────
    [Fact(DisplayName = "6. Negative upstream condition works deterministically without monotonicity violation")]
    public void Negative_Upstream_Condition_Works_Deterministically()
    {
        var questions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q1", Order = 1, Type = QuestionType.Single, ShowIf = null },
            new() { Id = "Q2_NEG", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q1", Op = ConditionalOperator.Neq, Value = "yes" } } }
        };

        // When Q1 == "no", Q2_NEG is visible
        var answers1 = new Dictionary<string, object> { ["Q1"] = "no", ["Q2_NEG"] = "ans" };
        var (v1, e1, _) = ScoringEngine.ResolveEffectiveState(questions, answers1);
        Assert.Contains(v1, q => q.Id == "Q2_NEG");
        Assert.True(e1.ContainsKey("Q2_NEG"));

        // When Q1 == "yes", Q2_NEG is hidden
        var answers2 = new Dictionary<string, object> { ["Q1"] = "yes", ["Q2_NEG"] = "ans" };
        var (v2, e2, _) = ScoringEngine.ResolveEffectiveState(questions, answers2);
        Assert.DoesNotContain(v2, q => q.Id == "Q2_NEG");
        Assert.False(e2.ContainsKey("Q2_NEG"));
    }

    // ─── 7. Multi-Level Stale Chain (Parent -> Child -> Grandchild -> Great-Grandchild) ───
    [Fact(DisplayName = "7. Multi-level stale chain is completely eliminated in forward pass")]
    public void Multi_Level_Stale_Chain_Eliminated()
    {
        var questions = new List<DiagnosticQuestion>
        {
            new() { Id = "Q1", Order = 1, Type = QuestionType.Single, ShowIf = null },
            new() { Id = "Q2", Order = 2, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q1", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "Q3", Order = 3, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q2", Op = ConditionalOperator.Eq, Value = "yes" } } },
            new() { Id = "Q4", Order = 4, Type = QuestionType.Single, ShowIf = new() { new() { QuestionId = "Q3", Op = ConditionalOperator.Eq, Value = "yes" } } }
        };

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
    }

    // ─── 8. Product Subscription Routing ──────────────────────────────────
    [Fact(DisplayName = "8. Product subscription routing PROD-10 -> PROD-13 -> PROD-13A is strictly authoritative")]
    public void Product_Subscription_Chain_Routing()
    {
        // 1. Paid subscription -> PROD-13 visible, PROD-13A visible when autoRenew=yes
        var subAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "clear"
        };
        var (vSub, eSub, fSub) = ScoringEngine.ResolveEffectiveState(_allQuestions, subAnswers);
        Assert.Contains(vSub, q => q.Id == "PROD-13");
        Assert.Contains(vSub, q => q.Id == "PROD-13A");
        Assert.True(eSub.ContainsKey("PROD-13"));
        Assert.True(eSub.ContainsKey("PROD-13A"));
        Assert.True(fSub.Facts.ContainsKey("product.autoRenew"));

        // 2. Change PROD-10 to free -> PROD-13 and PROD-13A both become hidden
        var freeAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "current",
            ["PROD-10"] = "free",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "clear"
        };
        var (vFree, eFree, fFree) = ScoringEngine.ResolveEffectiveState(_allQuestions, freeAnswers);
        Assert.DoesNotContain(vFree, q => q.Id == "PROD-13");
        Assert.DoesNotContain(vFree, q => q.Id == "PROD-13A");
        Assert.False(eFree.ContainsKey("PROD-13"));
        Assert.False(eFree.ContainsKey("PROD-13A"));
        Assert.False(fFree.Facts.ContainsKey("product.autoRenew"));
    }

    // ─── 9. Team Module Routing Regression ─────────────────────────────────
    [Fact(DisplayName = "9. Team module routing evaluates correctly from upstream effective facts")]
    public void Team_Module_Routing_Regression()
    {
        var soloTeam = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-02"] = "stale_val"
        };
        var (vSolo, eSolo, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, soloTeam);
        Assert.DoesNotContain(vSolo, q => q.Id == "TEAM-02");
        Assert.False(eSolo.ContainsKey("TEAM-02"));

        var fullTeam = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-02"] = "3_5"
        };
        var (vFull, eFull, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, fullTeam);
        Assert.Contains(vFull, q => q.Id == "TEAM-02");
        Assert.True(eFull.ContainsKey("TEAM-02"));
    }

    // ─── 10. Founders Module Routing Regression ───────────────────────────
    [Fact(DisplayName = "10. Founders module routing evaluates correctly from upstream effective facts")]
    public void Founders_Module_Routing_Regression()
    {
        var soloFounders = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["FND-01"] = "stale_val"
        };
        var (vSolo, eSolo, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, soloFounders);
        Assert.DoesNotContain(vSolo, q => q.Id == "FND-01");
        Assert.False(eSolo.ContainsKey("FND-01"));

        var multiFounders = new Dictionary<string, object>
        {
            ["FND-C01"] = "multi",
            ["FND-01"] = "documented"
        };
        var (vMulti, eMulti, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, multiFounders);
        Assert.Contains(vMulti, q => q.Id == "FND-01");
        Assert.True(eMulti.ContainsKey("FND-01"));
    }

    // ─── 11. Corporate Module Routing Regression ──────────────────────────
    [Fact(DisplayName = "11. Corporate module routing evaluates correctly from upstream effective facts")]
    public void Corporate_Module_Routing_Regression()
    {
        var noEntity = new Dictionary<string, object>
        {
            ["COR-C01"] = "none",
            ["COR-01"] = "stale_val"
        };
        var (vNo, eNo, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, noEntity);
        Assert.DoesNotContain(vNo, q => q.Id == "COR-01");
        Assert.False(eNo.ContainsKey("COR-01"));

        var registered = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "commercial"
        };
        var (vReg, eReg, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, registered);
        Assert.Contains(vReg, q => q.Id == "COR-01");
        Assert.True(eReg.ContainsKey("COR-01"));
    }

    // ─── 12. IP Module Routing Regression ─────────────────────────────────
    [Fact(DisplayName = "12. IP module routing evaluates correctly from upstream effective facts")]
    public void Ip_Module_Routing_Regression()
    {
        var noProduct = new Dictionary<string, object>
        {
            ["IP-01"] = "idea",
            ["IP-04"] = "stale_val"
        };
        var (vNo, eNo, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, noProduct);
        Assert.DoesNotContain(vNo, q => q.Id == "IP-04");
        Assert.False(eNo.ContainsKey("IP-04"));

        var hasProduct = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-04"] = "documented"
        };
        var (vHas, eHas, _) = ScoringEngine.ResolveEffectiveState(_allQuestions, hasProduct);
        Assert.Contains(vHas, q => q.Id == "IP-04");
        Assert.True(eHas.ContainsKey("IP-04"));
    }

    // ─── 13. ComputeResult and GetNavigationState Match Exactly ───────────
    [Fact(DisplayName = "13. ComputeResult and GetNavigationState use identical visible and effective state")]
    public void ComputeResult_And_GetNavigationState_Match()
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
