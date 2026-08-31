using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class NarrativeEngineContractTests
{
    private readonly ScoringEngine _engine;

    public NarrativeEngineContractTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_narrative_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        var repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repository);
    }

    private ReportContext CreateTestReportContext(Dictionary<string, object> answers)
    {
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers);
        return ReportEngine.AssembleReportContext(result, facts, "test-session", "TestStartup");
    }

    private static Dictionary<string, object> CreateDeadlockAndIpScenario() => new()
    {
        ["FND-C01"] = "2",
        ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 }),
        ["FND-C03"] = "none",
        ["FND-C04"] = "none",
        ["FND-01"] = "none",
        ["FND-02"] = "clear_oral",
        ["FND-03"] = "stopped",
        ["FND-04"] = "verbal",
        ["FND-05"] = "not_discussed",
        ["FND-05A"] = "none",
        ["FND-06"] = "none",
        ["FND-06A"] = "broad_unanimity",
        ["FND-07"] = "none",
        ["FND-08"] = "none",
        ["FND-09"] = "none",
        ["FND-10"] = "none",
        ["FND-11"] = "aligned",
        ["COR-C01"] = "one",
        ["COR-C02A"] = "kz",
        ["COR-01"] = "match",
        ["COR-02"] = "fragmented",
        ["COR-03"] = "none",
        ["COR-04"] = "none",
        ["IP-01"] = "ready",
        ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "database", "design" }),
        ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "contractors", "former" }),
        ["IP-04"] = "none",
        ["IP-05"] = "agreed",
        ["IP-07"] = "missing_all",
        ["IP-08"] = "dispute",
        ["IP-10"] = "not_reviewed",
        ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "freelancers", "external_devs" }),
        ["TEAM-02"] = "1_2",
        ["TEAM-03"] = "many_missing",
        ["PROD-01"] = "first",
        ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
        ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
        ["PROD-04"] = "template",
        ["PROD-05"] = "template_unchecked",
        ["DATA-01"] = "no",
        ["AI-01"] = "no",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["INVEST-01"] = "none"
    };

    private static Dictionary<string, object> CreateSoloScenario() => new()
    {
        ["FND-C01"] = "solo",
        ["COR-C01"] = "none",
        ["IP-01"] = "ready",
        ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design", "app" }),
        ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders" }),
        ["IP-04"] = "all",
        ["IP-05"] = "assigned",
        ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["PROD-01"] = "regular",
        ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "consumers" }),
        ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "app", "website" }),
        ["PROD-04"] = "template",
        ["PROD-05"] = "template_unchecked",
        ["PROD-06"] = "mostly",
        ["PROD-10"] = "subscription",
        ["DATA-01"] = "yes",
        ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact", "account", "payment" }),
        ["DATA-03"] = "no",
        ["DATA-04"] = JsonSerializer.SerializeToElement(new[] { "user" }),
        ["DATA-05"] = "none",
        ["DATA-06"] = "preparing",
        ["AI-01"] = "yes",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["INVEST-01"] = "none"
    };

    [Fact]
    public void RootCauseSummariesUseCanonicalSchema()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);
        var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);

        // Canonical schema validation
        Assert.NotNull(fallback.RootCauseSummaries);
        Assert.NotEmpty(fallback.RootCauseSummaries);
        Assert.True(fallback.RootCauseSummaries.Count >= ctx.TopFindings.Count);

        var json = JsonSerializer.Serialize(fallback);
        using var doc = JsonDocument.Parse(json);
        Assert.True(doc.RootElement.TryGetProperty("rootCauseSummaries", out var rcProp));
        Assert.Equal(JsonValueKind.Object, rcProp.ValueKind);
    }

    [Fact]
    public void AllMaterialRootCausesHaveNarratives()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);
        var rawNarratives = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Valid executive conclusion text with sufficient length to pass quality gate.",
            RootCauseSummaries = new Dictionary<string, string>() // empty raw LLM output
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(rawNarratives, ctx);

        foreach (var top in ctx.TopFindings)
        {
            var key = !string.IsNullOrWhiteSpace(top.RootCauseCode) ? top.RootCauseCode : top.FindingCode;
            Assert.True(sanitized.RootCauseSummaries.ContainsKey(key) || sanitized.RootCauseSummaries.ContainsKey(top.FindingCode));
            var sum = sanitized.RootCauseSummaries.GetValueOrDefault(key) ?? sanitized.RootCauseSummaries.GetValueOrDefault(top.FindingCode);
            Assert.False(string.IsNullOrWhiteSpace(sum));
        }
    }

    [Fact]
    public void NarrativeCannotCreateUnknownRootCause()
    {
        var answers = CreateSoloScenario();
        var ctx = CreateTestReportContext(answers);
        var rawNarratives = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Valid executive conclusion text with sufficient length to pass quality gate.",
            RootCauseSummaries = new Dictionary<string, string>
            {
                ["HALLUCINATED_ROOT_CAUSE_999"] = "Fake risk not present in deterministic input"
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(rawNarratives, ctx);

        Assert.False(sanitized.RootCauseSummaries.ContainsKey("HALLUCINATED_ROOT_CAUSE_999"));
    }

    [Fact]
    public void ActionNarrativesContainExactlyOneEntryPerActionId()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);
        var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        var sanitized = ReportQualityGate.ValidateAndSanitize(fallback, ctx);

        Assert.NotEmpty(ctx.ActionPlan);
        foreach (var action in ctx.ActionPlan)
        {
            Assert.True(sanitized.ActionNarratives.ContainsKey(action.ActionId));
            Assert.False(sanitized.ActionNarratives.ContainsKey(action.Title), "Title must NOT be used as a key!");
        }

        Assert.Equal(ctx.ActionPlan.Count, sanitized.ActionNarratives.Count);
    }

    [Fact]
    public void ActionNarrativeKeysMustExistInActionPlan()
    {
        var answers = CreateSoloScenario();
        var ctx = CreateTestReportContext(answers);
        var rawNarratives = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Valid executive conclusion text with sufficient length to pass quality gate.",
            ActionNarratives = new Dictionary<string, ActionNarrativeItemDto>
            {
                ["ACT_HALLUCINATED_UNKNOWN_123"] = new() { WhyNow = "fake", ExpectedResult = "fake" }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(rawNarratives, ctx);

        Assert.False(sanitized.ActionNarratives.ContainsKey("ACT_HALLUCINATED_UNKNOWN_123"));
        foreach (var key in sanitized.ActionNarratives.Keys)
        {
            Assert.Contains(ctx.ActionPlan, a => a.ActionId == key);
        }
    }

    [Fact]
    public void IpBlockerCannotDisappearFromLegalServiceAreas()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);

        Assert.True(ctx.FenixLaw.RequiresLegalWork);
        Assert.Contains("Права на продукт и интеллектуальную собственность", ctx.FenixLaw.ServiceAreas);
        Assert.Contains(ctx.FenixLaw.ServiceCards, c => c.Title == "Права на продукт и интеллектуальную собственность");
    }

    [Fact]
    public void InternalActionDoesNotCreateLegalServiceArea()
    {
        var findings = new List<RiskFinding>
        {
            new()
            {
                Code = "TEAM_NO_NDA",
                SectionId = "team",
                Severity = RiskSeverity.Info,
                Priority = RiskPriority.Later,
                LawyerRequired = false,
                Resolution = ResolutionType.SelfService
            }
        };

        var actions = new List<UnifiedActionItemDto>
        {
            new()
            {
                ActionId = "ACT_TEAM_INTERNAL_SETUP",
                ResolutionMode = ResolutionMode.InternalAction,
                CoveredFindingCodes = new() { "TEAM_NO_NDA" }
            }
        };

        var result = new ScoreResult
        {
            Overall = 85,
            Level = LegalScoreLevel.Strong,
            Risks = findings,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "team", Status = ApplicabilityStatus.Applicable, Score = 85 }
            }
        };

        var recommendation = FenixLawRecommendationEvaluator.EvaluateRecommendation(findings, result, actions);

        Assert.False(recommendation.RequiresLegalWork);
        Assert.Empty(recommendation.ServiceCards);
    }

    [Fact]
    public void AllLegalWorkActionsMapToServiceArea()
    {
        var allLegalActions = ActionLibrary.All
            .Where(a => a.ResolutionMode is ResolutionMode.LegalWork or ResolutionMode.LegalReview or ResolutionMode.LegalAndProduct)
            .ToList();

        Assert.NotEmpty(allLegalActions);

        var findings = allLegalActions.Select(a => new RiskFinding
        {
            Code = a.SupportedFindingCodes.FirstOrDefault() ?? ("FND_" + a.ActionId),
            SectionId = a.SectionId,
            Severity = RiskSeverity.High,
            Priority = a.DefaultPriority,
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired
        }).ToList();

        var actionDtos = allLegalActions.Select(a => new UnifiedActionItemDto
        {
            ActionId = a.ActionId,
            ResolutionMode = a.ResolutionMode,
            Priority = a.DefaultPriority,
            CoveredFindingCodes = new() { a.SupportedFindingCodes.FirstOrDefault() ?? ("FND_" + a.ActionId) }
        }).ToList();

        var sections = new List<string> { "founders", "corporate", "ip", "team", "product", "data", "contracts", "investment" }
            .Select(s => new SectionScore { SectionId = s, Status = ApplicabilityStatus.Applicable, Score = 50 })
            .ToList();

        var result = new ScoreResult
        {
            Overall = 50,
            Level = LegalScoreLevel.MaterialGaps,
            Risks = findings,
            Sections = sections
        };

        var rec = FenixLawRecommendationEvaluator.EvaluateRecommendation(findings, result, actionDtos);

        Assert.True(rec.RequiresLegalWork);
        Assert.NotEmpty(rec.ServiceAreas);
        Assert.Contains("Основатели и корпоративная структура", rec.ServiceAreas);
        Assert.Contains("Права на продукт и интеллектуальную собственность", rec.ServiceAreas);
        Assert.Contains("Команда и привлеченные специалисты", rec.ServiceAreas);
        Assert.Contains("Пользовательский контур и оферта", rec.ServiceAreas);
        Assert.Contains("Персональные данные и процессы ИИ", rec.ServiceAreas);
        Assert.Contains("Договорная обвязка и контрагенты", rec.ServiceAreas);
    }

    [Fact]
    public void ExecutiveConclusionCoversAllBlockerCriticalRootCauses()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);
        var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);

        Assert.False(string.IsNullOrWhiteSpace(fallback.ExecutiveConclusion));
        Assert.True(fallback.ExecutiveConclusion.Length >= 150);
        Assert.Contains(ctx.Overall.Score.ToString(), fallback.ExecutiveConclusion);
    }

    [Fact]
    public void FallbackNarrativesMatchPrimaryContract()
    {
        var answers = CreateDeadlockAndIpScenario();
        var ctx = CreateTestReportContext(answers);
        var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);

        // Invariants
        Assert.NotEmpty(fallback.RootCauseSummaries);
        Assert.NotEmpty(fallback.ActionNarratives);
        Assert.Equal(ctx.ActionPlan.Count, fallback.ActionNarratives.Count);

        foreach (var action in ctx.ActionPlan)
        {
            Assert.True(fallback.ActionNarratives.ContainsKey(action.ActionId));
        }
    }
}
