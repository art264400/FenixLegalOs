namespace FenixLegalOs.Scoring.Core;

public class OverallScorer
{
    public static int ComputeOverallScore(double totalApplicableModuleWeight, double weightedModuleScoreSum)
    {
        if (totalApplicableModuleWeight <= 0) return 0;
        return (int)Math.Round(weightedModuleScoreSum / totalApplicableModuleWeight);
    }

    public static string GetLevel(int score)
    {
        if (score >= 80) return "strong";
        if (score >= 60) return "attention";
        if (score >= 40) return "material_gaps";
        return "structural_risks";
    }

    public static string GetLevelTitle(string level)
    {
        return level switch
        {
            "strong" => "Сильная основа",
            "attention" => "Есть вопросы, требующие внимания",
            "material_gaps" => "Существенные пробелы",
            _ => "Структурные вопросы"
        };
    }

    public static string GetLevelText(string level)
    {
        return level switch
        {
            "strong" => "Базовый юридический контур сформирован на высоком уровне. Выявлены точечные зоны для усиления.",
            "attention" => "Ключевые элементы структуры присутствуют, однако есть существенные моменты, требующие юридической доработки.",
            "material_gaps" => "Обнаружены пробелы в защите прав или оформлении структуры, создающие уязвимости для бизнеса.",
            _ => "Юридическая основа бизнеса пока сформирована фрагментарно. Рекомендуется первоочередное закрытие критических рисков."
        };
    }
}
