using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FenixLegalOs.Models;

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
                 ?? "gpt-4o-mini";

        var keyStatus = string.IsNullOrWhiteSpace(_apiKey) ? "MISSING (check .env / OPENAI_API_KEY)" : "CONFIGURED";
        Console.WriteLine($"[AiReportService] Initialized -> Model: {_model}, BaseUrl: {_baseUrl}, ApiKey: {keyStatus}");
    }

    public async Task<string> GenerateExecutiveSummaryAsync(Dictionary<string, object> answers, ScoreResult result)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            Console.WriteLine("[AiReportService] API key is missing. Set OPENAI_API_KEY in .env or environment variables.");
            return "Персональное юридическое заключение формируется на основе ответов аудита и будет доступно в вашем отчете.";
        }

        try
        {
            var prompt = BuildPromptPayload(answers, result);
            var aiText = await CallLlmApiAsync(prompt);
            if (!string.IsNullOrWhiteSpace(aiText))
            {
                return aiText.Trim();
            }
            Console.WriteLine("[AiReportService] LLM API returned empty response.");
            return "Персональное юридическое заключение формируется и будет обновлено в вашем личном кабинете.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AiReportService] Exception during LLM API execution: {ex.Message}");
            return "Персональное юридическое заключение находится в обработке и будет доступно в вашем отчете.";
        }
    }

    private string BuildPromptPayload(Dictionary<string, object> answers, ScoreResult result)
    {
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;
        var relevantRisks = result.Risks
            .Select(r => new
            {
                r.Code,
                r.Severity,
                r.SectionId,
                r.Title,
                r.Finding,
                r.WhyItMatters,
                r.Recommendation
            }).ToList();

        var sectionsSummary = result.Sections
            .Select(s => new
            {
                s.SectionId,
                s.Title,
                s.Score,
                s.Status,
                s.Confidence
            }).ToList();

        var payload = new
        {
            overallScore = result.Overall,
            confidence = result.Confidence,
            sections = sectionsSummary,
            facts = new
            {
                entityStatus = facts.GetValueOrDefault("company.entityStatus"),
                primaryJurisdiction = facts.GetValueOrDefault("company.primaryJurisdiction"),
                jurisdictions = facts.GetValueOrDefault("company.jurisdictions"),
                entityCount = facts.GetValueOrDefault("company.entityCount"),
                groupStructure = facts.GetValueOrDefault("company.groupStructure"),
                structureNarrative = facts.GetValueOrDefault("company.structureNarrative"),
                founderCount = facts.GetValueOrDefault("founders.count"),
                equityDistribution = facts.GetValueOrDefault("founders.equityDistribution"),
                isEqual5050 = facts.GetValueOrDefault("founders.isEqual5050"),
                inactiveExists = facts.GetValueOrDefault("founders.inactiveExists"),
                hasDispute = facts.GetValueOrDefault("founders.dispute"),
                coreProductExists = facts.GetValueOrDefault("ip.coreProductExists"),
                productStage = facts.GetValueOrDefault("product.stage"),
                ipCreators = facts.GetValueOrDefault("ip.creators"),
                overallRightsEvidence = facts.GetValueOrDefault("ip.overallRightsEvidence"),
                founderRights = facts.GetValueOrDefault("ip.founderRights"),
                contractorRights = facts.GetValueOrDefault("ip.contractorRights"),
                formerCreatorStatus = facts.GetValueOrDefault("ip.formerCreatorStatus"),
                employerResourcesUsed = facts.GetValueOrDefault("ip.employerResourcesUsed"),
                thirdPartyComponents = facts.GetValueOrDefault("ip.thirdPartyComponentsUsed"),
                criticalAccountsControl = facts.GetValueOrDefault("ip.criticalAccountsControl"),
                brandDomainControl = facts.GetValueOrDefault("ip.brandDomainControl")
            },
            findings = relevantRisks,
            consultingRecommendation = result.Consulting
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string?> CallLlmApiAsync(string jsonContext)
    {
        var systemPrompt = @"Вы — профессиональный венчурный юридический аналитик и старший партнер сервиса Fenix Legal OS.
Ваша задача — сформировать индивидуальное структурированное юридическое заключение (Legal Memo) для основателей технологического бизнеса по итогам комплексной правовой диагностики (Сооснователи, Корпоративная структура, Интеллектуальная собственность и права на продукт, Команда).

СТРОГИЕ ПРАВИЛА (LLM CONTRACT v1.1):
1. Не меняйте баллы и severity рисков. Опирайтесь ТОЛЬКО на переданные факты и findings.
2. В блоке «1. Юридический профиль проекта» понятно и емко опишите структуру владения, компании и статус продукта/IP (опирайтесь на facts.structureNarrative, статус юрлица, количество фаундеров, стадию продукта).
3. В блоке «⚠️ 2. Ключевые точки внимания» вы ДОЛЖНЫ использовать ИСКЛЮЧИТЕЛЬНО риски из переданного массива `findings`. КАТЕГОРИЧЕСКИ ЗАПРЕЩЕНО самостоятельно выдумывать или добавлять риски, если их нет в массиве findings.
   - Для КАЖДОГО выявленного риска обязательно и подробно раскройте два аспекта: «Почему это важно» (венчурные последствия, риск дедлока, срыв Due Diligence, угроза потери кода/компании) и «Что делать» (конкретные юридические инструменты, формулировки, соглашения).
   - Сгруппируйте или перечислите все выявленные риски в порядке важности (🔴 CRITICAL, затем 🟠 HIGH, затем 🟡 MEDIUM).
   - Если массив findings пуст: напишите ровно одну строку: `* 🟢 **Существенных рисков не выявлено**: Базовая юридическая основа проекта и текущей стадии зафиксирована корректно.`
4. В блоке «📋 3. Пошаговый Action Plan» сформируйте нумерованный план конкретных действий (от 3 до 7 шагов), прямо вытекающих из выявленных рисков и рекомендаций (в приоритетном порядке: закрытие критических пробелов, затем оформление текущей деятельности). Не ограничивайтесь шаблонными тремя пунктами, если рисков больше — охватите все ключевые зоны.
5. Не используйте обвинительных формулировок («Вы нарушаете закон»). Пишите профессионально, конструктивно, емко и доброжелательно.
6. Текст должен быть на безупречном русском языке с четким форматированием в Markdown.

ФОРМАТ ВЫВОДА (MARKDOWN):
### 🎯 1. Юридический профиль проекта
Краткая сводка: фаундеры, юрисдикции и роли компаний, статус продукта и прав на интеллектуальную собственность.

### ⚠️ 2. Ключевые точки внимания
Для каждого выявленного риска используйте следующий формат:
* 🔴 **Название риска**
  • **Почему это важно:** Развернутое объяснение последствий для бизнеса, основателей и инвестиционного раунда.
  • **Что делать:** Конкретный юридический шаг и инструмент исправления.

* 🟠 **Название риска**
  • **Почему это важно:** Объяснение существенных рисков при масштабировании.
  • **Что делать:** Порядок урегулирования и оформления.

* 🟡 **Название риска**
  • **Почему это важно:** Влияние на прозрачность и операционную стабильность.
  • **Что делать:** Рекомендации по наведению порядка в документах.

### 📋 3. Пошаговый Action Plan
Нумерованный список конкретных юридических и организационных шагов (в порядке приоритета, от 3 до 7 пунктов):
1. **Название шага**: Конкретное действие (какой документ подготовить/подписать, с кем урегулировать).
2. **Название шага**: Конкретное действие.
...

### 💼 4. Рекомендация Fenix Law
Краткое экспертное резюме венчурного юриста о готовности бизнеса к масштабированию и привлечению инвестиций.";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Вот данные диагностического аудита проекта:\n\n{jsonContext}\n\nСформируйте экспертное Legal Memo:" }
            },
            temperature = 0.3,
            max_tokens = 2500
        };

        var url = _baseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[AiReportService] OpenAI API Error ({response.StatusCode}): {err}");
            return null;
        }

        var resJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resJson);
        var content = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return content;
    }
}
