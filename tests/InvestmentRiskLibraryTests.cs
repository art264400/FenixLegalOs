using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Core;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentRiskLibraryTests
{
    [Fact(DisplayName = "1. Exactly 12 Investment RiskDefinitions registered with unique exact codes")]
    public void Investment_Risks_Count_And_Uniqueness()
    {
        Assert.Equal(12, InvestmentRisks.All.Count);

        var codes = InvestmentRisks.All.Select(r => r.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());

        var expectedCodes = new[]
        {
            "INVEST_PRIOR_INVESTMENT_UNCLEAR",
            "INVEST_FUTURE_CAP_TABLE_UNCLEAR",
            "INVEST_DILUTION_NOT_MODELED",
            "INVEST_ROUND_NOT_DEFINED",
            "INVEST_RUNWAY_WARNING",
            "INVEST_FIN_MODEL_WEAK",
            "INVEST_METRICS_UNVERIFIABLE",
            "INVEST_DD_DOCS_NOT_READY",
            "INVEST_TERMS_NOT_UNDERSTOOD",
            "INVEST_DEAL_UNREVIEWED",
            "INVEST_SELF_AWARENESS_GAP",
            "INVEST_ROUND_BLOCKER"
        };

        Assert.Equal(expectedCodes.OrderBy(x => x), codes.OrderBy(x => x));
    }

    [Fact(DisplayName = "2. Total system risks count in DataBank is exactly 100")]
    public void Total_System_Risks_Count()
    {
        Assert.Equal(100, DataBank.Risks.Count);
        Assert.Equal(12, DataBank.Risks.Count(r => r.SectionId == "investment"));
    }

    [Fact(DisplayName = "3. Exact canonical severity metadata for all 12 Investment risks")]
    public void Investment_Risks_Severity_Metadata()
    {
        var riskMap = InvestmentRisks.All.ToDictionary(r => r.Code);

        Assert.Equal(RiskSeverity.High, riskMap["INVEST_PRIOR_INVESTMENT_UNCLEAR"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_FUTURE_CAP_TABLE_UNCLEAR"].Severity);
        Assert.Equal(RiskSeverity.Medium, riskMap["INVEST_DILUTION_NOT_MODELED"].Severity);
        Assert.Equal(RiskSeverity.Medium, riskMap["INVEST_ROUND_NOT_DEFINED"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_RUNWAY_WARNING"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_FIN_MODEL_WEAK"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_METRICS_UNVERIFIABLE"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_DD_DOCS_NOT_READY"].Severity);
        Assert.Equal(RiskSeverity.Critical, riskMap["INVEST_TERMS_NOT_UNDERSTOOD"].Severity);
        Assert.Equal(RiskSeverity.High, riskMap["INVEST_DEAL_UNREVIEWED"].Severity);
        Assert.Equal(RiskSeverity.Medium, riskMap["INVEST_SELF_AWARENESS_GAP"].Severity);
        Assert.Equal(RiskSeverity.Blocker, riskMap["INVEST_ROUND_BLOCKER"].Severity);
    }

    [Fact(DisplayName = "4. Exact canonical RootCauseGroups and ServiceCodes")]
    public void Investment_Risks_Groups_And_Services()
    {
        var riskMap = InvestmentRisks.All.ToDictionary(r => r.Code);

        Assert.Equal("INVESTMENT_HISTORY", riskMap["INVEST_PRIOR_INVESTMENT_UNCLEAR"].RootCauseGroup);
        Assert.Equal("EQUITY_PROMISE", riskMap["INVEST_FUTURE_CAP_TABLE_UNCLEAR"].RootCauseGroup);
        Assert.Equal("INVESTMENT_ECONOMICS", riskMap["INVEST_DILUTION_NOT_MODELED"].RootCauseGroup);
        Assert.Equal("INVESTMENT_PREPARATION", riskMap["INVEST_ROUND_NOT_DEFINED"].RootCauseGroup);
        Assert.Equal("INVESTMENT_PREPARATION", riskMap["INVEST_RUNWAY_WARNING"].RootCauseGroup);
        Assert.Equal("INVESTMENT_PREPARATION", riskMap["INVEST_FIN_MODEL_WEAK"].RootCauseGroup);
        Assert.Equal("INVESTMENT_EVIDENCE", riskMap["INVEST_METRICS_UNVERIFIABLE"].RootCauseGroup);
        Assert.Equal("INVESTMENT_DOCUMENTS", riskMap["INVEST_DD_DOCS_NOT_READY"].RootCauseGroup);
        Assert.Equal("INVESTMENT_DEAL", riskMap["INVEST_TERMS_NOT_UNDERSTOOD"].RootCauseGroup);
        Assert.Equal("INVESTMENT_DEAL", riskMap["INVEST_DEAL_UNREVIEWED"].RootCauseGroup);
        Assert.Equal("INVESTMENT_READINESS", riskMap["INVEST_SELF_AWARENESS_GAP"].RootCauseGroup);
        Assert.Equal("ROUND_BLOCKER", riskMap["INVEST_ROUND_BLOCKER"].RootCauseGroup);

        Assert.Equal("INVESTOR_READINESS", riskMap["INVEST_PRIOR_INVESTMENT_UNCLEAR"].ServiceCode);
        Assert.Equal("DEAL_SUPPORT", riskMap["INVEST_TERMS_NOT_UNDERSTOOD"].ServiceCode);
        Assert.Equal("DEAL_SUPPORT", riskMap["INVEST_DEAL_UNREVIEWED"].ServiceCode);
    }

    [Fact(DisplayName = "5. No invented suppressions in Investment RiskDefinitions")]
    public void Investment_Risks_SuppressCodes_Empty()
    {
        foreach (var risk in InvestmentRisks.All)
        {
            Assert.True(risk.SuppressCodes == null || risk.SuppressCodes.Count == 0,
                $"Risk {risk.Code} has non-empty suppress codes");
        }
    }

    [Fact(DisplayName = "6. Affected dimensions mapped only to canonical dimensions")]
    public void Investment_Risks_Affected_Dimensions()
    {
        var riskMap = InvestmentRisks.All.ToDictionary(r => r.Code);

        Assert.Contains("prior_investments", riskMap["INVEST_PRIOR_INVESTMENT_UNCLEAR"].AffectedDimensions);
        Assert.Contains("future_ownership", riskMap["INVEST_FUTURE_CAP_TABLE_UNCLEAR"].AffectedDimensions);
        Assert.Contains("dilution", riskMap["INVEST_DILUTION_NOT_MODELED"].AffectedDimensions);
        Assert.Contains("round_definition", riskMap["INVEST_ROUND_NOT_DEFINED"].AffectedDimensions);
        Assert.Contains("runway", riskMap["INVEST_RUNWAY_WARNING"].AffectedDimensions);
        Assert.Contains("financial_model", riskMap["INVEST_FIN_MODEL_WEAK"].AffectedDimensions);
        Assert.Contains("metrics_evidence", riskMap["INVEST_METRICS_UNVERIFIABLE"].AffectedDimensions);
        Assert.Contains("dd_documents", riskMap["INVEST_DD_DOCS_NOT_READY"].AffectedDimensions);
        Assert.Contains("deal_terms", riskMap["INVEST_TERMS_NOT_UNDERSTOOD"].AffectedDimensions);
        Assert.Contains("deal_review", riskMap["INVEST_DEAL_UNREVIEWED"].AffectedDimensions);

        // Cross-cutting risks have empty affected dimensions (not mapped to scored dimensions)
        Assert.Empty(riskMap["INVEST_SELF_AWARENESS_GAP"].AffectedDimensions);
        Assert.Empty(riskMap["INVEST_ROUND_BLOCKER"].AffectedDimensions);
    }

    [Fact(DisplayName = "7. [Regression] Cross-cutting RiskDefinition with AffectedDimensions = [] is valid and not attached to unrelated dimensions")]
    public void CrossCutting_RiskDefinitions_Are_Not_Forcibly_Mapped()
    {
        var gapRisk = InvestmentRisks.All.First(r => r.Code == "INVEST_SELF_AWARENESS_GAP");
        var blockerRisk = InvestmentRisks.All.First(r => r.Code == "INVEST_ROUND_BLOCKER");

        Assert.Empty(gapRisk.AffectedDimensions);
        Assert.Empty(blockerRisk.AffectedDimensions);

        // StrongAreasCalculator handles empty AffectedDimensions without throwing or polluting dimensions
        var dims = StrongAreasCalculator.GetAffectedDimensions(gapRisk.Code);
        Assert.Empty(dims);
    }
}
