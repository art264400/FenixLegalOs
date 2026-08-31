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
        var findingsList = mergedFindings?.ToList() ?? new List<RiskFinding>();

        var strongDimensionIds = new List<string>();

        foreach (var dim in allDimensionScores)
        {
            if (!dim.IsApplicable || dim.Score < 80)
                continue;

            if (!IsDimensionConflictedWithRisks(dim.DimensionId, findingsList))
            {
                strongDimensionIds.Add(dim.DimensionId);
            }
        }

        return strongDimensionIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(displayResolver)
            .ToList();
    }

    /// <summary>
    /// Checks whether a dimension's positive claim conflicts with any active legal risk findings.
    /// Eliminates semantic contradictions such as claiming 'Отсутствие споров' when ownership is disputed.
    /// </summary>
    public static bool IsDimensionConflictedWithRisks(string dimensionId, IEnumerable<RiskFinding> findings)
    {
        if (findings == null) return false;

        // Invariant: Only severe findings (Blocker, Critical, High) block positive claims / Strong Areas
        var severeRisks = findings
            .Where(r => r.Severity is RiskSeverity.Critical or RiskSeverity.High or RiskSeverity.Blocker)
            .ToList();

        foreach (var r in severeRisks)
        {
            // 1. Direct affected dimensions match
            var aff = GetAffectedDimensionsForFinding(r);
            if (aff.Contains(dimensionId, StringComparer.OrdinalIgnoreCase))
                return true;

            // 2. Semantic Cross-Risk Conflict Rules
            var rCode = r.Code ?? "";
            var rGrp = r.RootCauseGroup ?? "";

            // A. Dispute & Founder Conflict Contradictions
            if (dimensionId.Equals("existing_dispute", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("founder_disputes", StringComparison.OrdinalIgnoreCase))
            {
                if (rGrp.Equals("FOUNDER_CONFLICT", StringComparison.OrdinalIgnoreCase) ||
                    rGrp.Equals("OWNERSHIP", StringComparison.OrdinalIgnoreCase) ||
                    rGrp.Equals("FOUNDER_EQUITY", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("DISPUTE", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("CONFLICT", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // B. Equity & Cap Table Contradictions
            if (dimensionId.Equals("equity_clarity", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("cap_table_clarity", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("ownership_accuracy", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("ownership_history", StringComparison.OrdinalIgnoreCase))
            {
                if (rGrp.Equals("FOUNDER_EQUITY", StringComparison.OrdinalIgnoreCase) ||
                    rGrp.Equals("EQUITY_PROMISE", StringComparison.OrdinalIgnoreCase) ||
                    rGrp.Equals("OWNERSHIP", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("EQUITY", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("CAP_TABLE", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("DISPUTE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // C. Governance & Deadlock Contradictions
            if (dimensionId.Equals("governance", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("deadlock", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("corp_governance", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("signatory_powers", StringComparison.OrdinalIgnoreCase))
            {
                if (rGrp.Equals("FOUNDER_DEADLOCK", StringComparison.OrdinalIgnoreCase) ||
                    rGrp.Equals("CORPORATE_GOVERNANCE", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("DEADLOCK", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("SIGNATORY", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("GOVERNANCE", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // D. Vesting & Exit Contradictions
            if (dimensionId.Equals("early_exit_equity", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("exit_continuity", StringComparison.OrdinalIgnoreCase))
            {
                if (rGrp.Equals("FOUNDER_VESTING", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("VESTING", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("LEAVER", StringComparison.OrdinalIgnoreCase) ||
                    rCode.Contains("EXIT", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // E. IP Contradictions
            if (dimensionId.StartsWith("ip_", StringComparison.OrdinalIgnoreCase))
            {
                if (r.SectionId.Equals("ip", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // F. Team Contradictions
            if (dimensionId.StartsWith("team_", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("written_agreements", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("work_format", StringComparison.OrdinalIgnoreCase))
            {
                if (r.SectionId.Equals("team", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // G. Data & AI Contradictions
            if (dimensionId.StartsWith("data_", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.StartsWith("ai_", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("privacy_notice", StringComparison.OrdinalIgnoreCase))
            {
                if (r.SectionId.Equals("data", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // H. Contracts Contradictions
            if (dimensionId.StartsWith("contract_", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("written_form", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("model_match", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("risk_allocation", StringComparison.OrdinalIgnoreCase))
            {
                if (r.SectionId.Equals("contracts", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // I. Investment Contradictions
            if (dimensionId.StartsWith("invest_", StringComparison.OrdinalIgnoreCase) ||
                dimensionId.Equals("investment_readiness", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static List<string> GetAffectedDimensions(string riskCode)
    {
        var risk = DataBank.Risks.FirstOrDefault(r => r.Code.Equals(riskCode, StringComparison.OrdinalIgnoreCase));
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
