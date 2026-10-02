using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Models;

/// <summary>
/// Safe pre-payment projection of a diagnostic result.
/// Premium findings, recommendations, roadmap data and internal scoring details
/// must never be added to this contract.
/// </summary>
public sealed class ScoreResultPreviewDto
{
    public bool IsPreview { get; init; } = true;
    public int Overall { get; init; }
    public int Confidence { get; init; }
    public string ConfidenceText { get; init; } = "";
    public LegalScoreLevel Level { get; init; }
    public string LevelTitle { get; init; } = "";
    public string LevelText { get; init; } = "";
    public List<SectionScorePreviewDto> Sections { get; init; } = new();
    public int CriticalCount { get; init; }
    public int HighCount { get; init; }
    public int MediumCount { get; init; }
    public int StrengthCount { get; init; }
    public int AnsweredCount { get; init; }

    public static ScoreResultPreviewDto From(ScoreResult result)
    {
        return new ScoreResultPreviewDto
        {
            Overall = result.Overall,
            Confidence = result.Confidence,
            ConfidenceText = result.ConfidenceText,
            Level = result.Level,
            LevelTitle = result.LevelTitle,
            LevelText = result.LevelText,
            Sections = result.Sections.Select(section => new SectionScorePreviewDto
            {
                SectionId = section.SectionId,
                Title = section.Title,
                Score = section.Score,
                Status = section.Status,
                Confidence = section.Confidence
            }).ToList(),
            CriticalCount = result.CriticalCount,
            HighCount = result.HighCount,
            MediumCount = result.MediumCount,
            StrengthCount = result.Strengths.Count,
            AnsweredCount = result.AnsweredCount
        };
    }
}

public sealed class SectionScorePreviewDto
{
    public string SectionId { get; init; } = "";
    public string Title { get; init; } = "";
    public int? Score { get; init; }
    public ApplicabilityStatus Status { get; init; } = ApplicabilityStatus.Applicable;
    public int Confidence { get; init; }
}
