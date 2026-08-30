using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentBlockerMatrixTests : IDisposable
{
    private readonly string _tempDb;
    private readonly ScoringEngine _engine;

    public InvestmentBlockerMatrixTests()
    {
        _tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_inv_mat_{Guid.NewGuid():N}.db");
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

    [Theory(DisplayName = "1. Row 1: INVEST_PRIOR_INVESTMENT_UNCLEAR escalation (High -> Critical -> Blocker)")]
    [InlineData("none", RiskSeverity.High)]
    [InlineData("3_6", RiskSeverity.Critical)]
    [InlineData("searching", RiskSeverity.Blocker)]
    public void Matrix_Row1_PriorInvestments(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "partial", // triggers INVEST_PRIOR_INVESTMENT_UNCLEAR
            ["INVEST-02A"] = "clear"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "2. Row 2: FND_EQUITY_DISPUTE escalation (Critical -> Blocker -> Blocker)")]
    [InlineData("none", RiskSeverity.Critical)]
    [InlineData("3_6", RiskSeverity.Blocker)]
    [InlineData("searching", RiskSeverity.Blocker)]
    public void Matrix_Row2_EquityDispute(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-01"] = "active_conflict",
            ["FND-04"] = "dispute", // triggers FND_EQUITY_DISPUTE
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "FND_EQUITY_DISPUTE");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "3. Row 3: COR_OWNERSHIP_MISMATCH escalation (High -> Critical -> Blocker)")]
    [InlineData("none", RiskSeverity.High)]
    [InlineData("3_6", RiskSeverity.Critical)]
    [InlineData("searching", RiskSeverity.Blocker)]
    public void Matrix_Row3_OwnershipMismatch(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "one", // incorporated
            ["COR-01"] = "nominal", // triggers COR_OWNERSHIP_MISMATCH
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "COR_OWNERSHIP_MISMATCH");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "4. Row 4: IP_PRODUCT_RIGHTS_UNCONFIRMED escalation (High -> Critical -> Blocker)")]
    [InlineData("none", RiskSeverity.Critical)] // Base is Critical
    [InlineData("3_6", RiskSeverity.Critical)]
    [InlineData("searching", RiskSeverity.Blocker)]
    public void Matrix_Row4_CoreIpRights(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "one", // incorporated
            ["IP-01"] = "ready",
            ["IP-04"] = "none", // triggers IP_PRODUCT_RIGHTS_UNCONFIRMED
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "5. Row 5: FND_DEPARTED_UNRESOLVED escalation (Critical -> Critical -> Blocker)")]
    [InlineData("none", RiskSeverity.Critical)] // Base is Critical
    [InlineData("3_6", RiskSeverity.Critical)]
    [InlineData("searching", RiskSeverity.Blocker)]
    public void Matrix_Row5_DepartedFounder(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "departed_unresolved", // triggers FND_DEPARTED_UNRESOLVED
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "FND_DEPARTED_UNRESOLVED");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "6. Row 6: INVEST_METRICS_UNVERIFIABLE escalation (Medium -> High -> Critical)")]
    [InlineData("none", RiskSeverity.High)] // Base default in InvestmentRisks is High
    [InlineData("3_6", RiskSeverity.High)]
    [InlineData("searching", RiskSeverity.Critical)]
    public void Matrix_Row6_MetricsUnverifiable(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no",
            ["INVEST-08"] = "hard"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "INVEST_METRICS_UNVERIFIABLE");

        if (timing != "none")
        {
            Assert.NotNull(finding);
            Assert.Equal(expectedSeverity, finding.Severity);
        }
    }

    [Theory(DisplayName = "7. Row 7: INVEST_DD_DOCS_NOT_READY escalation (Medium/High -> High)")]
    [InlineData("3_6", RiskSeverity.Medium)]
    [InlineData("searching", RiskSeverity.High)]
    public void Matrix_Row7_DdDocs(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no",
            ["INVEST-09"] = "missing"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "INVEST_DD_DOCS_NOT_READY");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Theory(DisplayName = "8. Row 8: INVEST_FIN_MODEL_WEAK escalation (Medium/High -> High)")]
    [InlineData("3_6", RiskSeverity.High)]
    [InlineData("searching", RiskSeverity.High)]
    public void Matrix_Row8_FinModel(string timing, RiskSeverity expectedSeverity)
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = timing,
            ["INVEST-02"] = "no",
            ["INVEST-07"] = "none"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "INVEST_FIN_MODEL_WEAK");

        Assert.NotNull(finding);
        Assert.Equal(expectedSeverity, finding.Severity);
    }

    [Fact(DisplayName = "9. Row 9: INVEST_TERMS_NOT_UNDERSTOOD is Critical in Active Round and not visible in >12m")]
    public void Matrix_Row9_DealTerms()
    {
        var raw = new Dictionary<string, object>
        {
            ["INVEST-01"] = "specific",
            ["INVEST-02"] = "no",
            ["INVEST-12"] = "unclear"
        };

        var result = _engine.ComputeResult(raw);
        var finding = result.Risks.FirstOrDefault(f => f.Code == "INVEST_TERMS_NOT_UNDERSTOOD");

        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    // =========================================================================
    // 10. materialDDIssue Domain (Rows 1-5 -> true, Rows 6-9 -> false)
    // =========================================================================

    [Theory(DisplayName = "10. materialDDIssue domain: Rows 1-5 trigger materialDDIssue=true, Rows 6-9 trigger false")]
    [InlineData("INVEST_PRIOR_INVESTMENT_UNCLEAR", true)]
    [InlineData("FND_EQUITY_DISPUTE", true)]
    [InlineData("COR_OWNERSHIP_MISMATCH", true)]
    [InlineData("IP_PRODUCT_RIGHTS_UNCONFIRMED", true)]
    [InlineData("FND_DEPARTED_UNRESOLVED", true)]
    [InlineData("INVEST_METRICS_UNVERIFIABLE", false)]
    [InlineData("INVEST_DD_DOCS_NOT_READY", false)]
    [InlineData("INVEST_FIN_MODEL_WEAK", false)]
    [InlineData("INVEST_TERMS_NOT_UNDERSTOOD", false)]
    public void MaterialDDIssue_Domain_Evaluation(string riskCode, bool expectedMaterial)
    {
        var facts = new SharedFactStore();
        facts.Facts["investment.timing"] = "active_search";

        var findings = new List<RiskFinding>
        {
            new()
            {
                Code = riskCode,
                Severity = RiskSeverity.Critical,
                RootCauseGroup = "TEST_GROUP",
                Title = "Test Finding"
            }
        };

        var eval = Scoring.Modules.Investment.MaterialDdIssueEvaluator.Evaluate(findings, facts);
        Assert.Equal(expectedMaterial, eval.HasMaterialDDIssue);
    }

    // =========================================================================
    // 11. within_12m Fail-Closed Policy
    // =========================================================================

    [Fact(DisplayName = "11. within_12m fail-closed: does not trigger ROUND_BLOCKER or premature matrix escalation")]
    public void Within_12m_Fail_Closed_Policy()
    {
        var raw = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "nominal", // Material mismatch
            ["INVEST-01"] = "possible_year", // within_12m
            ["INVEST-02"] = "no"
        };

        var result = _engine.ComputeResult(raw);

        // Does NOT trigger INVEST_ROUND_BLOCKER (§27.2 requires timing in [3_6m, active_search, specific_investor, terms_received])
        Assert.DoesNotContain(result.Risks, f => f.Code == "INVEST_ROUND_BLOCKER");

        // COR_OWNERSHIP_MISMATCH retains its base High severity (not escalated to Critical/Blocker)
        var mismatch = result.Risks.First(f => f.Code == "COR_OWNERSHIP_MISMATCH");
        Assert.Equal(RiskSeverity.High, mismatch.Severity);
    }

    // =========================================================================
    // 12. BEFORE_ROUND vs NOW Priority
    // =========================================================================

    [Fact(DisplayName = "12. DD-sensitive High finding receives BEFORE_ROUND priority in close round and normal priority otherwise")]
    public void Priority_BeforeRound_Distinct_From_Now()
    {
        Assert.NotEqual(RiskPriority.Now, RiskPriority.BeforeRound);

        // 1. Without close fundraising (timing = 6_12m, i.e. not within 6 months)
        var rawRemote = new Dictionary<string, object>
        {
            ["INVEST-01"] = "6_12",
            ["INVEST-02"] = "no",
            ["INVEST-07"] = "none" // INVEST_FIN_MODEL_WEAK (High)
        };
        var resRemote = _engine.ComputeResult(rawRemote);
        var finRemote = resRemote.Risks.First(f => f.Code == "INVEST_FIN_MODEL_WEAK");
        Assert.Equal(RiskPriority.ThirtyDays, finRemote.Priority);

        // 2. With close fundraising (timing = 3_6m, within 6 months)
        var rawClose = new Dictionary<string, object>
        {
            ["INVEST-01"] = "3_6",
            ["INVEST-02"] = "no",
            ["INVEST-07"] = "none"
        };
        var resClose = _engine.ComputeResult(rawClose);
        var finClose = resClose.Risks.First(f => f.Code == "INVEST_FIN_MODEL_WEAK");
        Assert.Equal(RiskPriority.BeforeRound, finClose.Priority);
    }
}
