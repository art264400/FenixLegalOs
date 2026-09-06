using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FenixLegalOs.Models.Report;
using Microsoft.Extensions.Configuration;

namespace FenixLegalOs.Services;

public interface ILlmNarrativeClient
{
    bool IsConfigured { get; }
    Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(
        ModuleNarrativeRequestDto request, CancellationToken ct = default);

    Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(
        ActionBatchRequestDto request, CancellationToken ct = default);

    Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(
        ExecutiveSynthesisRequestDto request, CancellationToken ct = default);
}

public class LlmNarrativeClient : ILlmNarrativeClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly int _timeoutSeconds;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public LlmNarrativeClient(IConfiguration? config = null, HttpClient? httpClient = null)
    {
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

        var timeoutSec = 30;
        if (int.TryParse(config?["NarrativeGeneration:RequestTimeoutSeconds"], out var t) && t > 0)
        {
            timeoutSec = t;
        }
        _timeoutSeconds = timeoutSec;

        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(_timeoutSeconds) };
    }

    public async Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(
        ModuleNarrativeRequestDto request, CancellationToken ct = default)
    {
        var metrics = new NarrativeGenerationMetrics
        {
            Stage = "Module",
            ModuleOrBatch = request.Module?.SectionId ?? "unknown"
        };

        if (!IsConfigured)
        {
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = "API key not configured";
            return (null, metrics);
        }

        var systemPrompt = @"You are the narrative layer of a deterministic legal diagnostic system.
All legal conclusions, findings, severities, scores and priorities have already been determined by the system.
Your task is NOT to perform a new legal diagnosis.
Your task is to explain the supplied deterministic result clearly, professionally and specifically in Russian.
Use only supplied facts and findings.
Do not introduce a new legal fact.
Do not create or remove a finding.
Do not change severity, priority or score.
Do not claim that a document, obligation, violation or legal requirement exists unless that conclusion is supported by the supplied context.
Where the supplied information is insufficient for a jurisdiction-specific legal conclusion, use appropriately qualified language.

Return ONLY a JSON object with this exact structure:
{
  ""sectionId"": ""section_id"",
  ""summary"": ""Краткая характеристика ситуации в данном блоке (1-2 предложения)"",
  ""practicalMeaning"": ""Что это значит для бизнеса на практике: инвестиции, споры, контрагенты"",
  ""findingNarratives"": {
    ""FINDING_CODE"": {
      ""whyFound"": ""Почему система это выявила"",
      ""whyItMatters"": ""Бизнес-последствия"",
      ""recommendations"": [""Главный приоритетный шаг"", ""Следующий конкретный шаг"", ""Третий конкретный шаг""]
    }
  }
}
CRITICAL: Every key in findingNarratives MUST strictly match a FindingCode provided in the request. Never inject new FindingCodes.
For every finding return one ordered recommendations array containing exactly 3 concise, actionable and non-overlapping items.
The first item is the primary recommendation. The second and third items are subsequent steps and MUST NOT repeat or paraphrase the first item or each other.
Never return more than 3 items in recommendations array for any finding.
No Markdown markdown backticks around JSON. Return pure JSON.";

        var userJson = JsonSerializer.Serialize(request, JsonOptions);
        metrics.InputTokensApprox = EstimateTokens(systemPrompt + userJson);

        var sw = Stopwatch.StartNew();
        try
        {
            var rawContent = await SendChatCompletionAsync(systemPrompt, userJson, ct);
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                metrics.FallbackUsed = true;
                metrics.ErrorOrValidationFailure = "Empty LLM response";
                return (null, metrics);
            }

            metrics.OutputTokensApprox = EstimateTokens(rawContent);
            var cleaned = ExtractJsonBlock(rawContent);
            var parsed = JsonSerializer.Deserialize<ModuleNarrativeResponseDto>(cleaned, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            metrics.Success = parsed != null;
            return (parsed, metrics);
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = ex.Message;
            return (null, metrics);
        }
    }

    public async Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(
        ActionBatchRequestDto request, CancellationToken ct = default)
    {
        var actionIds = request.Actions?.Select(a => a.ActionId).ToList() ?? new List<string>();
        var metrics = new NarrativeGenerationMetrics
        {
            Stage = "ActionBatch",
            ModuleOrBatch = string.Join(",", actionIds)
        };

        if (!IsConfigured)
        {
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = "API key not configured";
            return (null, metrics);
        }

        var systemPrompt = @"You are the narrative layer of a deterministic legal diagnostic system.
Actions, their priority groups, resolution formats and IDs are already deterministically decided.
Your task is to provide clear, concise business reasons for urgency (whyNow) and expected results (expectedResult) in Russian.
Do not invent new actions, new documents, or new deadlines.
Preserve the ActionId exactly as supplied.

Return ONLY a JSON object with this exact structure:
{
  ""actions"": {
    ""ACTION_ID"": {
      ""whyNow"": ""Почему это нужно сделать именно сейчас на данном этапе"",
      ""expectedResult"": ""Ожидаемый практический результат для бизнеса""
    }
  }
}
CRITICAL: Every key in actions MUST match an ActionId provided in the request. Never inject new ActionIds.
No Markdown backticks around JSON. Return pure JSON.";

        var userJson = JsonSerializer.Serialize(request, JsonOptions);
        metrics.InputTokensApprox = EstimateTokens(systemPrompt + userJson);

        var sw = Stopwatch.StartNew();
        try
        {
            var rawContent = await SendChatCompletionAsync(systemPrompt, userJson, ct);
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                metrics.FallbackUsed = true;
                metrics.ErrorOrValidationFailure = "Empty LLM response";
                return (null, metrics);
            }

            metrics.OutputTokensApprox = EstimateTokens(rawContent);
            var cleaned = ExtractJsonBlock(rawContent);
            var parsed = JsonSerializer.Deserialize<ActionBatchResponseDto>(cleaned, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            metrics.Success = parsed != null;
            return (parsed, metrics);
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = ex.Message;
            return (null, metrics);
        }
    }

    public async Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(
        ExecutiveSynthesisRequestDto request, CancellationToken ct = default)
    {
        var metrics = new NarrativeGenerationMetrics
        {
            Stage = "Executive",
            ModuleOrBatch = "ExecutiveSynthesis"
        };

        if (!IsConfigured)
        {
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = "API key not configured";
            return (null, metrics);
        }

        var systemPrompt = @"You are the narrative synthesis layer of a deterministic legal diagnostic system Fenix SLS.
You receive the aggregated, already-diagnosed state of the startup: project profile, overall scores, risk counts, top findings, synthesized module summaries, top actions, positive factors and investment readiness.
Your task is NOT to re-diagnose or alter conclusions.
Your task is to write a cohesive executive synthesis for company founders and investors in professional Russian business language.

Requirements:
1. projectProfileNarrative: 2-3 concise sentences describing the current legal setup.
2. executiveConclusion: 800-1200 characters synthesizing the overall situation, main vulnerabilities, business consequences, and what determines the score.
3. rootCauseSummaries: short phrase (up to 150 chars) for each provided rootCause/topFinding.
4. fenixLawRecommendation: conclusion on whether legal support is recommended based on requiresLegalWork and service areas.

Return ONLY a JSON object with this exact structure:
{
  ""projectProfileNarrative"": ""2-3 предложения о текущей юридической конструкции"",
  ""executiveConclusion"": ""Синтез ситуации (800-1200 знаков)"",
  ""rootCauseSummaries"": {
    ""ROOT_CAUSE_OR_FINDING_CODE"": ""Краткая суть проблемы""
  },
  ""fenixLawRecommendation"": ""Заключение о юридической поддержке""
}
No Markdown backticks around JSON. Return pure JSON.";

        var userJson = JsonSerializer.Serialize(request, JsonOptions);
        metrics.InputTokensApprox = EstimateTokens(systemPrompt + userJson);

        var sw = Stopwatch.StartNew();
        try
        {
            var rawContent = await SendChatCompletionAsync(systemPrompt, userJson, ct);
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;

            if (string.IsNullOrWhiteSpace(rawContent))
            {
                metrics.FallbackUsed = true;
                metrics.ErrorOrValidationFailure = "Empty LLM response";
                return (null, metrics);
            }

            metrics.OutputTokensApprox = EstimateTokens(rawContent);
            var cleaned = ExtractJsonBlock(rawContent);
            var parsed = JsonSerializer.Deserialize<ExecutiveSynthesisResponseDto>(cleaned, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            metrics.Success = parsed != null;
            return (parsed, metrics);
        }
        catch (Exception ex)
        {
            sw.Stop();
            metrics.LatencyMs = sw.ElapsedMilliseconds;
            metrics.FallbackUsed = true;
            metrics.ErrorOrValidationFailure = ex.Message;
            return (null, metrics);
        }
    }

    private async Task<string?> SendChatCompletionAsync(string systemPrompt, string userContent, CancellationToken ct)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContent }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var json = JsonSerializer.Serialize(requestBody, JsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        var response = await _httpClient.SendAsync(request, linkedCts.Token);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(ct);
            Console.WriteLine($"[LlmNarrativeClient] API call failed ({response.StatusCode}): {err}");
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
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

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Approximation: ~3 chars per token for Russian/mixed text, ~4 chars for English/code
        return (int)Math.Ceiling(text.Length / 3.2);
    }
}
