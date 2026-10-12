using FenixLegalOs.Data;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class FenixLawRecommendationGroundingTests
{
    [Theory]
    [InlineData("all-built")]
    [InlineData("real-problem")]
    [InlineData("mixed")]
    [InlineData("unknown")]
    [InlineData("no-problems")]
    public void EveryServiceItem_HasAnActualFindingAndFaithfulSourceAnswers(string scenario)
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, result, _, context) = fixture.Assemble(scenario);
        var recommendation = context.FenixLaw;
        Assert.Equal(recommendation.ServiceCards.Select(card => card.Title).ToArray(), recommendation.ServiceAreas.ToArray());
        var allItems = recommendation.ServiceCards.SelectMany(card => card.Items).ToList();
        Assert.Equal(allItems.Count, allItems.Select(item => item.FindingCode).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(recommendation.ServiceCards, card =>
        {
            Assert.NotEmpty(card.Items);
            Assert.Equal(card.Items.Select(item => item.FindingCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(code => code).ToArray(), card.SourceFindingCodes.OrderBy(code => code).ToArray());
            Assert.All(card.Items, item =>
            {
                var finding = Assert.Single(result.Risks.Where(risk => risk.Code == item.FindingCode));
                Assert.NotEmpty(item.Title);
                Assert.NotEmpty(item.Recommendation);
                Assert.Equal(finding.Basis.Select(basis => (basis.QuestionId, basis.AnswerId)).OrderBy(basis => basis.QuestionId).ToArray(), item.SourceAnswers.Select(basis => (basis.QuestionId, basis.AnswerId)).OrderBy(basis => basis.QuestionId).ToArray());
            });
        });
        Assert.Equal(allItems.Count > 0, recommendation.RequiresLegalWork);
    }

    [Theory]
    [InlineData("all-built")]
    [InlineData("no-problems")]
    public void NoConfirmedProblems_ProducesNeutralRecommendationWithoutServiceUpsell(string scenario)
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, result, _, context) = fixture.Assemble(scenario);
        Assert.Empty(result.Risks);
        Assert.Empty(context.ActionPlan);
        Assert.False(context.FenixLaw.RequiresLegalWork);
        Assert.Empty(context.FenixLaw.ServiceCards);
        Assert.Empty(context.FenixLaw.ServiceAreas);
        Assert.Contains("не выявлен", context.FenixLaw.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("разработ", context.FenixLaw.SummaryText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("устранению", context.FenixLaw.SummaryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OneRealProblem_OnlyRecommendsCorrectingExistingProductRules()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (answers, result, _, context) = fixture.Assemble("real-problem");
        Assert.Equal("current", answers["PROD-04"]);
        var finding = Assert.Single(result.Risks);
        Assert.Equal("PROD_RULES_MISMATCH", finding.Code);
        var card = Assert.Single(context.FenixLaw.ServiceCards);
        var item = Assert.Single(card.Items);
        Assert.Equal("PROD_RULES_MISMATCH", item.FindingCode);
        Assert.False(item.RequiresClarification);
        AssertCorrectionOfExistingDocument(item.Recommendation);
        AssertNoUnrelatedDocumentCreation(context.FenixLaw);
    }

    [Fact]
    public void MixedProblems_WithSignedAgreementAndPublishedPolicy_RecommendsSpecificChanges()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (answers, result, _, context) = fixture.Assemble("mixed");
        Assert.Equal("signed", answers["FND-C04"]);
        Assert.Equal("current", answers["PROD-04"]);
        Assert.Equal("yes", answers["DATA-06"]);
        Assert.Equal(new[] { "DATA_PRIVACY_NOTICE_OUTDATED", "FND_NO_DEADLOCK_PROTECTION", "PROD_RULES_MISMATCH" }, result.Risks.Select(risk => risk.Code).OrderBy(code => code).ToArray());
        Assert.Equal(3, context.FenixLaw.ServiceCards.Count);
        var items = context.FenixLaw.ServiceCards.SelectMany(card => card.Items).ToDictionary(item => item.FindingCode);
        AssertCorrectionOfExistingDocument(items["PROD_RULES_MISMATCH"].Recommendation);
        AssertCorrectionOfExistingDocument(items["DATA_PRIVACY_NOTICE_OUTDATED"].Recommendation);
        Assert.Contains("тупик", items["FND_NO_DEADLOCK_PROTECTION"].Recommendation, StringComparison.OrdinalIgnoreCase);
        AssertNoUnrelatedDocumentCreation(context.FenixLaw);
    }

    [Fact]
    public void UnknownAnswers_RequireClarificationInsteadOfClaimingDocumentsAreAbsent()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, result, _, context) = fixture.Assemble("unknown");
        Assert.NotEmpty(result.Risks);
        Assert.All(result.Risks, risk => Assert.Contains(risk.Basis, basis => basis.AnswerId == "unknown"));
        var items = context.FenixLaw.ServiceCards.SelectMany(card => card.Items).ToList();
        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.True(item.RequiresClarification);
            Assert.Contains(item.SourceAnswers, basis => basis.AnswerId == "unknown");
            Assert.True(new[] { "уточн", "провери", "подтверд" }.Any(word => item.Recommendation.Contains(word, StringComparison.OrdinalIgnoreCase)), item.Recommendation);
            Assert.DoesNotContain("документы отсутствуют", item.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("документов нет", item.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("соглашение отсутствует", item.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("политики конфиденциальности нет", item.Recommendation, StringComparison.OrdinalIgnoreCase);
        });
        AssertNoUnrelatedDocumentCreation(context.FenixLaw);
    }

    [Theory]
    [InlineData("unknown", "unknown", false)]
    [InlineData("partial", "no", true)]
    public void UnknownSensitiveDataTransfer_DoesNotBecomeProofThroughSyntheticRuleBasis(string disclosure, string providerReview, bool hasConfirmedIssue)
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["DATA-03"] = "sometimes";
        answers["AI-01"] = "external";
        answers["AI-02"] = "ordinary";
        answers["AI-03"] = disclosure;
        answers["AI-04"] = providerReview;
        answers["AI-05"] = "unknown";
        var (effective, result, facts, context) = fixture.Assemble(answers, "unknown-sensitive-transfer");
        Assert.Equal("unknown", effective["AI-05"]);
        Assert.Equal("unknown", facts.Facts["ai.sensitiveDataSent"]);
        var finding = Assert.Single(result.Risks.Where(risk => risk.Code == "AI_SENSITIVE_DATA_TRANSFER"));
        Assert.Contains(finding.Basis, basis => basis.QuestionId == "AI-05" && basis.AnswerId == "sensitive_transfer");
        var remediation = FindingRemediationBuilder.Build(finding, facts);
        Assert.True(remediation.RequiresClarification);
        Assert.Equal(hasConfirmedIssue, remediation.HasConfirmedIssue);
        Assert.Contains("Установить, попадают ли", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("При подтверждении передачи", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("данные пользователей передаются", remediation.Title, StringComparison.OrdinalIgnoreCase);
        var item = Assert.Single(context.FenixLaw.ServiceCards.SelectMany(card => card.Items).Where(item => item.FindingCode == finding.Code));
        Assert.True(item.RequiresClarification);
        Assert.Equal(hasConfirmedIssue, item.HasConfirmedIssue);
        Assert.Equal(remediation.WhatToDo, item.Recommendation);
        if (hasConfirmedIssue)
        {
            Assert.Contains("Доработать соответствующие положения действующих документов", item.Recommendation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Проверить условия выбранного режима", item.Recommendation, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            var presented = Assert.Single(context.AllFindings.Where(risk => risk.Code == finding.Code));
            Assert.Contains("не подтверждены", presented.Finding, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(finding.Finding, presented.Finding, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("known_not_reviewed", "known_not_reviewed")]
    [InlineData("no", "none")]
    public void UnknownExternalServiceMap_DoesNotEraseConfirmedUnreviewedVendorTerms(string vendorAnswer, string normalizedVendorReview)
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["DATA-10"] = "yes";
        answers["DATA-10A"] = "unknown";
        answers["DATA-11"] = vendorAnswer;
        var (_, result, facts, context) = fixture.Assemble(answers, "unknown-map-known-vendor-gap");
        Assert.Equal("unknown", facts.Facts["data.externalServiceMap"]);
        Assert.Equal(normalizedVendorReview, facts.Facts["data.vendorTermsReview"]);
        var finding = Assert.Single(result.Risks.Where(risk => risk.Code == "DATA_THIRD_PARTY_UNKNOWN"));
        // The rule records the first trigger only; the confirmed second issue comes from facts.
        Assert.Contains(finding.Basis, basis => basis.QuestionId == "DATA-10A" && basis.AnswerId == "unknown");
        var remediation = FindingRemediationBuilder.Build(finding, facts);
        Assert.True(remediation.RequiresClarification);
        Assert.True(remediation.HasConfirmedIssue);
        Assert.Contains("Проверить, составлен ли перечень", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проверить существенные условия", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        // This finding is classified as internal data mapping by the existing action library.
        // Preserve its mixed remediation in the plan without inventing a legal service.
        Assert.DoesNotContain(context.FenixLaw.ServiceCards.SelectMany(card => card.Items), item => item.FindingCode == finding.Code);
        var action = Assert.Single(context.ActionPlan.Where(action => action.CoveredFindingCodes.Contains(finding.Code)));
        Assert.Equal(ResolutionMode.InternalAction, action.ResolutionMode);
        Assert.True(action.UseCanonicalRemediation);
        Assert.Contains(remediation.WhatToDo, action.WhatToDo, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("partial")]
    [InlineData("informal")]
    public void UnknownPastInvestorRights_DoesNotEraseConfirmedIncompleteInvestmentDocumentation(string priorStatus)
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["INVEST-02"] = priorStatus;
        answers["INVEST-02A"] = "unknown";
        var (_, result, facts, context) = fixture.Assemble(answers, "past-investment-mixed");
        Assert.Equal(priorStatus, facts.Facts["investment.priorInvestmentStatus"]);
        Assert.Equal("unknown", facts.Facts["investment.priorRightsClarity"]);
        var finding = Assert.Single(result.Risks.Where(risk => risk.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR"));
        Assert.Contains(finding.Basis, basis => basis.QuestionId == "INVEST-02" && basis.AnswerId == priorStatus);
        Assert.Contains(finding.Basis, basis => basis.QuestionId == "INVEST-02A" && basis.AnswerId == "unknown");
        var remediation = FindingRemediationBuilder.Build(finding, facts);
        Assert.True(remediation.RequiresClarification);
        Assert.True(remediation.HasConfirmedIssue);
        Assert.Contains("завершить неполное оформление действующих договоренностей", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Проверить документы и уточнить", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        var item = Assert.Single(context.FenixLaw.ServiceCards.SelectMany(card => card.Items).Where(item => item.FindingCode == finding.Code));
        Assert.True(item.RequiresClarification);
        Assert.True(item.HasConfirmedIssue);
        Assert.Equal(remediation.WhatToDo, item.Recommendation);
        var action = Assert.Single(context.ActionPlan.Where(action => action.CoveredFindingCodes.Contains(finding.Code)));
        Assert.True(action.UseCanonicalRemediation);
        Assert.Contains(remediation.WhatToDo, action.WhatToDo, StringComparison.Ordinal);
    }

    [Fact]
    public void FormalPastInvestmentWithUnknownRights_RequiresVerificationWithoutInventingIncompleteDocuments()
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["INVEST-02"] = "formal";
        answers["INVEST-02A"] = "unknown";
        var (_, result, facts, context) = fixture.Assemble(answers, "past-investment-pure-unknown");
        var finding = Assert.Single(result.Risks.Where(risk => risk.Code == "INVEST_PRIOR_INVESTMENT_UNCLEAR"));
        var remediation = FindingRemediationBuilder.Build(finding, facts);
        Assert.True(remediation.RequiresClarification);
        Assert.False(remediation.HasConfirmedIssue);
        Assert.Contains("Проверить документы и уточнить", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("завершить неполное оформление", remediation.WhatToDo, StringComparison.OrdinalIgnoreCase);
        var item = Assert.Single(context.FenixLaw.ServiceCards.SelectMany(card => card.Items).Where(item => item.FindingCode == finding.Code));
        Assert.True(item.RequiresClarification);
        Assert.False(item.HasConfirmedIssue);
    }

    [Theory]
    [InlineData("none", "none", "INVEST_PRIOR_INVESTMENT_UNCLEAR")]
    [InlineData("possible_year", "within_12m", "INVEST_PRIOR_INVESTMENT_UNCLEAR")]
    [InlineData("none", "none", "INVEST_FUTURE_CAP_TABLE_UNCLEAR")]
    [InlineData("possible_year", "within_12m", "INVEST_FUTURE_CAP_TABLE_UNCLEAR")]
    public void ExistingCapitalIssues_StayVisibleWithoutClaimingAnUnplannedNewRound(string timingAnswer, string normalizedTiming, string code)
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["INVEST-01"] = timingAnswer;
        answers["INVEST-02"] = code == "INVEST_PRIOR_INVESTMENT_UNCLEAR" ? "partial" : "formal";
        answers["INVEST-02A"] = "yes";
        answers["INVEST-03"] = code == "INVEST_FUTURE_CAP_TABLE_UNCLEAR" ? "current_only" : "exact";
        var (_, result, facts, context) = fixture.Assemble(answers, "no-round-existing-capital");
        Assert.Equal(normalizedTiming, facts.Facts["investment.timing"]);
        Assert.False(Assert.IsType<InvestmentReadinessReportDto>(context.InvestmentReadiness).NewRoundPlanned);
        var finding = Assert.Single(result.Risks.Where(risk => risk.Code == code));
        var item = Assert.Single(context.FenixLaw.ServiceCards.SelectMany(card => card.Items).Where(item => item.FindingCode == code));
        Assert.True(item.HasConfirmedIssue);
        var remediation = FindingRemediationBuilder.Build(finding, facts);
        var relatedActions = context.ActionPlan.Where(action => action.CoveredFindingCodes.Contains(code)).ToList();
        Assert.NotEmpty(relatedActions);
        var prose = context.FenixLaw.SummaryText + "\n" + item.Title + "\n" + item.Recommendation + "\n" + remediation.WhyNow + "\n" + string.Join("\n", relatedActions.Select(action => action.Title + " " + action.WhatToDo + " " + action.WhyNow + " " + action.ExpectedResult));
        foreach (var unplannedClaim in new[] { "новый раунд", "нового раунда", "перед раундом", "до раунда", "до привлечения инвестиций", "для due diligence" })
            Assert.DoesNotContain(unplannedClaim, prose, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnknownDeadlockMechanism_FullReportMarkupDoesNotAssertThatItIsAbsent()
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["FND-07"] = "unknown";
        var (_, result, facts, context) = fixture.Assemble(answers, "unknown-deadlock");
        var original = Assert.Single(result.Risks.Where(risk => risk.Code == "FND_NO_DEADLOCK_PROTECTION"));
        Assert.Contains(original.Basis, basis => basis.QuestionId == "FND-07" && basis.AnswerId == "unknown");
        var remediation = FindingRemediationBuilder.Build(original, facts);
        Assert.True(remediation.RequiresClarification);
        Assert.False(remediation.HasConfirmedIssue);
        var presented = Assert.Single(context.AllFindings.Where(risk => risk.Code == original.Code));
        Assert.Equal(remediation.Title, presented.Title);
        Assert.Contains("не подтверждены", presented.Finding, StringComparison.OrdinalIgnoreCase);
        var markup = fixture.Pdf.BuildTypstMarkup(context);
        Assert.Contains(remediation.Title, markup, StringComparison.Ordinal);
        Assert.DoesNotContain(original.Title, markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("По вашим ответам специального механизма на случай", markup, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlannedRoundWithoutPriorInvestment_CapTableActionDoesNotInventConvertibleLoansOrBlockers()
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["INVEST-01"] = "3_6";
        answers["INVEST-03"] = "current_only";
        var (_, result, _, context) = fixture.Assemble(answers, "planned-round-current-capital");
        Assert.Empty(result.InvestmentReadiness.Blockers);
        var action = Assert.Single(context.ActionPlan.Where(a => a.CoveredFindingCodes.Contains("INVEST_FUTURE_CAP_TABLE_UNCLEAR")));
        Assert.True(action.UseCanonicalRemediation);
        var text = action.Title + " " + action.WhatToDo + " " + action.WhyNow + " " + action.ExpectedResult;
        Assert.DoesNotContain("конвертируемые займы", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("сквозные юридические блокеры", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("структур", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OrphanLegalAction_DoesNotCreateAServiceOrLegalWorkRequirement()
    {
        var result = new ScoreResult
        {
            Sections = new() { new() { SectionId = "founders", Score = 100, Status = ApplicabilityStatus.Applicable } }
        };
        var actions = new List<UnifiedActionItemDto>
        {
            new() { ActionId = "ACT_FOUNDER_AGREEMENT_SHA", Title = "Несвязанное действие", ResolutionMode = ResolutionMode.LegalWork, CoveredFindingCodes = new() { "NONEXISTENT_FINDING" } }
        };
        var recommendation = FenixLawRecommendationEvaluator.EvaluateRecommendation(new(), result, actions);
        Assert.False(recommendation.RequiresLegalWork);
        Assert.Empty(recommendation.ServiceCards);
        Assert.Empty(recommendation.ServiceAreas);
    }

    [Fact]
    public void RealActivityWithoutAnEntity_IsNotHiddenByCorporateModuleApplicability()
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["COR-C01"] = "none";
        answers["TEAM-01"] = new[] { "employees" };
        answers["TEAM-02"] = "1_2";
        var (_, result, facts, context) = fixture.Assemble(answers, "no-entity-active");
        Assert.Equal(true, facts.Facts["team.hasNonFounderTeam"]);
        Assert.Equal(ApplicabilityStatus.NotApplicable, result.Sections.Single(section => section.SectionId == "corporate").Status);
        Assert.Contains(result.Risks, risk => risk.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        Assert.True(context.FenixLaw.RequiresLegalWork);
        Assert.Contains(context.FenixLaw.ServiceCards.SelectMany(card => card.Items), item => item.FindingCode == "COR_NO_ENTITY_FOR_ACTIVITY");
    }

    [Fact]
    public void MoreThanFourEligibleAreas_AllRemainVisibleWithTheirSources()
    {
        var sectionIds = new[] { "founders", "ip", "team", "product", "data", "contracts" };
        var findings = sectionIds.Select(section =>
        {
            var definition = DataBank.Risks.First(risk => risk.SectionId == section && ActionLibrary.ResolveActionForFinding(new RiskFinding { Code = risk.Code, SectionId = risk.SectionId }).ResolutionMode is ResolutionMode.LegalWork or ResolutionMode.LegalReview or ResolutionMode.LegalAndProduct);
            return new RiskFinding
            {
                Code = definition.Code,
                SectionId = definition.SectionId,
                RootCauseGroup = definition.RootCauseGroup,
                Title = definition.Title,
                Finding = definition.Finding,
                WhyItMatters = definition.WhyItMatters,
                Recommendation = definition.Recommendation,
                Recommendations = definition.Recommendations.ToList(),
                Severity = definition.Severity,
                Priority = definition.Priority,
                LawyerRequired = definition.LawyerRequired,
                Resolution = definition.Resolution,
                ResolutionMode = definition.ResolutionMode
            };
        }).ToList();
        var result = new ScoreResult
        {
            Risks = findings,
            Sections = sectionIds.Select(section => new SectionScore { SectionId = section, Score = 50, Status = ApplicabilityStatus.Applicable }).ToList()
        };
        var actions = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, new SharedFactStore());
        var recommendation = FenixLawRecommendationEvaluator.EvaluateRecommendation(findings, result, actions);
        Assert.Equal(6, recommendation.ServiceAreas.Count);
        Assert.Equal(6, recommendation.ServiceCards.Count);
        Assert.Equal(findings.Select(finding => finding.Code).OrderBy(code => code).ToArray(), recommendation.ServiceCards.SelectMany(card => card.SourceFindingCodes).OrderBy(code => code).ToArray());
    }

    [Fact]
    public void GeneratedFenixLawSummary_CannotExpandCanonicalServiceScope()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, _, _, context) = fixture.Assemble("real-problem");
        const string unsupported = "Необходимо разработать новое фаундерское соглашение, новую политику конфиденциальности и провести полный юридический аудит.";
        var raw = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
        raw.FenixLawRecommendation = unsupported;
        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, context);
        Assert.Equal(context.FenixLaw.SummaryText, sanitized.FenixLawRecommendation);
        var synthesis = new ExecutiveSynthesisResponseDto { FenixLawRecommendation = unsupported };
        var (_, response, _) = ChunkedNarrativeValidator.ValidateAndSanitizeExecutive(synthesis, context);
        Assert.Equal(context.FenixLaw.SummaryText, response.FenixLawRecommendation);
    }

    private static void AssertCorrectionOfExistingDocument(string recommendation) => Assert.True(new[] { "обнов", "доработ", "актуализ", "свер", "измен" }.Any(word => recommendation.Contains(word, StringComparison.OrdinalIgnoreCase)), recommendation);

    private static void AssertNoUnrelatedDocumentCreation(FenixLawRecommendationReportDto recommendation)
    {
        var prose = recommendation.SummaryText + "\n" + string.Join("\n", recommendation.ServiceCards.Select(card => card.Description + "\n" + string.Join("\n", card.Items.Select(item => item.Recommendation))));
        foreach (var forbidden in new[] { "Разработка фаундерского соглашения", "Разработать и подписать корпоративный договор", "Составить индивидуальную Политику конфиденциальности", "Разработать новую Политику", "Разработать новую оферту", "документов нет", "политики конфиденциальности нет" })
            Assert.DoesNotContain(forbidden, prose, StringComparison.OrdinalIgnoreCase);
    }
}

internal static class FenixLawScenarioFixtures
{
    public static Dictionary<string, object> Answers(string scenario)
    {
        // Current bank values produce complete good diagnostics; hidden answers are removed by ResolveEffectiveState.
        var answers = DataBank.Questions.Where(question => question.Type == QuestionType.Single && question.ScoreMode == ScoreMode.Diagnostic && question.Options is { Count: > 0 }).ToDictionary(question => question.Id, question => (object)question.Options!.Where(option => option.ConfidenceClass == ConfidenceClass.Known).OrderByDescending(option => option.Score ?? 0).First().Id);
        var context = new Dictionary<string, object>
        {
            ["FND-C01"] = "2", ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 51, ["founder_2"] = 49 }, ["FND-C03"] = "none", ["FND-C04"] = "signed", ["FND-06A"] = "different_thresholds",
            ["COR-C01"] = "one", ["COR-C02A"] = "kz", ["COR-03"] = "none", ["COR-T01"] = "none",
            ["IP-01"] = "ready", ["IP-02"] = new[] { "code", "brand", "domain" }, ["IP-03"] = new[] { "founders" }, ["IP-11"] = "no",
            ["TEAM-01"] = new[] { "none" },
            ["PROD-01"] = "first", ["PROD-02"] = new[] { "companies" }, ["PROD-03"] = new[] { "website" }, ["PROD-07"] = "company", ["PROD-10"] = "free", ["PROD-18"] = "no", ["PROD-20"] = "no", ["PROD-21"] = "one", ["PROD-22"] = new[] { "none" },
            ["DATA-01"] = "yes", ["DATA-02"] = new[] { "contact" }, ["DATA-03"] = "no", ["DATA-04"] = new[] { "user" }, ["DATA-08"] = new[] { "core_service" }, ["DATA-10"] = "yes", ["DATA-12"] = "one",
            ["AI-01"] = "no", ["AI-02"] = "none", ["CONTRACT-01"] = new[] { "none" }, ["INVEST-01"] = "none", ["INVEST-02"] = "no", ["INVEST-10"] = "none"
        };
        foreach (var answer in context) answers[answer.Key] = answer.Value;
        switch (scenario)
        {
            case "all-built": break;
            case "real-problem": answers["PROD-05"] = "changed"; break;
            case "mixed": answers["PROD-05"] = "changed"; answers["DATA-07"] = "changed"; answers["FND-07"] = "only_agree"; break;
            case "unknown":
                answers["FND-C04"] = "unknown"; answers["FND-07"] = "unknown"; answers["PROD-04"] = "unknown"; answers["PROD-06"] = "unknown"; answers["DATA-06"] = "unknown"; break;
            case "no-problems":
                answers["FND-C01"] = "solo"; answers["COR-C01"] = "none"; answers["IP-01"] = "idea"; answers["PROD-01"] = "prelaunch"; answers["DATA-01"] = "no"; answers["DATA-02"] = new[] { "none" }; answers["DATA-10"] = "no"; break;
            default: throw new ArgumentException($"Unknown Fenix Law test scenario: {scenario}");
        }
        return answers;
    }
}

internal sealed class FenixLawScenarioFixture : IDisposable
{
    private readonly string _databasePath;
    private readonly string _connectionString;
    public string Root { get; }
    public ScoringEngine Scoring { get; }
    public TypstPdfService Pdf { get; }

    public FenixLawScenarioFixture()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"fenix-law-grounding-{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FENIX_DB_PATH"] = _databasePath, ["AiSettings:StrictLlm"] = "false" }).Build();
        var database = new DbInitializer(config);
        _connectionString = database.ConnectionString;
        database.Initialize();
        Scoring = new ScoringEngine(new QuestionRepository(database));
        Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        Pdf = new TypstPdfService(new TestWebHostEnvironment(Root), new AiReportService(config, new NoNetworkLlmClient()));
    }

    public (Dictionary<string, object> Answers, ScoreResult Result, SharedFactStore Facts, ReportContext Context) Assemble(string scenario) => Assemble(FenixLawScenarioFixtures.Answers(scenario), scenario);

    public (Dictionary<string, object> Answers, ScoreResult Result, SharedFactStore Facts, ReportContext Context) Assemble(Dictionary<string, object> answers, string scenario)
    {
        foreach (var answer in answers)
        {
            var question = DataBank.Questions.Single(question => question.Id == answer.Key);
            if (question.Type is not (QuestionType.Single or QuestionType.Multiple)) continue;
            var selected = answer.Value is IEnumerable<string> values ? values : new[] { (string)answer.Value };
            Assert.All(selected, value => Assert.Contains(question.Options!, option => option.Id == value));
        }
        var result = Scoring.ComputeResult(answers);
        var (_, effectiveAnswers, facts) = ScoringEngine.ResolveEffectiveState(DataBank.Questions, answers);
        var context = ReportEngine.AssembleReportContext(result, facts, "fenix-law-" + scenario, "Проверка Fenix Law · " + scenario);
        return (effectiveAnswers, result, facts, context);
    }

    public void Dispose()
    {
        using (var connection = new SqliteConnection(_connectionString)) SqliteConnection.ClearPool(connection);
        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }

    private sealed class TestWebHostEnvironment(string root) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class NoNetworkLlmClient : ILlmNarrativeClient
    {
        public bool IsConfigured => false;
        public Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(ModuleNarrativeRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Fenix Law scenario must never call a model.");
        public Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(ActionBatchRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Fenix Law scenario must never call a model.");
        public Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(ExecutiveSynthesisRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("Fenix Law scenario must never call a model.");
    }
}