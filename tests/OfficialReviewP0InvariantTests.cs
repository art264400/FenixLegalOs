using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace FenixLegalOs.Tests;

public class OfficialReviewP0InvariantTests
{
    private class TestWebHostEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact(DisplayName = "1. Every input finding receives expected representation and unknown IDs are rejected in Quality Gate")]
    public void EveryInputFinding_ReceivesExpectedRepresentation_UnknownIdsRejected()
    {
        var ctx = new ReportContext
        {
            TopFindings = new List<TopFindingSummaryDto>
            {
                new() { FindingCode = "FND_DEADLOCK", RootCauseCode = "FOUNDER_CONTROL", ShortSummary = "Тупиковая ситуация" }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new()
                {
                    SectionId = "founders",
                    Findings = new List<ReportFindingCardDto>
                    {
                        new() { FindingCode = "FND_DEADLOCK", Title = "Тупик", WhyFound = "50/50", WhyItMatters = "Блокировка", Recommendation = "SHA", Recommendations = new() { "Шаг 1" } }
                    }
                }
            }
        };

        var raw = new ReportNarrativesDto
        {
            RootCauseSummaries = new Dictionary<string, string>
            {
                ["FOUNDER_CONTROL"] = "Корректный нарратив",
                ["UNKNOWN_HACK_ID"] = "Галлюцинация"
            },
            ModuleNarratives = new Dictionary<string, ModuleNarrativeDto>
            {
                ["founders"] = new()
                {
                    Summary = "Разбор",
                    PracticalMeaning = "Бизнес-риски",
                    FindingNarratives = new Dictionary<string, FindingNarrativeDto>
                    {
                        ["FND_DEADLOCK"] = new() { WhyFound = "50/50 контроль", WhyItMatters = "Паралич", Recommendation = "Утвердить SHA", Recommendations = new() { "Шаг 1" } },
                        ["UNKNOWN_FINDING"] = new() { WhyFound = "Неизвестный риск", WhyItMatters = "Х", Recommendation = "Y" }
                    }
                }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        // Valid IDs are preserved
        Assert.True(sanitized.RootCauseSummaries.ContainsKey("FOUNDER_CONTROL"));
        Assert.True(sanitized.ModuleNarratives["founders"].FindingNarratives.ContainsKey("FND_DEADLOCK"));

        // Unknown IDs are NOT leaked into client document
        Assert.False(sanitized.RootCauseSummaries.ContainsKey("UNKNOWN_HACK_ID"));
        Assert.False(sanitized.ModuleNarratives["founders"].FindingNarratives.ContainsKey("UNKNOWN_FINDING"));
    }

    [Fact(DisplayName = "2. All Critical and High findings are covered by registry without artificial Take(N) dropping")]
    public void AllCriticalAndHighFindings_CoveredByRegistry_NoTakeDrop()
    {
        var allFindings = new List<RiskFinding>();
        for (int i = 1; i <= 15; i++)
        {
            allFindings.Add(new RiskFinding
            {
                Code = $"FND_CRIT_{i}",
                SectionId = "founders",
                Title = $"Критический риск {i}",
                Severity = i <= 3 ? RiskSeverity.Critical : RiskSeverity.High,
                Priority = RiskPriority.Now,
                Finding = "Факты",
                WhyItMatters = "Важность",
                Recommendation = "Рекомендация"
            });
        }

        var ctx = new ReportContext
        {
            AllFindings = allFindings,
            FocusModules = new List<FocusModuleDetailDto>
            {
                new()
                {
                    SectionId = "founders",
                    Title = "Сооснователи",
                    Findings = allFindings.Select(f => new ReportFindingCardDto
                    {
                        FindingCode = f.Code,
                        Title = f.Title,
                        Severity = f.Severity,
                        SeverityLabel = "Критический",
                        WhyFound = f.Finding,
                        WhyItMatters = f.WhyItMatters,
                        Recommendation = f.Recommendation
                    }).ToList()
                }
            }
        };

        var pdfService = new TypstPdfService(new TestWebHostEnv(), new AiReportService(new ConfigurationBuilder().Build()));
        var typst = pdfService.BuildTypstMarkup(ctx);

        // Verify that all 15 findings are present in the table
        for (int i = 1; i <= 15; i++)
        {
            Assert.Contains($"Критический риск {i}", typst);
        }
    }

    [Fact(DisplayName = "3. Actions and Findings have consistent ResolutionMode across all representations")]
    public void ActionLinkedToExistingFindings_ResolutionModesConsistentAcrossAllViews()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "incorporated";

        var fndRisk = FoundersRisks.All.First(r => r.Code == "FND_DEADLOCK");
        var ipRisk = IpRisks.All.First(r => r.Code == "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");

        var findingDeadlock = new RiskFinding
        {
            Code = fndRisk.Code,
            SectionId = fndRisk.SectionId,
            Title = fndRisk.Title,
            Severity = fndRisk.Severity,
            Priority = fndRisk.Priority,
            ResolutionMode = fndRisk.ResolutionMode,
            Finding = fndRisk.Finding,
            WhyItMatters = fndRisk.WhyItMatters,
            Recommendation = fndRisk.Recommendation
        };

        var findingIp = new RiskFinding
        {
            Code = ipRisk.Code,
            SectionId = ipRisk.SectionId,
            Title = ipRisk.Title,
            Severity = ipRisk.Severity,
            Priority = ipRisk.Priority,
            ResolutionMode = ipRisk.ResolutionMode,
            Finding = ipRisk.Finding,
            WhyItMatters = ipRisk.WhyItMatters,
            Recommendation = ipRisk.Recommendation
        };

        var plan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(new List<RiskFinding> { findingDeadlock, findingIp }, facts);

        // Verify that ResolutionMode is LegalWork for both
        Assert.Equal(ResolutionMode.LegalWork, findingDeadlock.ResolutionMode);
        Assert.Equal(ResolutionMode.LegalWork, findingIp.ResolutionMode);

        var ipAction = plan.First(a => a.CoveredFindingCodes.Contains("IP_FOUNDER_RIGHTS_NOT_TRANSFERRED"));
        Assert.Equal(ResolutionMode.LegalWork, ipAction.ResolutionMode);
        Assert.Equal("Требуется юридическая работа", ipAction.ResolutionFormat);
    }

    [Fact(DisplayName = "4. Privacy policy creation/update is LegalWork, not InternalAction")]
    public void PrivacyPolicy_NotRenderedAsInternalAction()
    {
        var privacyAction = ActionLibrary.GetById("ACT_DATA_PRIVACY_POLICY_CREATE");
        Assert.NotNull(privacyAction);
        Assert.Equal(ResolutionMode.LegalWork, privacyAction.ResolutionMode);

        var privacyRisk = DataAiRisks.All.First(r => r.Code == "DATA_PRIVACY_NOTICE_OUTDATED");
        Assert.Equal(ResolutionMode.LegalWork, privacyRisk.ResolutionMode);
    }

    [Fact(DisplayName = "5. Blockers cannot coexist with 'не обнаружено' text")]
    public void Blockers_CannotCoexistWithNoneFoundText()
    {
        var ctx = new ReportContext
        {
            InvestmentReadiness = new InvestmentReadinessReportDto
            {
                IsApplicable = true,
                ReadinessScore = 45,
                Category = "Сквозные юридические блокеры",
                SummaryDescription = "Базовая готовность инвест-блока составляет 78/100, однако общая готовность к сделке ограничена 2 критическими блокерами.",
                BlockerTitles = new List<string> { "Сооснователи: Риск тупика", "IP: Принадлежность продукта" }
            }
        };

        var pdfService = new TypstPdfService(new TestWebHostEnv(), new AiReportService(new ConfigurationBuilder().Build()));
        var typst = pdfService.BuildTypstMarkup(ctx);

        Assert.Contains("Сооснователи: Риск тупика", typst);
        Assert.Contains("IP: Принадлежность продукта", typst);
        Assert.DoesNotContain("Критичных блокеров для раунда не обнаружено", typst);
    }

    [Fact(DisplayName = "6. Vesting expectedResult is protected from LLM distortion")]
    public void VestingExpectedResult_NotDistorted()
    {
        var vestingAction = ActionLibrary.GetById("ACT_FOUNDER_VESTING_LEAVER");
        Assert.NotNull(vestingAction);

        var ctx = new ReportContext
        {
            ActionPlan = new List<UnifiedActionItemDto>
            {
                new()
                {
                    ActionId = vestingAction.ActionId,
                    Title = vestingAction.Title,
                    WhyNow = vestingAction.BusinessReason,
                    ExpectedResult = vestingAction.RequiredOutcome,
                    WhatToDo = vestingAction.WhatToDo
                }
            }
        };

        // Simulated LLM trying to hallucinate "при недостижении согласия" into vesting
        var raw = new ReportNarrativesDto
        {
            ActionNarratives = new Dictionary<string, ActionNarrativeItemDto>
            {
                ["ACT_FOUNDER_VESTING_LEAVER"] = new()
                {
                    WhyNow = "Пояснение",
                    ExpectedResult = "В корпоративном договоре закреплен график выкупа долей при недостижении согласия."
                }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        // Invariant: canonical outcome remains intact
        Assert.Contains("выходе из проекта", sanitized.ActionNarratives["ACT_FOUNDER_VESTING_LEAVER"].ExpectedResult);
        Assert.DoesNotContain("при недостижении согласия", sanitized.ActionNarratives["ACT_FOUNDER_VESTING_LEAVER"].ExpectedResult);
    }

    [Fact(DisplayName = "7. Dynamic navigation links in 8-Zone map use context page counter")]
    public void MultiPageModules_UseDynamicTypstLabelsAndPageCounters()
    {
        var ctx = new ReportContext
        {
            ModuleCards = new List<ModuleCardDto>
            {
                new() { SectionId = "founders", Title = "Сооснователи", RenderMode = ReportRenderMode.Focus, Score = 41 },
                new() { SectionId = "corporate", Title = "Корпоративная структура", RenderMode = ReportRenderMode.Focus, Score = 55 },
                new() { SectionId = "contracts", Title = "Договоры", RenderMode = ReportRenderMode.Compact, Score = 70 }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new() { SectionId = "founders", Title = "Сооснователи", Score = 41, ScoreBand = "Существенные пробелы" },
                new() { SectionId = "corporate", Title = "Корпоративная структура", Score = 55, ScoreBand = "Требует внимания" }
            },
            CompactModules = new List<CompactModuleDto>
            {
                new() { SectionId = "contracts", Title = "Договоры", Score = 70, StatusText = "Умеренно" }
            }
        };

        var pdfService = new TypstPdfService(new TestWebHostEnv(), new AiReportService(new ConfigurationBuilder().Build()));
        var typst = pdfService.BuildTypstMarkup(ctx);

        // Assert dynamic labels and links exist
        Assert.Contains("<sec-founders>", typst);
        Assert.Contains("<sec-corporate>", typst);
        Assert.Contains("<sec-compact>", typst);
        Assert.Contains("counter(page).at(<sec-founders>)", typst);
        Assert.Contains("counter(page).at(<sec-corporate>)", typst);
        Assert.Contains("counter(page).at(<sec-compact>)", typst);
    }

    [Fact(DisplayName = "8. ResolutionMode is correctly diversified and consistent across findings and action plan")]
    public void ResolutionMode_DiversifiedAndConsistentAcrossModulesAndActionPlan()
    {
        // Check Team findings
        var accessFinding = new RiskFinding { Code = "TEAM_ACCESS_CONTROL_GAP", SectionId = "team" };
        var accessAction = ActionLibrary.ResolveActionForFinding(accessFinding);
        Assert.Equal(ResolutionMode.InternalAction, accessAction.ResolutionMode);

        var offboardingFinding = new RiskFinding { Code = "TEAM_OFFBOARDING_GAP", SectionId = "team" };
        var offboardingAction = ActionLibrary.ResolveActionForFinding(offboardingFinding);
        Assert.Equal(ResolutionMode.InternalAction, offboardingAction.ResolutionMode);

        // Check Data / AI findings
        var dataMapFinding = new RiskFinding { Code = "DATA_MAP_INCOMPLETE", SectionId = "data" };
        var dataMapAction = ActionLibrary.ResolveActionForFinding(dataMapFinding);
        Assert.Equal(ResolutionMode.InternalAction, dataMapAction.ResolutionMode);

        var aiFinding = new RiskFinding { Code = "AI_USER_DATA_TRANSFER", SectionId = "data" };
        var aiAction = ActionLibrary.ResolveActionForFinding(aiFinding);
        Assert.Equal(ResolutionMode.LegalReview, aiAction.ResolutionMode);

        var deletionFinding = new RiskFinding { Code = "DATA_RETENTION_UNDEFINED", SectionId = "data" };
        var deletionAction = ActionLibrary.ResolveActionForFinding(deletionFinding);
        Assert.Equal(ResolutionMode.LegalAndProduct, deletionAction.ResolutionMode);

        var privacyFinding = new RiskFinding { Code = "DATA_PRIVACY_NOTICE_MISSING", SectionId = "data" };
        var privacyAction = ActionLibrary.ResolveActionForFinding(privacyFinding);
        Assert.Equal(ResolutionMode.LegalWork, privacyAction.ResolutionMode);

        // Check IP findings
        var ipDomainFinding = new RiskFinding { Code = "IP_DOMAIN_BRAND_CONTROL", SectionId = "ip" };
        var ipDomainAction = ActionLibrary.ResolveActionForFinding(ipDomainFinding);
        Assert.Equal(ResolutionMode.LegalWork, ipDomainAction.ResolutionMode);

        var ipContentFinding = new RiskFinding { Code = "IP_CONTENT_RIGHTS", SectionId = "ip" };
        var ipContentAction = ActionLibrary.ResolveActionForFinding(ipContentFinding);
        Assert.Equal(ResolutionMode.LegalReview, ipContentAction.ResolutionMode);

        // Check Product findings
        var ugcFinding = new RiskFinding { Code = "PROD_USER_CONTENT_RULES", SectionId = "product" };
        var ugcAction = ActionLibrary.ResolveActionForFinding(ugcFinding);
        Assert.Equal(ResolutionMode.LegalAndProduct, ugcAction.ResolutionMode);

        var minorsFinding = new RiskFinding { Code = "PROD_MINORS_REVIEW", SectionId = "product" };
        var minorsAction = ActionLibrary.ResolveActionForFinding(minorsFinding);
        Assert.Equal(ResolutionMode.LegalAndProduct, minorsAction.ResolutionMode);
    }

    [Fact(DisplayName = "9. ExecutiveConclusion provides comprehensive grounded synthesis for complex scenario")]
    public void ExecutiveConclusion_ProvidesGroundedSynthesisForComplexScenario()
    {
        var ctx = new ReportContext
        {
            Overall = new OverallScoreDto { Score = 48, LevelTitle = "Требует существенной доработки" },
            TopFindings = new List<TopFindingSummaryDto>
            {
                new() { FindingCode = "FND_DEADLOCK", DetailSectionId = "founders", ShortSummary = "Риск тупика" },
                new() { FindingCode = "IP_PRODUCT_RIGHTS_UNCONFIRMED", DetailSectionId = "ip", ShortSummary = "Неполные права на продукт" }
            },
            AllFindings = new List<RiskFinding>
            {
                new() { Code = "FND_DEADLOCK", Severity = RiskSeverity.Critical },
                new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", Severity = RiskSeverity.Critical },
                new() { Code = "TEAM_NO_WRITTEN_CONTRACTS", Severity = RiskSeverity.High },
                new() { Code = "DATA_PRIVACY_NOTICE_MISSING", Severity = RiskSeverity.High },
                new() { Code = "PROD_RULES_DISCREPANCY", Severity = RiskSeverity.High }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new() { SectionId = "data", Title = "Данные и конфиденциальность", Score = 31 },
                new() { SectionId = "team", Title = "Команда и сотрудники", Score = 36 },
                new() { SectionId = "ip", Title = "Интеллектуальная собственность", Score = 38 },
                new() { SectionId = "founders", Title = "Сооснователи", Score = 41 }
            },
            PositiveFactors = new List<PositiveFactorDto>
            {
                new() { Title = "Регистрация юридического лица" }
            }
        };

        var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);

        Assert.NotNull(narratives.ExecutiveConclusion);
        Assert.True(narratives.ExecutiveConclusion.Length > 300, "ExecutiveConclusion should be substantive (300+ chars)");
        Assert.Contains("48 / 100", narratives.ExecutiveConclusion);
        Assert.Contains("2 критических", narratives.ExecutiveConclusion);
        Assert.Contains("«Данные и конфиденциальность» (31/100)", narratives.ExecutiveConclusion);
        Assert.Contains("«Команда и сотрудники» (36/100)", narratives.ExecutiveConclusion);
        Assert.Contains("Подтвержденные сильные стороны проекта", narratives.ExecutiveConclusion);
    }

    [Fact(DisplayName = "10. ContextFingerprint detects stale narratives and applies fallback safely")]
    public void ContextFingerprint_DetectsStaleNarratives_AppliesFallback()
    {
        var ctx = new ReportContext
        {
            Overall = new OverallScoreDto { Score = 48 },
            AllFindings = new List<RiskFinding>
            {
                new() { Code = "FND_DEADLOCK", Severity = RiskSeverity.Critical }
            },
            TopFindings = new List<TopFindingSummaryDto>
            {
                new() { FindingCode = "FND_DEADLOCK" }
            }
        };

        var staleNarratives = new ReportNarrativesDto
        {
            ContextFingerprint = "OLD_STALE_HASH_12345",
            ExecutiveConclusion = "Устаревший текст из другого проекта..."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(staleNarratives, ctx);

        // Fallback is safely applied and new fingerprint is computed
        Assert.NotEqual("OLD_STALE_HASH_12345", sanitized.ContextFingerprint);
        Assert.Equal(ReportQualityGate.ComputeContextFingerprint(ctx), sanitized.ContextFingerprint);
        Assert.DoesNotContain("Устаревший текст из другого проекта", sanitized.ExecutiveConclusion);
    }

    [Fact(DisplayName = "11. Prohibited hyperbolic phrases are filtered in QualityGate")]
    public void QualityGate_FiltersHyperbolicPhrases()
    {
        var ctx = new ReportContext();

        Assert.False(ReportQualityGate.IsFactuallyGrounded("Инвестор приостановит сделку и откажется от раунда.", ctx));
        Assert.False(ReportQualityGate.IsFactuallyGrounded("Гарантированный отказ инвесторов.", ctx));
        Assert.False(ReportQualityGate.IsFactuallyGrounded("100% срыв сделки.", ctx));
        Assert.True(ReportQualityGate.IsFactuallyGrounded("Наличие неурегулированных вопросов может осложнить инвестиционную проверку.", ctx));
    }
}
