using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Investment;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentScoreExplanationPdfTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly string _root;
    private readonly ScoringEngine _scoring;
    private readonly TypstPdfService _pdf;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public InvestmentScoreExplanationPdfTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"investment-score-explanation-{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _databasePath,
            ["AiSettings:StrictLlm"] = "false"
        }).Build();
        var database = new DbInitializer(config);
        _connectionString = database.ConnectionString;
        database.Initialize();
        _scoring = new ScoringEngine(new QuestionRepository(database));
        _root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        _pdf = new TypstPdfService(new TestWebHostEnvironment(_root), new AiReportService(config, new NoNetworkLlmClient()));
    }

    [Fact]
    public void UserAnswers_With65Points_ExplainActualInvestmentFactorsAndPreparedItems()
    {
        var answers = UserAnswers();
        Assert.Equal(77, answers.Count);
        var result = _scoring.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers);
        var context = ReportEngine.AssembleReportContext(result, facts, "user-score-65", "Проверка · инвестиционная оценка 65");
        var investment = Assert.IsType<InvestmentReadinessReportDto>(context.InvestmentReadiness);
        Assert.Equal(65, result.Sections.Single(section => section.SectionId == "investment").Score);
        Assert.Equal(65, result.InvestmentReadiness.ReadinessScore);
        Assert.Equal(65, investment.BaseScore);
        Assert.Equal(65, investment.ReadinessScore);
        Assert.Empty(result.InvestmentReadiness.Blockers);
        Assert.Empty(investment.BlockerTitles);
        Assert.Equal(0, investment.UnresolvedBlockersCount);
        Assert.True(investment.HasFactorBreakdown);
        Assert.Equal(new[] { "future_ownership", "dd_documents", "dilution" }, investment.MainScoreReasons.Select(reason => reason.DimensionId).ToArray());
        Assert.All(investment.MainScoreReasons, reason =>
        {
            Assert.NotEmpty(reason.Title);
            Assert.NotEmpty(reason.Explanation);
            Assert.True(reason.Impact > 0);
            Assert.True(reason.Weight > 0);
            Assert.InRange(reason.Score, 0, 99);
        });
        Assert.Equal(investment.MainScoreReasons.OrderByDescending(reason => reason.Impact).Select(reason => reason.DimensionId).ToArray(), investment.MainScoreReasons.Select(reason => reason.DimensionId).ToArray());
        Assert.NotEmpty(investment.AdditionalScoreReasons);
        Assert.Equal(2, investment.PreparedItems.Count);
        Assert.Contains(investment.PreparedItems, item => item.Contains("показател", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(investment.PreparedItems, item => item.Contains("презентац", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(investment.PreparedItems, item => result.Strengths.Contains(item));
        Assert.Contains("65", investment.ScoreExplanation);
        Assert.NotEmpty(investment.BlockerEffectExplanation);
        AssertScoreExplanationMarkup(context);
    }

    [Theory]
    [InlineData(78, 59)]
    [InlineData(40, 40)]
    public void CanonicalBlockerCap_PreservesBaseAndActualReadinessInExplanation(int baseScore, int expectedReadiness)
    {
        var (result, facts) = SyntheticBlockerScenario(baseScore);
        Assert.Equal(expectedReadiness, result.InvestmentReadiness.ReadinessScore);
        Assert.Single(result.InvestmentReadiness.Blockers);
        var context = ReportEngine.AssembleReportContext(result, facts, $"blocker-base-{baseScore}");
        var investment = Assert.IsType<InvestmentReadinessReportDto>(context.InvestmentReadiness);
        Assert.Equal(baseScore, investment.BaseScore);
        Assert.Equal(expectedReadiness, investment.ReadinessScore);
        Assert.Equal(1, investment.UnresolvedBlockersCount);
        Assert.Single(investment.BlockerTitles);
        Assert.True(investment.HasFactorBreakdown);
        Assert.Contains(baseScore.ToString(), investment.ScoreExplanation);
        Assert.NotEmpty(investment.BlockerEffectExplanation);
        AssertScoreExplanationMarkup(context);
    }

    [Fact]
    public async Task GenerateUserAndBlockerExplanationPdfs_WithoutExternalModelCalls()
    {
        var userAnswers = UserAnswers();
        var userResult = _scoring.ComputeResult(userAnswers);
        var userFacts = FactNormalizer.NormalizeFacts(userAnswers);
        var capped = SyntheticBlockerScenario(78);
        var belowCap = SyntheticBlockerScenario(40);
        var cases = new[]
        {
            (Id: "user-base65-readiness65", Company: "Проверка · ответы пользователя · 65 баллов", Result: userResult, Facts: userFacts, ExpectedBase: 65, ExpectedReady: 65),
            (Id: "blocker-base78-readiness59", Company: "Проверка · один блокер · 78 → 59 баллов", Result: capped.Result, Facts: capped.Facts, ExpectedBase: 78, ExpectedReady: 59),
            (Id: "blocker-base40-readiness40", Company: "Проверка · оценка ниже ограничения · 40 баллов", Result: belowCap.Result, Facts: belowCap.Facts, ExpectedBase: 40, ExpectedReady: 40)
        };
        var directory = Path.Combine(_root, "tmp", "pdfs", "investment-score-explanation");
        Directory.CreateDirectory(directory);
        foreach (var scenario in cases)
        {
            var context = ReportEngine.AssembleReportContext(scenario.Result, scenario.Facts, scenario.Id, scenario.Company);
            var investment = Assert.IsType<InvestmentReadinessReportDto>(context.InvestmentReadiness);
            Assert.Equal(scenario.ExpectedBase, investment.BaseScore);
            Assert.Equal(scenario.ExpectedReady, investment.ReadinessScore);
            AssertScoreExplanationMarkup(context);
            var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
            var sanitized = ReportQualityGate.ValidateAndSanitize(narratives, context);
            var pdf = await _pdf.GeneratePdfAsync(scenario.Result, scenario.Facts, scenario.Id, scenario.Company, rawNarratives: narratives);
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 10000, $"PDF too small for {scenario.Id}");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Take(5).ToArray()));
            await File.WriteAllBytesAsync(Path.Combine(directory, scenario.Id + ".pdf"), pdf);
            await File.WriteAllTextAsync(Path.Combine(directory, scenario.Id + ".typ"), _pdf.BuildTypstMarkup(context));
            await File.WriteAllTextAsync(Path.Combine(directory, scenario.Id + ".json"), JsonSerializer.Serialize(new { answers = scenario.Id.StartsWith("user-") ? userAnswers : null, result = scenario.Result, facts = scenario.Facts, context, narratives, sanitized }, JsonOptions));
        }
    }

    private void AssertScoreExplanationMarkup(ReportContext context)
    {
        var markup = _pdf.BuildTypstMarkup(context);
        Assert.Contains("ЧТО СНИЗИЛО БАЗОВУЮ ОЦЕНКУ", markup);
        Assert.Contains("ЧТО УЖЕ ПОДГОТОВЛЕНО", markup);
        var investment = Assert.IsType<InvestmentReadinessReportDto>(context.InvestmentReadiness);
        Assert.All(investment.MainScoreReasons, reason => Assert.Contains(reason.Title, markup));
    }

    private (ScoreResult Result, SharedFactStore Facts) SyntheticBlockerScenario(int baseScore)
    {
        var baseline = _scoring.ComputeResult(UserAnswers());
        var baselineSection = baseline.Sections.Single(section => section.SectionId == "investment");
        var definition = DataBank.Risks.Single(risk => risk.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        var findings = new List<RiskFinding>
        {
            new()
            {
                Code = definition.Code,
                RootCauseGroup = definition.RootCauseGroup,
                SectionId = definition.SectionId,
                Severity = RiskSeverity.Critical,
                Priority = RiskPriority.BeforeRound,
                Title = definition.Title,
                Finding = definition.Finding,
                WhyItMatters = definition.WhyItMatters,
                Recommendation = definition.Recommendation,
                Recommendations = definition.Recommendations.ToList(),
                AffectedDimensions = definition.AffectedDimensions.ToList(),
                Basis = new() { new() { QuestionId = "IP-04", AnswerId = "none" } },
                LawyerRequired = definition.LawyerRequired,
                Resolution = definition.Resolution,
                ResolutionMode = definition.ResolutionMode,
                ServiceCode = definition.ServiceCode
            }
        };
        var facts = FactNormalizer.NormalizeFacts(UserAnswers());
        var result = new ScoreResult
        {
            Overall = 60,
            Confidence = 100,
            CriticalCount = 1,
            Risks = findings,
            Sections = DataBank.Sections.Select(section => new SectionScore
            {
                SectionId = section.Id,
                Title = section.Title,
                Weight = section.Weight,
                Score = section.Id == "investment" ? baseScore : section.Id == "ip" ? 50 : null,
                Status = section.Id is "investment" or "ip" ? ApplicabilityStatus.Applicable : ApplicabilityStatus.NotApplicable,
                Dimensions = section.Id == "investment" ? baselineSection.Dimensions.Select(dimension => new DimensionScore
                {
                    DimensionId = dimension.DimensionId,
                    Score = baseScore,
                    Weight = dimension.Weight,
                    IsApplicable = dimension.IsApplicable
                }).ToList() : new()
            }).ToList(),
            InvestmentReadiness = InvestmentReadinessCalculator.Calculate(baseScore, findings, facts)
        };
        return (result, facts);
    }

    // Embedded snapshot of all 77 supplied answers. No attachment file is required to rerun this regression.
    private static Dictionary<string, object> UserAnswers() => new()
    {
        ["FND-C01"] = "2",
        ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 51, ["founder_2"] = 49 },
        ["FND-C03"] = "none",
        ["FND-C04"] = "none",
        ["FND-01"] = "none",
        ["FND-02"] = "clear_oral",
        ["FND-03"] = "temporary_part_time",
        ["FND-04"] = "verbal",
        ["FND-05"] = "not_discussed",
        ["FND-05A"] = "none",
        ["FND-06"] = "verbal",
        ["FND-06A"] = "undefined",
        ["FND-07"] = "only_agree",
        ["FND-08"] = "none",
        ["FND-09"] = "unknown",
        ["FND-10"] = "unrelated",
        ["FND-11"] = "not_discussed",
        ["COR-C01"] = "none",
        ["IP-01"] = "ready",
        ["IP-02"] = new[] { "code", "web", "database", "own_data", "brand", "domain" },
        ["IP-03"] = new[] { "founders" },
        ["IP-04"] = "none",
        ["IP-05"] = "agreed",
        ["IP-10"] = "no",
        ["IP-11"] = "yes",
        ["IP-11A"] = "no",
        ["IP-12"] = "unchecked",
        ["IP-13"] = "one_founder",
        ["IP-14"] = "founder",
        ["IP-15"] = "unknown",
        ["TEAM-01"] = new[] { "none" },
        ["PROD-01"] = "prelaunch",
        ["PROD-04"] = "current",
        ["PROD-05"] = "yes",
        ["PROD-06"] = "clear",
        ["PROD-07"] = "company",
        ["PROD-10"] = "one_off",
        ["PROD-11"] = "clear",
        ["PROD-12"] = "published",
        ["PROD-18"] = "no",
        ["PROD-21"] = "multiple",
        ["PROD-21A"] = "no",
        ["PROD-22"] = new[] { "none" },
        ["DATA-01"] = "yes",
        ["DATA-02"] = new[] { "contact", "account" },
        ["DATA-03"] = "no",
        ["DATA-04"] = new[] { "user" },
        ["DATA-05"] = "main_only",
        ["DATA-06"] = "yes",
        ["DATA-07"] = "yes",
        ["DATA-08"] = new[] { "core_service" },
        ["DATA-10"] = "yes",
        ["DATA-10A"] = "yes",
        ["DATA-11"] = "known_not_reviewed",
        ["DATA-12"] = "multiple",
        ["DATA-13"] = "yes",
        ["DATA-14"] = "yes",
        ["DATA-15"] = "defined",
        ["DATA-16"] = "manual",
        ["DATA-17"] = "yes",
        ["AI-01"] = "external",
        ["AI-02"] = "none",
        ["AI-04"] = "not_specific",
        ["AI-07"] = "no",
        ["CONTRACT-01"] = new[] { "none" },
        ["INVEST-01"] = "3_6",
        ["INVEST-02"] = "no",
        ["INVEST-03"] = "current_only",
        ["INVEST-04"] = "rough",
        ["INVEST-05"] = "amount_rough",
        ["INVEST-06"] = "rough",
        ["INVEST-06A"] = "3_6",
        ["INVEST-07"] = "simple",
        ["INVEST-08"] = "yes",
        ["INVEST-09"] = "scattered",
        ["INVEST-10"] = "none",
        ["INVEST-11"] = "current",
    };

    public void Dispose()
    {
        using (var connection = new SqliteConnection(_connectionString))
            SqliteConnection.ClearPool(connection);
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private sealed class TestWebHostEnvironment(string root) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class NoNetworkLlmClient : ILlmNarrativeClient
    {
        public bool IsConfigured => false;
        public Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(ModuleNarrativeRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF explanation regression must never call a model.");
        public Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(ActionBatchRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF explanation regression must never call a model.");
        public Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(ExecutiveSynthesisRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF explanation regression must never call a model.");
    }
}
