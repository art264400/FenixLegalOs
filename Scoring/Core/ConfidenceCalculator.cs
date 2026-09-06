using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

public class ConfidenceTracker
{
    public double TotalDiagnosticQuestionWeight { get; private set; }
    public double WeightedConfidenceSum { get; private set; }
    public List<string> UnknownMaterialFacts { get; } = new();
    public HashSet<string> UnknownMaterialSections { get; } = new();

    public void TrackQuestion(
        ConfidenceClass confidenceClass,
        double dimWeight,
        double withinWeight,
        string? sectionId = null,
        string? sectionTitle = null,
        string? questionId = null)
    {
        double confFactor = confidenceClass switch
        {
            ConfidenceClass.Known => 1.0,
            ConfidenceClass.Partial => 0.5,
            ConfidenceClass.Unknown => 0.0,
            _ => 1.0
        };
        double effectiveQWeight = (dimWeight * withinWeight) / 100.0;
        TotalDiagnosticQuestionWeight += effectiveQWeight;
        WeightedConfidenceSum += confFactor * effectiveQWeight;

        if (confidenceClass is ConfidenceClass.Unknown or ConfidenceClass.Partial)
        {
            if (!string.IsNullOrWhiteSpace(questionId))
            {
                UnknownMaterialFacts.Add(questionId);
            }
            var title = !string.IsNullOrWhiteSpace(sectionTitle) ? sectionTitle : sectionId;
            if (!string.IsNullOrWhiteSpace(title))
            {
                UnknownMaterialSections.Add(title);
            }
        }
    }

    public int ComputeOverallConfidence()
    {
        if (TotalDiagnosticQuestionWeight <= 0) return 0;
        return (int)Math.Round((WeightedConfidenceSum / TotalDiagnosticQuestionWeight) * 100.0);
    }
}

public class ConfidenceCalculator
{
    public static string GetConfidenceText(int conf)
    {
        if (conf >= 80) return "Высокая определенность ответов.";
        if (conf >= 50) return "Умеренная определенность (часть ответов требует проверки фактов).";
        return "Низкая определенность (много ответов «Не уверен»). Рекомендуется уточнить факты.";
    }

    public static string? GetConfidenceExplanation(int conf, IEnumerable<string>? affectedSectionTitles)
    {
        var list = affectedSectionTitles?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
        if (list == null || list.Count == 0 || conf >= 100)
        {
            return null;
        }

        string affectedText = list.Count switch
        {
            1 => $"блока «{list[0]}»",
            2 => $"блоков «{list[0]}» и «{list[1]}»",
            _ => $"блоков {string.Join(", ", list.Take(list.Count - 1).Select(s => $"«{s}»"))} и «{list.Last()}»"
        };

        return $"По нескольким применимым вопросам получены неопределенные ответы. Это снижает точность оценки {affectedText}.";
    }
}
