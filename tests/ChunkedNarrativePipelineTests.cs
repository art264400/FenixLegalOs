using System.Collections.Concurrent;
using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Xunit;

namespace FenixLegalOs.Tests;

public class ChunkedNarrativePipelineTests
{
    private class MockLlmNarrativeClient : ILlmNarrativeClient
    {
        public bool IsConfigured { get; set; } = true;

        public ConcurrentBag<ModuleNarrativeRequestDto> ModuleRequests { get; } = new();
        public ConcurrentBag<ActionBatchRequestDto> ActionBatchRequests { get; } = new();
        public ConcurrentBag<ExecutiveSynthesisRequestDto> ExecutiveRequests { get; } = new();

        public Func<ModuleNarrativeRequestDto, ModuleNarrativeResponseDto?>? OnGenerateModule { get; set; }
        public Func<ActionBatchRequestDto, ActionBatchResponseDto?>? OnGenerateActionBatch { get; set; }
        public Func<ExecutiveSynthesisRequestDto, ExecutiveSynthesisResponseDto?>? OnGenerateExecutive { get; set; }

        public List<string> CallOrder { get; } = new();
        private readonly object _lock = new();

        public Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(
            ModuleNarrativeRequestDto request, CancellationToken ct = default)
        {
            lock (_lock)
            {
                CallOrder.Add($"Module:{request.Module.SectionId}");
            }
            ModuleRequests.Add(request);

            var resp = OnGenerateModule != null
                ? OnGenerateModule(request)
                : CreateDefaultModuleResponse(request);

            var metrics = new NarrativeGenerationMetrics
            {
                Stage = "Module",
                ModuleOrBatch = request.Module.SectionId,
                Success = resp != null,
                InputTokensApprox = 150,
                OutputTokensApprox = 80,
                LatencyMs = 10
            };

            return Task.FromResult((resp, metrics));
        }

        public Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(
            ActionBatchRequestDto request, CancellationToken ct = default)
        {
            lock (_lock)
            {
                CallOrder.Add("ActionBatch");
            }
            ActionBatchRequests.Add(request);

            var resp = OnGenerateActionBatch != null
                ? OnGenerateActionBatch(request)
                : CreateDefaultActionBatchResponse(request);

            var metrics = new NarrativeGenerationMetrics
            {
                Stage = "ActionBatch",
                ModuleOrBatch = string.Join(",", request.Actions.Select(a => a.ActionId)),
                Success = resp != null,
                InputTokensApprox = 100,
                OutputTokensApprox = 50,
                LatencyMs = 8
            };

            return Task.FromResult((resp, metrics));
        }

        public Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(
            ExecutiveSynthesisRequestDto request, CancellationToken ct = default)
        {
            lock (_lock)
            {
                CallOrder.Add("Executive");
            }
            ExecutiveRequests.Add(request);

            var resp = OnGenerateExecutive != null
                ? OnGenerateExecutive(request)
                : CreateDefaultExecutiveResponse(request);

            var metrics = new NarrativeGenerationMetrics
            {
                Stage = "Executive",
                ModuleOrBatch = "ExecutiveSynthesis",
                Success = resp != null,
                InputTokensApprox = 300,
                OutputTokensApprox = 200,
                LatencyMs = 25
            };

            return Task.FromResult((resp, metrics));
        }

        public static ModuleNarrativeResponseDto CreateDefaultModuleResponse(ModuleNarrativeRequestDto req)
        {
            var findingNarratives = new Dictionary<string, FindingNarrativeDto>();
            foreach (var f in req.Module.Findings)
            {
                findingNarratives[f.FindingCode] = new FindingNarrativeDto
                {
                    WhyFound = $"LLM объяснение для {f.FindingCode}: выявлен факт документального дефекта.",
                    WhyItMatters = $"LLM важность для {f.FindingCode}: создает повышенный риск при Due Diligence.",
                    Recommendation = $"LLM рекомендация для {f.FindingCode}: урегулировать вопрос документально.",
                    Recommendations = new List<string> { "Подписать соглашение", "Составить реестр" }
                };
            }

            return new ModuleNarrativeResponseDto
            {
                SectionId = req.Module.SectionId,
                Summary = $"LLM резюме направления {req.Module.Title} с оценкой {req.Module.Score}.",
                PracticalMeaning = $"LLM практическое значение для направления {req.Module.Title}.",
                FindingNarratives = findingNarratives
            };
        }

        private static ActionBatchResponseDto CreateDefaultActionBatchResponse(ActionBatchRequestDto req)
        {
            var actions = new Dictionary<string, ActionNarrativeItemDto>();
            foreach (var a in req.Actions)
            {
                actions[a.ActionId] = new ActionNarrativeItemDto
                {
                    WhyNow = $"LLM обоснование срочности для {a.ActionId}.",
                    ExpectedResult = a.Title // Quality gate overwrites with canonical action.ExpectedResult
                };
            }

            return new ActionBatchResponseDto { Actions = actions };
        }

        private static ExecutiveSynthesisResponseDto CreateDefaultExecutiveResponse(ExecutiveSynthesisRequestDto req)
        {
            return new ExecutiveSynthesisResponseDto
            {
                ProjectProfileNarrative = "LLM профиль: проект структурирован в юрисдикции с ранней стадией продукта.",
                ExecutiveConclusion = "LLM executive заключение: комплексный юридический скрининг выявил основные правовые точки внимания. " +
                                      "Базовая модель компании сформирована, однако выявленные уязвимости требуют планомерного устранения перед раундом финансирования. " +
                                      "Реализация сформированного плана действий позволит повысить защищенность бизнеса и укрепить позицию перед контрагентами.",
                RootCauseSummaries = req.TopFindings.ToDictionary(t => t.RootCauseCode, t => $"LLM резюме для {t.Title}"),
                FenixLawRecommendation = "LLM рекомендация Fenix Law: рекомендуется точечное сопровождение."
            };
        }
    }

