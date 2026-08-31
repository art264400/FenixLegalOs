using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Report;

/// <summary>
/// Strictly-typed JSON response contract for LLM text synthesis.
/// The LLM ONLY produces textual narratives and explanations.
/// </summary>
public class ReportNarrativesDto
{
    [JsonPropertyName("projectProfileNarrative")]
    public string ProjectProfileNarrative { get; set; } = string.Empty;

    [JsonPropertyName("executiveConclusion")]
    public string ExecutiveConclusion { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseSummaries")]
    public Dictionary<string, string> RootCauseSummaries { get; set; } = new();

    /// <summary>
    /// Legacy alias during transition: auto-normalizes into canonical RootCauseSummaries.
    /// </summary>
    [JsonPropertyName("topRiskSummaries")]
    public Dictionary<string, string>? TopRiskSummaries
    {
        get => null; // Do not serialize downstream
        set
        {
            if (value != null)
            {
                foreach (var (k, v) in value)
                {
                    if (!RootCauseSummaries.ContainsKey(k))
                        RootCauseSummaries[k] = v;
                }
            }
        }
    }

    [JsonPropertyName("moduleNarratives")]
    public Dictionary<string, ModuleNarrativeDto> ModuleNarratives { get; set; } = new();

    [JsonPropertyName("actionNarratives")]
    public Dictionary<string, ActionNarrativeItemDto> ActionNarratives { get; set; } = new();

    [JsonPropertyName("nextStepNarrative")]
    public string NextStepNarrative { get; set; } = string.Empty;

    [JsonPropertyName("fenixLawRecommendation")]
    public string FenixLawRecommendation { get; set; } = string.Empty;
}

public class ModuleNarrativeDto
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("practicalMeaning")]
    public string PracticalMeaning { get; set; } = string.Empty;

    [JsonPropertyName("findingNarratives")]
    public Dictionary<string, FindingNarrativeDto> FindingNarratives { get; set; } = new();
}

public class FindingNarrativeDto
{
    [JsonPropertyName("whyFound")]
    public string? WhyFound { get; set; }

    [JsonPropertyName("whyItMatters")]
    public string? WhyItMatters { get; set; }

    [JsonPropertyName("recommendation")]
    public string? Recommendation { get; set; }
}

public class ActionNarrativeItemDto
{
    [JsonPropertyName("whyNow")]
    public string WhyNow { get; set; } = string.Empty;

    [JsonPropertyName("expectedResult")]
    public string ExpectedResult { get; set; } = string.Empty;
}
