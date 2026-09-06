using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Report;

public class CompactSharedProjectContextDto
{
    [JsonPropertyName("jurisdiction")]
    public string Jurisdiction { get; set; } = string.Empty;

    [JsonPropertyName("entityStatus")]
    public string EntityStatus { get; set; } = string.Empty;

    [JsonPropertyName("founders")]
    public string Founders { get; set; } = string.Empty;

    [JsonPropertyName("ownership")]
    public string Ownership { get; set; } = string.Empty;

    [JsonPropertyName("productStage")]
    public string ProductStage { get; set; } = string.Empty;

    [JsonPropertyName("productCreators")]
    public string ProductCreators { get; set; } = string.Empty;

    [JsonPropertyName("users")]
    public string Users { get; set; } = string.Empty;

    [JsonPropertyName("fundraising")]
    public string Fundraising { get; set; } = string.Empty;

    [JsonPropertyName("overallScore")]
    public int OverallScore { get; set; }

    [JsonPropertyName("overallBand")]
    public string OverallBand { get; set; } = string.Empty;

    [JsonPropertyName("overallConfidence")]
    public int OverallConfidence { get; set; }
}

public class ModuleFindingInputDto
{
    [JsonPropertyName("findingCode")]
    public string FindingCode { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseCode")]
    public string RootCauseCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("priority")]
    public string Priority { get; set; } = string.Empty;

    [JsonPropertyName("whyFound")]
    public string WhyFound { get; set; } = string.Empty;

    [JsonPropertyName("whyItMatters")]
    public string WhyItMatters { get; set; } = string.Empty;

    // Kept for internal/backward compatibility, but the LLM receives one canonical list.
    [JsonIgnore]
    public string Recommendation { get; set; } = string.Empty;

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}

public class ModuleNarrativeInputDto
{
    [JsonPropertyName("sectionId")]
    public string SectionId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("band")]
    public string Band { get; set; } = string.Empty;

    [JsonPropertyName("maxSeverity")]
    public string MaxSeverity { get; set; } = string.Empty;

    [JsonPropertyName("findings")]
    public List<ModuleFindingInputDto> Findings { get; set; } = new();
}

public class ModuleNarrativeRequestDto
{
    [JsonPropertyName("projectContext")]
    public CompactSharedProjectContextDto ProjectContext { get; set; } = new();

    [JsonPropertyName("crossModuleSignals")]
    public List<string> CrossModuleSignals { get; set; } = new();

    [JsonPropertyName("module")]
    public ModuleNarrativeInputDto Module { get; set; } = new();
}

public class ModuleNarrativeResponseDto
{
    [JsonPropertyName("sectionId")]
    public string SectionId { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("practicalMeaning")]
    public string PracticalMeaning { get; set; } = string.Empty;

    [JsonPropertyName("findingNarratives")]
    public Dictionary<string, FindingNarrativeDto> FindingNarratives { get; set; } = new();
}

public class ActionSourceFindingDto
{
    [JsonPropertyName("findingCode")]
    public string FindingCode { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("whyFound")]
    public string WhyFound { get; set; } = string.Empty;
}

public class ActionNarrativeInputDto
{
    [JsonPropertyName("actionId")]
    public string ActionId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("whatToDo")]
    public string WhatToDo { get; set; } = string.Empty;

    [JsonPropertyName("priorityGroup")]
    public string PriorityGroup { get; set; } = string.Empty;

    [JsonPropertyName("resolutionMode")]
    public string ResolutionMode { get; set; } = string.Empty;

    [JsonPropertyName("sourceFindings")]
    public List<ActionSourceFindingDto> SourceFindings { get; set; } = new();
}

public class ActionBatchRequestDto
{
    [JsonPropertyName("projectContext")]
    public CompactSharedProjectContextDto ProjectContext { get; set; } = new();

    [JsonPropertyName("actions")]
    public List<ActionNarrativeInputDto> Actions { get; set; } = new();
}

public class ActionBatchResponseDto
{
    [JsonPropertyName("actions")]
    public Dictionary<string, ActionNarrativeItemDto> Actions { get; set; } = new();
}

public class ModuleExecutiveSummaryDto
{
    [JsonPropertyName("sectionId")]
    public string SectionId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public int Score { get; set; }

