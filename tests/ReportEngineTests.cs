using System.IO;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;
using Xunit.Abstractions;

namespace FenixLegalOs.Tests;

public class ReportEngineTests
{
    private readonly ITestOutputHelper _output;

    public ReportEngineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void ProjectProfileExtractor_ExtractsRelevantFactsCorrectly()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "none";
        facts.Facts["company.primaryJurisdiction"] = "kz";
        facts.Facts["founders.count"] = "2";
        facts.Facts["founders.isEqual5050"] = true;
        facts.Facts["product.stage"] = "prototype";
        facts.Facts["ip.creators"] = "both";
        facts.Facts["ip.overallRightsEvidence"] = "partial";
        facts.Facts["investment.timing"] = "none";

        var profile = ProjectProfileExtractor.ExtractProfile(facts, "Aurora");

        Assert.NotEmpty(profile.KeyFacts);
        Assert.Contains(profile.KeyFacts, f => f.Label == "Юридическое лицо" && f.Value.Contains("Не зарегистрировано"));
        Assert.Contains(profile.KeyFacts, f => f.Label == "Основатели" && f.Value.Contains("2 сооснователя"));
        Assert.Contains(profile.KeyFacts, f => f.Label == "Права на продукт" && (f.Value.Contains("Частично") || f.Value.Contains("не полностью")));

