using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using Microsoft.Data.Sqlite;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentReportScenarioTests : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly ScoringEngine _scoring;
    private readonly TypstPdfService _pdf;
    private readonly string _root;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public InvestmentReportScenarioTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"investment-report-scenarios-{Guid.NewGuid():N}.db");
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
        _pdf = new TypstPdfService(new TestWebHostEnvironment(_root),
            new AiReportService(config, new NoNetworkLlmClient()));
    }

    public static IEnumerable<object[]> InvestmentScenarios()
    {
        yield return Row("none", "no", "none", false, false, false, false, false, "Нет, пока не планируем");
        yield return Row("none", "formal", "none", true, false, false, false, true, "Нет, пока не планируем");
        yield return Row("none", "partial", "none", true, false, false, false, true, "Нет, пока не планируем");
        yield return Row("none", "unknown", "none", true, false, false, false, true, "Нет, пока не планируем");
        yield return Row("possible_year", "no", "within_12m", true, false, true, false, false, "Возможно в течение ближайшего года");
        yield return Row("6_12", "no", "6_12m", true, true, false, false, false, "Планируем в ближайшие 6–12 месяцев");
        yield return Row("3_6", "no", "3_6m", true, true, false, true, false, "Планируем в ближайшие 3–6 месяцев");
        yield return Row("searching", "no", "active_search", true, true, false, true, false, "Уже ищем инвесторов");
        yield return Row("specific", "no", "specific_investor", true, true, false, true, false, "Уже обсуждаем условия с конкретным инвестором");
        yield return Row("terms", "no", "terms_received", true, true, false, true, false, "Уже получили предложение с условиями сделки");
        yield return Row(null, "no", null, false, false, false, false, false, "Не указано");
        yield return Row(null, "formal", null, true, false, false, false, true, "Не указано");
    }

    private static object[] Row(string? answer, string prior, string? timing, bool applicable,
        bool planned, bool possible, bool close, bool hasPrior, string label) =>
        new object[] { answer!, prior, timing!, applicable, planned, possible, close, hasPrior, label };

    [Theory]
    [MemberData(nameof(InvestmentScenarios))]
    public void QuestionnaireToReport_PreservesDeclaredInvestmentSituationAndLegalFindings(
        string? answer, string priorAnswer, string? timing, bool applicable, bool planned,
        bool possible, bool close, bool hasPrior, string expectedLabel)
    {
        var answers = Answers(answer, priorAnswer);
        var result = _scoring.ComputeResult(answers);
        var originalResult = JsonSerializer.Serialize(result, JsonOptions);
        var facts = FactNormalizer.NormalizeFacts(answers);
        var context = ReportEngine.AssembleReportContext(result, facts, "investment-scenario", "Проверка инвестиционного контекста");
        var investment = context.InvestmentReadiness;
        var compact = ContextSelector.ExtractCompactSharedContext(context);
        var compactInvestment = ContextSelector.ExtractCompactInvestmentReadiness(context);
        var fallback = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
        var sanitized = ReportQualityGate.ValidateAndSanitize(fallback, context);
        var markup = _pdf.BuildTypstMarkup(context);

        Assert.Equal(applicable, result.Sections.Single(section => section.SectionId == "investment").Status == ApplicabilityStatus.Applicable);
        Assert.Equal(applicable, result.InvestmentReadiness.Applicable);
        Assert.Equal(applicable, investment.IsApplicable);
        Assert.Equal(result.InvestmentReadiness.ReadinessScore ?? 0, investment.ReadinessScore);
        Assert.Equal(timing, investment.InvestmentTiming);
        Assert.Equal(planned, investment.NewRoundPlanned);
        Assert.Equal(possible, investment.NewRoundPossible);
        Assert.Equal(close, investment.RoundIsCloseOrActive);
        Assert.Equal(hasPrior, investment.HasPriorInvestment);
        Assert.Equal(expectedLabel, compact.Fundraising);
        Assert.Equal(timing, compact.InvestmentTiming);
        Assert.Equal(planned, compact.NewRoundPlanned);
        Assert.Equal(possible, compact.NewRoundPossible);
        Assert.Equal(hasPrior, compact.HasPriorInvestment);
        Assert.Equal(investment.PriorInvestmentStatus, compact.PriorInvestmentStatus);
        if (applicable)
        {
            Assert.NotNull(compactInvestment);
            Assert.Equal(result.InvestmentReadiness.ReadinessScore, compactInvestment.ReadinessScore);
            Assert.Equal(planned, compactInvestment.NewRoundPlanned);
            Assert.Equal(possible, compactInvestment.NewRoundPossible);
            Assert.Equal(hasPrior, compactInvestment.HasPriorInvestment);
        }
        else
        {
            Assert.Null(compactInvestment);
        }

        Assert.Equal(result.Overall, context.Overall.Score);
        Assert.Equal(result.Risks.Select(f => (f.Code, f.Severity, f.Priority)).OrderBy(f => f.Code).ToArray(),
            context.AllFindings.Select(f => (f.Code, f.Severity, f.Priority)).OrderBy(f => f.Code).ToArray());
        Assert.Contains(result.Risks, risk => risk.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.Contains(context.AllFindings, risk => risk.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.Contains(context.FocusModules, module => module.SectionId == "product" && module.Findings.Count > 0);
        Assert.Contains(context.ActionPlan, action => action.CoveredFindingCodes.Contains("IP_PRODUCT_RIGHTS_UNCONFIRMED"));
        Assert.NotEmpty(sanitized.ExecutiveConclusion);
        Assert.Equal(originalResult, JsonSerializer.Serialize(result, JsonOptions));

        if (!close)
        {
            Assert.Empty(result.InvestmentReadiness.Blockers);
            Assert.Empty(investment.BlockerTitles);
            Assert.Empty(investment.CrossModuleBlockers);
            Assert.Equal(0, investment.UnresolvedBlockersCount);
            Assert.DoesNotContain(ContextSelector.SelectCrossModuleSignals(context, "ip"), signal => signal.Contains("Due Diligence", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(ContextSelector.SelectCrossModuleSignals(context, "investment"), signal => signal.Contains("Due Diligence", StringComparison.OrdinalIgnoreCase));
        }
        else
        {
            Assert.NotEmpty(result.InvestmentReadiness.Blockers);
            Assert.Equal(result.InvestmentReadiness.Blockers.OrderBy(title => title).ToArray(), investment.BlockerTitles.OrderBy(title => title).ToArray());
            Assert.True(investment.ReadinessScore <= 59);
            Assert.Contains(ContextSelector.SelectCrossModuleSignals(context, "ip"), signal => signal.Contains("Due Diligence", StringComparison.OrdinalIgnoreCase));
        }

        if (!planned)
        {
            var operationalProse = string.Join("\n", context.FocusModules.Select(module => module.PracticalMeaning)
                .Concat(context.ActionPlan.Select(action => action.PriorityGroup + " " + action.WhyNow + " " + action.ExpectedResult))
                .Concat(new[] { sanitized.ExecutiveConclusion })
                .Concat(sanitized.ModuleNarratives.Values.Select(module => module.PracticalMeaning)));
            foreach (var unsupported in new[] { "до первого раунда", "сорвет раунд", "затянет раунд", "снижает инвестиционную привлекательность", "при проверке потенциальным инвестором", "до инвестиционного раунда", "блокер Due Diligence" })
                Assert.False(operationalProse.Contains(unsupported, StringComparison.OrdinalIgnoreCase), $"Without a planned round, found unsupported consequence: {unsupported}");
            Assert.DoesNotMatch(@"#section-header\([^\r\n]*""ГОТОВНОСТЬ К ИНВЕСТИЦИЯМ""[^\r\n]*<sec-investment>", markup);
        }
        else
        {
            Assert.Matches(@"#section-header\([^\r\n]*""ГОТОВНОСТЬ К ИНВЕСТИЦИЯМ""[^\r\n]*<sec-investment>", markup);
        }
        if (applicable && !planned && hasPrior)
            Assert.Contains("ИНВЕСТИЦИОННАЯ ИСТОРИЯ И ОБЯЗАТЕЛЬСТВА", markup);
        if (possible)
        {
            Assert.Contains("ИНВЕСТИЦИОННЫЕ ПЛАНЫ", markup);
            Assert.Contains("возможность", investment.RoundVerdict);
        }
        if (!applicable)
            Assert.DoesNotContain("<sec-investment>", markup);
        if (priorAnswer == "unknown")
        {
            Assert.Equal("unknown", investment.PriorInvestmentStatus);
            Assert.Equal("unknown", compact.PriorInvestmentStatus);
            Assert.Contains("не определены", investment.SummaryDescription);
            Assert.Contains("не определены", markup);
        }
    }

    [Fact]
    public void PriorInvestmentWithoutNewRound_DoesNotApplyIndependent45PointReportCap()
    {
        var answers = Answers("none", "formal");
        var result = _scoring.ComputeResult(answers);
        var context = ReportEngine.AssembleReportContext(result, FactNormalizer.NormalizeFacts(answers), "prior-investment");

        Assert.Contains(result.Risks, risk => risk.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.Equal(100, result.Sections.Single(section => section.SectionId == "investment").Score);
        Assert.Equal(100, result.InvestmentReadiness.ReadinessScore);
        Assert.Equal(100, context.InvestmentReadiness.ReadinessScore);
        Assert.Empty(context.InvestmentReadiness.CrossModuleBlockers);
    }

    [Fact]
    public async Task GenerateFiveInvestmentScenarioPdfs_WithDeterministicNarrativesAndNoNetwork()
    {
        var directory = Path.Combine(_root, "tmp", "pdfs", "investment-scenarios");
        Directory.CreateDirectory(directory);
        var cases = new[]
        {
            (Id: "none-no-prior", Timing: "none", Prior: "no", Company: "Проверка · без инвестиционных планов"),
            (Id: "none-unclear-prior", Timing: "none", Prior: "unknown", Company: "Проверка · неопределенная инвестиционная история"),
            (Id: "possible-year", Timing: "possible_year", Prior: "no", Company: "Проверка · инвестиции возможны в течение года"),
            (Id: "planned-six-twelve", Timing: "6_12", Prior: "no", Company: "Проверка · раунд через 6–12 месяцев"),
            (Id: "active-search", Timing: "searching", Prior: "no", Company: "Проверка · активный поиск инвесторов")
        };
        foreach (var scenario in cases)
        {
            var answers = Answers(scenario.Timing, scenario.Prior);
            var result = _scoring.ComputeResult(answers);
            var facts = FactNormalizer.NormalizeFacts(answers);
            var context = ReportEngine.AssembleReportContext(result, facts, scenario.Id, scenario.Company);
            var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
            var pdf = await _pdf.GeneratePdfAsync(result, facts, scenario.Id, scenario.Company, rawNarratives: narratives);
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 10000, $"Generated PDF too small for {scenario.Id}: {pdf.Length} bytes");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Take(5).ToArray()));
            await File.WriteAllBytesAsync(Path.Combine(directory, scenario.Id + ".pdf"), pdf);
            await File.WriteAllTextAsync(Path.Combine(directory, scenario.Id + ".json"), JsonSerializer.Serialize(new { answers, result, context, narratives }, JsonOptions));
            await File.WriteAllTextAsync(Path.Combine(directory, scenario.Id + ".typ"), _pdf.BuildTypstMarkup(context));
        }
    }

    private static Dictionary<string, object> Answers(string? timing, string prior)
    {
        var answers = new Dictionary<string, object>
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
            ["IP-02"] = new[] { "code" },
            ["IP-03"] = new[] { "founders" },
            ["IP-04"] = "none",
            ["TEAM-01"] = new[] { "none" },
            ["PROD-01"] = "first",
            ["PROD-02"] = new[] { "companies" },
            ["PROD-03"] = new[] { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
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
            ["PROD-22"] = new[] { "none" },
            ["DATA-01"] = "no",
            ["DATA-02"] = new[] { "none" },
            ["AI-01"] = "no",
            ["CONTRACT-01"] = new[] { "none" },
            ["INVEST-02"] = prior
        };
        if (timing != null) answers["INVEST-01"] = timing;
        if (prior != "no") answers["INVEST-02A"] = prior == "formal" ? "yes" : "unknown";
        if (prior is "formal" or "partial") answers["INVEST-03"] = "exact";
        if (timing != null && timing != "none")
        {
            answers["INVEST-03"] = "exact";
            answers["INVEST-04"] = "yes";
            answers["INVEST-05"] = "clear";
            answers["INVEST-06"] = "regular";
            answers["INVEST-06A"] = "gt12";
            answers["INVEST-07"] = "current";
            answers["INVEST-08"] = "yes";
            answers["INVEST-09"] = "organized";
            answers["INVEST-10"] = "none";
            answers["INVEST-11"] = "current";
        }
        if (timing is "specific" or "terms")
        {
            answers["INVEST-12"] = "yes";
            answers["INVEST-13"] = "reserved_only";
            answers["INVEST-14"] = "yes";
            answers["INVEST-15"] = "specialist";
        }
        return answers;
    }

    public void Dispose()
    {
        // Release only this fixture's pool; other test classes may use SQLite concurrently.
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
        public Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(ModuleNarrativeRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Scenario verification must never call a model.");
        public Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(ActionBatchRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Scenario verification must never call a model.");
        public Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(ExecutiveSynthesisRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Scenario verification must never call a model.");
    }
}
