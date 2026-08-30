using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Modules.Investment;

public record MatrixRow(
    string ProblemLabel,
    List<string> RiskCodes,
    string RootCauseGroup,
    RiskSeverity? LongHorizonSeverity,
    RiskSeverity? MidTermSeverity,
    RiskSeverity? ActiveRoundSeverity
);

public static class InvestmentBlockerMatrix
{
    public static readonly List<MatrixRow> Rows = new()
    {
        // 1. Неоформленные прошлые инвестиции (§17.3)
        new(
            "Неоформленные прошлые инвестиции",
            new() { "INVEST_PRIOR_INVESTMENT_UNCLEAR" },
            "INVESTMENT_HISTORY",
            RiskSeverity.High,
            RiskSeverity.Critical,
            RiskSeverity.Blocker
        ),

        // 2. Спор о долях (§17.3)
        new(
            "Спор о долях",
            new() { "FND_EQUITY_DISPUTE", "COR_OWNERSHIP_DISPUTE" },
            "EQUITY_DISPUTE",
            RiskSeverity.Critical,
            RiskSeverity.Blocker,
            RiskSeverity.Blocker
        ),

        // 3. Существенное расхождение долей и документов (§17.3)
        new(
            "Существенное расхождение долей и документов",
            new() { "COR_OWNERSHIP_MISMATCH" },
            "CAP_TABLE",
            RiskSeverity.High,
            RiskSeverity.Critical,
            RiskSeverity.Blocker
        ),

        // 4. Права на основной продукт не подтверждены (§17.3)
        new(
            "Права на основной продукт не подтверждены",
            new() { "IP_PRODUCT_RIGHTS_UNCONFIRMED" },
            "CORE_IP",
            RiskSeverity.High,
            RiskSeverity.Critical,
            RiskSeverity.Blocker
        ),

        // 5. Ушедший основатель с нерешенной долей (§17.3)
        new(
            "Ушедший основатель с нерешенной долей",
            new() { "FND_DEPARTED_UNRESOLVED" },
            "FOUNDER_EXIT",
            RiskSeverity.High,
            RiskSeverity.Critical,
            RiskSeverity.Blocker
        ),

        // 6. Ключевые финансовые показатели нельзя подтвердить (§17.3)
        new(
            "Ключевые финансовые показатели нельзя подтвердить",
            new() { "INVEST_METRICS_UNVERIFIABLE" },
            "INVESTMENT_EVIDENCE",
            RiskSeverity.Medium,
            RiskSeverity.High,
            RiskSeverity.Critical
        ),

        // 7. Документы не систематизированы (§17.3)
        new(
            "Документы не систематизированы",
            new() { "INVEST_DD_DOCS_NOT_READY" },
            "INVESTMENT_DOCUMENTS",
            RiskSeverity.Medium,
            RiskSeverity.Medium,
            RiskSeverity.High
        ),

        // 8. Нет финансовой модели (§17.3)
        new(
            "Нет финансовой модели",
            new() { "INVEST_FIN_MODEL_WEAK" },
            "INVESTMENT_PREPARATION",
            RiskSeverity.Medium,
            RiskSeverity.High,
            RiskSeverity.High
        ),

        // 9. Не понятны последствия условий инвестора (§17.3)
        new(
            "Не понятны последствия условий инвестора",
            new() { "INVEST_TERMS_NOT_UNDERSTOOD" },
            "INVESTMENT_DEAL",
            null, // N/A on >12m
            null, // N/A on 3-12m
            RiskSeverity.Critical
        )
    };

    public static RiskSeverity? GetTargetSeverity(string riskCode, FundraisingTimingBucket bucket)
    {
        var row = Rows.FirstOrDefault(r => r.RiskCodes.Contains(riskCode, StringComparer.OrdinalIgnoreCase));
        if (row == null) return null;

        return bucket switch
        {
            FundraisingTimingBucket.ActiveRound => row.ActiveRoundSeverity,
            FundraisingTimingBucket.MidTerm => row.MidTermSeverity,
            FundraisingTimingBucket.None => row.LongHorizonSeverity,
            _ => null
        };
    }

    /// <summary>
    /// Applies fundraising timing severity and priority overlays to final findings (§17.3 / §18).
    /// </summary>
    public static List<RiskFinding> ApplyOverlay(
        List<RiskFinding> findings,
        SharedFactStore facts)
    {
        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        bool priorInvestment = facts.Facts.TryGetValue("investment.priorInvestment", out var pi) && (pi is true || pi?.ToString() == "unknown");
        bool isInvestmentApplicable = timing != "none" || priorInvestment;

        // If investment is not applicable or timing is none, no timing overlay applies
        if (!isInvestmentApplicable || string.IsNullOrEmpty(timing) || timing == "none")
        {
            return findings;
        }

        var bucket = InvestmentTimingClassifier.GetTimingBucket(facts);
        bool isCloseOrActive = InvestmentTimingClassifier.IsCloseOrActiveRound(facts);

        var result = new List<RiskFinding>();

        foreach (var finding in findings)
        {
            var targetSev = GetTargetSeverity(finding.Code, bucket);
            var updatedSev = targetSev.HasValue ? targetSev.Value : finding.Severity;

            var updatedPriority = finding.Priority;
            if (isCloseOrActive && (updatedSev is RiskSeverity.High or RiskSeverity.Critical or RiskSeverity.Blocker))
            {
                // §18: fundraising within 6 months: DD-sensitive High issues get priority BEFORE_ROUND / Now
                updatedPriority = RiskPriority.Now;
            }

            result.Add(new RiskFinding
            {
                Code = finding.Code,
                SectionId = finding.SectionId,
                Modules = new(finding.Modules),
                Severity = updatedSev,
                Priority = updatedPriority,
                RootCauseGroup = finding.RootCauseGroup,
                Resolution = finding.Resolution,
                ServiceCode = finding.ServiceCode,
                Title = finding.Title,
                Finding = finding.Finding,
                WhyItMatters = finding.WhyItMatters,
                Recommendation = finding.Recommendation,
                Recommendations = new(finding.Recommendations),
                AffectedDimensions = new(finding.AffectedDimensions),
                LawyerRequired = finding.LawyerRequired,
                Basis = new(finding.Basis)
            });
        }

        return result;
    }
}
