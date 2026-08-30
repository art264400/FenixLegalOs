using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

public class StrongAreasCalculator
{
    public static List<string> CalculateStrongAreas(
        IEnumerable<DimensionScore> allDimensionScores,
        IEnumerable<RiskFinding> mergedFindings,
        Func<string, string>? displayNameProvider = null)
    {
        var displayResolver = displayNameProvider ?? DataBank.GetDimensionDisplayName;

        var severeFindings = mergedFindings
            .Where(r => r.Severity is RiskSeverity.Critical or RiskSeverity.High or RiskSeverity.Blocker)
            .ToList();

        var strongDimensionIds = new List<string>();

        foreach (var dim in allDimensionScores)
        {
            if (!dim.IsApplicable || dim.Score < 80)
                continue;

            bool hasSevereRisk = severeFindings.Any(r => GetAffectedDimensionsForFinding(r).Contains(dim.DimensionId));
            if (!hasSevereRisk)
            {
                strongDimensionIds.Add(dim.DimensionId);
            }
        }

        return strongDimensionIds
            .Distinct()
            .Select(displayResolver)
            .ToList();
    }

    public static List<string> GetAffectedDimensions(string riskCode)
    {
        var risk = DataBank.Risks.FirstOrDefault(r => r.Code == riskCode);
        if (risk == null)
        {
            throw new InvalidOperationException($"Unknown risk code '{riskCode}' in RiskLibrary.");
        }
        return risk.AffectedDimensions;
    }

    public static string GetDimensionDisplayName(string dimensionId)
    {
        return DataBank.GetDimensionDisplayName(dimensionId);
    }

    private static List<string> GetAffectedDimensionsForFinding(RiskFinding finding)
    {
        if (finding.AffectedDimensions != null && finding.AffectedDimensions.Count > 0)
        {
            return finding.AffectedDimensions;
        }

        return GetAffectedDimensions(finding.Code);
    }
}
