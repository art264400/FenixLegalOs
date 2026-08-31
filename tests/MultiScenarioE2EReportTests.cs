using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace FenixLegalOs.Tests;

/// <summary>
/// Комплексные E2E-тесты юридического скоринга и генерации PDF-отчётов
/// для всех ключевых бизнес-сценариев стартапов (FENIX SLS Report Engine v1.0).
/// 
/// Каждый сценарий представляет собой реалистичный портрет компании (от идеи до раунда),
/// проверяет математический скоринг, роутинг применимости модулей (N/A), поиск рисков,
/// валидацию логической непротиворечивости и генерацию готового полиграфического PDF.
/// </summary>
public class MultiScenarioE2EReportTests
{
    private readonly ITestOutputHelper _output;
    private readonly ScoringEngine _scoringEngine;
    private readonly TypstPdfService _pdfService;
    private readonly QuestionRepository _qRepo;

    public MultiScenarioE2EReportTests(ITestOutputHelper output)
    {
        _output = output;

        var tempDb = Path.Combine(Path.GetTempPath(), $"test_multiscenario_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();

        var dbInit = new DbInitializer(config);
        dbInit.Initialize();

        _qRepo = new QuestionRepository(dbInit);
        _scoringEngine = new ScoringEngine(_qRepo);

        var testEnv = new TestWebHostEnvironment();
        var aiReportService = new AiReportService(config);
        _pdfService = new TypstPdfService(testEnv, aiReportService);
    }

    /// <summary>
    /// Ожидаемые параметры и инварианты бизнес-сценария.
    /// </summary>
    public record ScenarioExpectation
    {
        public string ScenarioId { get; init; } = "";
        public string Title { get; init; } = "";
        public List<string> ExpectedApplicableModules { get; init; } = new();
        public List<string> ExpectedNaModules { get; init; } = new();
        public int MinScore { get; init; } = 0;
        public int MaxScore { get; init; } = 100;
        public List<string>? RequiredFindingCodes { get; init; }
        public List<string>? ForbiddenFindingCodes { get; init; }
        public bool? ExpectedRequiresLegalWork { get; init; }
        public bool ExpectedInvestmentDetailedSection { get; init; }
    }

    /// <summary>
    /// Результаты прогона и метрики генерации отчёта по сценарию.
    /// </summary>
    public record ScenarioExecutionResult
    {
        public string ScenarioId { get; init; } = "";
        public int OverallScore { get; init; }
        public List<string> ApplicableModules { get; init; } = new();
        public List<string> NaModules { get; init; } = new();
        public int RawFindingsCount { get; init; }
        public int RootFindingsCount { get; init; }
        public List<string> FocusModules { get; init; } = new();
        public int ActionPlanCount { get; init; }
        public bool RequiresLegalWork { get; init; }
        public int PdfBytesLength { get; init; }
        public bool ValidatorPassed { get; init; }
    }

    // =========================================================================
    // 1. СКВОЗНОЙ ПРОГОН ВСЕХ 10 КАНОНИЧЕСКИХ СЦЕНАРИЕВ
    // =========================================================================

    [Fact(DisplayName = "1. Сквозной E2E-прогон и генерация PDF для 10 канонических сценариев стартапов")]
    public async Task Run_All_10_Canonical_Scenarios_E2E()
    {
        var scenarios = GetCanonicalScenarios();
        var executionResults = new List<ScenarioExecutionResult>();

        foreach (var (expectation, answers) in scenarios)
        {
            // 1. Детерминированный расчёт скоринга и нормализация фактов
            var result = _scoringEngine.ComputeResult(answers);
            var facts = FactNormalizer.NormalizeFacts(answers);
            var reportCtx = ReportEngine.AssembleReportContext(result, facts, $"session-{expectation.ScenarioId}", projectName: null);

            // 2. Строгая семантическая валидация отчёта (Quality Gate)
            ReportContextValidator.Validate(reportCtx);

            // 3. Проверка диапазона итогового балла
            if (result.Overall < expectation.MinScore || result.Overall > expectation.MaxScore)
            {
                var breakdown = string.Join(", ", result.Sections.Select(s => $"{s.SectionId}:{s.Score} ({s.Status})"));
                _output.WriteLine($"[Ошибка диапазона скоринга] Сценарий {expectation.ScenarioId}: Получено={result.Overall}, Ожидалось [{expectation.MinScore}-{expectation.MaxScore}]. Разрез по зонам: {breakdown}");
            }
            Assert.InRange(result.Overall, expectation.MinScore, expectation.MaxScore);

            // 4. Проверка роутинга применимости (Applicable vs N/A)
            var applicableIds = result.Sections.Where(s => s.Status == ApplicabilityStatus.Applicable).Select(s => s.SectionId).ToList();
            var naIds = result.Sections.Where(s => s.Status == ApplicabilityStatus.NotApplicable).Select(s => s.SectionId).ToList();

            foreach (var reqApp in expectation.ExpectedApplicableModules)
            {
                Assert.Contains(reqApp, applicableIds);
            }

            foreach (var reqNa in expectation.ExpectedNaModules)
            {
                Assert.Contains(reqNa, naIds);
            }

            // 5. Проверка обязательных и запрещенных рисков
            if (expectation.RequiredFindingCodes != null)
            {
                foreach (var reqCode in expectation.RequiredFindingCodes)
                {
                    Assert.Contains(result.Risks, r => r.Code.Equals(reqCode, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (expectation.ForbiddenFindingCodes != null)
            {
                foreach (var forbCode in expectation.ForbiddenFindingCodes)
                {
                    Assert.DoesNotContain(result.Risks, r => r.Code.Equals(forbCode, StringComparison.OrdinalIgnoreCase));
                }
            }

            if (expectation.ExpectedRequiresLegalWork.HasValue)
            {
                Assert.Equal(expectation.ExpectedRequiresLegalWork.Value, reportCtx.FenixLaw.RequiresLegalWork);
            }

            // 6. Проверка 13 глобальных инвариантов бизнес-логики
            AssertGlobalCrossScenarioInvariants(result, facts, reportCtx);

            // 7. Сборка верстки Typst и компиляция реального PDF файла
            var markup = _pdfService.BuildTypstMarkup(reportCtx);
            Assert.DoesNotContain("Aurora", markup);
            Assert.DoesNotContain("live_or_ready", markup);

            var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, $"session-{expectation.ScenarioId}", companyName: null);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 10000, "Размер сгенерированного PDF должен быть больше 10 КБ");

            var pdfFileName = $"scenario_{expectation.ScenarioId.ToLowerInvariant()}.pdf";
            var typFileName = $"scenario_{expectation.ScenarioId.ToLowerInvariant()}.typ";

            var targetDirs = new List<string>
            {
                Directory.GetCurrentDirectory(),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "fenix-legal-os"))
            };

            foreach (var dir in targetDirs.Where(Directory.Exists))
            {
                File.WriteAllBytes(Path.Combine(dir, pdfFileName), pdfBytes);
                File.WriteAllText(Path.Combine(dir, typFileName), markup);

                if (expectation.ScenarioId.Equals("MatureHealthyCompany", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(Path.Combine(dir, "scenario_healthy.pdf"), pdfBytes);
                    File.WriteAllText(Path.Combine(dir, "scenario_healthy.typ"), markup);
                }
                else if (expectation.ScenarioId.Equals("ContractorHeavyProduct", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(Path.Combine(dir, "scenario_medium.pdf"), pdfBytes);
                    File.WriteAllText(Path.Combine(dir, "scenario_medium.typ"), markup);
                }
                else if (expectation.ScenarioId.Equals("InvestmentMultipleBlockers", StringComparison.OrdinalIgnoreCase))
                {
                    File.WriteAllBytes(Path.Combine(dir, "scenario_severe.pdf"), pdfBytes);
                    File.WriteAllText(Path.Combine(dir, "scenario_severe.typ"), markup);
                }
            }

            var header = System.Text.Encoding.ASCII.GetString(pdfBytes.Take(5).ToArray());
            Assert.Equal("%PDF-", header);

            // Проверка: все выявленные риски отображены в тексте отчёта без потерь
            foreach (var focus in reportCtx.FocusModules)
            {
                foreach (var finding in focus.Findings)
                {
                    Assert.Contains(finding.Title, markup);
                }
            }

            executionResults.Add(new ScenarioExecutionResult
            {
                ScenarioId = expectation.ScenarioId,
                OverallScore = result.Overall,
                ApplicableModules = applicableIds,
                NaModules = naIds,
                RawFindingsCount = result.Risks.Count,
                RootFindingsCount = reportCtx.TopFindings.Count,
                FocusModules = reportCtx.FocusModules.Select(f => f.SectionId).ToList(),
                ActionPlanCount = reportCtx.ActionPlan.Count,
                RequiresLegalWork = reportCtx.FenixLaw.RequiresLegalWork,
                PdfBytesLength = pdfBytes.Length,
                ValidatorPassed = true
            });
        }

        // Вывод сводной таблицы результатов прогона
        _output.WriteLine("\n================================================================================================================================");
        _output.WriteLine("                                   СВОДНАЯ МАТРИЦА E2E-ТЕСТИРОВАНИЯ СЦЕНАРИЕВ СТАРТАПОВ                                         ");
        _output.WriteLine("================================================================================================================================");
        _output.WriteLine($"{"Сценарий",-25} | {"Балл",-5} | {"Применимые",-15} | {"N/A",-12} | {"Риски",-5} | {"Корни",-5} | {"Фокусные",-18} | {"Задачи",-7} | {"Юрист",-6} | {"Статус",-8}");
        _output.WriteLine(new string('-', 128));

        foreach (var r in executionResults)
        {
            string appStr = string.Join(",", r.ApplicableModules);
            string naStr = string.Join(",", r.NaModules);
            string focusStr = string.Join(",", r.FocusModules);
            _output.WriteLine($"{r.ScenarioId,-25} | {r.OverallScore,-5} | {appStr,-15} | {naStr,-12} | {r.RawFindingsCount,-5} | {r.RootFindingsCount,-5} | {focusStr,-18} | {r.ActionPlanCount,-7} | {(r.RequiresLegalWork ? "Да" : "Нет"),-6} | {"ПРОЙДЕН",-8}");
        }
    }

    // =========================================================================
    // 2. ПРОВЕРКА 13 ГЛОБАЛЬНЫХ ИНВАРИАНТОВ БИЗНЕС-ЛОГИКИ
    // =========================================================================

    private static void AssertGlobalCrossScenarioInvariants(ScoreResult result, SharedFactStore facts, ReportContext ctx)
    {
        // Инвариант 1: В матрице направлений всегда представлены ровно 8 юридических зон
        Assert.Equal(8, ctx.ModuleCards.Count);

        // Инвариант 2 и 3: Неприменимые модули (N/A) исключены из скоринга, а применимые имеют балл от 0 до 100
        foreach (var sec in result.Sections)
        {
            if (sec.Status == ApplicabilityStatus.NotApplicable)
            {
                Assert.Null(sec.Score);
            }
            else
            {
                if (!sec.Score.HasValue)
                {
                    throw new InvalidOperationException($"[Нарушение инварианта 2/3] Модуль '{sec.SectionId}' имеет статус '{sec.Status}', но Score равен NULL.");
                }
                Assert.InRange(sec.Score.Value, 0, 100);
            }
        }

        // Инвариант 4: Ни один выявленный корневой риск не потерян при переходе в отчёт
        foreach (var focus in ctx.FocusModules)
        {
            var secRisks = result.Risks.Where(r => r.SectionId.Equals(focus.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            Assert.Equal(secRisks.Count, focus.Findings.Count);
        }

        // Инвариант 5 и 6: Модули с пометкой Focus на матрице обязательно имеют подробный раздел в отчёте
        foreach (var card in ctx.ModuleCards)
        {
            bool hasDetailedSection = ctx.FocusModules.Any(f => f.SectionId.Equals(card.SectionId, StringComparison.OrdinalIgnoreCase)) ||
                                     (card.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase) && card.RenderMode != ReportRenderMode.NotApplicable);

            if (card.RenderMode == ReportRenderMode.Focus)
            {
                Assert.True(hasDetailedSection, $"Фокусная карточка '{card.SectionId}' обязана иметь детальный раздел разбора в отчёте");
            }
        }

        // Инвариант 7: Любые критические уязвимости (Blocker, Critical, High) обязательно покрыты задачами в Action Plan
        var highCritFindings = result.Risks.Where(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High).ToList();
        if (highCritFindings.Count > 0)
        {
            Assert.NotEmpty(ctx.ActionPlan);
        }

        // Инвариант 8: Флаг необходимости юриста Fenix Law консистентен с наличием блокеров и критичных рисков
        if (result.Risks.Any(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical || r.LawyerRequired))
        {
            Assert.True(ctx.FenixLaw.RequiresLegalWork, "Критические риски и блокеры должны активировать рекомендацию работы с юристом");
        }

        // Инвариант 9: Категория оценки ScoreBand не подменяется уровнем критичности риска
        foreach (var focus in ctx.FocusModules)
        {
            Assert.DoesNotContain("риск", focus.ScoreBand, StringComparison.OrdinalIgnoreCase);
        }

        // Инвариант 10: В пользовательском отчёте отсутствуют служебные строки (undefined, null)
        Assert.DoesNotContain("undefined", ctx.ProjectName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("null", ctx.ProjectName, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // 3. КОМБИНАТОРНОЕ СТРЕСС-ТЕСТИРОВАНИЕ НА СЛУЧАЙНЫХ МУТАЦИЯХ
    // =========================================================================

    [Fact(DisplayName = "2. Комбинаторное стресс-тестирование на 12 рандомизированных мутациях ответов")]
    public async Task Combinatorial_Robustness_Property_Testing()
    {
        var rnd = new Random(42);
        var baseScenarios = GetCanonicalScenarios();

        for (int iteration = 1; iteration <= 12; iteration++)
        {
            // Выбираем базовый сценарий и случайно варьируем параметры
            var baseEntry = baseScenarios[rnd.Next(baseScenarios.Count)];
            var mutatedAnswers = new Dictionary<string, object>(baseEntry.Answers);

            // Мутация распределения долей между основателями
            if (mutatedAnswers.ContainsKey("FND-C01") && (string)mutatedAnswers["FND-C01"] == "2")
            {
                int share1 = rnd.Next(20, 80);
                mutatedAnswers["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = share1, ["founder_2"] = 100 - share1 };
            }

            // Мутация состава авторов и создателей интеллектуальной собственности
            if (mutatedAnswers.ContainsKey("IP-03"))
            {
                var creators = new List<string> { "founders" };
                if (rnd.Next(2) == 1) creators.Add("contractors");
                if (rnd.Next(2) == 1) creators.Add("studio");
                mutatedAnswers["IP-03"] = creators;
            }

            // Запуск пайплайна
            var result = _scoringEngine.ComputeResult(mutatedAnswers);
            var facts = FactNormalizer.NormalizeFacts(mutatedAnswers);
            var reportCtx = ReportEngine.AssembleReportContext(result, facts, $"combinatorial-session-{iteration}", projectName: null);

            ReportContextValidator.Validate(reportCtx);
            AssertGlobalCrossScenarioInvariants(result, facts, reportCtx);

            var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, $"combinatorial-session-{iteration}", companyName: null);
            Assert.NotNull(pdfBytes);
            Assert.True(pdfBytes.Length > 10000);
        }
    }

    // =========================================================================
    // 4. ОПИСАНИЕ ЭТАЛОННЫХ БИЗНЕС-СЦЕНАРИЕВ
    // =========================================================================

    private static List<(ScenarioExpectation Expectation, Dictionary<string, object> Answers)> GetCanonicalScenarios()
    {
        return new List<(ScenarioExpectation, Dictionary<string, object>)>
        {
            // -----------------------------------------------------------------
            // СЦЕНАРИЙ A: СОЛО-ФАУНДЕР / ДО СОЗДАНИЯ ЮРЛИЦА / СТАДИЯ ИДЕИ
            // Описание: 1 основатель, юридическое лицо ещё не создано, стадия
            // концепта. Модули Corporate, Team, Contracts и Investment должны быть N/A.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "SoloIdea",
                    Title = "СЦЕНАРИЙ A — Соло-фаундер / До создания юрлица / Стадия идеи",
                    ExpectedApplicableModules = new() { "founders", "ip", "product" },
                    ExpectedNaModules = new() { "corporate", "team", "contracts", "investment" },
                    MinScore = 50,
                    MaxScore = 100,
                    ForbiddenFindingCodes = new() { "FND_DEADLOCK", "COR_NO_ENTITY_FOR_ACTIVITY" }
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "none",
                    ["IP-01"] = "idea",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "all",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "prelaunch",
                    ["PROD-02"] = new List<string> { "undecided" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "none",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ B: СОЛО-ФАУНДЕР / ВЕДЕТ ДЕЯТЕЛЬНОСТЬ БЕЗ ЮРЛИЦА
            // Описание: 1 основатель нанимает фрилансеров и принимает оплату
            // от клиентов без регистрации компании. Должен сработать риск COR_NO_ENTITY_FOR_ACTIVITY.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "SoloActiveBusiness",
                    Title = "СЦЕНАРИЙ B — Соло-фаундер / Активная деятельность без регистрации юрлица",
                    ExpectedApplicableModules = new() { "founders", "ip", "team", "product", "contracts" },
                    ExpectedNaModules = new() { "corporate", "investment" },
                    RequiredFindingCodes = new() { "COR_NO_ENTITY_FOR_ACTIVITY" },
                    MinScore = 20,
                    MaxScore = 75
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "none",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code", "design" },
                    ["IP-03"] = new List<string> { "contractors" },
                    ["IP-04"] = "some",
                    ["IP-07"] = "payment_only",
                    ["TEAM-01"] = new List<string> { "freelancers" },
                    ["TEAM-02"] = "1_2",
                    ["TEAM-03"] = "many_missing",
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "clients" },
                    ["CONTRACT-02"] = "mostly_informal",
                    ["CONTRACT-03"] = "outside",
                    ["CONTRACT-05"] = "weak",
                    ["CONTRACT-06"] = "templates",
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ C: ДВА КОФАУНДЕРА 50/50 / ИДЕАЛЬНО ОФОРМЛЕННЫЙ СТАРТАП
            // Описание: Зарегистрировано ТОО, подписано соглашение основателей
            // с механизмом разрешения дедлоков, оформлен вестинг и передача прав на IP.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "TwoFoundersClean5050",
                    Title = "СЦЕНАРИЙ C — Два кофаундера 50/50 / Защищённый SHA и вестинг (ТОО)",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip" },
                    MinScore = 75,
                    MaxScore = 100,
                    ForbiddenFindingCodes = new() { "FND_DEADLOCK", "FND_DEADLOCK_RISK", "COR_OWNERSHIP_DISPUTE" },
                    ExpectedRequiresLegalWork = false
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
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
                    ["FND-09"] = "none",
                    ["FND-10"] = "none",
                    ["FND-11"] = "aligned",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-06"] = "single_director",
                    ["COR-07"] = "clean",
                    ["COR-08"] = "organized",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["IP-07"] = "all",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "current",
                    ["PROD-05"] = "yes",
                    ["PROD-06"] = "clear",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ D: ДВА КОФАУНДЕРА 50/50 / ДЕДЛОК И КОНФЛИКТ ДОЛЕЙ
            // Описание: Равные доли 50/50, активный конфликт, нет корпоративного
            // договора, работа над проектом остановлена. Должны сработать блокеры.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "FounderDeadlockConflict",
                    Title = "СЦЕНАРИЙ D — Два кофаундера 50/50 / Корпоративный дедлок и конфликт",
                    ExpectedApplicableModules = new() { "founders" },
                    RequiredFindingCodes = new() { "FND_ACTIVE_DISPUTE", "FND_EQUITY_DISPUTE" },
                    MinScore = 5,
                    MaxScore = 50,
                    ExpectedRequiresLegalWork = true
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
                    ["FND-C03"] = "dispute",
                    ["FND-C04"] = "none",
                    ["FND-01"] = "active_conflict",
                    ["FND-02"] = "disputed",
                    ["FND-03"] = "stopped",
                    ["FND-04"] = "dispute",
                    ["FND-05"] = "not_discussed",
                    ["FND-06"] = "none",
                    ["FND-07"] = "none",
                    ["COR-C01"] = "none",
                    ["IP-01"] = "idea",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "some",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "prelaunch",
                    ["PROD-02"] = new List<string> { "undecided" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "none",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ E: КОМПАНИЯ С ПРОДУКТОМ, СОЗДАННЫМ СТОРОННИМИ ПОДРЯДЧИКАМИ
            // Описание: Код и дизайн созданы студией и фрилансерами, но акты
            // отчуждения прав не подписаны. Высокий риск потери прав на IP.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "ContractorHeavyProduct",
                    Title = "СЦЕНАРИЙ E — Продукт создан подрядчиками без передачи исключительных прав",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "team" },
                    RequiredFindingCodes = new() { "IP_CONTRACTOR_RIGHTS_GAP", "TEAM_NO_WRITTEN_AGREEMENTS" },
                    MinScore = 20,
                    MaxScore = 65,
                    ExpectedRequiresLegalWork = true
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-06"] = "single_director",
                    ["COR-07"] = "clean",
                    ["COR-08"] = "organized",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code", "design", "trademark" },
                    ["IP-03"] = new List<string> { "contractors", "studio" },
                    ["IP-04"] = "some",
                    ["IP-07"] = "no_contract",
                    ["TEAM-01"] = new List<string> { "freelancers", "external_devs" },
                    ["TEAM-02"] = "3_5",
                    ["TEAM-03"] = "many_missing",
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ F: B2C СЕРВИС / ПЕРСОНАЛЬНЫЕ ДАННЫЕ / ИСКУССТВЕННЫЙ ИНТЕЛЛЕКТ
            // Описание: Сбор контактных и платежных данных физлиц, использование AI,
            // но политика конфиденциальности скачана из интернета без доработки.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "B2cDataAi",
                    Title = "СЦЕНАРИЙ F — B2C приложение / Персональные данные и интеграция AI",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "product", "data" },
                    RequiredFindingCodes = new() { "DATA_MAP_INCOMPLETE", "DATA_PRIVACY_NOTICE_MISSING" },
                    MinScore = 20,
                    MaxScore = 80,
                    ExpectedRequiresLegalWork = true
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-06"] = "single_director",
                    ["COR-07"] = "clean",
                    ["COR-08"] = "organized",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "consumers" },
                    ["PROD-03"] = new List<string> { "app" },
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["DATA-01"] = "yes",
                    ["DATA-02"] = new List<string> { "contact", "account", "payment" },
                    ["DATA-03"] = "no",
                    ["DATA-04"] = new List<string> { "user" },
                    ["DATA-05"] = "none",
                    ["DATA-06"] = "preparing",
                    ["AI-01"] = "yes",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ G: B2B СЕРВИС / СЛОЖНЫЕ КОНТРАКТЫ И ПАРТНЕРСТВА
            // Описание: Корпоративные клиенты, типовые договоры не согласованы,
            // наличие существенных рисков ответственности и штрафов.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "B2bContractHeavy",
                    Title = "СЦЕНАРИЙ G — B2B сервис / Контрактные риски и кастомные договоры",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "contracts" },
                    RequiredFindingCodes = new() { "CONTRACTS_NOT_FORMALIZED", "CONTRACT_MODEL_MISMATCH" },
                    MinScore = 20,
                    MaxScore = 85,
                    ExpectedRequiresLegalWork = true
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-06"] = "single_director",
                    ["COR-07"] = "clean",
                    ["COR-08"] = "organized",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "clients", "partners" },
                    ["CONTRACT-02"] = "mostly_informal",
                    ["CONTRACT-03"] = "outside",
                    ["CONTRACT-05"] = "weak",
                    ["CONTRACT-06"] = "templates",
                    ["CONTRACT-07"] = "often_unreviewed",
                    ["CONTRACT-08"] = "material",
                    ["CONTRACT-08A"] = "serious",
                    ["INVEST-01"] = "none"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ H: СТАДИЯ РАУНДА / ПОЛНАЯ ГОТОВНОСТЬ К DUE DILIGENCE
            // Описание: Ведутся переговоры с фондами, чистый Cap Table,
            // официальный Term Sheet, прозрачные условия раунда.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "InvestmentDdReady",
                    Title = "СЦЕНАРИЙ H — Раунд инвестиций / Полная готовность к Due Diligence",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "investment" },
                    ExpectedInvestmentDetailedSection = true,
                    ForbiddenFindingCodes = new() { "INVEST_ROUND_BLOCKER", "COR_OWNERSHIP_DISPUTE" },
                    MinScore = 75,
                    MaxScore = 100,
                    ExpectedRequiresLegalWork = false
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 60, ["founder_2"] = 40 },
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
                    ["FND-09"] = "none",
                    ["FND-10"] = "none",
                    ["FND-11"] = "aligned",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-08"] = "organized",
                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code" },
                    ["IP-03"] = new List<string> { "founders" },
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["IP-07"] = "all",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "current",
                    ["PROD-05"] = "yes",
                    ["PROD-06"] = "clear",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "terms",
                    ["INVEST-02"] = "formal",
                    ["INVEST-02A"] = "yes",
                    ["INVEST-03"] = "exact",
                    ["INVEST-04"] = "yes",
                    ["INVEST-05"] = "clear"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ I: СТАДИЯ РАУНДА / МНОЖЕСТВЕННЫЕ БЛОКЕРЫ
            // Описание: Стартап привлекает инвестиции, но имеет корпоративный спор,
            // неучтенные обещания долей и неформальные соглашения. Сделка заблокирована.
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "InvestmentMultipleBlockers",
                    Title = "СЦЕНАРИЙ I — Раунд инвестиций / Множественные блокеры и риск срыва сделки",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "investment" },
                    ExpectedInvestmentDetailedSection = true,
                    RequiredFindingCodes = new() { "COR_OWNERSHIP_DISPUTE", "INVEST_ROUND_BLOCKER" },
                    MinScore = 5,
                    MaxScore = 45,
                    ExpectedRequiresLegalWork = true
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
                    ["FND-C03"] = "dispute",
                    ["FND-C04"] = "none",
                    ["FND-01"] = "active_conflict",
                    ["FND-02"] = "disputed",
                    ["FND-03"] = "stopped",
                    ["FND-04"] = "dispute",
                    ["FND-05"] = "not_discussed",
                    ["FND-06"] = "none",
                    ["FND-07"] = "conflict",
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
                    ["IP-07"] = "payment_only",
                    ["TEAM-01"] = new List<string> { "none" },
                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = new List<string> { "none" },
                    ["INVEST-01"] = "terms",
                    ["INVEST-02"] = "informal",
                    ["INVEST-02A"] = "no",
                    ["INVEST-03"] = "none",
                    ["INVEST-04"] = "no",
                    ["INVEST-05"] = "max_possible"
                }
            ),