    [JsonPropertyName("maxSeverity")]
    public string MaxSeverity { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public class TopFindingExecutiveSummaryDto
{
    [JsonPropertyName("findingCode")]
    public string FindingCode { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseCode")]
    public string RootCauseCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;
}

public class TopActionExecutiveSummaryDto
{
    [JsonPropertyName("actionId")]
    public string ActionId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("priorityGroup")]
    public string PriorityGroup { get; set; } = string.Empty;
}

public class CompactInvestmentReadinessDto
{
    [JsonPropertyName("isApplicable")]
    public bool IsApplicable { get; set; }

    [JsonPropertyName("readinessScore")]
    public int ReadinessScore { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("unresolvedBlockersCount")]
    public int UnresolvedBlockersCount { get; set; }

    [JsonPropertyName("blockerTitles")]
    public List<string> BlockerTitles { get; set; } = new();
}

public class CompactProjectProfileExecutiveDto
{
    [JsonPropertyName("jurisdiction")]
    public string Jurisdiction { get; set; } = string.Empty;

    [JsonPropertyName("entityStatus")]
    public string EntityStatus { get; set; } = string.Empty;

    [JsonPropertyName("founders")]
    public string Founders { get; set; } = string.Empty;

    [JsonPropertyName("ownership")]
    public string Ownership { get; set; } = string.Empty;

    [JsonPropertyName("productStage")]
    public string ProductStage { get; set; } = string.Empty;

    [JsonPropertyName("productCreators")]
    public string ProductCreators { get; set; } = string.Empty;

    [JsonPropertyName("users")]
    public string Users { get; set; } = string.Empty;

    [JsonPropertyName("fundraising")]
    public string Fundraising { get; set; } = string.Empty;
}

public class ExecutiveSynthesisRequestDto
{
    [JsonPropertyName("projectProfile")]
    public CompactProjectProfileExecutiveDto ProjectProfile { get; set; } = new();

    [JsonPropertyName("overallScore")]
    public int OverallScore { get; set; }

    [JsonPropertyName("overallBand")]
    public string OverallBand { get; set; } = string.Empty;

    [JsonPropertyName("overallLevelTitle")]
    public string OverallLevelTitle { get; set; } = string.Empty;

    [JsonPropertyName("overallConfidence")]
    public int OverallConfidence { get; set; }

    [JsonPropertyName("riskCounts")]
    public Dictionary<string, int> RiskCounts { get; set; } = new();

    [JsonPropertyName("topFindings")]
    public List<TopFindingExecutiveSummaryDto> TopFindings { get; set; } = new();

    [JsonPropertyName("moduleSummaries")]
    public List<ModuleExecutiveSummaryDto> ModuleSummaries { get; set; } = new();

    [JsonPropertyName("topActions")]
    public List<string> TopActions { get; set; } = new();

    [JsonPropertyName("positiveFactors")]
    public List<string> PositiveFactors { get; set; } = new();

    [JsonPropertyName("investmentReadiness")]
    public CompactInvestmentReadinessDto? InvestmentReadiness { get; set; }

    [JsonPropertyName("requiresLegalWork")]
    public bool RequiresLegalWork { get; set; }

    [JsonPropertyName("fenixLawServiceAreas")]
    public List<string> FenixLawServiceAreas { get; set; } = new();
}

public class ExecutiveSynthesisResponseDto
{
    [JsonPropertyName("projectProfileNarrative")]
    public string ProjectProfileNarrative { get; set; } = string.Empty;

    [JsonPropertyName("executiveConclusion")]
    public string ExecutiveConclusion { get; set; } = string.Empty;

    [JsonPropertyName("rootCauseSummaries")]
    public Dictionary<string, string> RootCauseSummaries { get; set; } = new();

    [JsonPropertyName("fenixLawRecommendation")]
    public string FenixLawRecommendation { get; set; } = string.Empty;
}

public class NarrativeGenerationMetrics
{
    public string Stage { get; set; } = string.Empty;
    public string? ModuleOrBatch { get; set; }
    public int InputTokensApprox { get; set; }
    public int OutputTokensApprox { get; set; }
    public long LatencyMs { get; set; }
    public bool Success { get; set; }
    public bool FallbackUsed { get; set; }
    public string? ErrorOrValidationFailure { get; set; }
}

public class NarrativeChunkFingerprints
{
    public string ContextFingerprint { get; set; } = string.Empty;
    public Dictionary<string, string> ModuleFingerprints { get; set; } = new();
    public Dictionary<string, string> ActionBatchFingerprints { get; set; } = new();
    public string ExecutiveFingerprint { get; set; } = string.Empty;
}