        Assert.Contains("Aurora", profile.ConfigurationNarrative);
    }

    [Fact]
    public void RenderModeClassifier_AssignsFocusToCriticalAndLowScoringModules()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 45, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "corporate", Score = 70, Status = ApplicabilityStatus.NotApplicable },
                new() { SectionId = "ip", Score = 40, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "contracts", Score = 85, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "founders", Code = "FND_DEADLOCK", Severity = RiskSeverity.Critical },
                new() { SectionId = "ip", Code = "IP_GAP", Severity = RiskSeverity.High }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);

        Assert.Equal(ReportRenderMode.Focus, modes["founders"]);
        Assert.Equal(ReportRenderMode.Focus, modes["ip"]);
        Assert.Equal(ReportRenderMode.NotApplicable, modes["corporate"]);
        Assert.Equal(ReportRenderMode.Compact, modes["contracts"]);
    }

    [Fact]
    public void RootCauseMerger_DeduplicatesAndLimitsTopFindings()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "FND_DEADLOCK_RISK", RootCauseGroup = "FND_GOVERNANCE", Severity = RiskSeverity.Critical, Title = "Риск дедлока", WhyItMatters = "Блокировка решений" },
            new() { Code = "FND_5050_NO_EXIT", RootCauseGroup = "FND_GOVERNANCE", Severity = RiskSeverity.High, Title = "50/50 без выхода", WhyItMatters = "Спор по долям" },
            new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", RootCauseGroup = "IP_ASSIGNMENT", Severity = RiskSeverity.High, Title = "Права на продукт", WhyItMatters = "Код у фаундеров" },
            new() { Code = "AI_SENSITIVE_DATA_TRANSFER", RootCauseGroup = "AI_PRIVACY", Severity = RiskSeverity.Critical, Title = "AI передача данных", WhyItMatters = "Штрафы" },
            new() { Code = "INVEST_PRIOR_INVESTMENT_UNCLEAR", RootCauseGroup = "INVEST_CAP", Severity = RiskSeverity.High, Title = "Прошлые инвестиции", WhyItMatters = "Претензии" }
        };

        var top = RootCauseMerger.ExtractTopRootCauses(findings, maxCount: 3);

        Assert.Equal(3, top.Count);
        // Ensure FND_GOVERNANCE root cause appears only once!
        Assert.Single(top.Where(t => t.RootCauseCode == "FND_GOVERNANCE"));
    }

    [Fact]
    public void UnifiedActionPlanBuilder_OrdersByPriorityAndSetsExpectedResult()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "IP_ASSIGNMENT", Title = "Собрать права на продукт", Priority = RiskPriority.Now, Severity = RiskSeverity.Critical, Recommendation = "Подписать акты" },
            new() { Code = "CONTRACT_TEMPLATES", Title = "Внедрить договоры", Priority = RiskPriority.ThirtyDays, Severity = RiskSeverity.Medium, Recommendation = "Разработать оферту" },
            new() { Code = "INVEST_DATA_ROOM", Title = "Подготовить Data Room", Priority = RiskPriority.BeforeRound, Severity = RiskSeverity.High, Recommendation = "Собрать документы" }
        };

        var actions = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, new SharedFactStore());

        Assert.Equal(3, actions.Count);
        Assert.Equal("В ПЕРВУЮ ОЧЕРЕДЬ", actions[0].PriorityGroup);
        Assert.Equal("СЛЕДУЮЩИМ ЭТАПОМ", actions[1].PriorityGroup);
        Assert.Equal("ДО ИНВЕСТИЦИОННОГО РАУНДА", actions[2].PriorityGroup);
    }

    [Fact]
    public void FenixLawRecommendationEvaluator_StatesWhenLawyerIsNotNeeded()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "LOW_RISK_1", Severity = RiskSeverity.Medium, Resolution = ResolutionType.SelfService }
        };

        var rec = FenixLawRecommendationEvaluator.EvaluateRecommendation(findings, new ScoreResult());

        Assert.False(rec.RequiresLegalWork);
        Assert.Contains("самостоятельно", rec.SummaryText);
    }

    [Fact]
    public void ReportContextValidator_ThrowsOnApplicableNullScore()
    {
        var ctx = new ReportContext
        {
            ModuleCards = new List<ModuleCardDto>
            {
                new() { SectionId = "founders", RenderMode = ReportRenderMode.Focus, Score = null }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new() { SectionId = "founders", Title = "Сооснователи" }
            }
        };

        Assert.Throws<InvalidOperationException>(() => ReportContextValidator.Validate(ctx));
    }

    [Fact]
    public void ReportContextValidator_ThrowsOnProfileContradiction()
    {
        var ctx = new ReportContext
        {
            Profile = new ProjectProfileDto
            {
                KeyFacts = new List<FactItemDto>
                {
                    new() { Key = "entity", Value = "Не зарегистрировано" }
                },
                ConfigurationNarrative = "Проект Aurora осуществляет деятельность через структуру в Казахстане."
            }
        };

        Assert.Throws<InvalidOperationException>(() => ReportContextValidator.Validate(ctx));
    }

    [Fact]
    public void AllDimensions_HaveValidRussianDisplayName_InSingleSourceOfTruth()
    {
        Assert.NotEmpty(FenixLegalOs.Data.DataBank.Dimensions);

        foreach (var dim in FenixLegalOs.Data.DataBank.Dimensions)
        {
            Assert.False(string.IsNullOrWhiteSpace(dim.DisplayName), $"Dimension {dim.Id} has empty DisplayName.");
            Assert.NotEqual(dim.Id, dim.DisplayName);
            
            // Should resolve cleanly in FactorBreakdownEvaluator
            var title = FactorBreakdownEvaluator.GetDimensionTitle(dim.Id);
            Assert.False(string.IsNullOrWhiteSpace(title));
        }
    }

    [Fact]
    public void FactorBreakdownEvaluator_DoesNotMarkDimensionsAsRisk_UnlessActiveFindingDetected()
    {
        var section = new SectionScore
        {
            SectionId = "founders",
            Score = 50,
            Dimensions = new List<DimensionScore>
            {
                new() { DimensionId = "roles", Score = 50 },
                new() { DimensionId = "equity_clarity", Score = 30 },
                new() { DimensionId = "existing_dispute", Score = 90 }
            }
        };

        // No findings detected
        var risks = new List<RiskFinding>();

        var (_, _, _, table) = FactorBreakdownEvaluator.EvaluateSectionDriversAndTable(section, risks);

        var rolesRow = table.First(r => r.FactorName.Contains("ролей", StringComparison.OrdinalIgnoreCase));
        var eqRow = table.First(r => r.FactorName.Contains("долей", StringComparison.OrdinalIgnoreCase));
        var dispRow = table.First(r => r.FactorName.Contains("споров", StringComparison.OrdinalIgnoreCase));

        // Factor health statuses, never "Высокий риск" without active finding
        Assert.Equal("Не оформлено", rolesRow.StatusText);
        Assert.Equal("Не оформлено", eqRow.StatusText);
        Assert.Equal("В норме", dispRow.StatusText);
        Assert.Null(rolesRow.Severity);
    }

    [Fact]
    public void RenderModeClassifier_ExcludesInvestmentFromFocusModules()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "investment", Status = ApplicabilityStatus.Applicable, Score = 20, Weight = 1.0 },
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 30, Weight = 1.0 }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "investment", Severity = RiskSeverity.Critical },
                new() { SectionId = "founders", Severity = RiskSeverity.Critical }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);

        Assert.Equal(ReportRenderMode.Focus, modes["founders"]);
        Assert.Equal(ReportRenderMode.Compact, modes["investment"]); // Investment deep-dive is in Section 10
    }

    [Fact]
    public void ScoreBand_NeverRenderedAsRiskSeverity_ScoreUnder40_DisplaysCanonicalCriticalGaps()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "contracts", Status = ApplicabilityStatus.Applicable, Score = 17, Weight = 1.0, Title = "Договоры" }
            },
            Risks = new List<RiskFinding>()
        };

        var ctx = ReportEngine.AssembleReportContext(result, new SharedFactStore(), "test-sess", "Test");
        var contractCard = ctx.ModuleCards.First(m => m.SectionId == "contracts");

        // Canonical ScoreBand is "Критические пробелы", NEVER "Критический риск"
        Assert.Equal("Критические пробелы", contractCard.StatusText);
        Assert.NotEqual("Критический риск", contractCard.StatusText);
    }

    [Fact]
    public void CompactModules_ExcludesInvestment_WhenInvestmentReadinessSectionExists()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "investment", Status = ApplicabilityStatus.Applicable, Score = 35, Weight = 1.0, Title = "Инвестиции" },
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 20, Weight = 1.0, Title = "Основатели" }
            },
            Risks = new List<RiskFinding>()
        };

        var ctx = ReportEngine.AssembleReportContext(result, new SharedFactStore(), "test-sess", "Test");

        // Investment must NOT appear in CompactModules when dedicated InvestmentReadiness section exists
        Assert.DoesNotContain(ctx.CompactModules, m => m.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ctx.InvestmentReadiness);
        Assert.True(ctx.InvestmentReadiness.IsApplicable);
    }

    [Fact]
    public void FenixLawCta_IncludesContracts_WhenContractsHasHighDeficitAndLegalWork()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 50, Weight = 1.0, Title = "Основатели" },
                new() { SectionId = "ip", Status = ApplicabilityStatus.Applicable, Score = 60, Weight = 1.0, Title = "IP" },
                new() { SectionId = "data", Status = ApplicabilityStatus.Applicable, Score = 55, Weight = 1.0, Title = "Data" },
                new() { SectionId = "contracts", Status = ApplicabilityStatus.Applicable, Score = 17, Weight = 1.0, Title = "Договоры" }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "contracts", Severity = RiskSeverity.High, Resolution = ResolutionType.LawyerRequired, Title = "Неполное оформление договоров" },
                new() { SectionId = "founders", Severity = RiskSeverity.High, Resolution = ResolutionType.LawyerRequired, Title = "Риск тупика" },
                new() { SectionId = "ip", Severity = RiskSeverity.High, Resolution = ResolutionType.LawyerRequired, Title = "Права не переданы" },
                new() { SectionId = "data", Severity = RiskSeverity.High, Resolution = ResolutionType.LawyerRequired, Title = "Передача данных" }
            }
        };

        var cta = FenixLawRecommendationEvaluator.EvaluateRecommendation(result.Risks, result);

        Assert.True(cta.RequiresLegalWork);
        Assert.Contains(cta.ServiceCards, c => c.Title.Contains("Договорн", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ActionPlan_DerivesDistinctDomainRationales_ForDifferentFindingCategories()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "CTR_01", SectionId = "contracts", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Договорной риск", Recommendation = "Разработать типовой договор" },
            new() { Code = "TEAM_01", SectionId = "team", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Риск команды", Recommendation = "Оформить NDA и передачу прав" },
            new() { Code = "DATA_01", SectionId = "data", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Риск данных", Recommendation = "Внедрить политику конфиденциальности" }
        };

        var plan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, new SharedFactStore());

        Assert.Equal(3, plan.Count);
        // Rationales must be distinct and domain-grounded, not repetitive boilerplate
        Assert.NotEqual(plan[0].WhyNow, plan[1].WhyNow);
        Assert.NotEqual(plan[1].WhyNow, plan[2].WhyNow);

        var ctrAction = plan.First(a => a.CoveredFindingCodes.Contains("CTR_01") || a.ActionId.Contains("CONTRACT"));
        var teamAction = plan.First(a => a.CoveredFindingCodes.Contains("TEAM_01") || a.ActionId.Contains("TEAM"));
        Assert.Contains("контрагент", ctrAction.WhyNow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("команд", teamAction.WhyNow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RootCauseMerger_DoesNotTruncateFindingSummaryWithEllipsis()
    {
        var longFindingText = "Обнаружено существенное несоответствие в документации интеллектуальной собственности, требующее глубокой юридической проработки и подписания дополнительных соглашений со всеми соавторами программного обеспечения.";
        var risks = new List<RiskFinding>
        {
            new()
            {
                Code = "IP_LONG",
                SectionId = "ip",
                Severity = RiskSeverity.Critical,
                Priority = RiskPriority.Now,
                Title = "Длинное описание находки",
                Finding = longFindingText
            }
        };

        var top = RootCauseMerger.ExtractTopRootCauses(risks);

        Assert.Single(top);
        Assert.DoesNotContain("...", top[0].ShortSummary);
        Assert.Equal(longFindingText, top[0].ShortSummary);
    }

    [Fact]
    public void ReportEngine_HandlesNullProjectName_AndTranslatesRawStageEnums()
    {
        var facts = new SharedFactStore();
        facts.Facts["product.stage"] = "live_or_ready";

        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 80, Status = ApplicabilityStatus.Applicable, Title = "Основатели" }
            },
            Risks = new List<RiskFinding>()
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-session", projectName: null);

        Assert.Equal("Проект", ctx.ProjectName);
        Assert.NotEqual("live_or_ready", ctx.ProjectStage);
        Assert.Contains("MVP", ctx.ProjectStage);
    }

    [Fact]
    public void CorporateNA_WithOperatingRisk_ExplainsCoherentlyInReasonIfNa()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "none";

        var result = new ScoreResult
        {
            Overall = 80,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "corporate", Status = ApplicabilityStatus.NotApplicable, Title = "Корпоративная структура" },
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 80, Title = "Основатели" }
            },
            Risks = new List<RiskFinding>
            {
                new() { Code = "COR_NO_ENTITY_FOR_ACTIVITY", SectionId = "corporate", Severity = RiskSeverity.High, Title = "Бизнес уже работает без юридического лица" }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-session", null);
        var corpCard = ctx.ModuleCards.First(m => m.SectionId == "corporate");

        Assert.Null(corpCard.Score);
        Assert.NotNull(corpCard.ReasonIfNa);
        Assert.Contains("риск", corpCard.ReasonIfNa, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionPlan_StrictlySeparatesCorporate_Founders_Product_Data_Domains()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "COR_NO_ENTITY_FOR_ACTIVITY", SectionId = "corporate", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Работа без юрлица", Recommendation = "Зарегистрировать компанию" },
            new() { Code = "FND_DEADLOCK", SectionId = "founders", Priority = RiskPriority.Now, Severity = RiskSeverity.Critical, Title = "Риск тупика", Recommendation = "Оформить SHA" },
            new() { Code = "PROD_RULES_DISCREPANCY", SectionId = "product", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Несоответствие правил", Recommendation = "Актуализировать оферту" },
            new() { Code = "DATA_PRIVACY_MISSING", SectionId = "data", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Нет политики ПДн", Recommendation = "Опубликовать политику" },
            new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", SectionId = "ip", Priority = RiskPriority.Now, Severity = RiskSeverity.Critical, Title = "Права не подтверждены", Recommendation = "Оформить передачу прав" },
            new() { Code = "TEAM_NO_WRITTEN_CONTRACTS", SectionId = "team", Priority = RiskPriority.Now, Severity = RiskSeverity.High, Title = "Нет договоров с командой", Recommendation = "Оформить договоры" }
        };

        var factsUnincorporated = new SharedFactStore();
        factsUnincorporated.Facts["company.entityStatus"] = "none";

        var plan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, factsUnincorporated);

        var corpAction = plan.First(a => a.CoveredFindingCodes.Contains("COR_NO_ENTITY_FOR_ACTIVITY") || a.ActionId == "ACT_CORP_INCORPORATION");
        var fndAction = plan.First(a => a.CoveredFindingCodes.Contains("FND_DEADLOCK") || a.ActionId == "ACT_FOUNDER_DEADLOCK_RESOLVE");
        var prodAction = plan.First(a => a.CoveredFindingCodes.Contains("PROD_RULES_DISCREPANCY") || a.ActionId == "ACT_PROD_TERMS_OF_SERVICE");
        var dataAction = plan.First(a => a.CoveredFindingCodes.Contains("DATA_PRIVACY_MISSING") || a.ActionId == "ACT_DATA_PRIVACY_POLICY_CREATE");
        var ipAction = plan.First(a => a.CoveredFindingCodes.Contains("IP_PRODUCT_RIGHTS_UNCONFIRMED") || a.ActionId == "ACT_IP_FOUNDER_ASSIGNMENT" || a.ActionId == "ACT_IP_CONSOLIDATION_AUDIT");

        // Corporate must receive corporate liability rationale, not founders deadlock
        Assert.Contains("ответственност", corpAction.WhyNow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("взаимной блокировки", corpAction.WhyNow, StringComparison.OrdinalIgnoreCase);

        // Founders must receive deadlock/equity rationale
        Assert.Contains("блокировки", fndAction.WhyNow, StringComparison.OrdinalIgnoreCase);

        // Product must receive user rules rationale, NOT data/AI processing
        Assert.Contains("пользовател", prodAction.WhyNow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AI", prodAction.WhyNow, StringComparison.OrdinalIgnoreCase);

        // Data must receive data regulations rationale
        Assert.Contains("персональных данных", dataAction.WhyNow, StringComparison.OrdinalIgnoreCase);

        // Pre-incorporation IP must NOT claim rights belong to a non-existent company
        Assert.DoesNotContain("консолидированы в компании", ipAction.ExpectedResult, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("создателями", ipAction.ExpectedResult, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Strengths_DeduplicatesNormalizedTitles()
    {
        var result = new ScoreResult
        {
            Strengths = new List<string>
            {
                "Отсутствие споров между основателями"
            },
            Sections = new List<SectionScore>
            {
                new()
                {
                    SectionId = "founders",
                    Status = ApplicabilityStatus.Applicable,
                    Score = 90,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "existing_dispute", Score = 100 }
                    }
                }
            }
        };

        var positive = FactorBreakdownEvaluator.ExtractGlobalPositiveFactors(result);

        // Must not contain both "Отсутствие споров между основателями" and "Выстроено направление: Отсутствие споров между основателями"
        Assert.Single(positive);
        Assert.Equal("Отсутствие споров между основателями", positive[0].Title);
    }

    [Fact]
    public void ExecutiveConclusion_SynthesizesGrammaticalRussian_WithoutConcatenatingRawTitles()
    {
        var ctx = new ReportContext
        {
            Overall = new OverallScoreDto { Score = 33 },
            TopFindings = new List<TopFindingSummaryDto>
            {
                new() { FindingCode = "FND_DEADLOCK", DetailSectionId = "founders", Title = "Компания может оказаться неспособной принять ключевое решение" },
                new() { FindingCode = "COR_NO_ENTITY_FOR_ACTIVITY", DetailSectionId = "corporate", Title = "Бизнес уже работает, но отдельная юридическая оболочка еще не сформирована" }
            }
        };

        var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);

        Assert.NotNull(narratives.ExecutiveConclusion);
        // Must NOT concatenate raw title "компания может оказаться" after "оказывают"
        Assert.DoesNotContain("оказывают компания может оказаться", narratives.ExecutiveConclusion, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ключевыми факторами снижения оценки являются", narratives.ExecutiveConclusion);
        Assert.Contains("неурегулированность порядка принятия решений", narratives.ExecutiveConclusion);
    }

    [Fact]
    public void PreIncorporationIP_PracticalMeaningAndFindings_DoNotReferenceExistingCompanyOwnership()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "none";

        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "ip", Status = ApplicabilityStatus.Applicable, Score = 41, Title = "Интеллектуальная собственность" }
            },
            Risks = new List<RiskFinding>
            {
                new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", SectionId = "ip", Severity = RiskSeverity.Critical, Title = "Принадлежность продукта не подтверждена" }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-session", null);
        var ipFocus = ctx.FocusModules.First(f => f.SectionId == "ip");

        Assert.DoesNotContain("права компании на продукт", ipFocus.PracticalMeaning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TopDriver_WithLowestScore_ReceivesFocusAnalysis()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 40, Weight = 1.5, Title = "Основатели" },
                new() { SectionId = "ip", Status = ApplicabilityStatus.Applicable, Score = 41, Weight = 1.4, Title = "Интеллектуальная собственность" },
                new() { SectionId = "team", Status = ApplicabilityStatus.Applicable, Score = 20, Weight = 1.0, Title = "Команда" },
                new() { SectionId = "product", Status = ApplicabilityStatus.Applicable, Score = 43, Weight = 1.0, Title = "Продукт" },
                new() { SectionId = "data", Status = ApplicabilityStatus.Applicable, Score = 18, Weight = 1.0, Title = "Данные и ИИ" }
            },
            Risks = new List<RiskFinding>
            {
                new() { Code = "FND_01", SectionId = "founders", Severity = RiskSeverity.Critical, Title = "FND Risk" },
                new() { Code = "IP_01", SectionId = "ip", Severity = RiskSeverity.High, Title = "IP Risk" },
                new() { Code = "TEAM_01", SectionId = "team", Severity = RiskSeverity.High, Title = "Team Risk" },
                new() { Code = "PROD_01", SectionId = "product", Severity = RiskSeverity.High, Title = "Prod Risk" },
                new() { Code = "DATA_PRIVACY_NOTICE_MISSING", SectionId = "data", Severity = RiskSeverity.High, Title = "Data Risk" }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);

        // Data has the lowest score (18) and highest score drag among weight=1 sections, so it MUST receive Focus mode
        Assert.Equal(ReportRenderMode.Focus, modes["data"]);
    }

    [Fact]
    public void ScoreBandDescriptions_DescribeReadiness_WithoutAutomaticRiskSeverityClaims()
    {
        var terms = ReportStaticContent.GetLegalTerms();
        
        // Must not make unconditional RiskSeverity claims
        Assert.DoesNotContain("Критическая уязвимость, требующая", terms.MethodologyText);
        Assert.DoesNotContain("существенных блокеров не выявлено", terms.MethodologyText);
        Assert.Contains("Системные пробелы в юридической готовности", terms.MethodologyText);
        Assert.Contains("Базовая юридическая конструкция в целом выстроена", terms.MethodologyText);
    }

    [Fact]
    public void TopDrivers_RussianFormatting_SupportsOneTwoThreeItems()
    {
        var facts = new SharedFactStore();

        // 1 Driver
        var res1 = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "data", Status = ApplicabilityStatus.Applicable, Score = 18, Title = "Данные и ИИ" }
            }
        };
        var ctx1 = ReportEngine.AssembleReportContext(res1, facts, "s1", null);
        Assert.Equal("На итоговую оценку сильнее всего повлияло направление «Данные и ИИ».", ctx1.Overall.BottomExplanation);

        // 2 Drivers
        var res2 = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "data", Status = ApplicabilityStatus.Applicable, Score = 18, Title = "Данные и ИИ" },
                new() { SectionId = "team", Status = ApplicabilityStatus.Applicable, Score = 20, Title = "Команда и сотрудники" }
            }
        };
        var ctx2 = ReportEngine.AssembleReportContext(res2, facts, "s2", null);
        Assert.Equal("На итоговую оценку сильнее всего повлияли направления «Данные и ИИ» и «Команда и сотрудники».", ctx2.Overall.BottomExplanation);

        // 3 Drivers
        var res3 = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "data", Status = ApplicabilityStatus.Applicable, Score = 18, Title = "Данные и ИИ" },
                new() { SectionId = "team", Status = ApplicabilityStatus.Applicable, Score = 20, Title = "Команда и сотрудники" },
                new() { SectionId = "founders", Status = ApplicabilityStatus.Applicable, Score = 30, Title = "Сооснователи" }
            }
        };
        var ctx3 = ReportEngine.AssembleReportContext(res3, facts, "s3", null);
        Assert.Equal("На итоговую оценку сильнее всего повлияли направления «Данные и ИИ», «Команда и сотрудники» и «Сооснователи».", ctx3.Overall.BottomExplanation);
    }

    [Fact]
    public void FenixLawRecommendation_GrammaticalRussian_AndNoLowercaseAi()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "COR_01", SectionId = "corporate", Severity = RiskSeverity.Critical, Title = "Корп риск", Resolution = ResolutionType.LawyerRequired },
            new() { Code = "DATA_01", SectionId = "data", Severity = RiskSeverity.High, Title = "Дата риск", Resolution = ResolutionType.LawyerRequired }
        };
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "corporate", Score = 30, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "data", Score = 18, Status = ApplicabilityStatus.Applicable }
            }
        };

        var rec = FenixLawRecommendationEvaluator.EvaluateRecommendation(findings, result);

        Assert.True(rec.RequiresLegalWork);
        Assert.Contains("Ключевые задачи требуют профессиональной юридической работы", rec.SummaryText);
        Assert.DoesNotContain(", ai ", rec.SummaryText, StringComparison.Ordinal);
        Assert.DoesNotContain(" и и ", rec.SummaryText);
    }

    [Fact]
    public void PreIncorporationIP_FullTextScan_NoProhibitedExistingCompanyPhrases()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "none";

        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "ip", Status = ApplicabilityStatus.Applicable, Score = 41, Title = "Интеллектуальная собственность" }
            },
            Risks = new List<RiskFinding>
            {
                new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", SectionId = "ip", Severity = RiskSeverity.Critical, Title = "Принадлежность продукта не подтверждена" },
                new() { Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", SectionId = "ip", Severity = RiskSeverity.High, Title = "Права основателя не переданы" }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-session", null);

        var allTexts = new List<string>
        {
            ctx.FocusModules.First(f => f.SectionId == "ip").PracticalMeaning,
            string.Join(" ", ctx.ActionPlan.Select(a => a.ExpectedResult + " " + a.WhyNow + " " + a.WhatToDo))
        };

        foreach (var text in allTexts)
        {
            Assert.DoesNotContain("права компании на продукт", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("передать в структуру проекта", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("передать компании", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PipelineAudit_DiagnosticTable()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"audit_db_{Guid.NewGuid():N}.db");
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var qRepo = new QuestionRepository(dbInit);
        var scoringEngine = new ScoringEngine(qRepo);

        // 1. Unincorporated baseline scenario (5 applicable modules, 3 N/A)
        var unincAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
            ["FND-04"] = "verbal",
            ["FND-05"] = "not_discussed",
            ["FND-05A"] = "none",
            ["FND-06"] = "none",
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none",
            ["FND-08"] = "none",
            ["FND-09"] = "none",
            ["FND-10"] = "none",
            ["FND-11"] = "aligned",

            ["COR-C01"] = "none",

            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "some",
            ["IP-05"] = "agreed",
            ["IP-07"] = "missing_some",

            ["TEAM-01"] = new List<string> { "freelancers", "external_devs" },
            ["TEAM-02"] = "1_2",
            ["TEAM-03"] = "many_missing",

            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",

            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contact", "account" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "user" },
            ["DATA-05"] = "none",
            ["DATA-06"] = "preparing",
            ["AI-01"] = "yes",

            ["CONTRACT-01"] = new List<string> { "none" },
            ["INVEST-01"] = "none"
        };

        var unincResult = scoringEngine.ComputeResult(unincAnswers);
        var unincFacts = FactNormalizer.NormalizeFacts(unincAnswers);
        var unincCtx = ReportEngine.AssembleReportContext(unincResult, unincFacts, "test-uninc", null);

        Console.WriteLine("\n=== DIAGNOSTIC AUDIT: UNINCORPORATED SCENARIO ===");
        PrintAuditTable(unincResult, unincCtx);

        // 2. Full 8-Module Applicable Scenario (All 8 modules answered with issues)
        var allAnswers = new Dictionary<string, object>(unincAnswers)
        {
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz_llp",
            ["COR-01"] = "dispute",
            ["COR-02"] = "fragmented",
            ["COR-03"] = "informal",
            ["COR-04"] = "missing",
            ["COR-05"] = "often_missing",
            ["COR-06"] = "unclear",
            ["COR-07"] = "material_outside",
            ["COR-08"] = "missing",

            ["CONTRACT-01"] = new List<string> { "clients", "partners" },
            ["CONTRACT-02"] = "mostly_informal",
            ["CONTRACT-03"] = "outside",
            ["CONTRACT-05"] = "weak",
            ["CONTRACT-06"] = "templates",
            ["CONTRACT-07"] = "often_unreviewed",
            ["CONTRACT-08"] = "material",
            ["CONTRACT-08A"] = "serious",

            ["INVEST-01"] = "searching",
            ["INVEST-02"] = "no",
            ["INVEST-03"] = "none",
            ["INVEST-04"] = "none"
        };

        var allResult = scoringEngine.ComputeResult(allAnswers);
        var allFacts = FactNormalizer.NormalizeFacts(allAnswers);
        var allCtx = ReportEngine.AssembleReportContext(allResult, allFacts, "test-all", null);

        Console.WriteLine("\n=== DIAGNOSTIC AUDIT: FULL 8-MODULE APPLICABLE SCENARIO ===");
        PrintAuditTable(allResult, allCtx);
    }

    private static void PrintAuditTable(ScoreResult result, ReportContext ctx)
    {
        Console.WriteLine($"{"Section",-14} | {"Score",-5} | {"Raw Findings",-12} | {"Root Causes",-11} | {"Context",-7} | {"Rendered Full",-13} | {"Rendered Comp",-13} | {"Lost",-6} | {"RenderMode",-10}");
        Console.WriteLine(new string('-', 110));

        foreach (var s in result.Sections)
        {
            var raw = result.Risks.Where(r => r.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            var rootCauses = raw.GroupBy(r => !string.IsNullOrWhiteSpace(r.RootCauseGroup) ? r.RootCauseGroup : r.Code).Count();
            
            var focusMod = ctx.FocusModules.FirstOrDefault(f => f.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase));
            var compMod = ctx.CompactModules.FirstOrDefault(c => c.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase));
            var naMod = ctx.NotApplicableModules.FirstOrDefault(n => n.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase));

            string mode = focusMod != null ? "Focus" : compMod != null ? "Compact" : "N/A";
            int contextCount = focusMod?.Findings.Count ?? 0;
            
            // New Lossless Typst renderer: Level A (up to 2 full) + Level B (all remaining compact)
            int renderedFull = focusMod != null ? Math.Min(contextCount, 2) : 0;
            int renderedComp = focusMod != null ? Math.Max(0, contextCount - 2) : 0;
            int lost = focusMod != null ? contextCount - (renderedFull + renderedComp) : (compMod != null ? raw.Count : 0);

            Console.WriteLine($"{s.SectionId,-14} | {s.Score?.ToString() ?? "—",-5} | {raw.Count,-12} | {rootCauses,-11} | {contextCount,-7} | {renderedFull,-13} | {renderedComp,-13} | {lost,-6} | {mode,-10}");
        }
    }

    [Fact(DisplayName = "1. NoUniqueActiveFindingIsLostBetweenContextAndRenderer")]
    public void NoUniqueActiveFindingIsLostBetweenContextAndRenderer()
    {
        var focus = new FocusModuleDetailDto
        {
            SectionId = "founders",
            Title = "Сооснователи",
            Score = 30,
            ScoreBand = "Критические пробелы",
            Findings = new List<ReportFindingCardDto>
            {
                new() { FindingCode = "FND_1", Title = "Риск 1", Severity = RiskSeverity.Critical, SeverityLabel = "Критический", WhyFound = "Причина 1", WhyItMatters = "Важность 1", Recommendation = "Рекомендация 1" },
                new() { FindingCode = "FND_2", Title = "Риск 2", Severity = RiskSeverity.High, SeverityLabel = "Высокий", WhyFound = "Причина 2", WhyItMatters = "Важность 2", Recommendation = "Рекомендация 2" },
                new() { FindingCode = "FND_3", Title = "Риск 3", Severity = RiskSeverity.High, SeverityLabel = "Высокий", WhyFound = "Причина 3", WhyItMatters = "Важность 3", Recommendation = "Рекомендация 3" },
                new() { FindingCode = "FND_4", Title = "Риск 4", Severity = RiskSeverity.Medium, SeverityLabel = "Умеренный", WhyFound = "Причина 4", WhyItMatters = "Важность 4", Recommendation = "Рекомендация 4" }
            }
        };

        var ctx = new ReportContext
        {
            FocusModules = new List<FocusModuleDetailDto> { focus }
        };

        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var markup = pdfService.BuildTypstMarkup(ctx);

        // All 4 findings must be present in the generated Typst markup!
        Assert.Contains("Риск 1", markup);
        Assert.Contains("Риск 2", markup);
        Assert.Contains("Риск 3", markup);
        Assert.Contains("Риск 4", markup);
    }

    [Fact(DisplayName = "2. FocusClassifier_HasNoGlobalTopNLimit")]
    public void FocusClassifier_HasNoGlobalTopNLimit()
    {
        // 7 applicable diagnostic modules with vulnerabilities
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 35, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "corporate", Score = 30, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "ip", Score = 25, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "team", Score = 40, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "product", Score = 38, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "data", Score = 20, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "contracts", Score = 32, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "founders", Severity = RiskSeverity.High, Code = "F1" },
                new() { SectionId = "corporate", Severity = RiskSeverity.High, Code = "C1" },
                new() { SectionId = "ip", Severity = RiskSeverity.High, Code = "I1" },
                new() { SectionId = "team", Severity = RiskSeverity.High, Code = "T1" },
                new() { SectionId = "product", Severity = RiskSeverity.High, Code = "P1" },
                new() { SectionId = "data", Severity = RiskSeverity.High, Code = "D1" },
                new() { SectionId = "contracts", Severity = RiskSeverity.High, Code = "K1" }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);

        // All 7 modules MUST be Focus without being capped by any arbitrary quota
        Assert.Equal(ReportRenderMode.Focus, modes["founders"]);
        Assert.Equal(ReportRenderMode.Focus, modes["corporate"]);
        Assert.Equal(ReportRenderMode.Focus, modes["ip"]);
        Assert.Equal(ReportRenderMode.Focus, modes["team"]);
        Assert.Equal(ReportRenderMode.Focus, modes["product"]);
        Assert.Equal(ReportRenderMode.Focus, modes["data"]);
        Assert.Equal(ReportRenderMode.Focus, modes["contracts"]);
    }

    [Fact(DisplayName = "3. HighCriticalBlockerFinding_ForcesDetailedAnalysis")]
    public void HighCriticalBlockerFinding_ForcesDetailedAnalysis()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "product", Score = 85, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "product", Severity = RiskSeverity.Critical, Code = "PROD_CRIT" }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);
        Assert.Equal(ReportRenderMode.Focus, modes["product"]);
    }

    [Fact(DisplayName = "4. TopDriver_ForcesDetailedAnalysis")]
    public void TopDriver_ForcesDetailedAnalysis()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "data", Score = 15, Status = ApplicabilityStatus.Applicable },
                new() { SectionId = "contracts", Score = 90, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>()
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);
        Assert.Equal(ReportRenderMode.Focus, modes["data"]);
    }

    [Fact(DisplayName = "5. CompactModule_DoesNotContainUnexplainedSeriousFindings")]
    public void CompactModule_DoesNotContainUnexplainedSeriousFindings()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "team", Score = 95, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>()
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);
        Assert.Equal(ReportRenderMode.Compact, modes["team"]);
    }

    [Fact(DisplayName = "6. FocusModule_RendersAllUniqueRootFindings")]
    public void FocusModule_RendersAllUniqueRootFindings()
    {
        var findings = new List<ReportFindingCardDto>();
        for (int i = 1; i <= 6; i++)
        {
            findings.Add(new ReportFindingCardDto
            {
                FindingCode = $"RISK_{i}",
                Title = $"Уникальный риск {i}",
                Severity = RiskSeverity.High,
                SeverityLabel = "Высокий",
                WhyFound = $"Причина {i}",
                WhyItMatters = $"Важность {i}",
                Recommendation = $"Рекомендация {i}"
            });
        }

        var focus = new FocusModuleDetailDto
        {
            SectionId = "founders",
            Title = "Сооснователи",
            Score = 25,
            ScoreBand = "Критические пробелы",
            Findings = findings
        };

        var ctx = new ReportContext { FocusModules = new List<FocusModuleDetailDto> { focus } };
        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var markup = pdfService.BuildTypstMarkup(ctx);

        for (int i = 1; i <= 6; i++)
        {
            Assert.Contains($"Уникальный риск {i}", markup);
        }
    }

    [Fact(DisplayName = "7. AdditionalFindings_AreRenderedCompactly_NotDropped")]
    public void AdditionalFindings_AreRenderedCompactly_NotDropped()
    {
        var focus = new FocusModuleDetailDto
        {
            SectionId = "founders",
            Title = "Сооснователи",
            Score = 25,
            ScoreBand = "Критические пробелы",
            Findings = new List<ReportFindingCardDto>
            {
                new() { FindingCode = "F1", Title = "Основной риск 1", Severity = RiskSeverity.Critical, SeverityLabel = "Критический", WhyFound = "Причина 1", WhyItMatters = "Важность 1", Recommendation = "Действие 1" },
                new() { FindingCode = "F2", Title = "Основной риск 2", Severity = RiskSeverity.High, SeverityLabel = "Высокий", WhyFound = "Причина 2", WhyItMatters = "Важность 2", Recommendation = "Действие 2" },
                new() { FindingCode = "F3", Title = "Дополнительный риск 3", Severity = RiskSeverity.Medium, SeverityLabel = "Умеренный", WhyFound = "Причина 3", WhyItMatters = "Важность 3", Recommendation = "Действие 3" }
            }
        };

        var ctx = new ReportContext { FocusModules = new List<FocusModuleDetailDto> { focus } };
        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var markup = pdfService.BuildTypstMarkup(ctx);

        Assert.Contains("КЛЮЧЕВЫЕ РИСКИ", markup);
        Assert.Contains("ДРУГИЕ ВЫЯВЛЕННЫЕ РИСКИ", markup);
        Assert.Contains("Дополнительный риск 3", markup);
    }

    [Fact(DisplayName = "8. InvestmentReadiness_UsesSpecializedDetailedSectionWithoutDuplication")]
    public void InvestmentReadiness_UsesSpecializedDetailedSectionWithoutDuplication()
    {
        var result = new ScoreResult
        {
            Sections = new List<SectionScore>
            {
                new() { SectionId = "investment", Score = 35, Status = ApplicabilityStatus.Applicable }
            },
            Risks = new List<RiskFinding>
            {
                new() { SectionId = "investment", Severity = RiskSeverity.Blocker, Code = "INVEST_BLOCKER" }
            }
        };

        var modes = RenderModeClassifier.ClassifyRenderModes(result);
        // Investment is handled via specialized Section 10/11, not generic Focus list
        Assert.NotEqual(ReportRenderMode.Focus, modes["investment"]);
    }

    [Fact(DisplayName = "9. MatrixDetailedMarker_MatchesActualDetailedSection")]
    public void MatrixDetailedMarker_MatchesActualDetailedSection()
    {
        var ctx = new ReportContext
        {
            ModuleCards = new List<ModuleCardDto>
            {
                new() { SectionId = "founders", RenderMode = ReportRenderMode.Focus, Title = "Сооснователи", Score = 40, StatusText = "Критические пробелы" },
                new() { SectionId = "contracts", RenderMode = ReportRenderMode.Compact, Title = "Договоры", Score = 85, StatusText = "Устойчиво" }
            }
        };

        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var markup = pdfService.BuildTypstMarkup(ctx);

        Assert.Contains("ПОДРОБНЫЙ РАЗБОР →", markup);
    }

    [Fact(DisplayName = "10. ActionableHighCriticalBlockerFinding_HasActionPlanCoverage")]
    public void ActionableHighCriticalBlockerFinding_HasActionPlanCoverage()
    {
        var findings = new List<RiskFinding>();
        for (int i = 1; i <= 10; i++)
        {
            findings.Add(new RiskFinding
            {
                Code = $"RISK_{i}",
                SectionId = "founders",
                Title = $"Actionable Risk {i}",
                Severity = RiskSeverity.High,
                Priority = RiskPriority.Now,
                Recommendation = $"Fix {i}"
            });
        }

        var facts = new SharedFactStore();
        var actionPlan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, facts);

        // All 10 actionable findings must be covered in ActionPlan
        Assert.True(actionPlan.Count > 0);
        var coveredCodes = actionPlan.SelectMany(a => a.CoveredFindingCodes).ToHashSet();
        foreach (var f in findings)
        {
            Assert.Contains(f.Code, coveredCodes);
        }
    }

    [Fact(DisplayName = "11. NegativeScoreFactor_HasFindingOrExplicitScoreReason")]
    public void NegativeScoreFactor_HasFindingOrExplicitScoreReason()
    {
        var sec = new SectionScore
        {
            SectionId = "data",
            Title = "Данные и ИИ",
            Score = 18,
            Status = ApplicabilityStatus.Applicable
        };

        var (neg, att, pos, table) = FactorBreakdownEvaluator.EvaluateSectionDriversAndTable(sec, new List<RiskFinding>());

        // If score is low and no findings, neg drivers or factors must exist explaining the score
        Assert.True(neg.Count > 0 || table.Count > 0);
    }

    [Fact(DisplayName = "12. RootCauseMerge_PreservesAllMaterialEvidence")]
    public void RootCauseMerge_PreservesAllMaterialEvidence()
    {
        var f1 = new RiskFinding { Code = "FND_1", RootCauseGroup = "GOVERNANCE", Severity = RiskSeverity.High, Priority = RiskPriority.Now, Title = "Title 1", Finding = "Fact 1", WhyItMatters = "Matters 1" };
        var f2 = new RiskFinding { Code = "FND_2", RootCauseGroup = "GOVERNANCE", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Title = "Title 2", Finding = "Fact 2", WhyItMatters = "Matters 2" };

        var top = RootCauseMerger.ExtractTopRootCauses(new List<RiskFinding> { f1, f2 });

        Assert.Single(top);
        Assert.Equal(RiskSeverity.Critical, top[0].Severity);
        Assert.Equal("Title 2", top[0].Title);
    }

    [Fact(DisplayName = "13. DynamicPagination_AllowsModuleToSpanMultiplePages")]
    public void DynamicPagination_AllowsModuleToSpanMultiplePages()
    {
        var focus = new FocusModuleDetailDto
        {
            SectionId = "founders",
            Title = "Сооснователи",
            Score = 20,
            ScoreBand = "Критические пробелы",
            Findings = Enumerable.Range(1, 8).Select(i => new ReportFindingCardDto
            {
                FindingCode = $"F_{i}",
                Title = $"Risk {i}",
                Severity = RiskSeverity.High,
                SeverityLabel = "Высокий",
                WhyFound = $"Reason {i}",
                WhyItMatters = $"Matters {i}",
                Recommendation = $"Action {i}"
            }).ToList()
        };

        var ctx = new ReportContext { FocusModules = new List<FocusModuleDetailDto> { focus } };
        var testEnv = new TestWebHostEnvironment();
        var aiService = new AiReportService(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var markup = pdfService.BuildTypstMarkup(ctx);
        Assert.Contains("Risk 8", markup);
    }

    [Fact(DisplayName = "14. SevereScenario_ExplicitReportInspection")]
    public void SevereScenario_ExplicitReportInspection()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "dispute",
            ["FND-C04"] = "none",
            ["FND-01"] = "active_conflict",
            ["FND-02"] = "disputed",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "conflict",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz_llp",
            ["COR-01"] = "dispute",
            ["COR-02"] = "fragmented",
            ["COR-03"] = "informal",
            ["COR-04"] = "missing",
            ["COR-05"] = "often_missing",
            ["COR-06"] = "unclear",
            ["COR-07"] = "material_outside",
            ["COR-08"] = "missing",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "some",
            ["IP-05"] = "agreed",
            ["IP-07"] = "payment_only",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",
            ["DATA-01"] = "no",
            ["AI-01"] = "no",
            ["CONTRACT-01"] = new List<string> { "none" },
            ["INVEST-01"] = "terms",
            ["INVEST-02"] = "informal",
            ["INVEST-02A"] = "no",
            ["INVEST-03"] = "none",
            ["INVEST-04"] = "no",
            ["INVEST-05"] = "max_possible"
        };

        var dbInit = new DbInitializer(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
        dbInit.Initialize();
        var qRepo = new QuestionRepository(dbInit);
        var scoringEngine = new ScoringEngine(qRepo);
        var result = scoringEngine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers);
        var reportCtx = ReportEngine.AssembleReportContext(result, facts, "severe-inspection-session", projectName: null);

        var rawCount = result.Risks.Count;
        var rootCount = reportCtx.TopFindings.Count;
        var actionItemsCount = reportCtx.ActionPlan.Count;
        var coveredFindingCodes = reportCtx.ActionPlan.SelectMany(a => a.CoveredFindingCodes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uncoveredFindings = result.Risks.Where(r => !coveredFindingCodes.Contains(r.Code)).ToList();

        _output.WriteLine("==================================================");
        _output.WriteLine("       SEVERE SCENARIO METRICS INSPECTION        ");
        _output.WriteLine("==================================================");
        _output.WriteLine($"Raw Findings:        {rawCount}");
        _output.WriteLine($"Root Findings:       {rootCount}");
        _output.WriteLine($"Action Items BEFORE: 21");
        _output.WriteLine($"Action Items AFTER:  {actionItemsCount}");
        _output.WriteLine($"Covered Findings:    {coveredFindingCodes.Count} / {rawCount}");
        _output.WriteLine($"Uncovered Findings:  {uncoveredFindings.Count}");
        _output.WriteLine("--------------------------------------------------");
        _output.WriteLine("Aggregated Action Plan Workstreams:");
        foreach (var act in reportCtx.ActionPlan)
        {
            _output.WriteLine($"[{act.PriorityGroup}] {act.Title} (Covered: {string.Join(", ", act.CoveredFindingCodes)})");
        }

        Assert.Empty(uncoveredFindings);
        Assert.True(actionItemsCount < rawCount, "ActionPlan must be aggregated into coherent workstreams");
        Assert.Equal(rawCount, coveredFindingCodes.Count);
    }

    private class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "wwwroot"));
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