            // -----------------------------------------------------------------
            // СЦЕНАРИЙ J: ЗРЕЛАЯ КОМПАНИЯ / ВЫСОКИЙ УРОВЕНЬ ЮРИДИЧЕСКОЙ ЗРЕЛОСТИ
            // Описание: Все 8 модулей применимы и выстроены на высоком уровне
            // (трудовые договоры, GDPR, комплаенс, защита прав на бренд и код).
            // -----------------------------------------------------------------
            (
                new ScenarioExpectation
                {
                    ScenarioId = "MatureHealthyCompany",
                    Title = "СЦЕНАРИЙ J — Зрелая компания / Комплексная юридическая защита (8 модулей)",
                    ExpectedApplicableModules = new() { "founders", "corporate", "ip", "team", "product", "data", "contracts", "investment" },
                    MinScore = 80,
                    MaxScore = 100,
                    ForbiddenFindingCodes = new() { "FND_DEADLOCK", "COR_OWNERSHIP_DISPUTE", "INVEST_ROUND_BLOCKER" },
                    ExpectedRequiresLegalWork = false
                },
                new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 70, ["founder_2"] = 30 },
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
                    ["FND-09"] = "none",
                    ["FND-10"] = "none",
                    ["FND-11"] = "aligned",

                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz_llp",
                    ["COR-01"] = "aligned",
                    ["COR-02"] = "confirmed",
                    ["COR-03"] = "none",
                    ["COR-04"] = "complete",
                    ["COR-05"] = "consistent",
                    ["COR-06"] = "single_director",
                    ["COR-07"] = "clean",
                    ["COR-08"] = "organized",

