using FenixLegalOs.Models;

namespace FenixLegalOs.Services;

public record LeadHeatResult(int Score, string Label);

public class LeadHeatEngine
{
    public static LeadHeatResult Compute(Dictionary<string, object> answers, ScoreResult? result, bool isConsultation)
    {
        int score = 0;

        if (answers.TryGetValue("i_round", out var roundVal) && roundVal != null)
        {
            var rStr = roundVal.ToString();
            if (rStr == "m3") score += 40;
            else if (rStr == "m3_6") score += 25;
            else if (rStr == "m6_12") score += 15;
        }

        if (result != null)
        {
            score += result.CriticalCount * 15;
            score += result.HighCount * 5;
        }

        if (isConsultation) score += 30;

        string label = score switch
        {
            >= 70 => "priority",
            >= 45 => "hot",
            >= 20 => "warm",
            _ => "cold"
        };

        return new LeadHeatResult(score, label);
    }
}
