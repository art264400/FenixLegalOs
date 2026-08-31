using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class NarrativeEngineGroundingTests
{
    private ReportContext CreateBaseReportContext(string scenario = "standard")
    {
        var ctx = new ReportContext
        {
            SessionId = "test_grounding_session",
            ProjectName = "Titan Cyber",
            ProjectStage = "MVP",
            Overall = new OverallScoreDto
            {
                Score = 45,
                Band = "MaterialGaps",
                LevelTitle = "Существенные пробелы",
                Confidence = 100,
                TopDrivers = new List<string> { "Интеллектуальная собственность" }
            },
            Profile = new ProjectProfileDto
            {
                KeyFacts = new List<FactItemDto>
                {
                    new() { Key = "entity", Label = "Юридическое лицо", Value = "Зарегистрировано (одно юрлицо)" },
                    new() { Key = "jurisdiction", Label = "Юрисдикция", Value = "Казахстан" },
                    new() { Key = "founders", Label = "Основатели", Value = "2 сооснователя" },
                    new() { Key = "creators", Label = "Кто создает продукт", Value = "Внешние разработчики" },
                    new() { Key = "ip_rights", Label = "Права на продукт", Value = "Права не оформлены" }
                },
                ConfigurationNarrative = "Titan Cyber осуществляет деятельность через структуру в юрисдикции Казахстан."
            },
            TopFindings = new List<TopFindingSummaryDto>
            {
                new()
                {
                    RootCauseCode = "RC_IP_TRANSFER_GAP",
                    FindingCode = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
                    Title = "Права на продукт не подтверждены документально",
                    Severity = RiskSeverity.Blocker,
                    ShortSummary = "Продукт создавался подрядчиками, но права не переданы компании полностью."
                }
            },
            FocusModules = new List<FocusModuleDetailDto>
            {
                new()
                {
                    SectionId = "ip",
                    Title = "Интеллектуальная собственность",
                    Score = 30,
                    ScoreBand = "Критические пробелы",
                    Findings = new List<ReportFindingCardDto>
                    {
                        new()
                        {
                            FindingCode = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
                            Title = "Права на продукт не подтверждены документально",
                            Severity = RiskSeverity.Blocker,
                            WhyFound = "Продукт создавался подрядчиками, но акты передачи прав отсутствуют.",
                            WhyItMatters = "Создает существенный риск при проведении Due Diligence и привлечении инвестиций.",
                            Recommendation = "Подписать договоры и акты передачи прав со всеми подрядчиками."
                        }
                    }
                }
            },
            ActionPlan = new List<UnifiedActionItemDto>
            {
                new()
                {
                    ActionId = "ACT_IP_ASSIGNMENT",
                    Title = "Подписать акты передачи прав с разработчиками",
                    WhyNow = "Необходимо до открытия инвестиционного раунда.",
                    ExpectedResult = "Исключительные права полностью закреплены за компанией.",
                    ResolutionMode = ResolutionMode.LegalWork
                }
            },
            FenixLaw = new FenixLawRecommendationReportDto
            {
                RequiresLegalWork = true,
                SummaryText = "Рекомендуется юридическая помощь по направлению IP."
            }
        };

        ctx.ExecutiveConclusion = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx).ExecutiveConclusion;
        return ctx;
    }

    [Fact]
    public void Grounding_ContractorDoesNotInventDeveloperDeparture()
    {
        var ctx = CreateBaseReportContext();

        // LLM hallucinated that developer "left/quit" although context only says contractors participated
        var raw = new ReportNarrativesDto
        {
            RootCauseSummaries = new Dictionary<string, string>
            {
                ["RC_IP_TRANSFER_GAP"] = "Разработка велась подрядчиками, что привело к спору при уходе разработчика."
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        // Grounding gate must reject the hallucinated departure narrative and fall back to grounded summary
        Assert.DoesNotContain("уходе", sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.TopFindings[0].ShortSummary, sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"]);
    }

    [Fact]
    public void Grounding_UnconfirmedRightsDoesNotInventNonExistentActs()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            RootCauseSummaries = new Dictionary<string, string>
            {
                ["RC_IP_TRANSFER_GAP"] = "Договоры авторского заказа никогда не подписывались и акты приема-передачи вовсе отсутствуют."
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("никогда не подписывались", sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.TopFindings[0].ShortSummary, sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"]);
    }

    [Fact]
    public void Grounding_IpIssueDoesNotInventInstitutionalInvestorRefusal()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Текущая оценка (45/100) показывает, что институциональные инвесторы откажут в раунде финансирования."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("институциональные инвесторы откажут", sanitized.ExecutiveConclusion, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.ExecutiveConclusion, sanitized.ExecutiveConclusion);
    }

    [Fact]
    public void Grounding_DisputeDoesNotInventConflictCause()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            RootCauseSummaries = new Dictionary<string, string>
            {
                ["RC_IP_TRANSFER_GAP"] = "Возник конфликт из-за невыплаты денег и гонорара подрядчикам."
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("из-за невыплаты денег", sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.TopFindings[0].ShortSummary, sanitized.RootCauseSummaries["RC_IP_TRANSFER_GAP"]);
    }

    [Fact]
    public void Grounding_UnknownFactDoesNotProduceDefinitiveNegativeStatement()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Поскольку информация не была указана, в компании гарантированно нет никаких локальных актов и точно отсутствуют договоры."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("точно отсутствуют", sanitized.ExecutiveConclusion, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.ExecutiveConclusion, sanitized.ExecutiveConclusion);
    }

    [Fact]
    public void Grounding_ActionNarrativesPreserveResolutionScope()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            ActionNarratives = new Dictionary<string, ActionNarrativeItemDto>
            {
                ["ACT_IP_ASSIGNMENT"] = new()
                {
                    WhyNow = "Если не подписать сейчас, институциональные инвесторы откажут во входе в капитал.",
                    ExpectedResult = "Исключительные права полностью закреплены за компанией."
                }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("институциональные инвесторы откажут", sanitized.ActionNarratives["ACT_IP_ASSIGNMENT"].WhyNow, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.ActionPlan[0].WhyNow, sanitized.ActionNarratives["ACT_IP_ASSIGNMENT"].WhyNow);
    }

    [Fact]
    public void Grounding_ProjectProfileMatchesExactKeyFacts()
    {
        var ctx = CreateBaseReportContext();

        // Valid grounded narrative that synthesizes key facts
        var validGroundedProfile = "Titan Cyber осуществляет деятельность в Казахстане. В проекте 2 сооснователя. Разработка ведется с участием внешних разработчиков.";
        var raw = new ReportNarrativesDto
        {
            ProjectProfileNarrative = validGroundedProfile
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.Equal(validGroundedProfile, sanitized.ProjectProfileNarrative);
    }

    [Fact]
    public void Grounding_MissingDocumentDoesNotAssertNeverExisted()
    {
        var ctx = CreateBaseReportContext();

        var raw = new ReportNarrativesDto
        {
            ModuleNarratives = new Dictionary<string, ModuleNarrativeDto>
            {
                ["ip"] = new()
                {
                    Summary = "В направлении IP договоры с разработчиками никогда не существовали и вовсе отсутствуют.",
                    PracticalMeaning = "Вопросы прав проверяются инвесторами."
                }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("никогда не существовали", sanitized.ModuleNarratives["ip"].Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx).ModuleNarratives["ip"].Summary, sanitized.ModuleNarratives["ip"].Summary);
    }

    [Fact]
    public void Grounding_CausalRelationshipMustBeGroundedInFindings()
    {
        var ctx = CreateBaseReportContext();

        // Inventing that a conflict arose because of developer payment dispute
        var raw = new ReportNarrativesDto
        {
            ProjectProfileNarrative = "Разработка велась подрядчиками, что вызвало конфликт из-за гонорара."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.DoesNotContain("из-за гонорара", sanitized.ProjectProfileNarrative, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ctx.Profile.ConfigurationNarrative, sanitized.ProjectProfileNarrative);
    }

    [Fact]
    public void Grounding_AllGeneratedScenarioArtifactsAreFactuallyGrounded()
    {
        var scenarios = new[] { "healthy", "medium", "severe", "investment_blocker_heavy" };
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        foreach (var sc in scenarios)
        {
            var jsonPath = Path.Combine(baseDir, "output", "scenarios", $"{sc}_report.json");
            Assert.True(File.Exists(jsonPath), $"Artifact {jsonPath} must exist.");

            var jsonText = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(jsonText);
            var root = doc.RootElement;

            // Ensure no extreme investor rejection hallucination in any generated artifact
            Assert.DoesNotContain("институциональные инвесторы откажут", jsonText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("гарантированный отказ инвесторов", jsonText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("100% срыв", jsonText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("точно отсутствуют", jsonText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Grounding_MultiCreatorsIncludesFormerDevelopers()
    {
        var facts = new SharedFactStore();
        facts.Facts["ip.creators"] = new List<string> { "contractors", "former" };
        facts.Facts["product.stage"] = "prototype";
        facts.Facts["ip.overallRightsEvidence"] = "partial";
        facts.Facts["corporate.status"] = "incorporated";
        facts.Facts["corporate.jurisdiction"] = "kz";
        facts.Facts["founders.count"] = "2";
        facts.Facts["founders.splitType"] = "50_50";

        var profile = ProjectProfileExtractor.ExtractProfile(facts, "Titan Cyber");
        Assert.Contains("бывшие", profile.ConfigurationNarrative, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("внешние разработчики и бывшие участники команды", profile.ConfigurationNarrative, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActionLibrary_DoesNotContainOverPrescriptiveParameters()
    {
        foreach (var action in FenixLegalOs.Data.ActionLibrary.ActionLibrary.All)
        {
            Assert.DoesNotContain("Russian Roulette", action.RequiredOutcome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Texas Shootout", action.RequiredOutcome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("3–4 летний", action.RequiredOutcome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("1-летним клиффом", action.RequiredOutcome, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("100% покрытия", action.RequiredOutcome, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FactorBreakdown_DecouplesScoreBandFromSeverity()
    {
        var sec = new SectionScore
        {
            SectionId = "corporate",
            Score = 85,
            Dimensions = new List<DimensionScore>
            {
                new() { DimensionId = "ownership_accuracy", Score = 85 },
                new() { DimensionId = "corporate_approvals", Score = 65 },
                new() { DimensionId = "authority", Score = 40 }
            }
        };

        var (_, _, _, table) = FactorBreakdownEvaluator.EvaluateSectionDriversAndTable(sec, new List<RiskFinding>());

        foreach (var row in table)
        {
            Assert.DoesNotContain("Критические пробелы", row.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Существенные пробелы", row.StatusText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Устойчиво", row.StatusText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void FocusNarrative_IsScoreAwareForHighScores()
    {
        var ctx = CreateBaseReportContext();
        ctx.FocusModules[0].Score = 85;
        ctx.FocusModules[0].ScoreBand = "Устойчиво";
        ctx.FocusModules[0].Findings = new List<ReportFindingCardDto>
        {
            new()
            {
                FindingCode = "COR_GOVERNANCE_GAP",
                Title = "Не систематизированы корпоративные решения",
                Severity = RiskSeverity.Medium,
                SeverityLabel = "Умеренный",
                WhyFound = "Протоколы собраний не велись.",
                WhyItMatters = "Затрудняет подтверждение полномочий.",
                Recommendation = "Оформить протоколы."
            }
        };

        var fallbacks = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        var corpSummary = fallbacks.ModuleNarratives[ctx.FocusModules[0].SectionId].Summary;

        Assert.Contains("высокий уровень готовности", corpSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Основные риски связаны с незавершенным оформлением", corpSummary, StringComparison.OrdinalIgnoreCase);
    }
}