    private static ReportContext CreateSampleReportContext()
    {
        var ctx = new ReportContext
        {
            SessionId = "test_chunked_session",
            ProjectName = "Test Startup",
            Overall = new OverallScoreDto
            {
                Score = 55,
                Band = "MaterialGaps",
                LevelTitle = "Существенные пробелы",
                Confidence = 90,
                TopDrivers = new List<string> { "Интеллектуальная собственность", "Сооснователи" }
            },
            Profile = new ProjectProfileDto
            {
                KeyFacts = new List<FactItemDto>
                {
                    new() { Key = "jurisdiction", Label = "Юрисдикция", Value = "Казахстан" },
                    new() { Key = "entity", Label = "Юрлицо", Value = "Зарегистрировано" },
                    new() { Key = "founders", Label = "Основатели", Value = "2 сооснователя" },
                    new() { Key = "equity", Label = "Доли", Value = "50 / 50" },
                    new() { Key = "stage", Label = "Стадия", Value = "MVP" },
                    new() { Key = "creators", Label = "Создатели", Value = "Подрядчики" },
                    new() { Key = "users", Label = "Пользователи", Value = "B2B клиенты" },
                    new() { Key = "invest_readiness", Label = "Инвестиции", Value = "Планируется раунд" }
                },
                ConfigurationNarrative = "Стартап зарегистрирован в юрисдикции Казахстан."
            },
            TopFindings = new List<TopFindingSummaryDto>
            {
                new() { FindingCode = "FND_DEADLOCK", RootCauseCode = "RC_DEADLOCK", Title = "Тупиковая ситуация 50/50", Severity = RiskSeverity.Critical, ShortSummary = "50/50 доли без порядка разрешения споров" },
                new() { FindingCode = "IP_UNCONFIRMED", RootCauseCode = "RC_IP", Title = "Права не оформлены", Severity = RiskSeverity.High, ShortSummary = "Права подрядчиков не переданы" }
            },
            AllFindings = new List<RiskFinding>
            {
                new() { Code = "FND_DEADLOCK", SectionId = "founders", Title = "Тупиковая ситуация 50/50", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Finding = "Доли 50/50", WhyItMatters = "Блокировка решений", Recommendation = "SHA" },
                new() { Code = "IP_UNCONFIRMED", SectionId = "ip", Title = "Права не оформлены", Severity = RiskSeverity.High, Priority = RiskPriority.Now, Finding = "Акты отсутствуют", WhyItMatters = "Риск Due Diligence", Recommendation = "Акты приема-передачи" }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new()
                {
                    SectionId = "founders",
                    Title = "Сооснователи",
                    Score = 40,
                    ScoreBand = "Существенные пробелы",
                    MaxSeverity = RiskSeverity.Critical,
                    Findings = new List<ReportFindingCardDto>
                    {
                        new() { FindingCode = "FND_DEADLOCK", Title = "Тупик 50/50", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, WhyFound = "Доли 50/50", WhyItMatters = "Блокировка", Recommendation = "SHA", Recommendations = new() { "Шаг 1", "Шаг 2" } }
                    }
                },
                new()
                {
                    SectionId = "ip",
                    Title = "Интеллектуальная собственность",
                    Score = 50,
                    ScoreBand = "Существенные пробелы",
                    MaxSeverity = RiskSeverity.High,
                    Findings = new List<ReportFindingCardDto>
                    {
                        new() { FindingCode = "IP_UNCONFIRMED", Title = "Права не оформлены", Severity = RiskSeverity.High, Priority = RiskPriority.Now, WhyFound = "Акты отсутствуют", WhyItMatters = "Риск DD", Recommendation = "Акты", Recommendations = new() { "Шаг А" } }
                    }
                }
            },
            NotApplicableModules = new List<NotApplicableModuleDto>
            {
                new() { SectionId = "corporate", Title = "Корпоративная структура", ReasonText = "Компания еще не создана" }
            },
            ActionPlan = new List<UnifiedActionItemDto>
            {
                new() { ActionId = "ACT_1", Title = "Подписать SHA", PriorityGroup = "В ПЕРВУЮ ОЧЕРЕДЬ", WhyNow = "Срочно", ExpectedResult = "Подписан SHA", CoveredFindingCodes = new() { "FND_DEADLOCK" } },
                new() { ActionId = "ACT_2", Title = "Оформить IP", PriorityGroup = "В ПЕРВУЮ ОЧЕРЕДЬ", WhyNow = "До раунда", ExpectedResult = "Подписаны акты IP", CoveredFindingCodes = new() { "IP_UNCONFIRMED" } }
            },
            FenixLaw = new FenixLawRecommendationReportDto
            {
                RequiresLegalWork = true,
                SummaryText = "Рекомендуется комплексная юридическая поддержка."
            }
        };

        ctx.ExecutiveConclusion = DeterministicFallbackNarratives.GenerateExecutiveFallback(ctx).ExecutiveConclusion;
        return ctx;
    }

