using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

public class OverallScorer
{
    public static int ComputeOverallScore(double totalApplicableModuleWeight, double weightedModuleScoreSum)
    {
        if (totalApplicableModuleWeight <= 0) return 0;
        return (int)Math.Round(weightedModuleScoreSum / totalApplicableModuleWeight);
    }

    public static LegalScoreLevel GetLevel(int score)
    {
        if (score >= 80) return LegalScoreLevel.Strong;
        if (score >= 60) return LegalScoreLevel.Attention;
        if (score >= 40) return LegalScoreLevel.MaterialGaps;
        return LegalScoreLevel.StructuralRisks;
    }

    public static string GetLevelTitle(LegalScoreLevel level)
    {
        return level switch
        {
            LegalScoreLevel.Strong => "Сильная основа",
            LegalScoreLevel.Attention => "Есть вопросы, требующие внимания",
            LegalScoreLevel.MaterialGaps => "Существенные пробелы",
            LegalScoreLevel.StructuralRisks => "Структурные вопросы",
            _ => "Структурные вопросы"
        };
    }

    public static string GetLevelText(LegalScoreLevel level)
    {
        return level switch
        {
            LegalScoreLevel.Strong => "Базовый юридический контур сформирован на высоком уровне. Выявлены точечные зоны для усиления.",
            LegalScoreLevel.Attention => "Ключевые элементы структуры присутствуют, однако есть существенные моменты, требующие юридической доработки.",
            LegalScoreLevel.MaterialGaps => "Обнаружены пробелы в защите прав или оформлении структуры, создающие уязвимости для бизнеса.",
            LegalScoreLevel.StructuralRisks => "Юридическая основа бизнеса пока сформирована фрагментарно. Рекомендуется первоочередное закрытие критических рисков.",
            _ => "Юридическая основа бизнеса пока сформирована фрагментарно. Рекомендуется первоочередное закрытие критических рисков."
        };
    }
}
