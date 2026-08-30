using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentCrossModuleIntegrationTests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;

    public InvestmentCrossModuleIntegrationTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_inv_cm_{Guid.NewGuid():N}.db");
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
    // 1. INVEST_SELF_AWARENESS_GAP (§27.2 Class A)
    // =========================================================================

    [Fact(DisplayName = "1.1 INVEST_SELF_AWARENESS_GAP fires when selfReportedIssues=none and external High finding exists")]
    public void SelfAwarenessGap_Fires_With_External_High()
    {
        var raw = new Dictionary<string, object>
        {
            // External module with High/Critical finding: IP (IP-01=ready, IP-04=none, COR-C01=one)
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-04"] = "none",
            // Investment module: timing=searching, selfReportedIssues=none
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-10"] = "none"
        };

        var result = _engine.ComputeResult(raw);

        Assert.Contains(result.Risks, f => f.Code == "INVEST_SELF_AWARENESS_GAP" && f.Severity == RiskSeverity.Medium);

        var gapFinding = result.Risks.First(f => f.Code == "INVEST_SELF_AWARENESS_GAP");
        Assert.Contains(gapFinding.Basis, b => b.QuestionId == "INVEST-10" && b.AnswerId == "none");
        Assert.Contains(gapFinding.Basis, b => b.QuestionId == "IP-04" && b.AnswerId == "none");
    }

    [Fact(DisplayName = "1.2 INVEST_SELF_AWARENESS_GAP fires with external Critical finding")]
    public void SelfAwarenessGap_Fires_With_External_Critical()
    {
        var raw = new Dictionary<string, object>
        {
            // Founders dispute (Critical)
            ["FND-C01"] = "2",
            ["FND-01"] = "active_conflict",
            ["FND-04"] = "dispute",
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-10"] = "none"
        };

        var result = _engine.ComputeResult(raw);

        Assert.Contains(result.Risks, f => f.Code == "INVEST_SELF_AWARENESS_GAP");
    }

    [Theory(DisplayName = "1.3 INVEST_SELF_AWARENESS_GAP does NOT fire when selfReportedIssues != none")]
    [InlineData("small")]
    [InlineData("material_plan")]
    [InlineData("material_unresolved")]
    [InlineData("unknown")]
    public void SelfAwarenessGap_DoesNotFire_When_Issues_Reported(string answer)
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-04"] = "none", // External Critical/High
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-10"] = answer
        };

        var result = _engine.ComputeResult(raw);

        Assert.DoesNotContain(result.Risks, f => f.Code == "INVEST_SELF_AWARENESS_GAP");
    }

    [Fact(DisplayName = "1.4 INVEST_SELF_AWARENESS_GAP does NOT fire if only Investment-local High findings exist")]
    public void SelfAwarenessGap_DoesNotFire_For_InvestmentOnly_Findings()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-07"] = "none", // Investment-local High (FIN_MODEL_WEAK)
            ["INVEST-10"] = "none"
        };

        var result = _engine.ComputeResult(raw);

        Assert.Contains(result.Risks, f => f.Code == "INVEST_FIN_MODEL_WEAK");
        Assert.DoesNotContain(result.Risks, f => f.Code == "INVEST_SELF_AWARENESS_GAP");
    }

    // =========================================================================
    // 2. INVEST_ROUND_BLOCKER (§27.2 Class A)
    // =========================================================================

    [Fact(DisplayName = "2.1 INVEST_ROUND_BLOCKER fires when close/active round and materialDDIssue=true")]
    public void RoundBlocker_Fires_When_Active_And_MaterialDDIssue()
    {
        var raw = new Dictionary<string, object>
        {
            // Corporate ownership mismatch (Material DD issue)
            ["COR-C01"] = "one",
            ["COR-01"] = "nominal",
            // Active search
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);

        Assert.Contains(result.Risks, f => f.Code == "INVEST_ROUND_BLOCKER" && f.Severity == RiskSeverity.Blocker);

        var blockerFinding = result.Risks.First(f => f.Code == "INVEST_ROUND_BLOCKER");
        Assert.Contains(blockerFinding.Basis, b => b.QuestionId == "INVEST-01" && b.AnswerId == "active_search");
        Assert.Contains(blockerFinding.Basis, b => b.QuestionId == "COR-01" && b.AnswerId == "nominal");
    }

    [Fact(DisplayName = "2.2 INVEST_ROUND_BLOCKER does NOT fire when timing is none even if material issue exists")]
    public void RoundBlocker_DoesNotFire_When_Timing_Is_None()
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "nominal",
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "formal",
            ["INVEST-02A"] = "yes"
        };

        var result = _engine.ComputeResult(raw);

        Assert.DoesNotContain(result.Risks, f => f.Code == "INVEST_ROUND_BLOCKER");
    }

    [Fact(DisplayName = "2.3 INVEST_ROUND_BLOCKER does NOT fire when active round has no material DD issues")]
    public void RoundBlocker_DoesNotFire_Without_Material_DD_Issue()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-04"] = "yes",
            ["INVEST-05"] = "clear",
            ["INVEST-06"] = "regular",
            ["INVEST-06A"] = "gt12",
            ["INVEST-07"] = "current",
            ["INVEST-08"] = "yes",
            ["INVEST-09"] = "organized"
        };

        var result = _engine.ComputeResult(raw);

        Assert.DoesNotContain(result.Risks, f => f.Code == "INVEST_ROUND_BLOCKER");
    }
}
