using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class StructuralCleanupTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public StructuralCleanupTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_structural_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1. [Authoritative Score] Неотвеченный диагностический вопрос не создает score.{questionId}")]
    public void Unanswered_Diagnostic_Question_Produces_No_Score()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-01"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(1.0, facts.GetQuestionScore("FND-01"));
        Assert.Null(facts.GetQuestionScore("FND-03"));
        Assert.Null(facts.GetQuestionScore("FND-05"));
        Assert.False(facts.Facts.ContainsKey("score.FND-03"));
    }

    [Fact(DisplayName = "2. [Authoritative Score] Отвеченный диагностический вопрос получает ровно один канонический балл")]
    public void Answered_Diagnostic_Question_Produces_Exact_Canonical_Score()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = "minor", // score = 0.75
            ["FND-03"] = "below_expected", // score = 0.25
            ["FND-05"] = "repurchase" // score = 0.90
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(0.75, facts.GetQuestionScore("FND-01"));
        Assert.Equal(0.25, facts.GetQuestionScore("FND-03"));
        Assert.Equal(0.90, facts.GetQuestionScore("FND-05"));
    }

    [Fact(DisplayName = "3. [Authoritative Score] Generic infrastructure пишет question scores без участия module normalizer")]
    public void Generic_FactNormalizer_Writes_Scores_Without_Module_Normalizers()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-02"] = "written", // score = 1.0
            ["COR-01"] = "match",   // score = 1.0
            ["IP-04"] = "all"       // score = 1.0
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(1.0, facts.GetQuestionScore("FND-02"));
        Assert.Equal(1.0, facts.GetQuestionScore("COR-01"));
        Assert.Equal(1.0, facts.GetQuestionScore("IP-04"));
    }

    [Fact(DisplayName = "4. [StrongAreas] Неприменимый (N/A) dimension никогда не становится Strong Area")]
    public void NA_Dimension_Is_Never_A_Strong_Area()
    {
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "roles", Score = 100, Weight = 8, IsApplicable = false },
            new() { DimensionId = "governance", Score = 90, Weight = 12, IsApplicable = true }
        };
        var findings = new List<RiskFinding>();

        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(dimScores, findings);
        Assert.Single(strongAreas);
        Assert.Equal("Корпоративное управление и прозрачный порядок принятия решений", strongAreas[0]);
    }

    [Fact(DisplayName = "5. [StrongAreas] score >= 80 без severe finding создает Strong Area")]
    public void Score_Gte_80_Without_Severe_Finding_Yields_Strong_Area()
    {
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "roles", Score = 80, Weight = 8, IsApplicable = true }
        };
        var findings = new List<RiskFinding>();

        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(dimScores, findings);
        Assert.Single(strongAreas);
        Assert.Equal("Четкое разделение ролей и зон ответственности", strongAreas[0]);
    }

    [Fact(DisplayName = "6. [StrongAreas] score >= 80 с HIGH/CRITICAL finding блокирует Strong Area")]
    public void Score_Gte_80_With_High_Finding_Blocks_Strong_Area()
    {
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "roles", Score = 85, Weight = 8, IsApplicable = true }
        };
        var findings = new List<RiskFinding>
        {
            new() { Code = "FND_ROLE_AMBIGUITY", Severity = RiskSeverity.High, AffectedDimensions = new() { "roles" } }
        };

        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(dimScores, findings);
        Assert.Empty(strongAreas);
    }

    [Fact(DisplayName = "7. [StrongAreas] MEDIUM finding не блокирует Strong Area")]
    public void Medium_Finding_Does_Not_Block_Strong_Area()
    {
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "equity_clarity", Score = 85, Weight = 12, IsApplicable = true }
        };
        var findings = new List<RiskFinding>
        {
            new() { Code = "FND_EQUITY_NOT_FORMALIZED", Severity = RiskSeverity.Medium, AffectedDimensions = new() { "equity_clarity" } }
        };

        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(dimScores, findings);
        Assert.Single(strongAreas);
        Assert.Equal("Распределение долей основателей (Соответствие долей)", strongAreas[0]);
    }

    [Fact(DisplayName = "8. [Invariant] Каждый HIGH / CRITICAL / BLOCKER RiskDefinition имеет валидный AffectedDimensions")]
    public void Every_Severe_RiskDefinition_Has_Valid_Affected_Dimensions()
    {
        var severeRisks = DataBank.Risks
            .Where(r => r.Severity is RiskSeverity.High or RiskSeverity.Critical or RiskSeverity.Blocker)
            .ToList();

        Assert.NotEmpty(severeRisks);
        foreach (var risk in severeRisks)
        {
            foreach (var dim in risk.AffectedDimensions)
            {
                Assert.True(DataBank.Dimensions.Any(d => d.Id == dim),
                    $"Risk '{risk.Code}' references non-existent dimension '{dim}'.");
            }
        }
    }

    [Fact(DisplayName = "9. [Invariant] StrongAreasCalculator не содержит module-specific литералов FND_, COR_, IP_, TEAM_")]
    public void StrongAreasCalculator_Source_Contains_Zero_Business_Literals()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Scoring", "Core", "StrongAreasCalculator.cs");
        var fullPath = Path.GetFullPath(path);
        Assert.True(File.Exists(fullPath), $"File not found at {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("FND_", content);
        Assert.DoesNotContain("COR_", content);
        Assert.DoesNotContain("IP_", content);
        Assert.DoesNotContain("TEAM_", content);
    }

    [Fact(DisplayName = "10. [Deduplication] Strong Areas дедуплицируются по DimensionId без дублей в результате")]
    public void Strong_Areas_Deduplicates_By_DimensionId()
    {
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "governance", Score = 85, Weight = 12, IsApplicable = true },
            new() { DimensionId = "governance", Score = 90, Weight = 12, IsApplicable = true }
        };
        var findings = new List<RiskFinding>();

        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(dimScores, findings);
        Assert.Single(strongAreas);
    }
}
