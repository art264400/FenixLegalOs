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
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
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
    }

    public async Task<string> GenerateExecutiveSummaryAsync(Dictionary<string, object> answers, ScoreResult result)
    {
        // If API key is available, call LLM endpoint
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                var prompt = BuildPromptPayload(answers, result);
                var aiText = await CallLlmApiAsync(prompt);
                if (!string.IsNullOrWhiteSpace(aiText))
                {
                    return aiText.Trim();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiReportService] Error calling LLM API: {ex.Message}. Falling back to rule-based memo.");
            }
        }

        // Fallback rule-based memo generator compliant with LLM contract v1.1
        return GenerateRuleBasedMemo(answers, result);
    }

    private string BuildPromptPayload(Dictionary<string, object> answers, ScoreResult result)
    {
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;
        var relevantRisks = result.Risks
            .Where(r => r.SectionId is "founders" or "corporate")
            .Select(r => new
            {
                r.Code,
                r.Severity,
                r.Title,
                r.Finding,
                r.WhyItMatters,
                r.Recommendation
            }).ToList();

        var payload = new
        {
            overallScore = result.Overall,
            confidence = result.Confidence,
            foundersScore = result.Sections.FirstOrDefault(s => s.SectionId == "founders")?.Score,
            corporateScore = result.Sections.FirstOrDefault(s => s.SectionId == "corporate")?.Score,
            corporateStatus = result.Sections.FirstOrDefault(s => s.SectionId == "corporate")?.Status,
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
                hasDispute = facts.GetValueOrDefault("founders.dispute")
            },
            findings = relevantRisks,
            consultingRecommendation = result.Consulting
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<string?> CallLlmApiAsync(string jsonContext)
    {
        var systemPrompt = @"Вы — профессиональный венчурный юридический аналитик сервиса Fenix Legal OS.
Ваша задача — сформировать индивидуальное структурированное заключение (Legal Memo) для основателей IT-стартапа по итогам диагностики двух блоков: «Основатели» (Founders) и «Корпоративная структура» (Corporate Structure).

СТРОГИЕ ПРАВИЛА (LLM CONTRACT v1.1):
1. Не меняйте баллы и severity рисков. Опирайтесь только на переданные факты и findings.
2. В блоке «1. Юридический профиль проекта» обязательно понятно и профессионально опишите структуру владения и компаний стартапа, опираясь на факт facts.structureNarrative (например: если несколько компаний — четко укажите, какая компания выполняет роль холдинга, а какая работает с клиентами/платежами; если одна — укажите юрисдикцию).
3. Не используйте обвинительных формулировок («Вы нарушаете закон»). Пишите профессионально («По вашим ответам текущая модель может потребовать проверки...», «Система видит риск возникновения спора...»).
4. Не придумывайте рисков, которых нет в findings.
5. Текст должен быть на грамотном русском языке, структурированным, с понятными бизнес-выводами.

ФОРМАТ ВЫВОДА (MARKDOWN):
### 🎯 1. Юридический профиль проекта
Краткая сводка: фаундеры, распределение ролей, юрисдикции компаний и роли в структуре (холдинг / операционная / IP).

### ⚠️ 2. Ключевые точки внимания
Маркированный список выявленных рисков (используйте маркеры * с эмодзи 🔴/🟠/🟡, БЕЗ числовой нумерации):
* 🔴 **Название риска**: Описание сути и почему это критично.
* 🟠 **Название риска**: Описание сути и почему это критично.

### 📋 3. Пошаговый Action Plan на 30 дней
Нумерованный список практических шагов (строго 1., 2., 3.):
1. **Первый шаг**: Что конкретно сделать.
2. **Второй шаг**: Что конкретно сделать.
3. **Третий шаг**: Что конкретно сделать.

### 💼 4. Рекомендация Fenix Law
Краткий совет по формализации структуры и подготовке к сделкам.";

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = $"Вот данные диагностического аудита проекта:\n\n{jsonContext}\n\nСформируйте экспертное Legal Memo:" }
            },
            temperature = 0.3,
            max_tokens = 1500
        };

        var url = _baseUrl.TrimEnd('/') + "/chat/completions";
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[AiReportService] API error ({response.StatusCode}): {err}");
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

    private string GenerateRuleBasedMemo(Dictionary<string, object> answers, ScoreResult result)
    {
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;
        var sb = new StringBuilder();

        // 1. Profile
        sb.AppendLine("### 🎯 1. Юридический профиль проекта");
        
        var founderCount = facts.GetValueOrDefault("founders.count")?.ToString() ?? "1";
        var is5050 = facts.GetValueOrDefault("founders.isEqual5050") is true;
        var entityStatus = facts.GetValueOrDefault("company.entityStatus")?.ToString() ?? "none";
        var structureNarrative = facts.GetValueOrDefault("company.structureNarrative")?.ToString();

        if (founderCount == "1" || founderCount == "solo")
        {
            sb.AppendLine("Проект развивается **единственным основателем**.");
        }
        else
        {
            sb.AppendLine($"В проекте участвует команда из **{founderCount} сооснователей**" + 
                          (is5050 ? " с равным распределением долей (50/50)." : "."));
        }

        if (!string.IsNullOrWhiteSpace(structureNarrative))
        {
            sb.AppendLine(structureNarrative);
        }

        sb.AppendLine();

        // 2. Key Attention Points
        sb.AppendLine("### ⚠️ 2. Ключевые точки внимания");
        var relevantRisks = result.Risks.Where(r => r.SectionId is "founders" or "corporate").ToList();

        if (relevantRisks.Count == 0)
        {
            sb.AppendLine("По результатам диагностики блоков «Основатели» и «Корпоративная структура» критических структурных рисков не выявлено. Базовая конструкция зафиксирована корректно.");
        }
        else
        {
            foreach (var r in relevantRisks.Take(4))
            {
                var icon = r.Severity == "CRITICAL" ? "🔴" : (r.Severity == "HIGH" ? "🟠" : "🟡");
                sb.AppendLine($"* {icon} **{r.Title}**: {r.Finding} {r.WhyItMatters}");
            }
        }

        sb.AppendLine();

        // 3. Action Plan
        sb.AppendLine("### 📋 3. Пошаговый Action Plan на 30 дней");
        int step = 1;

        if (is5050)
        {
            sb.AppendLine($"{step++}. **Урегулировать риск тупика (Deadlock)**: Зафиксировать порядок принятия решений в Соглашении основателей (Founders' Agreement) или Корпоративном договоре.");
        }

        if (relevantRisks.Any(r => r.Code.Contains("EQUITY") || r.Code.Contains("VESTING") || r.Code.Contains("PROMISE")))
        {
            sb.AppendLine($"{step++}. **Закрепить вестинг и опционы**: Оформить график постепенного перехода прав (Vesting) и пул опционов для команды.");
        }

        if (entityStatus != "incorporated")
        {
            sb.AppendLine($"{step++}. **Сформировать корпоративную структуру**: Выбрать юрисдикцию инкорпорации (МФЦА для венчурного капитала или локальное ТОО для операционной деятельности).");
        }
        else if (relevantRisks.Any(r => r.Code.Contains("CAP_TABLE") || r.Code.Contains("OWNERSHIP")))
        {
            sb.AppendLine($"{step++}. **Синхронизировать Cap Table**: Сопоставить фактические договоренности с официальным реестром участников компании.");
        }

        sb.AppendLine($"{step++}. **Утвердить матрицу полномочий**: Четко разграничить операционные решения CEO и стратегические вопросы участников.");

        sb.AppendLine();

        // 4. Consulting
        sb.AppendLine("### 💼 4. Рекомендация Fenix Law");
        var ctaText = result.Consulting?.PrimaryCta ?? "Привести корпоративную структуру и договоренности основателей в порядок";
        sb.AppendLine($"Для безопасного масштабирования и подготовки к инвестициям рекомендуется: **{ctaText}**.");

        return sb.ToString();
    }
}
