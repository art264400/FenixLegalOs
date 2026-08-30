using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Investment;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentRuleEngineTests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;
    private readonly InvestmentRuleEngine _ruleEngine;
    private readonly List<RiskDefinition> _allRisks;

    public InvestmentRuleEngineTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_inv_rule_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
        _ruleEngine = new InvestmentRuleEngine();
        _allRisks = InvestmentRisks.All;
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
    // 1. EXACT CLASS A RULES (§27.2)
    // =========================================================================

    [Theory(DisplayName = "1.1 INVEST_PRIOR_INVESTMENT_UNCLEAR positive branches")]
    [InlineData("partial", "clear")]
    [InlineData("informal", "clear")]
    [InlineData("formal", "unclear")]
    [InlineData("formal", "no")]
    [InlineData("formal", "unknown")]
    public void Prior_Investment_Unclear_Positive(string status, string clarity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = status,
            ["INVEST-02A"] = clarity
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Contains(findings, f => f.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR" && f.Severity == RiskSeverity.High);
    }

    [Theory(DisplayName = "1.2 INVEST_PRIOR_INVESTMENT_UNCLEAR negative / near miss branches")]
    [InlineData("no", "")]
    [InlineData("unknown", "")]
    [InlineData("formal", "yes")]
    [InlineData("formal", "main")]
    public void Prior_Investment_Unclear_Negative(string status, string clarity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = status
        };
        if (!string.IsNullOrEmpty(clarity))
        {
            raw["INVEST-02A"] = clarity;
        }

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR");
    }

    [Theory(DisplayName = "1.3 INVEST_DD_DOCS_NOT_READY positive branches")]
    [InlineData("scattered")]
    [InlineData("reconstruct")]
    [InlineData("missing")]
    [InlineData("unknown")]
    public void Dd_Docs_Not_Ready_Positive(string folder)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-09"] = folder
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Contains(findings, f => f.Code == "INVEST_DD_DOCS_NOT_READY" && f.Severity == RiskSeverity.High);
    }

    [Theory(DisplayName = "1.4 INVEST_DD_DOCS_NOT_READY negative branches")]
    [InlineData("organized")]
    [InlineData("mostly")]
    public void Dd_Docs_Not_Ready_Negative(string folder)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-09"] = folder
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "INVEST_DD_DOCS_NOT_READY");
    }

    [Theory(DisplayName = "1.5 INVEST_TERMS_NOT_UNDERSTOOD positive branches")]
    [InlineData("price_only")]
    [InlineData("unclear")]
    [InlineData("not_reviewed")]
    public void Terms_Not_Understood_Positive(string understanding)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = understanding
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Contains(findings, f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD" && f.Severity == RiskSeverity.Critical);
    }

    [Theory(DisplayName = "1.6 INVEST_TERMS_NOT_UNDERSTOOD negative branches (mostly is excluded)")]
    [InlineData("yes")]
    [InlineData("mostly")]
    public void Terms_Not_Understood_Negative(string understanding)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = understanding
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD");
    }

    // =========================================================================
    // 2. CLASS B SOURCE-AUDITED BOUNDARIES (§25 / §24)
    // =========================================================================

    [Theory(DisplayName = "2.1 INVEST_FUTURE_CAP_TABLE_UNCLEAR boundaries")]
    [InlineData("current_only", true)]
    [InlineData("none", true)]
    [InlineData("exact", false)]
    [InlineData("mostly_promises", false)]
    [InlineData("unknown", false)]
    public void Future_Cap_Table_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-03"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_FUTURE_CAP_TABLE_UNCLEAR"));
    }

    [Theory(DisplayName = "2.2 INVEST_DILUTION_NOT_MODELED boundaries")]
    [InlineData("rough", true)]
    [InlineData("no", true)]
    [InlineData("one_scenario", false)]
    [InlineData("yes", false)]
    [InlineData("unknown", false)]
    public void Dilution_Not_Modeled_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-04"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_DILUTION_NOT_MODELED"));
    }

    [Theory(DisplayName = "2.3 INVEST_ROUND_NOT_DEFINED boundaries")]
    [InlineData("max_possible", true)]
    [InlineData("none", true)]
    [InlineData("amount_rough", false)]
    [InlineData("use_clear_amount_pending", false)]
    [InlineData("clear", false)]
    [InlineData("unknown", false)]
    public void Round_Not_Defined_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-05"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_ROUND_NOT_DEFINED"));
    }

    [Theory(DisplayName = "2.4 INVEST_RUNWAY_WARNING boundaries")]
    [InlineData("searching", "regular", "lt3", true)]
    [InlineData("searching", "no", "", true)]
    [InlineData("searching", "old", "", true)]
    [InlineData("searching", "regular", "gt12", false)]
    [InlineData("searching", "regular", "6_12", false)]
    [InlineData("none", "no", "", false)]
    public void Runway_Warning_Boundaries(string timing, string runwayKnown, string runwayMonths, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no",
            ["INVEST-06"] = runwayKnown
        };
        if (!string.IsNullOrEmpty(runwayMonths))
        {
            raw["INVEST-06A"] = runwayMonths;
        }

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_RUNWAY_WARNING"));
    }

    [Theory(DisplayName = "2.5 INVEST_FIN_MODEL_WEAK boundaries")]
    [InlineData("old", true)]
    [InlineData("fragments", true)]
    [InlineData("none", true)]
    [InlineData("simple", false)]
    [InlineData("current", false)]
    [InlineData("unknown", false)]
    public void Fin_Model_Weak_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-07"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_FIN_MODEL_WEAK"));
    }

    [Theory(DisplayName = "2.6 INVEST_METRICS_UNVERIFIABLE boundaries")]
    [InlineData("approx", true)]
    [InlineData("hard", true)]
    [InlineData("most", false)]
    [InlineData("yes", false)]
    [InlineData("unknown", false)]
    public void Metrics_Unverifiable_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-08"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_METRICS_UNVERIFIABLE"));
    }

    [Theory(DisplayName = "2.7 INVEST_DEAL_UNREVIEWED boundaries")]
    [InlineData("lawyer_unclear", true)]
    [InlineData("self", true)]
    [InlineData("none", true)]
    [InlineData("specialist", false)]
    [InlineData("unknown", false)]
    public void Deal_Unreviewed_Boundaries(string val, bool expected)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-15"] = val
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        var findings = _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(expected, findings.Any(f => f.Code == "INVEST_DEAL_UNREVIEWED"));
    }

    // =========================================================================
    // 3. PIPELINE INTEGRATION & STALE ISOLATION
    // =========================================================================

    [Fact(DisplayName = "3.1 Stale deal branch removes findings when timing switches from specific to none")]
    public void Stale_Deal_Findings_Removed()
    {
        // 1. Specific timing -> findings emitted
        var raw1 = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = "unclear",
            ["INVEST-15"] = "none"
        };
        var res1 = _engine.ComputeResult(raw1);
        Assert.Contains(res1.Risks, f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD");
        Assert.Contains(res1.Risks, f => f.Code == "INVEST_DEAL_UNREVIEWED");

        // 2. Switched to none -> findings disappear
        var raw2 = new Dictionary<string, object>(raw1)
        {
            ["INVEST-01"] = "none"
        };
        var res2 = _engine.ComputeResult(raw2);
        Assert.DoesNotContain(res2.Risks, f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD");
        Assert.DoesNotContain(res2.Risks, f => f.Code == "INVEST_DEAL_UNREVIEWED");
    }

    [Fact(DisplayName = "3.2 Stale runway months answer does not trigger finding when INVEST-06 switches to rough")]
    public void Stale_Runway_Months_Purged()
    {
        // 1. regular + lt3 -> finding emitted
        var raw1 = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-06"] = "regular",
            ["INVEST-06A"] = "lt3"
        };
        var res1 = _engine.ComputeResult(raw1);
        Assert.Contains(res1.Risks, f => f.Code == "INVEST_RUNWAY_WARNING");

        // 2. Switched INVEST-06 to no (runway unknown, 06A hidden -> answers purged)
        var raw2 = new Dictionary<string, object>(raw1)
        {
            ["INVEST-06"] = "no"
        };
        var (vis2, eff2, facts2) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw2);
        Assert.DoesNotContain("INVEST-06A", eff2.Keys);
        Assert.False(facts2.Facts.ContainsKey("investment.runwayMonthsBucket"));
    }

    [Fact(DisplayName = "3.3 Basis integrity: all emitted findings have non-empty Basis with valid QuestionId")]
    public void Basis_Integrity()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "terms",
            ["INVEST-02"] = "partial",
            ["INVEST-02A"] = "unclear",
            ["INVEST-03"] = "current_only",
            ["INVEST-04"] = "rough",
            ["INVEST-05"] = "max_possible",
            ["INVEST-06"] = "old",
            ["INVEST-07"] = "fragments",
            ["INVEST-08"] = "hard",
            ["INVEST-09"] = "missing",
            ["INVEST-12"] = "price_only",
            ["INVEST-15"] = "self"
        };

        var result = _engine.ComputeResult(raw);
        var invFindings = result.Risks.Where(f => f.Modules.Contains("investment")).ToList();

        Assert.Equal(10, invFindings.Count);
        foreach (var finding in invFindings)
        {
            Assert.NotEmpty(finding.Basis);
            Assert.All(finding.Basis, b =>
            {
                Assert.StartsWith("INVEST-", b.QuestionId);
                Assert.False(string.IsNullOrEmpty(b.AnswerId));
            });
        }
    }

    [Fact(DisplayName = "3.4 RuleEngine is immutable: facts are not mutated during Evaluate")]
    public void RuleEngine_Immutability()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes",
            ["INVEST-04"] = "rough"
        };

        var (_, _, store) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, raw);
        int factCountBefore = store.Facts.Count;

        _ruleEngine.Evaluate(store, _allRisks);

        Assert.Equal(factCountBefore, store.Facts.Count);
    }
}
