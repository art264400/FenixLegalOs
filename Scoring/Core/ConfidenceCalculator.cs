namespace FenixLegalOs.Scoring.Core;

public class ConfidenceTracker
{
    public double TotalDiagnosticQuestionWeight { get; private set; }
    public double WeightedConfidenceSum { get; private set; }

    public void TrackQuestion(string? confidenceClass, double dimWeight, double withinWeight)
    {
        double confFactor = confidenceClass switch
        {
            "known" => 1.0,
            "partial" => 0.5,
            "unknown" => 0.0,
            _ => 1.0
        };
        double effectiveQWeight = (dimWeight * withinWeight) / 100.0;
        TotalDiagnosticQuestionWeight += effectiveQWeight;
        WeightedConfidenceSum += confFactor * effectiveQWeight;
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
}
