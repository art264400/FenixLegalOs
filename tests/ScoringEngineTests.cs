using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly string _tempDbPath;

    public ScoringEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
    }

    [Fact]
    public void Deadlock_50_50_Should_Trigger_Critical_Risk()
    {
        // Arrange: 2 founders, verbal agreements, dispute on equity, no deadlock exit mechanism
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "dispute",
            ["FND-C04"] = "none",
            ["FND-01"] = "active_conflict",
            ["FND-02"] = "dispute",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "none",
            ["COR-C01"] = "none"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall <= 40, $"Expected very low overall score for critical conflict & deadlock, got: {result.Overall}");
        Assert.True(result.CriticalCount >= 1, $"Expected at least 1 critical risk, got {result.CriticalCount}");
        
        var hasDeadlockRisk = result.Risks.Any(r => 
            r.Severity is "CRITICAL" or "HIGH" &&
            (r.Code.Contains("FOUNDERS", StringComparison.OrdinalIgnoreCase) || 
             r.Title.Contains("доли", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("тупик", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("основател", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasDeadlockRisk, "Expected risk related to co-founders/deadlock to be present");
    }

    [Fact]
    public void Solo_Founder_Should_Mark_Founders_Section_Applicable_Or_Skipped()
    {
        // Arrange: Single founder
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "kz_llp",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 0);
        // For solo founder, co-founder deadlock risks should NOT be triggered
        var hasCoFounderDeadlock = result.Risks.Any(r => r.Code == "R_FOUNDERS_EQUITY_UNFIXED" && r.Severity == "CRITICAL");
        Assert.False(hasCoFounderDeadlock, "Solo founder should not trigger co-founder equity conflict risk");
    }

    [Fact]
    public void All_Best_Practices_Should_Produce_High_Score()
    {
        // Arrange: Fully compliant co-founders with signed SHA, vesting, deadlock exit
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "3",
            ["FND-C03"] = "none",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "mechanism",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 75, $"Expected overall score >= 75, got {result.Overall}");
        Assert.Equal(0, result.CriticalCount);
    }
}
