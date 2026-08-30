using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentStage3PipelineHardeningTests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;

    public InvestmentStage3PipelineHardeningTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_inv_pipe_{Guid.NewGuid():N}.db");
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
    // 1. BASE SCORE STABILITY (§17.3 / §26)
    // =========================================================================

    [Fact(DisplayName = "1. Base score stability: Readiness overlay and matrix severity changes do not alter base ModuleScore or OverallLegalScore")]
    public void Base_Score_Stability_Proof()
    {
        var raw = new Dictionary<string, object>
        {
            // Corporate mismatch (causes matrix blocker escalation in active round)
            ["COR-C01"] = "one",
            ["COR-01"] = "nominal",
            // Investment active round
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
            ["INVEST-11"] = "current"
        };

        var result = _engine.ComputeResult(raw);

        var invSec = result.Sections.First(s => s.SectionId == "investment");
        // Base Investment score = 100
        Assert.Equal(100, invSec.Score);

        // Readiness overlay is capped to 59 due to COR_OWNERSHIP_MISMATCH blocker
        Assert.Equal(59, result.InvestmentReadiness.ReadinessScore);

        // Crucial invariant: Investment Module Score remains 100, NOT capped to 59
        Assert.Equal(100, invSec.Score);
    }

    // =========================================================================
    // 2. STALE ANSWER ISOLATION
    // =========================================================================

    [Fact(DisplayName = "2. Stale deal branch answers and blockers are purged when timing changes from specific_investor to none")]
    public void Stale_Deal_Branch_Purged()
    {
        // 1. Specific timing with deal issues
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

        // 2. Switched timing to none
        var raw2 = new Dictionary<string, object>(raw1)
        {
            ["INVEST-01"] = "none"
        };
        var res2 = _engine.ComputeResult(raw2);
        Assert.DoesNotContain(res2.Risks, f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD");
        Assert.DoesNotContain(res2.Risks, f => f.Code == "INVEST_DEAL_UNREVIEWED");
        Assert.False(res2.InvestmentReadiness.Applicable);
        Assert.Null(res2.InvestmentReadiness.ReadinessScore);
    }

    // =========================================================================
    // 3. DETERMINISM
    // =========================================================================

    [Fact(DisplayName = "3. Deterministic execution: repeated ComputeResult calls produce identical analytical output")]
    public void Deterministic_Output_Verification()
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "two",
            ["COR-01"] = "material_mismatch",
            ["IP-01"] = "true",
            ["IP-02"] = "none",
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "unclear",
            ["INVEST-03"] = "current_only",
            ["INVEST-04"] = "rough",
            ["INVEST-05"] = "max_possible",
            ["INVEST-06"] = "old",
            ["INVEST-07"] = "fragments",
            ["INVEST-08"] = "hard",
            ["INVEST-09"] = "missing",
            ["INVEST-10"] = "none"
        };

        var r1 = _engine.ComputeResult(raw);
        var r2 = _engine.ComputeResult(raw);

        // Normalize timestamps
        r1.ComputedAt = "STATIC_TIME";
        r2.ComputedAt = "STATIC_TIME";

        var json1 = JsonSerializer.Serialize(r1, new JsonSerializerOptions { WriteIndented = true });
        var json2 = JsonSerializer.Serialize(r2, new JsonSerializerOptions { WriteIndented = true });

        Assert.Equal(json1, json2);
    }
}
