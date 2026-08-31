using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace FenixLegalOs.Tests;

public class ScenarioArtifactsGenerator
{
    private readonly ScoringEngine _engine;
    private readonly TypstPdfService _pdfService;
    private readonly AiReportService _aiReportService;
    private readonly string _outputDir;

    public ScenarioArtifactsGenerator()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_scenario_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        var repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repository);

        var testEnv = new TestEnv();
        _aiReportService = new AiReportService(inMemoryConfig);
        _pdfService = new TypstPdfService(testEnv, _aiReportService);

        _outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output", "scenarios");
        Directory.CreateDirectory(_outputDir);
    }

    [Fact]
    public async Task GenerateAllFourScenarios()
    {
        var scenarios = new Dictionary<string, (string Title, Dictionary<string, object> Answers)>
        {
            ["healthy"] = ("Aurora HealthTech (Healthy)", CreateHealthyPreset()),
            ["medium"] = ("Nova SaaS (Medium Risk)", CreateMediumPreset()),
            ["severe"] = ("Titan Cyber (Severe IP Blocker & Deadlock)", CreateSeverePreset()),
            ["investment_blocker_heavy"] = ("Apex Venture (Investment Blocker Heavy)", CreateInvestmentBlockerHeavyPreset())
        };

        foreach (var (key, (title, answers)) in scenarios)
        {
            Console.WriteLine($"[SCENARIO-START] {key}");
            var result = _engine.ComputeResult(answers);
            var facts = FactNormalizer.NormalizeFacts(answers);
            var reportCtx = ReportEngine.AssembleReportContext(result, facts, $"session_{key}", title);

            // Generate sanitized narratives
            var rawNarratives = await _aiReportService.GenerateReportNarrativesAsync(reportCtx);
            var sanitizedNarratives = ReportQualityGate.ValidateAndSanitize(rawNarratives, reportCtx);

            // Apply narratives to reportCtx
            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.ProjectProfileNarrative))
                reportCtx.Profile.ConfigurationNarrative = sanitizedNarratives.ProjectProfileNarrative;

            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.ExecutiveConclusion))
                reportCtx.ExecutiveConclusion = sanitizedNarratives.ExecutiveConclusion;

            foreach (var top in reportCtx.TopFindings)
            {
                if (sanitizedNarratives.RootCauseSummaries.TryGetValue(top.RootCauseCode, out var sum) ||
                    sanitizedNarratives.RootCauseSummaries.TryGetValue(top.FindingCode, out sum))
                {
                    top.ShortSummary = sum;
                }
            }

            foreach (var action in reportCtx.ActionPlan)
            {
                if (sanitizedNarratives.ActionNarratives.TryGetValue(action.ActionId, out var aNarrative))
                {
                    if (!string.IsNullOrWhiteSpace(aNarrative.WhyNow)) action.WhyNow = aNarrative.WhyNow;
                    if (!string.IsNullOrWhiteSpace(aNarrative.ExpectedResult)) action.ExpectedResult = aNarrative.ExpectedResult;
                }
            }

            if (!string.IsNullOrWhiteSpace(sanitizedNarratives.FenixLawRecommendation))
                reportCtx.FenixLaw.SummaryText = sanitizedNarratives.FenixLawRecommendation;

            // 1. Save JSON artifact
            var jsonPath = Path.Combine(_outputDir, $"{key}_report.json");
            var jsonDto = new
            {
                scenario = key,
                title,
                overallScore = reportCtx.Overall.Score,
                scoreBand = reportCtx.Overall.Band,
                levelTitle = reportCtx.Overall.LevelTitle,
                narratives = sanitizedNarratives,
                reportContext = reportCtx
            };

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(jsonDto, jsonOptions));

            // 2. Save PDF artifact
            var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, $"session_{key}", title);
            if (pdfBytes != null)
            {
                var pdfPath = Path.Combine(_outputDir, $"{key}_report.pdf");
                await File.WriteAllBytesAsync(pdfPath, pdfBytes);
                Console.WriteLine($"[GENERATED] {key}: JSON -> {jsonPath} | PDF ({pdfBytes.Length} bytes) -> {pdfPath}");
            }
            else
            {
                Console.WriteLine($"[GENERATED] {key}: JSON -> {jsonPath} | PDF compilation skipped/null");
            }
        }
    }

    private class TestEnv : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot"));
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    public static Dictionary<string, object> CreateHealthyPreset() => new()
    {
        ["FND-C01"] = "3",
        ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 30, ["founder_3"] = 20 }),
        ["FND-C03"] = "none",
        ["FND-C04"] = "signed",
        ["FND-01"] = "none",
        ["FND-02"] = "written",
        ["FND-03"] = "aligned",
        ["FND-04"] = "registered",
        ["FND-05"] = "vesting",
        ["FND-05A"] = "defined",
        ["FND-06"] = "written",
        ["FND-06A"] = "different_thresholds",
        ["FND-07"] = "full",
        ["FND-08"] = "full",
        ["FND-09"] = "documented",
        ["FND-10"] = "none",
        ["FND-11"] = "aligned",
        ["COR-C01"] = "multiple",
        ["COR-C02A"] = "aifc",
        ["COR-C02B"] = "2",
        ["COR-C02C"] = JsonSerializer.SerializeToElement(new[]
        {
            new Dictionary<string, object> { ["index"] = 2, ["jurisdiction"] = "kz", ["roles"] = new[] { "clients", "payments" } }
        }),
        ["COR-01"] = "match",
        ["COR-02"] = "complete",
        ["COR-03"] = "documented_included",
        ["COR-04"] = "complete",
        ["COR-04A"] = "yes",
        ["COR-05"] = "systematic",
        ["COR-06"] = "clear_limits",
        ["COR-07_GROUP"] = "aligned",
        ["COR-08"] = "organized",
        ["COR-T01"] = "none",
        ["IP-01"] = "ready",
        ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design", "brand" }),
        ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders", "employees" }),
        ["IP-04"] = "all",
        ["IP-05"] = "assigned",
        ["IP-06"] = "all",
        ["IP-10"] = "no",
        ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "employees" }),
        ["TEAM-02"] = "6_10",
        ["TEAM-03"] = "all",
        ["TEAM-06"] = "clear",
        ["PROD-01"] = "regular",
        ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
        ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
        ["PROD-04"] = "current",
        ["PROD-05"] = "yes",
        ["PROD-06"] = "clear",
        ["PROD-10"] = "subscription",
        ["PROD-14"] = "self_service",
        ["DATA-01"] = "yes",
        ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact", "account" }),
        ["DATA-03"] = "yes",
        ["DATA-04"] = JsonSerializer.SerializeToElement(new[] { "company" }),
        ["DATA-05"] = "clear",
        ["DATA-06"] = "organized",
        ["AI-01"] = "no",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "clients" }),
        ["CONTRACT-02"] = "always",
        ["CONTRACT-03"] = "clear",
        ["CONTRACT-04"] = "clear",
        ["CONTRACT-05"] = "clear",
        ["CONTRACT-06"] = "custom",
        ["CONTRACT-07"] = "reviewed",
        ["CONTRACT-08"] = "no",
        ["INVEST-01"] = "searching",
        ["INVEST-02"] = "formal",
        ["INVEST-02A"] = "yes",
        ["INVEST-03"] = "exact",
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

    public static Dictionary<string, object> CreateMediumPreset() => new()
    {
        ["FND-C01"] = "2",
        ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 60, ["founder_2"] = 40 }),
        ["FND-C03"] = "none",
        ["FND-C04"] = "none",
        ["FND-01"] = "none",
        ["FND-02"] = "written",
        ["FND-03"] = "aligned",
        ["FND-04"] = "registered",
        ["FND-05"] = "not_discussed",
        ["FND-05A"] = "none",
        ["FND-06"] = "none",
        ["FND-06A"] = "majority",
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
        ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design" }),
        ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders" }),
        ["IP-04"] = "some",
        ["IP-05"] = "agreed",
        ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["PROD-01"] = "first",
        ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
        ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
        ["PROD-04"] = "template",
        ["PROD-05"] = "template_unchecked",
        ["DATA-01"] = "no",
        ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["AI-01"] = "no",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "clients" }),
        ["CONTRACT-02"] = "some_in_messages",
        ["CONTRACT-03"] = "mostly",
        ["CONTRACT-04"] = "mostly",
        ["CONTRACT-05"] = "mostly",
        ["CONTRACT-06"] = "adapted",
        ["CONTRACT-07"] = "sometimes",
        ["CONTRACT-08"] = "noticeable",
        ["INVEST-01"] = "none",
        ["INVEST-02"] = "no"
    };

    public static Dictionary<string, object> CreateSeverePreset() => new()
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
        ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["AI-01"] = "no",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "clients" }),
        ["CONTRACT-02"] = "material_informal",
        ["CONTRACT-03"] = "generic",
        ["CONTRACT-04"] = "case",
        ["CONTRACT-05"] = "weak",
        ["CONTRACT-06"] = "templates",
        ["CONTRACT-07"] = "often",
        ["CONTRACT-08"] = "material",
        ["CONTRACT-08A"] = "serious",
        ["INVEST-01"] = "none",
        ["INVEST-02"] = "no"
    };

    public static Dictionary<string, object> CreateInvestmentBlockerHeavyPreset() => new()
    {
        ["FND-C01"] = "2",
        ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 }),
        ["FND-C03"] = "dispute",
        ["FND-C04"] = "none",
        ["FND-01"] = "active_conflict",
        ["FND-02"] = "disputed",
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
        ["COR-01"] = "conflict",
        ["COR-02"] = "fragmented",
        ["COR-03"] = "none",
        ["COR-04"] = "none",
        ["IP-01"] = "ready",
        ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "technology" }),
        ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "contractors", "former" }),
        ["IP-04"] = "none",
        ["IP-05"] = "dispute",
        ["IP-07"] = "missing_all",
        ["IP-08"] = "dispute",
        ["IP-10"] = "not_reviewed",
        ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "freelancers" }),
        ["TEAM-02"] = "1_2",
        ["TEAM-03"] = "many_missing",
        ["PROD-01"] = "first",
        ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
        ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
        ["PROD-04"] = "none",
        ["DATA-01"] = "no",
        ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "none" }),
        ["AI-01"] = "no",
        ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "clients" }),
        ["CONTRACT-02"] = "mostly_informal",
        ["CONTRACT-03"] = "generic",
        ["CONTRACT-04"] = "case",
        ["CONTRACT-05"] = "weak",
        ["CONTRACT-06"] = "copied",
        ["CONTRACT-07"] = "often",
        ["CONTRACT-08"] = "near_total",
        ["CONTRACT-08A"] = "serious",
        ["INVEST-01"] = "searching",
        ["INVEST-02"] = "informal",
        ["INVEST-02A"] = "no",
        ["INVEST-03"] = "none",
        ["INVEST-04"] = "no",
        ["INVEST-05"] = "none",
        ["INVEST-06"] = "no",
        ["INVEST-07"] = "none",
        ["INVEST-08"] = "hard",
        ["INVEST-09"] = "missing",
        ["INVEST-10"] = "material_unresolved",
        ["INVEST-11"] = "none"
    };
}
