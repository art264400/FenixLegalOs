using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;

namespace FenixLegalOs.Services;

public class AiReportService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;

    public AiReportService(IConfiguration? config = null)
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        _apiKey = Environment.GetEnvironmentVariable("AI_API_KEY") 
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
                  ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                  ?? config?["AiSettings:ApiKey"];
        _baseUrl = Environment.GetEnvironmentVariable("AI_BASE_URL") 
                   ?? config?["AiSettings:BaseUrl"] 
                   ?? "https://api.openai.com/v1";
        _model = Environment.GetEnvironmentVariable("AI_MODEL") 
                 ?? config?["AiSettings:Model"] 
                 ?? "gpt-5.6-sol";

        var keyStatus = string.IsNullOrWhiteSpace(_apiKey) ? "MISSING (check .env / OPENAI_API_KEY)" : "CONFIGURED";
        Console.WriteLine($"[AiReportService] Initialized -> Model: {_model}, BaseUrl: {_baseUrl}, ApiKey: {keyStatus}");
    }

    public async Task<ReportNarrativesDto> GenerateReportNarrativesAsync(ReportContext context)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Console.WriteLine("[AiReportService] API key missing -> using deterministic fallback narratives.");
            return DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
        }

        try
        {
            var promptPayload = BuildPromptPayload(context);
            var rawJson = await CallLlmApiAsync(promptPayload);

            if (!string.IsNullOrWhiteSpace(rawJson))
            {
                var cleanedJson = ExtractJsonBlock(rawJson);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var parsed = JsonSerializer.Deserialize<ReportNarrativesDto>(cleanedJson, options);
                return ReportQualityGate.ValidateAndSanitize(parsed, context);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AiReportService] Exception during LLM execution: {ex.Message} -> using fallback.");
        }

        return DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
    }

    private string BuildPromptPayload(ReportContext ctx)
    {
        // 1. Material findings: all Blocker, all Critical, and material High
        var materialFindings = ctx.AllFindings
            .Where(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High)
            .OrderByDescending(f => f.Severity switch
            {
                RiskSeverity.Blocker => 4,
                RiskSeverity.Critical => 3,
                RiskSeverity.High => 2,
                _ => 1
            })
            .ThenByDescending(f => f.Priority == RiskPriority.Now ? 2 : f.Priority == RiskPriority.BeforeRound ? 1 : 0)
            .Select(f => new
            {
                findingCode = f.Code,
                module = f.SectionId,
                title = f.Title,
                severity = f.Severity.ToString(),
                whyItMatters = f.WhyItMatters,
                rootCauseCode = !string.IsNullOrWhiteSpace(f.RootCauseGroup) ? f.RootCauseGroup : f.Code
            })
            .ToList();

        // 2. Root causes
        var rootCauses = ctx.TopFindings.Select(t => new
        {
            rootCauseCode = t.RootCauseCode,
            findingCode = t.FindingCode,
            title = t.Title,
            severity = t.Severity.ToString(),
            summary = t.ShortSummary
        }).ToList();

        // 3. Investment readiness
        var investmentReadiness = new
        {
            isApplicable = ctx.InvestmentReadiness?.IsApplicable ?? false,
            baseScore = ctx.InvestmentReadiness?.BaseScore ?? 0,
            baseCategory = ctx.InvestmentReadiness?.BaseCategory ?? "Не применимо",
            crossModuleBlockers = ctx.InvestmentReadiness?.CrossModuleBlockers.Select(b => new
            {
                module = b.ModuleTitle,
                findingCode = b.FindingCode,
                title = b.Title,
                severity = b.Severity.ToString(),
                whyItBlocksDueDiligence = b.WhyItBlocksDueDiligence
            }) ?? Enumerable.Empty<object>()
        };

        // 4. Grounded factual boundaries and allowed business impacts
        var knownFactsList = ctx.Profile.KeyFacts.Select(f => $"{f.Label}: {f.Value}").ToList();
        var allowedImpacts = ctx.FocusModules
            .SelectMany(m => m.Findings)
            .Select(f => f.WhyItMatters)
            .Concat(ctx.InvestmentReadiness?.CrossModuleBlockers.Select(b => b.WhyItBlocksDueDiligence) ?? Enumerable.Empty<string>())
            .Concat(ctx.ActionPlan.Select(a => a.WhyNow))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .ToList();

        var inputData = new
        {
            projectProfile = new
            {
                projectName = ctx.ProjectName,
                keyFacts = ctx.Profile.KeyFacts.Select(f => new { f.Label, f.Value }),
                baselineNarrative = ctx.Profile.ConfigurationNarrative
            },
            factualBoundaries = new
            {
                groundedKeyFacts = knownFactsList,
                allowedBusinessImpacts = allowedImpacts
            },
            overallAssessment = new
            {
                score = ctx.Overall.Score,
                scoreBand = ctx.Overall.Band,
                levelTitle = ctx.Overall.LevelTitle,
                confidence = ctx.Overall.Confidence,
                topDrivers = ctx.Overall.TopDrivers,
                strengths = ctx.PositiveFactors.Select(p => p.Title).ToList()
            },
            materialFindings = materialFindings,
            rootCauses = rootCauses,
            investmentReadiness = investmentReadiness,
            focusModules = ctx.FocusModules.Select(m => new
            {
                sectionId = m.SectionId,
                title = m.Title,
                score = m.Score,
                band = m.ScoreBand,
                maxSeverity = m.MaxSeverity.ToString(),
                findings = m.Findings.Select(f => new
                {
                    findingCode = f.FindingCode,
                    title = f.Title,
                    severity = f.Severity.ToString(),
                    whyFound = f.WhyFound,
                    whyItMatters = f.WhyItMatters,
                    recommendation = f.Recommendation,
                    priority = f.Priority.ToString()
                })
            }),
            actionPlan = ctx.ActionPlan.Select(a => new
            {
                actionId = a.ActionId,
                title = a.Title,
                businessReason = a.WhyNow,
                requiredOutcome = a.ExpectedResult,
                whatToDo = a.WhatToDo,
                priorityGroup = a.PriorityGroup,
                resolutionMode = a.ResolutionMode.ToString(),
                coveredFindings = a.CoveredFindingCodes
            }),
            fenixLaw = new
            {
                requiresLegalWork = ctx.FenixLaw.RequiresLegalWork,
                serviceAreas = ctx.FenixLaw.ServiceAreas.Count > 0
                    ? ctx.FenixLaw.ServiceAreas
                    : ctx.FenixLaw.ServiceCards.Select(s => s.Title).ToList()
            }
        };

        return JsonSerializer.Serialize(inputData, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string?> CallLlmApiAsync(string jsonContext)
    {
        var systemPrompt = @"Ты — редактор клиентского юридического отчета Fenix SLS.

Тебе передается результат юридического анализа, который УЖЕ полностью
сформирован детерминированным Legal Engine.

Ты НЕ проводишь юридический анализ.

Ты НЕ определяешь:
- существует ли риск;
- применимое законодательство;
- юридические последствия;
- severity;
- priority;
- Score;
- необходимые документы;
- необходимые действия;
- ResolutionMode;
- необходимость юридической помощи.

Твоя единственная задача — превратить уже сформированные движком факты,
выводы и рекомендации в ясный профессиональный русский текст,
НЕ ИЗМЕНЯЯ И НЕ РАСШИРЯЯ ИХ СМЫСЛ.

==================================================
ГЛАВНОЕ ПРАВИЛО ФАКТОЛОГИЧЕСКОЙ СВЯЗАННОСТИ (GROUNDING)
==================================================

Любое содержательное утверждение в твоем ответе должно иметь
прямое основание во входном JSON.

Если информация отсутствует во входных данных — ее не существует
для целей данного отчета.

Ты можешь синтезировать переданные факты (A + B -> понятное объяснение),
но СТРОГО ЗАПРЕЩЕНО выдумывать новые факты C.

==================================================
ПРАВИЛА ПРЕДОТВРАЩЕНИЯ ГАЛЛЮЦИНАЦИЙ (ANTI-HALLUCINATION RULES)
==================================================

1. ЗАПРЕТ ВЫДУМЫВАНИЯ СОБЫТИЙ (No Invented Events):
   - Если указано участие подрядчиков, запрещено писать «разработчик ушел», «программист покинул команду», если факт ухода прямо не передан во входных данных (например, в whyFound).

2. ЗАПРЕТ ВЫДУМЫВАНИЯ СТАТУСА ДОКУМЕНТОВ (No Invented Document States):
   - Если указано «права не подтверждены» или «договор устный», запрещено утверждать «акты приема-передачи никогда не составлялись» или «документы вовсе отсутствуют».

3. ЗАПРЕТ ВЫДУМЫВАНИЯ ПРИЧИН КОНФЛИКТА (No Invented Causes):
   - Если указан спор или тупик, запрещено придумывать причину конфликта (например, «конфликт из-за невыплаты денег», «ссора при уходе»).

4. ЗАПРЕТ КАТЕГОРИЧНЫХ КОММЕРЧЕСКИХ УГРОЗ (No Extreme Guarantees):
   - Запрещено писать «институциональные инвесторы гарантированно откажут», «сделка сорвется», «компания закроется».
   - Последствия должны строго опираться на allowedBusinessImpacts и whyItMatters («создает существенный риск при проведении Due Diligence», «осложняет привлечение инвестиций»).

5. СОХРАНЕНИЕ НЕОПРЕДЕЛЕННОСТИ (Preserve Uncertainty):
   - Если факт неизвестен или не подтвержден, описывай его как неопределенность («информация не зафиксирована документально», «требует сверки»), а не как утвердительное отсутствие.

6. ЗАПРЕТ РАСШИРЕНИЯ МАСШТАБА (No Scope Inflation):
   - Не превращай «подрядчики» в «все ключевые модули продукта», «часть команды» в «вся команда», если это не указано в whyFound.

==================================================
СТРОГО ЗАПРЕЩЕНО
==================================================

Запрещено самостоятельно добавлять:

- названия законов и нормативных актов;
- GDPR и иные правовые режимы;
- утверждения о нарушении закона;
- утверждения о незаконности;
- штрафы, санкции и ответственность;
- суммы;
- проценты;
- сроки;
- юридические тесты и критерии;
- обязательные требования;
- новые договоры или документы;
- новые юридические механизмы;
- новые риски;
- новые последствия;
- новые рекомендации;
- новые бизнес-риски;
- новые инвестиционные блокеры;
- новые условия сделок;
- новые параметры vesting / cliff / leaver;
- новые требования к структуре компании.

Это запрещено даже если такие выводы логично следуют из ситуации.

==================================================
ДЕТЕРМИНИРОВАННЫЕ ПОЛЯ
==================================================

Следующие значения являются окончательными:

Score
ScoreBand
Severity
Priority
Applicability
Finding
RootCause
BusinessImpact
Recommendation
Action
BusinessReason
RequiredOutcome
ResolutionMode
InvestmentBlocker
RequiresLegalWork
FenixLawServiceAreas

Их нельзя изменять, усиливать или расширять.

==================================================
FINDING NARRATIVES
==================================================

Для каждого Finding движок передает:

title
facts / whyFound
whyItMatters
recommendation
severity
priority

Ты можешь только переформулировать:

whyFound → whyFound
whyItMatters → whyItMatters
recommendation → recommendation

Смысл должен оставаться эквивалентным исходному.

Нельзя добавлять последствия или рекомендации, которых нет
в соответствующем Finding.

==================================================
ACTION NARRATIVES
==================================================

Для каждого Action движок передает:

actionId
title
businessReason
requiredOutcome
resolutionMode
coveredFindings

Сформируй:

whyNow = краткая переформулировка businessReason
expectedResult = краткая переформулировка requiredOutcome

Нельзя добавлять:
- новый документ;
- новый юридический механизм;
- срок;
- числовой параметр;
- юридическое последствие;
- дополнительное действие.

==================================================
PROJECT PROFILE
==================================================

Project Profile описывает только текущее состояние проекта.

Используй исключительно projectProfile.keyFacts.

Не интерпретируй их как риски.

Не давай рекомендаций.

Не используй severity.

Не говори, что что-либо ""необходимо исправить"".

==================================================
EXECUTIVE CONCLUSION
==================================================

Executive Conclusion — это синтез уже существующих результатов.

Используй только:

overallAssessment (score, scoreBand, levelTitle, topDrivers, strengths)
rootCauses (главные детерминированные корни проблем)
materialFindings (все Blocker, Critical и High находки)
investmentReadiness (готовность к сделке и сквозные блокеры)
fenixLaw (сервисные зоны)

Синтезируй эти детерминированные факты:
- опиши общую конструкцию компании
- выдели главные уязвимости (Blocker / Critical)
- объясни бизнес-последствия
- покажи, что определяет текущий Score.

Если Critical/Blocker отсутствуют, нельзя писать о критических
проблемах или необходимости устранения критических блокеров.

==================================================
MODULE NARRATIVES
==================================================

Не пиши общую юридическую теорию.

Запрещены generic-фразы вроде:

""Интеллектуальная собственность является важным активом компании.""

""Команда играет ключевую роль в успехе бизнеса.""

""Персональные данные требуют строгого соблюдения законодательства.""

Каждый абзац должен описывать именно переданный проект.

==================================================
FENIX LAW
==================================================

Не определяй самостоятельно необходимость юридической помощи.

Используй только:

requiresLegalWork
serviceAreas

Если requiresLegalWork = false:
не рекомендуй юридическое сопровождение.

Если requiresLegalWork = true:
опиши только переданные serviceAreas.

Fenix SLS называется:

""юридический скрининг""
или
""диагностика юридической готовности"".

Никогда не называй Fenix SLS:
""аудитом""
или
""юридическим аудитом"".

==================================================
ТЕРМИНОЛОГИЯ
==================================================

Не выводи внутренние идентификаторы пользователю:

FND_*
COR_*
IP_*
DATA_*
TEAM_*
PROD_*
ActionId
FactStore
SectionId
RootCauseGroup

Они разрешены только как ключи JSON для связи результата
с объектами движка.

Не используй эмодзи.

Не упоминай механизм генерации отчета.

Используй профессиональный, ясный русский деловой язык.

==================================================
ФОРМАТ
==================================================

Верни ТОЛЬКО JSON со следующей структурой:
{
  ""projectProfileNarrative"": ""2-4 емких предложения с описанием текущей юридической конструкции компании"",
  ""executiveConclusion"": ""Синтез ситуации (800-1200 знаков): общая конструкция, главные детерминированные уязвимости, бизнес-последствия, что определяет текущий Score"",
  ""rootCauseSummaries"": {
    ""ROOT_CAUSE_CODE_OR_FINDING_CODE"": ""Короткая емкая формулировка корневой проблемы (до 150 знаков)""
  },
  ""moduleNarratives"": {
    ""sectionId"": {
      ""summary"": ""Характеристика ситуации в данном направлении"",
      ""practicalMeaning"": ""Что это значит для бизнеса на практике: инвестиции, споры, сделки"",
      ""findingNarratives"": {
        ""FINDING_CODE"": {
          ""whyFound"": ""Почему SLS это выявил"",
          ""whyItMatters"": ""Почему это важно для бизнеса"",
          ""recommendation"": ""Что рекомендуется сделать""
        }
      }
    }
  },
  ""actionNarratives"": {
    ""ACTION_ID"": {
      ""whyNow"": ""Почему это нужно сделать на данном этапе"",
      ""expectedResult"": ""Ожидаемый практический результат""
    }
  },
  ""fenixLawRecommendation"": ""Заключение о необходимости юридической помощи по итогам скрининга""
}

Никакого Markdown вокруг JSON (никаких ```json).

Никакого текста до JSON.

Никакого текста после JSON.

Перед отправкой результата проверь каждое содержательное утверждение:
""Есть ли прямое основание для этого утверждения во входном JSON?""
Если нет — удали его.";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Входные структурированные данные Fenix SLS:\n\n{jsonContext}\n\nСформируйте JSON с нарративами:" }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[AiReportService] API call failed ({response.StatusCode}): {err}");
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(responseJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content;
    }

    private static string ExtractJsonBlock(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..];
        }
        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }
        return trimmed.Trim();
    }
}