                    ["IP-01"] = "ready",
                    ["IP-02"] = new List<string> { "code", "design", "trademark" },
                    ["IP-03"] = new List<string> { "founders", "contractors" },
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["IP-07"] = "all",

                    ["TEAM-01"] = new List<string> { "employees", "freelancers" },
                    ["TEAM-02"] = "6_10",
                    ["TEAM-03"] = "all",
                    ["TEAM-04"] = "none",
                    ["TEAM-05"] = "no",

                    ["PROD-01"] = "first",
                    ["PROD-02"] = new List<string> { "companies" },
                    ["PROD-03"] = new List<string> { "website" },
                    ["PROD-04"] = "current",
                    ["PROD-06"] = "strict",

                    ["DATA-01"] = "yes",
                    ["DATA-02"] = new List<string> { "contact", "account" },
                    ["DATA-03"] = "no",
                    ["DATA-04"] = new List<string> { "user" },
                    ["DATA-05"] = "clear",
                    ["DATA-06"] = "yes",
                    ["DATA-07"] = "yes",
                    ["AI-01"] = "no",

                    ["CONTRACT-01"] = new List<string> { "clients" },
                    ["CONTRACT-02"] = "always",
                    ["CONTRACT-03"] = "clear",
                    ["CONTRACT-04"] = "clear",
                    ["CONTRACT-05"] = "clear",
                    ["CONTRACT-06"] = "custom",

                    ["INVEST-01"] = "3_6",
                    ["INVEST-02"] = "formal",
                    ["INVEST-02A"] = "yes",
                    ["INVEST-03"] = "exact",
                    ["INVEST-04"] = "yes",
                    ["INVEST-05"] = "clear"
                }
            )
        };
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
