using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Modules.Investment;

namespace FenixLegalOs.Scoring.Core;

public class InvestmentReadinessEvaluator
{
    public static InvestmentReadinessOverlay Calculate(
        int? investmentSectionScore,
        List<RiskFinding> findings,
        SharedFactStore facts)
    {
        return InvestmentReadinessCalculator.Calculate(investmentSectionScore, findings, facts);
    }
}
