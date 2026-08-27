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
        var foundersSec = result.Sections.FirstOrDefault(s => s.SectionId == "founders");
        Assert.NotNull(foundersSec);
        Assert.Equal("APPLICABLE", foundersSec.Status);
        Assert.Equal(100, foundersSec.Score);
    }

    [Fact]
    public void Solo_Founder_With_Multiple_Entities_Should_Score_Correctly()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "kz",
            ["COR-C02B"] = "3",
            ["COR-01"] = "dispute",
            ["COR-02"] = "none",
            ["COR-03"] = "unclear_terms",
            ["COR-04"] = "missing",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-08"] = "organized",
            ["COR-07_GROUP"] = "minor_exceptions",
            ["COR-T01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.True(result.Overall > 0, $"Expected Overall > 0, got {result.Overall}");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("APPLICABLE", corpSec.Status);
        Assert.NotNull(corpSec.Score);
    }

    [Fact]
    public void Pre_Incorporation_Idea_Stage_Should_Not_Trigger_No_Entity_Risk()
    {
        // Solo founder, no company, but no active commercial activity
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Risks, r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("N_A", corpSec.Status);
    }

    [Fact]
    public void Pre_Incorporation_With_Active_Revenue_Or_Team_Should_Trigger_COR_NO_ENTITY_FOR_ACTIVITY()
    {
        // No company, but operates with team/revenue
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["TEAM-C01"] = "contractors",
            ["REV-01"] = "active"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        Assert.NotNull(finding);
        Assert.Equal("HIGH", finding.Severity);
        Assert.Equal("ENTITY_ALIGNMENT", finding.RootCauseGroup);
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

    [Fact]
    public void Single_Company_Builds_Single_Narrative_And_Calculates_COR07()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, facts["company.entityCount"]);
        Assert.Equal(false, facts["company.groupStructure"]);
        Assert.Equal("kz", facts["company.primaryJurisdiction"]);
        
        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("Казахстан", narrative);
        Assert.Contains("одну компанию", narrative);
    }

    [Fact]
    public void Group_Structure_Builds_Detailed_Narrative_With_Roles()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "aifc",
            ["COR-C02B"] = "2",
            ["COR-C02C"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["jurisdiction"] = "kz",
                    ["roles"] = new List<string> { "clients", "payments" }
                }
            },
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07_GROUP"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, facts["company.entityCount"]);
        Assert.Equal(true, facts["company.groupStructure"]);
        Assert.Equal("aifc", facts["company.primaryJurisdiction"]);

        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("МФЦА", narrative);
        Assert.Contains("2 компаний", narrative);
    }
}
