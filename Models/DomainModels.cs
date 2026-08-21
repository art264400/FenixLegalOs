using System.Text.Json.Serialization;

namespace FenixLegalOs.Models;

public record DiagnosticSection(
    string Id,
    int Order,
    string Title,
    string ShortTitle,
    int Weight
);

public record AnswerOption(
    string Id,
    string Label,
    double Score,
    string? Severity = null,
    string? RiskCode = null,
    bool Exclusive = false,
    string? ConfidenceClass = "known" // known, partial, unknown
);

public class ConditionalRule
{
    public string? QuestionId { get; set; }
    public string? Op { get; set; } // eq, neq, in, notIn, includes, answered, gte, lte
    public object? Value { get; set; }
    public List<ConditionalRule>? All { get; set; }
    public List<ConditionalRule>? Any { get; set; }
}

public class DiagnosticQuestion
{
    public string Id { get; set; } = "";
    public string SectionId { get; set; } = "";
    public string? DimensionId { get; set; }
    public int Order { get; set; }
    public string Question { get; set; } = "";
    public string? Explanation { get; set; }
    public string Type { get; set; } = "single"; // single, multiple, boolean, text, number, equity_split
    public string ScoreMode { get; set; } = "diagnostic"; // context, diagnostic, trigger, custom
    public List<AnswerOption>? Options { get; set; }
    public double Weight { get; set; } = 1.0;
    public double DimensionWeight { get; set; } = 1.0;
    public double WithinDimensionWeight { get; set; } = 1.0;
    public List<string> Tags { get; set; } = new();
    public List<ConditionalRule>? ShowIf { get; set; }
    public List<ConditionalRule>? SkipIf { get; set; }
    public bool Enabled { get; set; } = true;
}

public class RiskFindingBasis
{
    public string QuestionId { get; set; } = "";
    public string AnswerId { get; set; } = "";
}

public class RiskDefinition
{
    public string Code { get; set; } = "";
    public string RootCauseGroup { get; set; } = "GENERAL";
    public string Severity { get; set; } = "MEDIUM"; // INFO, MEDIUM, HIGH, CRITICAL, BLOCKER
    public string Priority { get; set; } = "LATER"; // NOW, 30_DAYS, BEFORE_ROUND, LATER
    public string SectionId { get; set; } = "";
    public List<string> Modules { get; set; } = new();
    public string Title { get; set; } = "";
    public string Finding { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public List<string> Recommendations { get; set; } = new();
    public string Recommendation { get; set; } = "";
    public bool LawyerRequired { get; set; }
    public string Resolution { get; set; } = "self"; // self, check_with_lawyer, lawyer_required
    public string? ServiceCode { get; set; }
    public List<string> SuppressCodes { get; set; } = new();
    public string? Cta { get; set; }
}

public class RiskFinding
{
    public string Code { get; set; } = "";
    public string RootCauseGroup { get; set; } = "GENERAL";
    public string Severity { get; set; } = "MEDIUM"; // INFO, MEDIUM, HIGH, CRITICAL, BLOCKER
    public string Priority { get; set; } = "LATER"; // NOW, 30_DAYS, BEFORE_ROUND, LATER
    public string SectionId { get; set; } = "";
    public List<string> Modules { get; set; } = new();
    public string Title { get; set; } = "";
    public string Finding { get; set; } = "";
    public string WhyItMatters { get; set; } = "";
    public List<string> Recommendations { get; set; } = new();
    public string Recommendation { get; set; } = "";
    public List<RiskFindingBasis> Basis { get; set; } = new();
    public bool LawyerRequired { get; set; }
    public string Resolution { get; set; } = "self";
    public string? ServiceCode { get; set; }
    public string? Cta { get; set; }
}

public class DimensionScore
{
    public string DimensionId { get; set; } = "";
    public int Score { get; set; }
    public double Weight { get; set; }
    public bool IsApplicable { get; set; } = true;
}

public class SectionScore
{
    public string SectionId { get; set; } = "";
    public string Title { get; set; } = "";
    public int? Score { get; set; }
    public double Weight { get; set; }
    public string Status { get; set; } = "APPLICABLE"; // APPLICABLE, N_A
    public int Confidence { get; set; } = 100;
    public List<DimensionScore> Dimensions { get; set; } = new();
    public List<string> Findings { get; set; } = new();
    public List<string> StrongAreas { get; set; } = new();
}

public class InvestmentReadinessOverlay
{
    public bool Applicable { get; set; } = true;
    public int ReadinessScore { get; set; }
    public List<string> Blockers { get; set; } = new();
}

public class ConsultingRecommendation
{
    public string PrimaryServiceCode { get; set; } = "FULL_LEGAL_ARCHITECTURE";
    public string PrimaryCta { get; set; } = "Провести полный юридический аудит компании";
    public string? SecondaryServiceCode { get; set; }
    public string? SecondaryCta { get; set; }
    public int ConsultingOpportunityScore { get; set; }
}

public class ScoreVersions
{
    public string QuestionBank { get; set; } = "1.1.0";
    public string ScoringEngine { get; set; } = "1.1.0";
    public string RiskLibrary { get; set; } = "1.1.0";
}

public class SharedFactStore
{
    public Dictionary<string, object?> Facts { get; set; } = new();
}

public class ScoreResult
{
    public int Overall { get; set; }
    public int Confidence { get; set; } = 85;
    public string ConfidenceText { get; set; } = "Высокая определенность ответов.";
    public string Level { get; set; } = "strong";
    public string LevelTitle { get; set; } = "";
    public string LevelText { get; set; } = "";
    public List<SectionScore> Sections { get; set; } = new();
    public List<RiskFinding> Risks { get; set; } = new();
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public List<string> Strengths { get; set; } = new();
    public int AnsweredCount { get; set; }
    public InvestmentReadinessOverlay InvestmentReadiness { get; set; } = new();
    public ConsultingRecommendation Consulting { get; set; } = new();
    public ScoreVersions Versions { get; set; } = new();
    public string ComputedAt { get; set; } = DateTime.UtcNow.ToString("o");
}

public class DiagnosticSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string AnswersJson { get; set; } = "{}";
    public string? LastSectionId { get; set; }
    public string? CompletedAt { get; set; }
    public string? ResultJson { get; set; }
    public bool Paid { get; set; }
    public string? PaidAt { get; set; }
    public int? PaymentAmount { get; set; }
    public string? PaymentMethod { get; set; }
}

public class Lead
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = "";
    public string Type { get; set; } = "report_gate";
    public string Name { get; set; } = "";
    public string? Company { get; set; }
    public string? Website { get; set; }
    public string Email { get; set; } = "";
    public string? Messenger { get; set; }
    public string? Interest { get; set; }
    public string? SourceRiskCode { get; set; }
    public int HeatScore { get; set; }
    public string HeatLabel { get; set; } = "cold";
    public string Status { get; set; } = "new";
    public bool Paid { get; set; }
    public string? PaidAt { get; set; }
    public int? PaymentAmount { get; set; }
    public string? PaymentMethod { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
}
