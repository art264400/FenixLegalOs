using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Modules.Investment;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentReadinessOverlayTests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;

    public InvestmentReadinessOverlayTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_inv_ov_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
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
    // 1. READINESS CAPS (§17.3)
    // =========================================================================

    [Theory(DisplayName = "1. Readiness caps: 0 -> base, 1 -> min(base, 59), >=2 -> min(base, 39)")]
    [InlineData(90, 0, 90)]
    [InlineData(90, 1, 59)]
    [InlineData(90, 2, 39)]
    [InlineData(90, 5, 39)]
    [InlineData(45, 1, 45)]
    [InlineData(30, 2, 30)]
    public void Readiness_Cap_Formula_PureFunction(int baseReadiness, int unresolvedBlockers, int expectedReadiness)
    {
        var facts = new SharedFactStore();
        facts.Facts["investment.timing"] = "active_search";
        facts.Facts["investment.activeFundraise"] = true;

        var findings = new List<RiskFinding>();
        // Add fake blocker findings matching the requested blocker count
        for (int i = 0; i < unresolvedBlockers; i++)
        {
            findings.Add(new RiskFinding
            {
                Code = i == 0 ? "INVEST_PRIOR_INVESTMENT_UNCLEAR" :
                       i == 1 ? "COR_OWNERSHIP_MISMATCH" :
                       i == 2 ? "IP_PRODUCT_RIGHTS_UNCONFIRMED" :
                       i == 3 ? "FND_DEPARTED_UNRESOLVED" : "FND_EQUITY_DISPUTE",
                Severity = RiskSeverity.Blocker,
                RootCauseGroup = $"GROUP_{i}",
                Title = $"Blocker Issue {i}"
            });
        }

        var overlay = InvestmentReadinessCalculator.Calculate(baseReadiness, findings, facts);

        Assert.True(overlay.Applicable);
        Assert.Equal(expectedReadiness, overlay.ReadinessScore);
    }

    [Fact(DisplayName = "2. E2E: 1 Blocker caps high base readiness (100) to 59")]
    public void E2E_One_Blocker_Caps_To_59()
    {
        var raw = new Dictionary<string, object>
        {
            // Clean high-scoring Investment module (base = 100)
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-04"] = "yes",
            ["INVEST-05"] = "clear",
            ["INVEST-06"] = "regular",
            ["INVEST-06A"] = "gt12",
            ["INVEST-07"] = "current",
            ["INVEST-08"] = "yes",
            ["INVEST-09"] = "organized",
            ["INVEST-10"] = "none",
            ["INVEST-11"] = "current",
            // 1 External Blocker issue: IP_PRODUCT_RIGHTS_UNCONFIRMED
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-04"] = "none"
        };

        var result = _engine.ComputeResult(raw);

        Assert.True(result.InvestmentReadiness.Applicable);
        Assert.Equal(59, result.InvestmentReadiness.ReadinessScore);
        Assert.Single(result.InvestmentReadiness.Blockers);
    }

    [Fact(DisplayName = "3. E2E: 2 Blockers cap high base readiness (100) to 39")]
    public void E2E_Two_Blockers_Cap_To_39()
    {
        var raw = new Dictionary<string, object>
        {
            // Clean high-scoring Investment module
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-04"] = "yes",
            ["INVEST-05"] = "clear",
            ["INVEST-06"] = "regular",
            ["INVEST-06A"] = "gt12",
            ["INVEST-07"] = "current",
            ["INVEST-08"] = "yes",
            ["INVEST-09"] = "organized",
            ["INVEST-10"] = "none",
            ["INVEST-11"] = "current",
            // 2 External Blocker issues: IP and Corporate Ownership Mismatch
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-04"] = "none",
            ["COR-01"] = "nominal"
        };

        var result = _engine.ComputeResult(raw);

        Assert.True(result.InvestmentReadiness.Applicable);
        Assert.Equal(39, result.InvestmentReadiness.ReadinessScore);
        Assert.Equal(2, result.InvestmentReadiness.Blockers.Count);
    }

    // =========================================================================
    // 2. APPLICABILITY & N/A BEHAVIOR
    // =========================================================================

    [Fact(DisplayName = "4. NotApplicable Investment module results in Applicable=false, Score=null, Blockers=[]")]
    public void NotApplicable_Investment_Readiness()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);

        Assert.False(result.InvestmentReadiness.Applicable);
        Assert.Null(result.InvestmentReadiness.ReadinessScore);
        Assert.Empty(result.InvestmentReadiness.Blockers);
    }

    [Fact(DisplayName = "5. Prior investment exists but timing=none -> Applicable=true, Score=baseReadiness, Blockers=[]")]
    public void Prior_Investment_Only_No_Active_Round_Blockers()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes",
            ["INVEST-03"] = "exact",
            // External issue exists, but since timing=none, it is not an active round blocker
            ["IP-01"] = "true",
            ["IP-02"] = "none"
        };

        var result = _engine.ComputeResult(raw);

        Assert.True(result.InvestmentReadiness.Applicable);
        Assert.Equal(100, result.InvestmentReadiness.ReadinessScore);
        Assert.Empty(result.InvestmentReadiness.Blockers);
    }
}
