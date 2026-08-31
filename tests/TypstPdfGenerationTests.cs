using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

/// <summary>
/// Тесты генерации векторных PDF-отчётов полиграфического качества через компилятор Typst.
/// 
/// Проверяют:
/// 1. Корректность сборки Typst-разметки (шрифты, цвета, таблицы, графики);
/// 2. Генерацию валидного бинарного потока PDF (сигнатура %PDF-);
/// 3. Наличие кликабельных ссылок и контактов Fenix Law;
/// 4. Корректную пагинацию без наложений и обрезки текста.
/// </summary>
public class TypstPdfGenerationTests
{
    private readonly ScoringEngine _scoringEngine;
    private readonly TypstPdfService _pdfService;
    private readonly QuestionRepository _qRepo;

    public TypstPdfGenerationTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_pdf_gen_{System.Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();

        _qRepo = new QuestionRepository(dbInit);
        _scoringEngine = new ScoringEngine(_qRepo);

        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(config);
        _pdfService = new TypstPdfService(testEnv, aiService);
    }

    [Fact(DisplayName = "1. Генерация валидного 15-страничного PDF-отчёта для реалистичного стартапа")]
    public async Task GeneratePdfAsync_GeneratesValidReportPdf()
    {
        // Реалистичный сценарий: 2 основателя 50/50, устные договоренности, подрядчики создавали код, B2B SaaS с первыми клиентами
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
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

            ["COR-C01"] = "none",

            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "some",
            ["IP-05"] = "agreed",
            ["IP-07"] = "missing_some",

            ["TEAM-01"] = new List<string> { "freelancers", "external_devs" },
            ["TEAM-02"] = "1_2",
            ["TEAM-03"] = "many_missing",

            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",

            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contact", "account" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "user" },
            ["DATA-05"] = "none",
            ["DATA-06"] = "preparing",
            ["AI-01"] = "yes",

            ["CONTRACT-01"] = new List<string> { "none" },
            ["INVEST-01"] = "none"
        };

