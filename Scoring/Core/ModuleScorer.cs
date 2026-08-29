using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Core;

public class ModuleScorer
{
    public static bool IsModuleApplicable(string sectionId, SharedFactStore facts, List<DiagnosticQuestion> sectionQs)
    {
        var f = facts.Facts;
        return sectionId switch
        {
            "founders" => true,
            "corporate" => (string?)f.GetValueOrDefault("company.entityStatus") is "incorporated" or "single" or "multiple" or "registering",
            "ip" => true,
            "team" => GetBoolFact(f, "team.hasNonFounderTeam"),
            "data" => GetBoolFact(f, "data.personalDataProcessed") || GetBoolFact(f, "ai.used"),
            "contracts" => GetBoolFact(f, "contracts.b2bRelevant"),
            "investment" => (string?)f.GetValueOrDefault("investment.timing") != "none" || GetBoolFact(f, "investment.priorInvestment"),
            _ => sectionQs.Count > 0
        };
    }

    public static int? ComputeSectionScore(
        DiagnosticSection section,
        double totalApplicableDimensionWeight,
        double weightedDimensionScoreSum,
        SharedFactStore facts)
    {
        if (totalApplicableDimensionWeight > 0)
        {
            return (int)Math.Round(weightedDimensionScoreSum / totalApplicableDimensionWeight);
        }

        // Generic normative policy: if module has a normative score registered in facts
        // (e.g. {sectionId}.normativeModuleScore = 100 for solo founders)
        if (facts.Facts.TryGetValue($"{section.Id}.normativeModuleScore", out var normVal) && normVal != null)
        {
            if (normVal is int normInt) return normInt;
            if (int.TryParse(normVal.ToString(), out var parsedInt)) return parsedInt;
        }

        return null;
    }

    private static bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }
}