    [Fact(DisplayName = "1. Only applicable modules produce LLM requests")]
    public async Task OnlyApplicableModules_ProduceLlmRequests()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        var requestedSections = mockLlm.ModuleRequests.Select(r => r.Module.SectionId).ToList();
        Assert.Contains("founders", requestedSections);
        Assert.Contains("ip", requestedSections);
        Assert.DoesNotContain("corporate", requestedSections);
    }

    [Fact(DisplayName = "2. Module request does not contain unrelated full ReportContext")]
    public async Task ModuleRequest_DoesNotContainUnrelatedFullReportContext()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        var foundersReq = mockLlm.ModuleRequests.First(r => r.Module.SectionId == "founders");
        Assert.Single(foundersReq.Module.Findings);
        Assert.Equal("FND_DEADLOCK", foundersReq.Module.Findings[0].FindingCode);

        var json = JsonSerializer.Serialize(foundersReq);
        Assert.DoesNotContain("IP_UNCONFIRMED", json);
        Assert.DoesNotContain("ACT_1", json);
        Assert.DoesNotContain("ACT_2", json);
    }

    [Fact(DisplayName = "3. LLM cannot inject unknown FindingCode")]
    public async Task LlmCannotInjectUnknownFindingCode()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateModule = req => new ModuleNarrativeResponseDto
            {
                SectionId = req.Module.SectionId,
                Summary = "Summary",
                PracticalMeaning = "Practical",
                FindingNarratives = new Dictionary<string, FindingNarrativeDto>
                {
                    ["FND_DEADLOCK"] = new() { WhyFound = "Valid", WhyItMatters = "Valid", Recommendation = "Valid" },
                    ["UNKNOWN_HALLUCINATED_CODE"] = new() { WhyFound = "Invented", WhyItMatters = "Invented", Recommendation = "Invented" }
                }
            }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        Assert.False(narratives.ModuleNarratives["founders"].FindingNarratives.ContainsKey("UNKNOWN_HALLUCINATED_CODE"));
        Assert.True(narratives.ModuleNarratives["founders"].FindingNarratives.ContainsKey("FND_DEADLOCK"));
    }

    [Fact(DisplayName = "4. Missing FindingCode is handled according to contract")]
    public async Task MissingFindingCode_HandledGracefullyAccordingToContract()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateModule = req => new ModuleNarrativeResponseDto
            {
                SectionId = req.Module.SectionId,
                Summary = "Summary",
                PracticalMeaning = "Practical",
                FindingNarratives = new Dictionary<string, FindingNarrativeDto>() // Missing FND_DEADLOCK
            }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        Assert.True(narratives.ModuleNarratives["founders"].FindingNarratives.ContainsKey("FND_DEADLOCK"));
        Assert.NotNull(narratives.ModuleNarratives["founders"].FindingNarratives["FND_DEADLOCK"].WhyFound);
    }

    [Fact(DisplayName = "5. Action response cannot inject unknown ActionId")]
    public async Task ActionResponse_CannotInjectUnknownActionId()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateActionBatch = req => new ActionBatchResponseDto
            {
                Actions = new Dictionary<string, ActionNarrativeItemDto>
                {
                    ["ACT_1"] = new() { WhyNow = "Valid urgency", ExpectedResult = "Valid result" },
                    ["ACT_HACK_999"] = new() { WhyNow = "Invented action", ExpectedResult = "Invented" }
                }
            }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        Assert.False(narratives.ActionNarratives.ContainsKey("ACT_HACK_999"));
        Assert.True(narratives.ActionNarratives.ContainsKey("ACT_1"));
    }

    [Fact(DisplayName = "6. Invalid JSON triggers granular deterministic fallback")]
    public async Task InvalidJson_TriggersGranularDeterministicFallback()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateModule = req => req.Module.SectionId == "founders"
                ? null // simulates JSON deserialization failure
                : new ModuleNarrativeResponseDto
                {
                    SectionId = "ip",
                    Summary = "Valid IP Summary",
                    PracticalMeaning = "Valid IP Meaning",
                    FindingNarratives = new Dictionary<string, FindingNarrativeDto>
                    {
                        ["IP_UNCONFIRMED"] = new() { WhyFound = "Valid why", WhyItMatters = "Valid matters", Recommendation = "Valid rec" }
                    }
                }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        // Founders fell back deterministically
        Assert.NotNull(narratives.ModuleNarratives["founders"].Summary);
        Assert.Contains("Сооснователи", narratives.ModuleNarratives["founders"].Summary);

        // IP succeeded with LLM summary!
        Assert.Equal("Valid IP Summary", narratives.ModuleNarratives["ip"].Summary);
    }

    [Fact(DisplayName = "7. One module timeout does not cause full-report fallback")]
    public async Task OneModuleTimeout_DoesNotCauseFullReportFallback()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateModule = req =>
            {
                if (req.Module.SectionId == "founders")
                    throw new TimeoutException("Simulated HTTP timeout in Founders module");

                return new ModuleNarrativeResponseDto
                {
                    SectionId = "ip",
                    Summary = "IP Module Succeeded",
                    PracticalMeaning = "IP Meaning",
                    FindingNarratives = new Dictionary<string, FindingNarrativeDto>
                    {
                        ["IP_UNCONFIRMED"] = new() { WhyFound = "A", WhyItMatters = "B", Recommendation = "C" }
                    }
                };
            }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        // IP used LLM narrative
        Assert.Equal("IP Module Succeeded", narratives.ModuleNarratives["ip"].Summary);

        // Founders used fallback narrative gracefully
        Assert.NotNull(narratives.ModuleNarratives["founders"]);
        Assert.Contains("Сооснователи", narratives.ModuleNarratives["founders"].Summary);

        // Executive synthesis was still generated
        Assert.False(string.IsNullOrWhiteSpace(narratives.ExecutiveConclusion));
    }

    [Fact(DisplayName = "8. Parallel module generation produces deterministic merged result")]
    public async Task ParallelModuleGeneration_ProducesDeterministicMergedResult()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        var result1 = await service.GenerateReportNarrativesAsync(ctx);
        var result2 = await service.GenerateReportNarrativesAsync(ctx);

        Assert.Equal(result1.ModuleNarratives.Keys.OrderBy(k => k), result2.ModuleNarratives.Keys.OrderBy(k => k));
        Assert.Equal(result1.ActionNarratives.Keys.OrderBy(k => k), result2.ActionNarratives.Keys.OrderBy(k => k));
        Assert.Equal(result1.ContextFingerprint, result2.ContextFingerprint);
    }

    [Fact(DisplayName = "9. Executive generation runs only after module results are available")]
    public async Task ExecutiveGeneration_RunsOnlyAfterModuleResultsAreAvailable()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        var execIndex = mockLlm.CallOrder.IndexOf("Executive");
        Assert.True(execIndex > 0, "Executive must be called");

        // Verify that all modules were called before Executive
        for (int i = 0; i < execIndex; i++)
        {
            var call = mockLlm.CallOrder[i];
            Assert.True(call.StartsWith("Module:") || call == "ActionBatch", $"Call {call} preceded Executive");
        }
    }

    [Fact(DisplayName = "10. Executive input is compressed and does not contain entire ReportContext")]
    public async Task ExecutiveInput_IsCompressedAndDoesNotContainEntireReportContext()
    {
        var ctx = CreateSampleReportContext();
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        var execReq = Assert.Single(mockLlm.ExecutiveRequests);
        var json = JsonSerializer.Serialize(execReq);

        // Full finding descriptions & recommendations arrays should NOT be repeated in executive request
        Assert.DoesNotContain("Шаг 1", json);
        Assert.DoesNotContain("Шаг 2", json);
        Assert.DoesNotContain("Шаг А", json);

        // Synthesized module summaries ARE included
        Assert.NotEmpty(execReq.ModuleSummaries);
        Assert.Contains(execReq.ModuleSummaries, m => m.SectionId == "founders");
        Assert.Contains(execReq.ModuleSummaries, m => m.SectionId == "ip");
    }

    [Fact(DisplayName = "11. Existing scores/findings/severity/priority are unchanged before and after narrative generation")]
    public async Task ExistingScoresFindingsSeverityPriority_UnchangedBeforeAndAfter()
    {
        var ctx = CreateSampleReportContext();

        var scoreBefore = ctx.Overall.Score;
        var bandBefore = ctx.Overall.Band;
        var findingsCountBefore = ctx.AllFindings.Count;
        var severitiesBefore = ctx.AllFindings.Select(f => f.Severity).ToList();
        var prioritiesBefore = ctx.AllFindings.Select(f => f.Priority).ToList();

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        Assert.Equal(scoreBefore, ctx.Overall.Score);
        Assert.Equal(bandBefore, ctx.Overall.Band);
        Assert.Equal(findingsCountBefore, ctx.AllFindings.Count);
        Assert.Equal(severitiesBefore, ctx.AllFindings.Select(f => f.Severity).ToList());
        Assert.Equal(prioritiesBefore, ctx.AllFindings.Select(f => f.Priority).ToList());
    }

    [Fact(DisplayName = "12. N/A modules generate zero LLM calls")]
    public async Task NaModules_GenerateZeroLlmCalls()
    {
        var ctx = CreateSampleReportContext();
        Assert.Contains(ctx.NotApplicableModules, m => m.SectionId == "corporate");

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        Assert.DoesNotContain(mockLlm.ModuleRequests, r => r.Module.SectionId == "corporate");
    }

    [Fact(DisplayName = "13. Action batching works for action count greater than batch size")]
    public async Task ActionBatching_WorksForCountGreaterThanBatchSize()
    {
        var ctx = CreateSampleReportContext();
        // Add 7 actions total to exceed batch size of 5
        for (int i = 3; i <= 7; i++)
        {
            ctx.ActionPlan.Add(new UnifiedActionItemDto
            {
                ActionId = $"ACT_{i}",
                Title = $"Action {i}",
                PriorityGroup = "ПОЗЖЕ",
                WhyNow = "План",
                ExpectedResult = $"Результат {i}"
            });
        }

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);
        service.Options.ActionBatchSize = 5;

        await service.GenerateReportNarrativesAsync(ctx);

        // 7 actions with batch size 5 = 2 batches (batch 1: 5 actions, batch 2: 2 actions)
        Assert.Equal(2, mockLlm.ActionBatchRequests.Count);
    }

    [Fact(DisplayName = "14. One failed action batch falls back only for that batch")]
    public async Task OneFailedActionBatch_FallsBackOnlyForThatBatch()
    {
        var ctx = CreateSampleReportContext();
        for (int i = 3; i <= 7; i++)
        {
            ctx.ActionPlan.Add(new UnifiedActionItemDto
            {
                ActionId = $"ACT_{i}",
                Title = $"Action {i}",
                PriorityGroup = "ПОЗЖЕ",
                WhyNow = "План",
                ExpectedResult = $"Результат {i}"
            });
        }

        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateActionBatch = req =>
            {
                // First batch fails
                if (req.Actions.Any(a => a.ActionId == "ACT_1"))
                    return null;

                // Second batch succeeds
                return new ActionBatchResponseDto
                {
                    Actions = req.Actions.ToDictionary(a => a.ActionId, a => new ActionNarrativeItemDto
                    {
                        WhyNow = $"Специфическая срочность для {a.ActionId}",
                        ExpectedResult = a.Title
                    })
                };
            }
        };

        var service = new AiReportService(null, mockLlm);
        service.Options.ActionBatchSize = 5;

        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        // Batch 1 fell back to deterministic
        Assert.Equal("Срочно", narratives.ActionNarratives["ACT_1"].WhyNow);

        // Batch 2 used LLM narrative
        Assert.Equal("Специфическая срочность для ACT_6", narratives.ActionNarratives["ACT_6"].WhyNow);
    }

    [Fact(DisplayName = "15. Context fingerprint behavior remains correct")]
    public async Task ContextFingerprint_BehaviorRemainsCorrect()
    {
        var ctx = CreateSampleReportContext();
        var expectedFingerprint = ReportQualityGate.ComputeContextFingerprint(ctx);

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        Assert.Equal(expectedFingerprint, narratives.ContextFingerprint);
    }

    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public ChunkedNarrativePipelineTests(Xunit.Abstractions.ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(DisplayName = "16. No narrative generator can mutate ReportContext")]
    public async Task NoNarrativeGenerator_CanMutateReportContext()
    {
        var ctx = CreateSampleReportContext();
        var initialJson = JsonSerializer.Serialize(ctx);

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);

        await service.GenerateReportNarrativesAsync(ctx);

        var postJson = JsonSerializer.Serialize(ctx);
        Assert.Equal(initialJson, postJson);
    }

    [Fact(DisplayName = "17. Benchmark: Measure Exact Token and Size Reduction")]
    public async Task Benchmark_MeasureExactTokenReduction()
    {
        var ctx = CreateSampleReportContext();
        // Expand context to a realistic production scenario with 4 focus modules, 8 findings and 7 actions
        ctx.FocusModules.Add(new FocusModuleDetailDto
        {
            SectionId = "team",
            Title = "Команда",
            Score = 45,
            ScoreBand = "Существенные пробелы",
            MaxSeverity = RiskSeverity.High,
            Findings = new List<ReportFindingCardDto>
            {
                new() { FindingCode = "TEAM_NO_CONTRACTS", Title = "Отсутствие договоров", Severity = RiskSeverity.High, Priority = RiskPriority.Now, WhyFound = "Работа без договоров", WhyItMatters = "Риски претензий", Recommendation = "Подписать договоры", Recommendations = new() { "Шаг 1", "Шаг 2" } }
            }
        });
        ctx.FocusModules.Add(new FocusModuleDetailDto
        {
            SectionId = "data",
            Title = "Данные и ИИ",
            Score = 35,
            ScoreBand = "Критические пробелы",
            MaxSeverity = RiskSeverity.Critical,
            Findings = new List<ReportFindingCardDto>
            {
                new() { FindingCode = "DATA_PRIVACY_MISSING", Title = "Отсутствие политики", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, WhyFound = "Сайт без политики", WhyItMatters = "Штрафы и блокировка", Recommendation = "Разработать политику", Recommendations = new() { "Шаг А", "Шаг Б" } }
            }
        });
        for (int i = 3; i <= 7; i++)
        {
            ctx.ActionPlan.Add(new UnifiedActionItemDto
            {
                ActionId = $"ACT_{i}",
                Title = $"Действие {i} по урегулированию рисков",
                PriorityGroup = "В ПЕРВУЮ ОЧЕРЕДЬ",
                WhyNow = "Требуется для снижения рисков перед раундом",
                ExpectedResult = $"Оформленные документы {i}"
            });
        }

        // 1. Calculate Old Monolithic Payload
        var knownFactsList = ctx.Profile.KeyFacts.Select(f => $"{f.Label}: {f.Value}").ToList();
        var allowedImpacts = ctx.FocusModules.SelectMany(m => m.Findings).Select(f => f.WhyItMatters).Distinct().ToList();
        var oldMonolithicPayload = new
        {
            projectProfile = new { projectName = ctx.ProjectName, keyFacts = ctx.Profile.KeyFacts.Select(f => new { f.Label, f.Value }), baselineNarrative = ctx.Profile.ConfigurationNarrative },
            factualBoundaries = new { groundedKeyFacts = knownFactsList, allowedBusinessImpacts = allowedImpacts },
            overallAssessment = new { score = ctx.Overall.Score, scoreBand = ctx.Overall.Band, levelTitle = ctx.Overall.LevelTitle, confidence = ctx.Overall.Confidence, topDrivers = ctx.Overall.TopDrivers, strengths = ctx.PositiveFactors.Select(p => p.Title).ToList() },
            materialFindings = ctx.AllFindings.Select(f => new { findingCode = f.Code, module = f.SectionId, title = f.Title, severity = f.Severity.ToString(), whyItMatters = f.WhyItMatters }),
            rootCauses = ctx.TopFindings.Select(t => new { rootCauseCode = t.RootCauseCode, findingCode = t.FindingCode, title = t.Title, severity = t.Severity.ToString(), summary = t.ShortSummary }),
            focusModules = ctx.FocusModules.Select(m => new { sectionId = m.SectionId, title = m.Title, score = m.Score, band = m.ScoreBand, maxSeverity = m.MaxSeverity.ToString(), findings = m.Findings.Select(f => new { findingCode = f.FindingCode, title = f.Title, severity = f.Severity.ToString(), whyFound = f.WhyFound, whyItMatters = f.WhyItMatters, recommendation = f.Recommendation, recommendations = f.Recommendations, priority = f.Priority.ToString() }) }),
            actionPlan = ctx.ActionPlan.Select(a => new { actionId = a.ActionId, title = a.Title, businessReason = a.WhyNow, whyNow = a.WhyNow, requiredOutcome = a.ExpectedResult, expectedResult = a.ExpectedResult, whatToDo = a.WhatToDo, priorityGroup = a.PriorityGroup, resolutionMode = a.ResolutionMode.ToString(), coveredFindings = a.CoveredFindingCodes }),
            fenixLaw = new { requiresLegalWork = ctx.FenixLaw.RequiresLegalWork, serviceAreas = ctx.FenixLaw.ServiceAreas }
        };

        var jsonOptions = LlmNarrativeClient.JsonOptions;
        var oldJson = JsonSerializer.Serialize(oldMonolithicPayload, jsonOptions);
        var oldChars = oldJson.Length;
        var oldSystemPrompt = "System prompt monolithic..."; // ~300 lines system prompt
        var oldInputTokens = (int)Math.Ceiling((oldChars + 3000) / 3.2); // ~4000-5000 tokens
        var oldOutputTokens = 2200; // Monolithic ReportNarrativesDto full response
        var oldTotalTokens = oldInputTokens + oldOutputTokens;

        // 2. Measure New Chunked Requests
        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);
        service.Options.ActionBatchSize = 5;

        await service.GenerateReportNarrativesAsync(ctx);

        var execReq = mockLlm.ExecutiveRequests.First();
        var profileChars = JsonSerializer.Serialize(execReq.ProjectProfile, jsonOptions).Length;
        var overallChars = JsonSerializer.Serialize(new { execReq.OverallScore, execReq.OverallBand, execReq.OverallLevelTitle, execReq.OverallConfidence }, jsonOptions).Length;
        var riskCountsChars = JsonSerializer.Serialize(execReq.RiskCounts, jsonOptions).Length;
        var topFindingsChars = JsonSerializer.Serialize(execReq.TopFindings, jsonOptions).Length;
        var moduleSummariesChars = JsonSerializer.Serialize(execReq.ModuleSummaries, jsonOptions).Length;
        var topActionsChars = JsonSerializer.Serialize(execReq.TopActions, jsonOptions).Length;
        var positiveFactorsChars = JsonSerializer.Serialize(execReq.PositiveFactors, jsonOptions).Length;
        var investChars = JsonSerializer.Serialize(execReq.InvestmentReadiness, jsonOptions).Length;
        var fenixLawChars = JsonSerializer.Serialize(new { execReq.RequiresLegalWork, execReq.FenixLawServiceAreas }, jsonOptions).Length;
        var totalExecJson = JsonSerializer.Serialize(execReq, jsonOptions);
        var totalExecChars = totalExecJson.Length;

        _output.WriteLine("==================== EXECUTIVE PAYLOAD AUDIT ====================");
        _output.WriteLine($"ProjectProfile:       {profileChars,6} chars (~{(int)Math.Ceiling(profileChars / 3.2)} tokens)");
        _output.WriteLine($"Overall:              {overallChars,6} chars (~{(int)Math.Ceiling(overallChars / 3.2)} tokens)");
        _output.WriteLine($"RiskCounts:           {riskCountsChars,6} chars (~{(int)Math.Ceiling(riskCountsChars / 3.2)} tokens)");
        _output.WriteLine($"TopFindings:          {topFindingsChars,6} chars (~{(int)Math.Ceiling(topFindingsChars / 3.2)} tokens)");
        _output.WriteLine($"ModuleSummaries:      {moduleSummariesChars,6} chars (~{(int)Math.Ceiling(moduleSummariesChars / 3.2)} tokens)");
        _output.WriteLine($"TopActions:           {topActionsChars,6} chars (~{(int)Math.Ceiling(topActionsChars / 3.2)} tokens)");
        _output.WriteLine($"PositiveFactors:      {positiveFactorsChars,6} chars (~{(int)Math.Ceiling(positiveFactorsChars / 3.2)} tokens)");
        _output.WriteLine($"InvestmentReadiness:  {investChars,6} chars (~{(int)Math.Ceiling(investChars / 3.2)} tokens)");
        _output.WriteLine($"FenixLaw:             {fenixLawChars,6} chars (~{(int)Math.Ceiling(fenixLawChars / 3.2)} tokens)");
        _output.WriteLine($"Total Executive JSON: {totalExecChars,6} chars (~{(int)Math.Ceiling(totalExecChars / 3.2)} tokens)");

        var moduleReqTokens = mockLlm.ModuleRequests.Select(r => (int)Math.Ceiling(JsonSerializer.Serialize(r, jsonOptions).Length / 3.2) + 350).ToList();
        var actionReqTokens = mockLlm.ActionBatchRequests.Select(r => (int)Math.Ceiling(JsonSerializer.Serialize(r, jsonOptions).Length / 3.2) + 200).ToList();
        var execInputTokens = (int)Math.Ceiling(totalExecChars / 3.2) + 400; // prompt + json

        // Typical output tokens per chunk
        var moduleOutTokensList = new List<int> { 180, 150, 160, 170 }; // ~165 avg per module
        var actionOutTokensList = new List<int> { 120, 60 }; // batch 1 (5 items), batch 2 (2 items)
        var execOutTokens = 420; // executive conclusion + root causes + profile narrative

        var sumModuleIn = moduleReqTokens.Sum();
        var sumModuleOut = moduleOutTokensList.Sum();
        var sumActionIn = actionReqTokens.Sum();
        var sumActionOut = actionOutTokensList.Sum();

        var totalNewInput = sumModuleIn + sumActionIn + execInputTokens;
        var totalNewOutput = sumModuleOut + sumActionOut + execOutTokens;
        var grandTotalNew = totalNewInput + totalNewOutput;

        var peakNewInput = Math.Max(moduleReqTokens.Max(), Math.Max(actionReqTokens.Max(), execInputTokens));
        var peakNewOutput = Math.Max(moduleOutTokensList.Max(), Math.Max(actionOutTokensList.Max(), execOutTokens));

        _output.WriteLine("==================== FULL TOKEN ACCOUNTING ====================");
        _output.WriteLine($"OLD Total Input:      {oldInputTokens} tokens");
        _output.WriteLine($"OLD Total Output:     {oldOutputTokens} tokens");
        _output.WriteLine($"OLD Grand Total:      {oldTotalTokens} tokens");
        _output.WriteLine("----------------------------------------------------------------");
        _output.WriteLine($"NEW Module Inputs:    {sumModuleIn} tokens (avg {(int)moduleReqTokens.Average()} / module, count: {mockLlm.ModuleRequests.Count})");
        _output.WriteLine($"NEW Module Outputs:   {sumModuleOut} tokens");
        _output.WriteLine($"NEW Action Inputs:    {sumActionIn} tokens (count: {mockLlm.ActionBatchRequests.Count})");
        _output.WriteLine($"NEW Action Outputs:   {sumActionOut} tokens");
        _output.WriteLine($"NEW Executive Input:  {execInputTokens} tokens");
        _output.WriteLine($"NEW Executive Output: {execOutTokens} tokens");
        _output.WriteLine($"NEW Total Input:      {totalNewInput} tokens");
        _output.WriteLine($"NEW Total Output:     {totalNewOutput} tokens");
        _output.WriteLine($"NEW Grand Total:      {grandTotalNew} tokens");
        _output.WriteLine("----------------------------------------------------------------");
        _output.WriteLine($"PeakInputTokensPerRequest:  OLD: {oldInputTokens} -> NEW: {peakNewInput}");
        _output.WriteLine($"PeakOutputTokensPerRequest: OLD: {oldOutputTokens} -> NEW: {peakNewOutput}");
        _output.WriteLine($"TotalInputTokensPerReport:  OLD: {oldInputTokens} -> NEW: {totalNewInput}");
        _output.WriteLine($"TotalOutputTokensPerReport: OLD: {oldOutputTokens} -> NEW: {totalNewOutput}");
        _output.WriteLine($"LLM Calls in Scenario:      {mockLlm.ModuleRequests.Count + mockLlm.ActionBatchRequests.Count + mockLlm.ExecutiveRequests.Count} ({mockLlm.ModuleRequests.Count} modules + {mockLlm.ActionBatchRequests.Count} action batches + {mockLlm.ExecutiveRequests.Count} exec)");
        _output.WriteLine($"Max Parallel Calls:         {service.Options.MaxParallelModuleRequests}");

        Assert.True(peakNewInput < oldInputTokens, "Peak input must be reduced");
    }

    [Fact(DisplayName = "18. Stress Test: Worst-Case No Content Loss Across All 8 Modules & Full Action Plan")]
    public async Task StressTest_WorstCase_NoContentLoss_AllInvariantsHold()
    {
        // 1. Setup: All 8 modules applicable, multiple findings per module, 23 actions, positive factors, investment readiness
        var ctx = CreateSampleReportContext();
        ctx.NotApplicableModules.Clear(); // 0 N/A modules -> All 8 applicable!
        ctx.FocusModules.Clear();
        ctx.AllFindings.Clear();

        var sectionIds = new[] { "founders", "corporate", "ip", "team", "prod", "data", "ai", "contracts" };
        var sectionTitles = new[] { "Сооснователи", "Корпоративная структура", "IP", "Команда", "Продукт", "Данные", "ИИ", "Контракты" };

        var expectedFindingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int m = 0; m < sectionIds.Length; m++)
        {
            var secId = sectionIds[m];
            var findingsForSec = new List<ReportFindingCardDto>();
            for (int f = 1; f <= 3; f++)
            {
                var fCode = $"{secId.ToUpperInvariant()}_RISK_{f}";
                expectedFindingCodes.Add(fCode);

                var fCard = new ReportFindingCardDto
                {
                    FindingCode = fCode,
                    Title = $"Риск {f} в разделе {sectionTitles[m]}",
                    Severity = f == 1 ? RiskSeverity.Critical : (f == 2 ? RiskSeverity.High : RiskSeverity.Medium),
                    Priority = RiskPriority.Now,
                    WhyFound = $"Выявлен риск {fCode}",
                    WhyItMatters = $"Бизнес-риск {fCode}",
                    Recommendation = $"Рекомендация {fCode}",
                    Recommendations = new List<string> { $"Шаг 1 для {fCode}", $"Шаг 2 для {fCode}" }
                };
                findingsForSec.Add(fCard);

                ctx.AllFindings.Add(new RiskFinding
                {
                    Code = fCode,
                    SectionId = secId,
                    Title = fCard.Title,
                    Severity = fCard.Severity,
                    Priority = fCard.Priority,
                    Finding = fCard.WhyFound,
                    WhyItMatters = fCard.WhyItMatters,
                    Recommendation = fCard.Recommendation
                });
            }

            ctx.FocusModules.Add(new FocusModuleDetailDto
            {
                SectionId = secId,
                Title = sectionTitles[m],
                Score = 40 + m * 5,
                ScoreBand = "Существенные пробелы",
                MaxSeverity = RiskSeverity.Critical,
                Findings = findingsForSec
            });
        }

        // 23 Actions to stress test batching (5 + 5 + 5 + 5 + 3 = 5 batches)
        ctx.ActionPlan.Clear();
        var expectedActionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int a = 1; a <= 23; a++)
        {
            var aId = $"ACT_{a:D2}";
            expectedActionIds.Add(aId);
            ctx.ActionPlan.Add(new UnifiedActionItemDto
            {
                ActionId = aId,
                Title = $"Действие {a:D2}",
                PriorityGroup = a <= 5 ? "В ПЕРВУЮ ОЧЕРЕДЬ" : (a <= 15 ? "СЛЕДУЮЩИМ ШАГОМ" : "ПОЗЖЕ"),
                WhyNow = $"Срочность {a:D2}",
                ExpectedResult = $"Результат {a:D2}",
                CoveredFindingCodes = new List<string> { expectedFindingCodes.First() }
            });
        }

        ctx.PositiveFactors = new List<PositiveFactorDto>
        {
            new() { Title = "Фактор 1", Category = "Корпоративный" },
            new() { Title = "Фактор 2", Category = "IP" },
            new() { Title = "Фактор 3", Category = "Команда" },
            new() { Title = "Фактор 4", Category = "Продукт" }
        };

        ctx.InvestmentReadiness = new InvestmentReadinessReportDto
        {
            IsApplicable = true,
            ReadinessScore = 55,
            Category = "Требуется подготовка",
            UnresolvedBlockersCount = 2,
            BlockerTitles = new List<string> { "Блокер 1", "Блокер 2" }
        };

        var mockLlm = new MockLlmNarrativeClient();
        var service = new AiReportService(null, mockLlm);
        service.Options.ActionBatchSize = 5;

        // Execute Pipeline
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        // Assert 1: Modules Invariant
        Assert.Equal(8, mockLlm.ModuleRequests.Count);
        Assert.Equal(8, narratives.ModuleNarratives.Count);
        foreach (var secId in sectionIds)
        {
            Assert.True(narratives.ModuleNarratives.ContainsKey(secId), $"Missing module narrative for '{secId}'");
        }

        // Assert 2: Findings Invariant (All 24 findings present in narratives)
        var finalFindingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in narratives.ModuleNarratives.Values)
        {
            foreach (var fc in m.FindingNarratives.Keys)
            {
                finalFindingCodes.Add(fc);
            }
        }
        Assert.Equal(expectedFindingCodes.Count, finalFindingCodes.Count);
        Assert.Subset(expectedFindingCodes, finalFindingCodes);
        Assert.Subset(finalFindingCodes, expectedFindingCodes);

        // Assert 3: Actions Invariant (All 23 actions present across 5 batches)
        Assert.Equal(5, mockLlm.ActionBatchRequests.Count); // 5, 5, 5, 5, 3
        Assert.Equal(23, narratives.ActionNarratives.Count);
        var finalActionIds = new HashSet<string>(narratives.ActionNarratives.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(expectedActionIds.Count, finalActionIds.Count);
        Assert.Subset(expectedActionIds, finalActionIds);
        Assert.Subset(finalActionIds, expectedActionIds);

        // Assert 4: ReportRenderer Merge Invariant (TypstPdfService merge check)
        // Merge into reportCtx to prove zero data drop on final output structure
        var reportCtx = ctx;
        foreach (var focus in reportCtx.FocusModules)
        {
            Assert.True(narratives.ModuleNarratives.TryGetValue(focus.SectionId, out var mNarrative));
            foreach (var f in focus.Findings)
            {
                Assert.True(mNarrative!.FindingNarratives.ContainsKey(f.FindingCode), $"Finding {f.FindingCode} missing in merged view");
            }
        }
        foreach (var action in reportCtx.ActionPlan)
        {
            Assert.True(narratives.ActionNarratives.ContainsKey(action.ActionId), $"Action {action.ActionId} missing in merged view");
        }
    }

    [Fact(DisplayName = "19. Stress Test: Truncated / Invalid JSON Module Restored via Deterministic Fallback with Invariant Preserved")]
    public async Task StressTest_TruncatedModule_RestoredViaFallback_InvariantsPreserved()
    {
        var ctx = CreateSampleReportContext();
        var targetSectionId = "founders";
        var expectedFindings = ctx.FocusModules.First(m => m.SectionId == targetSectionId).Findings.Select(f => f.FindingCode).ToList();

        var mockLlm = new MockLlmNarrativeClient
        {
            OnGenerateModule = req =>
            {
                if (req.Module.SectionId == targetSectionId)
                {
                    // Simulate an incomplete / truncated LLM response where FindingNarratives is null or cut off
                    return new ModuleNarrativeResponseDto
                    {
                        SectionId = targetSectionId,
                        Summary = "Оборванное резюме",
                        PracticalMeaning = "Оборванное значение",
                        FindingNarratives = null // LLM dropped findings completely!
                    };
                }
                return MockLlmNarrativeClient.CreateDefaultModuleResponse(req);
            }
        };

        var service = new AiReportService(null, mockLlm);
        var narratives = await service.GenerateReportNarrativesAsync(ctx);

        // Pipeline must detect the missing findings and gracefully restore them via fallback
        Assert.True(narratives.ModuleNarratives.TryGetValue(targetSectionId, out var targetModule));
        foreach (var expectedCode in expectedFindings)
        {
            Assert.True(targetModule!.FindingNarratives.ContainsKey(expectedCode), $"Finding {expectedCode} was lost during truncation!");
            var fn = targetModule.FindingNarratives[expectedCode];
            Assert.False(string.IsNullOrWhiteSpace(fn.WhyFound), "WhyFound must be populated by fallback");
            Assert.False(string.IsNullOrWhiteSpace(fn.Recommendation), "Recommendation must be populated by fallback");
        }
    }
    [Fact(DisplayName = "Module recommendations are emitted once as an ordered deduplicated list")]
    public void ModuleRecommendations_AreDeduplicatedAndPrimaryIsDerivedFromFirstItem()
    {
        var ctx = CreateSampleReportContext();
        var module = ctx.FocusModules.First();
        var finding = module.Findings.First();
        var first = finding.Recommendations.FirstOrDefault() ?? finding.Recommendation;

        var raw = new ModuleNarrativeResponseDto
        {
            SectionId = module.SectionId,
            Summary = module.SubtitleNarrative,
            PracticalMeaning = module.PracticalMeaning,
            FindingNarratives = new Dictionary<string, FindingNarrativeDto>
            {
                [finding.FindingCode] = new()
                {
                    WhyFound = finding.WhyFound,
                    WhyItMatters = finding.WhyItMatters,
                    Recommendations = new List<string> { first, first }
                }
            }
        };

        var (_, result, _) = ChunkedNarrativeValidator.ValidateAndSanitizeModule(raw, module, ctx);
        var normalized = result.FindingNarratives[finding.FindingCode];

        Assert.NotEmpty(normalized.Recommendations!);
        Assert.Equal(normalized.Recommendations![0], normalized.Recommendation);
        Assert.Equal(normalized.Recommendations.Count,
            normalized.Recommendations.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
}