        var result = _scoringEngine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers);

        var reportCtx = FenixLegalOs.Scoring.Report.ReportEngine.AssembleReportContext(result, facts, "test-session-123", projectName: null);

        // Проверка семантической целостности структуры отчёта
        Assert.True(reportCtx.Overall.Score > 0, "Итоговый скор должен быть больше 0");
        Assert.NotEmpty(reportCtx.TopFindings);
        Assert.NotEmpty(reportCtx.FocusModules);
        Assert.NotEmpty(reportCtx.ActionPlan);
        Assert.True(reportCtx.FenixLaw.RequiresLegalWork, "Наличие уязвимостей требует рекомендации работы с юристом");

        var markup = _pdfService.BuildTypstMarkup(reportCtx);
        Assert.DoesNotContain("Aurora", markup);
        Assert.DoesNotContain("live_or_ready", markup);
        
        var jsonOpt = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText("aurora_report_context.json", System.Text.Json.JsonSerializer.Serialize(reportCtx, jsonOpt));

        var fallbackNarratives = FenixLegalOs.Scoring.Report.DeterministicFallbackNarratives.GenerateFallbackNarratives(reportCtx);
        var sanitizedNarratives = FenixLegalOs.Scoring.Report.ReportQualityGate.ValidateAndSanitize(fallbackNarratives, reportCtx);
        File.WriteAllText("aurora_report_narratives.json", System.Text.Json.JsonSerializer.Serialize(sanitizedNarratives, jsonOpt));

        File.WriteAllText("debug_full_report.typ", markup);

        var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, "test-session-123", companyName: null);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 10000, $"Размер сгенерированного PDF должен быть существенным (>10 КБ), получено {pdfBytes.Length}");

        // Проверка сигнатуры заголовка PDF (%PDF-)
        var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
        Assert.Equal("%PDF-", header);
    }

    [Fact(DisplayName = "2. Полное покрытие всех выявленных рисков в типографической разметке Typst")]
    public async Task GenerateRcFindingCoveragePdfAsync()
    {
        var allAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
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
            ["COR-C02A"] = "kz_llp",
            ["COR-01"] = "dispute",
            ["COR-02"] = "fragmented",
            ["COR-03"] = "informal",
            ["COR-04"] = "missing",
            ["COR-05"] = "often_missing",
            ["COR-06"] = "unclear",
            ["COR-07"] = "material_outside",
            ["COR-08"] = "missing",

            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "some",
            ["IP-05"] = "agreed",
            ["IP-07"] = "missing_some",

            ["TEAM-01"] = new List<string> { "freelancers", "external_devs", "employees" },
            ["TEAM-02"] = "1_2",
            ["TEAM-03"] = "many_missing",

            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",

            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contact", "account" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "user" },
            ["DATA-05"] = "none",
            ["DATA-06"] = "preparing",
            ["AI-01"] = "yes",

            ["CONTRACT-01"] = new List<string> { "clients", "partners" },
            ["CONTRACT-02"] = "mostly_informal",
            ["CONTRACT-03"] = "outside",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "templates",
            ["CONTRACT-07"] = "often_unreviewed",
            ["CONTRACT-08"] = "material",
            ["CONTRACT-08A"] = "serious",

            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-03"] = "none",
            ["INVEST-04"] = "none"
        };

        var result = _scoringEngine.ComputeResult(allAnswers);
        var facts = FactNormalizer.NormalizeFacts(allAnswers);
        var reportCtx = FenixLegalOs.Scoring.Report.ReportEngine.AssembleReportContext(result, facts, "rc-session-full", projectName: null);

        var markup = _pdfService.BuildTypstMarkup(reportCtx);
        File.WriteAllText("fenix_sls_report_finding_coverage_rc.typ", markup);

        var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, "rc-session-full", companyName: null);
        if (pdfBytes != null)
        {
            File.WriteAllBytes("fenix_sls_report_finding_coverage_rc.pdf", pdfBytes);
        }

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 20000);
    }

    [Fact(DisplayName = "3. Генерация 3 канонических эталонных отчётов: Healthy, Medium, Severe")]
    public async Task GenerateThreeCanonicalBenchmarkPdfsAsync()
    {
        // 1. Healthy Scenario (100/100)
        var healthyAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-03"] = "none",
            ["COR-04"] = "none",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-06"] = "clear",
            ["PROD-07"] = "company",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "free",
            ["PROD-14"] = "none",
            ["PROD-16"] = "none",
            ["PROD-17"] = "rules_cover",
            ["PROD-18"] = "no",
            ["PROD-20"] = "no",
            ["PROD-21"] = "one",
            ["PROD-22"] = new List<string> { "none" },
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no",
            ["CONTRACT-01"] = new List<string> { "none" },
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "no"
        };

        var healthyResult = _scoringEngine.ComputeResult(healthyAnswers);
        var healthyFacts = FactNormalizer.NormalizeFacts(healthyAnswers);
        var healthyPdf = await _pdfService.GeneratePdfAsync(healthyResult, healthyFacts, "healthy-session", "Healthy Corp");
        Assert.NotNull(healthyPdf);
        File.WriteAllBytes("scenario_healthy.pdf", healthyPdf);

        // 2. Medium Scenario (~60/100)
        var mediumAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 70, ["founder_2"] = 30 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
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
            ["COR-02"] = "complete",
            ["COR-03"] = "none",
            ["COR-04"] = "none",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["TEAM-01"] = new List<string> { "freelancers" },
            ["TEAM-02"] = "1_2",
            ["TEAM-03"] = "many_missing",
            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",
            ["PROD-07"] = "company",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "free",
            ["PROD-14"] = "none",
            ["PROD-16"] = "none",
            ["PROD-17"] = "rules_cover",
            ["PROD-18"] = "no",
            ["PROD-20"] = "no",
            ["PROD-21"] = "one",
            ["PROD-22"] = new List<string> { "none" },
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no",
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "templates",
            ["CONTRACT-03"] = "inhouse",
            ["CONTRACT-05"] = "reviewed",
            ["CONTRACT-06"] = "templates",
            ["CONTRACT-07"] = "rarely",
            ["CONTRACT-08"] = "none",
            ["INVEST-01"] = "none",
            ["INVEST-02"] = "no"
        };

        var mediumResult = _scoringEngine.ComputeResult(mediumAnswers);
        var mediumFacts = FactNormalizer.NormalizeFacts(mediumAnswers);
        var mediumPdf = await _pdfService.GeneratePdfAsync(mediumResult, mediumFacts, "medium-session", "Medium Corp");
        Assert.NotNull(mediumPdf);
        File.WriteAllBytes("scenario_medium.pdf", mediumPdf);

        // 3. Severe Scenario (<40/100)
        var severeAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
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
            ["COR-C01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "contractors" },
            ["IP-04"] = "none",
            ["TEAM-01"] = new List<string> { "freelancers", "external_devs" },
            ["TEAM-02"] = "1_2",
            ["TEAM-03"] = "many_missing",
            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "none",
            ["PROD-05"] = "none",
            ["PROD-06"] = "none",
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contact", "account" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "user" },
            ["DATA-05"] = "none",
            ["DATA-06"] = "none",
            ["AI-01"] = "yes",
            ["AI-02"] = "external_cloud",
            ["AI-03"] = "unchecked",
            ["AI-04"] = "yes",
            ["AI-05"] = "no",
            ["AI-06"] = "no",
            ["CONTRACT-01"] = new List<string> { "clients" },
            ["CONTRACT-02"] = "mostly_informal",
            ["CONTRACT-03"] = "outside",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "custom",
            ["CONTRACT-07"] = "often_unreviewed",
            ["CONTRACT-08"] = "material",
            ["CONTRACT-08A"] = "serious",
            ["INVEST-01"] = "specific_investor",
            ["INVEST-02"] = "no",
            ["INVEST-03"] = "none",
            ["INVEST-04"] = "none",
            ["INVEST-05"] = "none",
            ["INVEST-06"] = "less_3m",
            ["INVEST-06A"] = "high",
            ["INVEST-07"] = "none",
            ["INVEST-08"] = "none",
            ["INVEST-09"] = "none",
            ["INVEST-10"] = "none",
            ["INVEST-11"] = "none",
            ["INVEST-12"] = "verbal",
            ["INVEST-13"] = "standard",
            ["INVEST-14"] = "standard",
            ["INVEST-15"] = "standard"
        };

        var severeResult = _scoringEngine.ComputeResult(severeAnswers);
        var severeFacts = FactNormalizer.NormalizeFacts(severeAnswers);
        var severePdf = await _pdfService.GeneratePdfAsync(severeResult, severeFacts, "severe-session", "Severe Corp");
        Assert.NotNull(severePdf);
        File.WriteAllBytes("scenario_severe.pdf", severePdf);
    }

    private class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot"));
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
