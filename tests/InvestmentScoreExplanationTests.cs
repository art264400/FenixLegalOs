using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentScoreExplanationTests
{
    [Fact]
    public void CurrentAnswers65_ExplainLargestWeightedShortfallsAndConfirmedPreparation()
    {
        var answers = new Dictionary<string, object>
        {
            ["INVEST-01"] = "3_6", ["INVEST-02"] = "no", ["INVEST-03"] = "current_only",
            ["INVEST-04"] = "rough", ["INVEST-05"] = "amount_rough", ["INVEST-06"] = "rough",
            ["INVEST-06A"] = "3_6", ["INVEST-07"] = "simple", ["INVEST-08"] = "yes",
            ["INVEST-09"] = "scattered", ["INVEST-10"] = "none", ["INVEST-11"] = "current"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var visible = InvestmentQuestions.All.Where(q => !new[] { "INVEST-02A", "INVEST-12", "INVEST-13", "INVEST-14", "INVEST-15" }.Contains(q.Id)).ToList();
        var dimensions = DimensionScorer.ComputeDimensions(visible, answers);
        var section = Section(dimensions.Dimensions.ToArray());
        section.Score = ModuleScorer.ComputeSectionScore(
            new DiagnosticSection("investment", 8, "Инвестиции", "Investment", 12),
            dimensions.TotalApplicableDimensionWeight, dimensions.WeightedDimensionScoreSum, facts);
        var investment = Investment(section.Score!.Value, section.Score.Value);

        InvestmentScoreExplanationBuilder.Populate(investment, section, facts);

        Assert.Equal(65, section.Score);
        Assert.Equal(new[] { "future_ownership", "dd_documents", "dilution" }, investment.MainScoreReasons.Select(r => r.DimensionId));
        Assert.Equal(new[] { 40, 62, 50 }, investment.MainScoreReasons.Select(r => r.Score));
        Assert.Equal(720.0 / 73, investment.MainScoreReasons[0].Impact, 8);
        Assert.Equal(570.0 / 73, investment.MainScoreReasons[1].Impact, 8);
        Assert.Contains("будущие изменения", investment.MainScoreReasons[0].Explanation);
        Assert.Contains("разных местах", investment.MainScoreReasons[1].Explanation);
        Assert.Contains("приблизительно", investment.MainScoreReasons[2].Explanation);
        Assert.Contains("сумма и цели финансирования", investment.AdditionalScoreReasons);
        Assert.Contains("расчет финансового запаса", investment.AdditionalScoreReasons);
        Assert.Contains("финансовый план", investment.AdditionalScoreReasons);
        Assert.Equal(2, investment.PreparedItems.Count);
        Assert.Contains(investment.PreparedItems, p => p.Contains("показатели"));
        Assert.Contains(investment.PreparedItems, p => p.Contains("Презентация"));
        Assert.Contains("Дополнительное ограничение не применялось", investment.BlockerEffectExplanation);
        Assert.Equal(65, investment.BaseScore);
        Assert.Equal(65, investment.ReadinessScore);
    }

    [Theory]
    [InlineData(100, 1, 59, 59, false)]
    [InlineData(100, 2, 39, 39, false)]
    [InlineData(100, 5, 39, 39, false)]
    [InlineData(40, 1, 40, 59, true)]
    [InlineData(30, 2, 30, 39, true)]
    [InlineData(59, 1, 59, 59, true)]
    public void BlockerCeilingIsExplainedAsLimitNotSubtractedPenalty(
        int baseScore, int blockers, int readinessScore, int cap, bool noExtraReduction)
    {
        var investment = Investment(baseScore, readinessScore);
        investment.UnresolvedBlockersCount = blockers;
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("metrics_evidence", 100, 10)), new SharedFactStore());

        Assert.Contains($"{cap}/100", investment.BlockerEffectExplanation);
        if (noExtraReduction) Assert.Contains("дополнительного снижения нет", investment.BlockerEffectExplanation);
        else Assert.Contains("не дополнительный штраф", investment.BlockerEffectExplanation);
        Assert.Equal(baseScore, investment.BaseScore);
        Assert.Equal(readinessScore, investment.ReadinessScore);
    }

    [Theory]
    [InlineData(100, 1, 45, true)]
    [InlineData(100, 2, 45, true)]
    [InlineData(65, 1, 40, true)]
    [InlineData(65, 0, 40, true)]
    [InlineData(100, 2, 39, false)]
    [InlineData(65, 0, 40, false)]
    public void LegacyReadinessNotMatchingCurrentRuleIsReportedWithoutInventedCapCause(
        int baseScore, int blockers, int savedReadiness, bool active)
    {
        var investment = Investment(baseScore, savedReadiness);
        investment.UnresolvedBlockersCount = blockers;
        investment.RoundIsCloseOrActive = active;
        InvestmentScoreExplanationBuilder.Populate(investment, Section(), new SharedFactStore());

        Assert.Contains($"базовая оценка: {baseScore}/100", investment.BlockerEffectExplanation);
        Assert.Contains($"итоговая готовность: {savedReadiness}/100", investment.BlockerEffectExplanation);
        Assert.Contains("не объясняют это различие", investment.BlockerEffectExplanation);
        Assert.Contains("без пересчета", investment.BlockerEffectExplanation);
        Assert.DoesNotContain("предел готовности", investment.BlockerEffectExplanation);
        Assert.DoesNotContain("ограничена уровнем", investment.BlockerEffectExplanation);
        Assert.DoesNotContain("Дополнительное ограничение не применялось", investment.BlockerEffectExplanation);
        Assert.Equal(baseScore, investment.BaseScore);
        Assert.Equal(savedReadiness, investment.ReadinessScore);
        Assert.False(investment.HasFactorBreakdown);
    }

    [Theory]
    [InlineData(true, false, false, "близкого или активного раунда не оценивались")]
    [InlineData(false, true, false, "рассматриваются как возможность")]
    [InlineData(false, false, true, "сведения о прошлых инвестициях")]
    [InlineData(false, false, false, "Планы нового раунда не указаны")]
    public void InactiveRoundDoesNotClaimBlockersWereFoundOrRuledOut(
        bool planned, bool possible, bool prior, string expected)
    {
        var investment = Investment(65, 65);
        investment.RoundIsCloseOrActive = false;
        investment.NewRoundPlanned = planned;
        investment.NewRoundPossible = possible;
        investment.HasPriorInvestment = prior;
        investment.UnresolvedBlockersCount = 2; // Even stale blocker metadata must not imply an assessed active round.
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("future_ownership", 40, 12)), new SharedFactStore());

        Assert.Contains(expected, investment.BlockerEffectExplanation);
        Assert.DoesNotContain("не выявлено", investment.BlockerEffectExplanation);
        Assert.DoesNotContain("39/100", investment.BlockerEffectExplanation);
    }

    [Fact]
    public void RankingUsesWeightedContributionNotLowestScoreAlone()
    {
        var section = Section(Dim("dilution", 0, 1), Dim("dd_documents", 60, 15), Dim("financial_model", 75, 10), Dim("future_ownership", 80, 12));
        var investment = Investment(65, 65);
        InvestmentScoreExplanationBuilder.Populate(investment, section, new SharedFactStore());

        Assert.Equal(new[] { "dd_documents", "financial_model", "future_ownership" }, investment.MainScoreReasons.Select(r => r.DimensionId));
        Assert.Contains("расчет размытия долей", investment.AdditionalScoreReasons);
        Assert.Equal(65, investment.BaseScore); // The explanation must never recompute the saved score.
    }

    [Fact]
    public void OnlyApplicablePositiveWeightFactorsCanExplainScore()
    {
        var excluded = Dim("dilution", 0, 100);
        excluded.IsApplicable = false;
        var investment = Investment(80, 80);
        InvestmentScoreExplanationBuilder.Populate(investment,
            Section(excluded, Dim("runway", 0, 0), Dim("deal_review", 0, double.NaN), Dim("prior_investments", -1, 10), Dim("financial_model", 80, 10)),
            new SharedFactStore());

        Assert.Single(investment.MainScoreReasons);
        Assert.Equal("financial_model", investment.MainScoreReasons[0].DimensionId);
        Assert.Equal(20, investment.MainScoreReasons[0].Impact);
    }

    [Fact]
    public void LegacyResultWithoutDimensionsDoesNotInventFactorsOrPreparation()
    {
        var investment = Investment(65, 65);
        investment.MainScoreReasons.Add(new() { Title = "stale" });
        investment.PreparedItems.Add("stale");
        var facts = Facts(("investment.metricsEvidence", "yes"), ("investment.pitchDeck", "current"));
        InvestmentScoreExplanationBuilder.Populate(investment, Section(), facts);

        Assert.False(investment.HasFactorBreakdown);
        Assert.Empty(investment.MainScoreReasons);
        Assert.Empty(investment.PreparedItems);
        Assert.Empty(investment.AdditionalScoreReasons);
        Assert.Contains("разбивка факторов", investment.ScoreExplanation);
        Assert.Contains("недоступна", investment.ScoreExplanation);
        Assert.Equal(65, investment.BaseScore);
    }

    [Fact]
    public void NullSectionUsesExplicitLegacyFallback()
    {
        var investment = Investment(70, 59);
        investment.UnresolvedBlockersCount = 1;
        InvestmentScoreExplanationBuilder.Populate(investment, null, new SharedFactStore());

        Assert.False(investment.HasFactorBreakdown);
        Assert.Contains("Сохраненная базовая оценка: 70/100", investment.ScoreExplanation);
        Assert.Contains("59/100", investment.BlockerEffectExplanation);
    }

    [Fact]
    public void FullFactorScoresDoNotInventReasonsEvenWithExternalBlockerCap()
    {
        var investment = Investment(100, 59);
        investment.UnresolvedBlockersCount = 1;
        var facts = Facts(("investment.metricsEvidence", "yes"));
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("metrics_evidence", 100, 10)), facts);

        Assert.True(investment.HasFactorBreakdown);
        Assert.Empty(investment.MainScoreReasons);
        Assert.Empty(investment.AdditionalScoreReasons);
        Assert.Contains("все применимые факторы получили полную оценку", investment.ScoreExplanation);
        Assert.Contains("59/100", investment.BlockerEffectExplanation);
        Assert.Single(investment.PreparedItems);
    }

    [Theory]
    [InlineData("future_ownership", "investment.futureOwnershipClarity")]
    [InlineData("dilution", "investment.dilutionModel")]
    [InlineData("round_definition", "investment.roundDefinition")]
    [InlineData("runway", "investment.runwayKnown")]
    [InlineData("financial_model", "investment.financialModel")]
    [InlineData("metrics_evidence", "investment.metricsEvidence")]
    [InlineData("dd_documents", "investment.documentFolder")]
    [InlineData("deal_terms", "investment.investorControl")]
    [InlineData("deal_review", "investment.dealReview")]
    public void UnknownAnswersAreUncertaintyNotAssertedAbsence(string dimension, string key)
    {
        var investment = Investment(15, 15);
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim(dimension, 15, 10)), Facts((key, "unknown")));
        var explanation = Assert.Single(investment.MainScoreReasons).Explanation;

        Assert.Contains("неопределенн", explanation.ToLowerInvariant());
        Assert.DoesNotContain("документов нет", explanation);
        Assert.DoesNotContain("отсутствуют", explanation);
        Assert.Empty(investment.PreparedItems);
    }

    [Fact]
    public void UnknownPastInvestmentsAreNotDescribedAsConfirmedFundsReceived()
    {
        var investment = Investment(15, 15);
        investment.NewRoundPlanned = false;
        investment.RoundIsCloseOrActive = false;
        investment.HasPriorInvestment = true;
        investment.PriorInvestmentStatus = "unknown";
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("prior_investments", 15, 15)),
            Facts(("investment.priorInvestment", "unknown")));

        Assert.Contains("не означает подтвержденное получение денег", Assert.Single(investment.MainScoreReasons).Explanation);
        Assert.Empty(investment.PreparedItems);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void SummaryPreservesUnknownPastInvestmentsWithOrWithoutLegacyBreakdown(
        bool legacyWithoutDimensions, bool uncertaintyInDto)
    {
        var investment = Investment(15, 15);
        investment.NewRoundPlanned = false;
        investment.RoundIsCloseOrActive = false;
        investment.HasPriorInvestment = true;
        investment.PriorInvestmentStatus = uncertaintyInDto ? "unknown" : null;
        var facts = uncertaintyInDto ? new SharedFactStore() : Facts(("investment.priorInvestment", "unknown"));
        var section = legacyWithoutDimensions ? Section() : Section(Dim("prior_investments", 15, 15));

        InvestmentScoreExplanationBuilder.Populate(investment, section, facts);

        Assert.Contains("Наличие или условия прошлых инвестиций не определены; их следует уточнить.", investment.ScoreExplanation);
        Assert.Empty(investment.PreparedItems);
        Assert.Equal(15, investment.BaseScore);
        Assert.Equal(15, investment.ReadinessScore);
        Assert.Equal(!legacyWithoutDimensions, investment.HasFactorBreakdown);
    }

    [Fact]
    public void ConfirmedPastInvestmentSummaryDoesNotAddUnknownQualifier()
    {
        var investment = Investment(100, 100);
        investment.HasPriorInvestment = true;
        investment.PriorInvestmentStatus = "true";
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("prior_investments", 100, 15)),
            Facts(("investment.priorInvestment", "true"), ("investment.priorInvestmentStatus", "formal")));

        Assert.DoesNotContain("не определены", investment.ScoreExplanation);
        Assert.Contains(investment.PreparedItems, item => item.Contains("оформлены документами"));
    }

    [Fact]
    public void PreparedItemsStayWithinActiveInvestmentDimensionsAndAtMostThree()
    {
        var investment = Investment(100, 100);
        var facts = Facts(("investment.metricsEvidence", "yes"), ("investment.pitchDeck", "current"),
            ("investment.documentFolder", "organized"), ("investment.financialModel", "current"),
            ("investment.dealReview", "specialist"));
        facts.Facts["product.stage"] = "live_or_ready";
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("metrics_evidence", 100, 10), Dim("dd_documents", 100, 15), Dim("financial_model", 100, 10)), facts);

        Assert.Equal(3, investment.PreparedItems.Count);
        Assert.DoesNotContain(investment.PreparedItems, p => p.Contains("юрист")); // Hidden deal review isn't evidence of preparation.
        Assert.DoesNotContain(investment.PreparedItems, p => p.Contains("продукт"));
    }

    [Fact]
    public void UnapplicableInvestmentDoesNotExplainOrAdvertiseHiddenAnswers()
    {
        var investment = Investment(0, 0);
        investment.IsApplicable = false;
        InvestmentScoreExplanationBuilder.Populate(investment, Section(Dim("metrics_evidence", 100, 10)),
            Facts(("investment.metricsEvidence", "yes")));

        Assert.False(investment.HasFactorBreakdown);
        Assert.Empty(investment.MainScoreReasons);
        Assert.Empty(investment.PreparedItems);
        Assert.Contains("неприменим", investment.ScoreExplanation);
    }

    [Fact]
    public void ExplanationDoesNotChangeSourceDimensionsOrTheirScores()
    {
        var dimension = Dim("future_ownership", 40, 12);
        var section = Section(dimension);
        section.Score = 65;
        var investment = Investment(65, 40);
        investment.UnresolvedBlockersCount = 1;
        InvestmentScoreExplanationBuilder.Populate(investment, section, Facts(("investment.futureOwnershipClarity", "current_only")));

        Assert.Equal(40, dimension.Score);
        Assert.Equal(12, dimension.Weight);
        Assert.Equal(65, section.Score);
        Assert.Equal(65, investment.BaseScore);
        Assert.Equal(40, investment.ReadinessScore);
    }

    private static InvestmentReadinessReportDto Investment(int baseScore, int readinessScore) => new()
    {
        IsApplicable = true, BaseScore = baseScore, ReadinessScore = readinessScore,
        NewRoundPlanned = true, RoundIsCloseOrActive = true, InvestmentTiming = "3_6m"
    };

    private static DimensionScore Dim(string id, int score, double weight) => new()
    {
        DimensionId = id, Score = score, Weight = weight, IsApplicable = true
    };

    private static SectionScore Section(params DimensionScore[] dimensions) => new()
    {
        SectionId = "investment", Title = "Инвестиции", Status = ApplicabilityStatus.Applicable,
        Dimensions = dimensions.ToList()
    };

    private static SharedFactStore Facts(params (string Key, string Value)[] values)
    {
        var facts = new SharedFactStore();
        foreach (var value in values) facts.Facts[value.Key] = value.Value;
        return facts;
    }
}