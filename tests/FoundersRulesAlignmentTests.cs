using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class FoundersRulesAlignmentTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public FoundersRulesAlignmentTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_fnd_align_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1.1 [FND_EQUITY_DISPUTE] founders.equityDispute=true активирует FND_EQUITY_DISPUTE")]
    public void FND_Equity_Dispute_From_Founders_Fact()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "dispute",
            ["COR-C01"] = "one",
            ["COR-01"] = "exact"
        };
        var result = _engine.ComputeResult(answers);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "FND_EQUITY_DISPUTE");
        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact(DisplayName = "1.2 [FND_EQUITY_DISPUTE] Cross-module capital.ownershipDispute=true активирует FND_EQUITY_DISPUTE")]
    public void FND_Equity_Dispute_From_Cross_Module_Corporate_OwnershipDispute()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "registered",
            ["COR-C01"] = "one",
            ["COR-01"] = "dispute"
        };
        var result = _engine.ComputeResult(answers);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "FND_EQUITY_DISPUTE");
        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact(DisplayName = "1.3 [FND_EQUITY_DISPUTE] При отсутствии equity dispute в обоих модулях FND_EQUITY_DISPUTE не создается")]
    public void FND_Equity_Dispute_Absent_When_Both_False()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "registered",
            ["COR-C01"] = "one",
            ["COR-01"] = "exact"
        };
        var result = _engine.ComputeResult(answers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "FND_EQUITY_DISPUTE");
    }

    [Fact(DisplayName = "2.1 [FND_DEAD_EQUITY] score(FND-05) <= 0.15 и score(FND-03) <= 0.25 активируют FND_DEAD_EQUITY")]
    public void FND_Dead_Equity_Score_Threshold_Boundary_Passes()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-05"] = "unknown", // score = 0.15
            ["FND-03"] = "below_expected" // score = 0.25
        };
        var result = _engine.ComputeResult(answers);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "FND_DEAD_EQUITY");
        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact(DisplayName = "2.2 [FND_DEAD_EQUITY] score(FND-05) > 0.15 не активирует FND_DEAD_EQUITY даже при low commitment")]
    public void FND_Dead_Equity_Fails_When_FND05_Score_Above_Threshold()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-05"] = "verbal_rule", // score = 0.55 (> 0.15)
            ["FND-03"] = "below_expected" // score = 0.25
        };
        var result = _engine.ComputeResult(answers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "FND_DEAD_EQUITY");
    }

    [Fact(DisplayName = "2.3 [FND_DEAD_EQUITY] score(FND-05) <= 0.15 с inactiveExists активирует FND_DEAD_EQUITY")]
    public void FND_Dead_Equity_Passes_With_InactiveExists()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "inactive_exist", // inactiveExists = true
            ["FND-05"] = "not_discussed", // score = 0.0
            ["FND-03"] = "aligned" // score = 1.0
        };
        var result = _engine.ComputeResult(answers);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "FND_DEAD_EQUITY");
        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact(DisplayName = "2.4 [FND_DEAD_EQUITY] score(FND-05) <= 0.15 с departedFounderExists активирует FND_DEAD_EQUITY")]
    public void FND_Dead_Equity_Passes_With_DepartedFounderExists()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "departed_clean", // departedFounderExists = true
            ["FND-05"] = "retains_all", // score = 0.10
            ["FND-03"] = "aligned" // score = 1.0
        };
        var result = _engine.ComputeResult(answers);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "FND_DEAD_EQUITY");
        Assert.NotNull(finding);
        Assert.Equal(RiskSeverity.Critical, finding.Severity);
    }

    [Fact(DisplayName = "3.1 [FND_DEPARTED_UNRESOLVED] Канонические условия unresolved, dispute, already_unresolved активируют риск")]
    public void FND_Departed_Unresolved_Canonical_Triggers()
    {
        // 1. unresolved
        var res1 = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "unresolved"
        });
        Assert.Contains(res1.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED" && r.Severity == RiskSeverity.Critical);

        // 2. dispute
        var res2 = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "dispute"
        });
        Assert.Contains(res2.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED" && r.Severity == RiskSeverity.Critical);

        // 3. already_unresolved via FND-08
        var res3 = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-08"] = "already_unresolved"
        });
        Assert.Contains(res3.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED" && r.Severity == RiskSeverity.Critical);
    }

    [Fact(DisplayName = "3.2 [FND_DEPARTED_UNRESOLVED] resolved, clean, formal_only и solo inactive не создают FND_DEPARTED_UNRESOLVED")]
    public void FND_Departed_Unresolved_Negative_Regression()
    {
        // resolved / clean
        var resClean = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "departed_clean"
        });
        Assert.DoesNotContain(resClean.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED");

        // formal_only
        var resFormal = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "formal_only"
        });
        Assert.DoesNotContain(resFormal.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED");

        // inactiveExists alone without unresolved departure
        var resInactive = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "inactive_exist",
            ["FND-C03"] = "none",
            ["FND-08"] = "full"
        });
        Assert.DoesNotContain(resInactive.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED");
    }
}
