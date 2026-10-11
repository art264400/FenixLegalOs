using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class InvestmentNarrativeGroundingTests
{
    public static IEnumerable<object?[]> UnsupportedRoundClaims()
    {
        var claims = new[]
        {
            "Неоформленные права могут сорвать инвестиционный раунд.",
            "Неоформленные права снижают инвестиционную привлекательность компании.",
            "Неоформленные права снижают привлекательность для инвесторов.",
            "Неоформленные права будут выявлены при инвестиционной проверке.",
            "Неоформленные права являются блокером Due Diligence.",
            "Проверка инвестором Due Diligence выявит неоформленные права.",
            "Компания должна подготовиться к привлечению венчурных инвестиций.",
            "Компания должна привлечь инвестиции после оформления прав."
        };
        foreach (var timing in new string?[] { null, "none", "within_12m" })
        foreach (var claim in claims)
            yield return new object?[] { timing, claim };
    }

    [Theory]
    [MemberData(nameof(UnsupportedRoundClaims))]
    public void QualityGate_RejectsUnsupportedFutureRoundClaims(string? timing, string claim)
    {
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext(timing)));
    }

    [Theory]
    [InlineData("6_12m")]
    [InlineData("3_6m")]
    [InlineData("active_search")]
    [InlineData("specific_investor")]
    [InlineData("terms_received")]
    public void QualityGate_AllowsConsequencesForDeclaredRound(string timing)
    {
        var ctx = CreateContext(timing);
        Assert.True(ReportQualityGate.IsFactuallyGrounded(
            "Неоформленные права могут затянуть заявленный инвестиционный раунд.", ctx));
    }

    [Theory]
    [InlineData("Если компания решит привлекать инвестиции, права потребуется подтвердить при подготовке нового раунда.")]
    [InlineData("В случае решения привлечь инвестиции перед возможным раундом потребуется проверить структуру владения.")]
    public void QualityGate_PossibleRoundAllowsOnlyConditionalFundraising(string claim)
    {
        Assert.True(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("within_12m")));
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none")));
    }

    [Theory]
    [InlineData("Новый раунд не планируется, но отсутствие прав затянет привлечение инвестиций.")]
    [InlineData("Новый раунд не планируется, однако эта проблема снижает инвестиционную привлекательность.")]
    [InlineData("Новый раунд не планируется; права на продукт станут блокером Due Diligence.")]
    public void QualityGate_NoRoundStatementDoesNotPermitAdditionalFundraisingClaim(string claim)
    {
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none")));
    }

    [Theory]
    [InlineData("В предыдущем раунде права не проверялись, а сейчас это снижает инвестиционную привлекательность.")]
    [InlineData("Права по предыдущему раунду подтверждены, но новый раунд будет затянут из-за договоров.")]
    [InlineData("Права по предыдущему раунду подтверждены; для привлечения инвестиций нужно закрыть юридические пробелы.")]
    public void QualityGate_PastRoundDoesNotGroundNewFundraisingClaim(string claim)
    {
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none", priorInvestment: true)));
    }

    [Theory]
    [InlineData("Если документы потеряются, это сорвет инвестиционный раунд.")]
    [InlineData("Если возникнет спор с клиентом, он затянет привлечение инвестиций.")]
    [InlineData("Если компания решит привлечь инвестиции, потребуется проверка, однако права уже затягивают инвестиционный раунд.")]
    public void QualityGate_UnrelatedConditionDoesNotEstablishPossibleRound(string claim)
    {
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("within_12m")));
    }

    [Theory]
    [InlineData("По предыдущему раунду необходимо подтвердить права прошлого инвестора.")]
    [InlineData("Условия состоявшегося инвестиционного раунда могут повлиять на контроль и распределение выплат.")]
    [InlineData("По результатам ранее проведенной инвестиционной проверки нужно исполнить обязательства перед инвестором.")]
    [InlineData("Компания ранее привлекла инвестиции; права прошлого инвестора нужно подтвердить документами.")]
    public void QualityGate_AllowsPastRoundObligationsOnlyWhenPriorInvestmentsExist(string claim)
    {
        Assert.True(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none", priorInvestment: true)));
        Assert.False(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none")));
    }

    [Theory]
    [InlineData("Новый инвестиционный раунд не планируется.")]
    [InlineData("Компания не планирует привлекать инвестиции.")]
    [InlineData("Планы нового инвестиционного раунда не заявлены. Оценка не предполагает, что компания готовится к привлечению инвестиций.")]
    [InlineData("Сейчас инвестиции рассматриваются как возможность, а не как начавшийся раунд.")]
    [InlineData("Продукт предоставляет клиентам сервис платежей и инвестиций; применимость специальных требований нужно проверить.")]
    [InlineData("Обещанные опционы сотрудникам влияют на распределение долей и контроль.")]
    public void QualityGate_AllowsNeutralStatementsAndFinancialProductBusinessModel(string claim)
    {
        Assert.True(ReportQualityGate.IsFactuallyGrounded(claim, CreateContext("none")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("within_12m")]
    public void QualityGate_FallsBackAcrossEveryNarrativeChannelForUnsupportedRound(string? timing)
    {
        var ctx = CreateContext(timing);
        const string claim = "Неоформленные права могут затянуть инвестиционный раунд и снизить инвестиционную привлекательность компании. ";
        var raw = new ReportNarrativesDto
        {
            ProjectProfileNarrative = claim,
            ExecutiveConclusion = string.Concat(Enumerable.Repeat(claim, 4)),
            FenixLawRecommendation = claim,
            RootCauseSummaries = new() { ["IP_ASSIGNMENT"] = claim },
            ModuleNarratives = new()
            {
                ["ip"] = new()
                {
                    Summary = claim,
                    PracticalMeaning = claim,
                    FindingNarratives = new()
                    {
                        ["IP_PRODUCT_RIGHTS_UNCONFIRMED"] = new()
                        {
                            WhyFound = claim,
                            WhyItMatters = claim,
                            Recommendations = new() { claim }
                        }
                    }
                }
            },
            ActionNarratives = new()
            {
                ["ACT_IP_CONSOLIDATION_AUDIT"] = new() { WhyNow = claim, ExpectedResult = claim }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);
        var module = sanitized.ModuleNarratives["ip"];
        var finding = module.FindingNarratives["IP_PRODUCT_RIGHTS_UNCONFIRMED"];
        var sourceFinding = ctx.FocusModules[0].Findings[0];

        Assert.Equal(ctx.Profile.ConfigurationNarrative, sanitized.ProjectProfileNarrative);
        Assert.Equal(ctx.ExecutiveConclusion, sanitized.ExecutiveConclusion);
        Assert.Equal(ctx.TopFindings[0].ShortSummary, sanitized.RootCauseSummaries["IP_ASSIGNMENT"]);
        Assert.Equal(DeterministicFallbackNarratives.GenerateModuleFallback(ctx, "ip").Summary, module.Summary);
        Assert.Equal(ctx.FocusModules[0].PracticalMeaning, module.PracticalMeaning);
        Assert.Equal(sourceFinding.WhyFound, finding.WhyFound);
        Assert.Equal(sourceFinding.WhyItMatters, finding.WhyItMatters);
        Assert.Equal(sourceFinding.Recommendations, finding.Recommendations);
        Assert.Equal(ctx.ActionPlan[0].WhyNow, sanitized.ActionNarratives["ACT_IP_CONSOLIDATION_AUDIT"].WhyNow);
        Assert.Equal(ctx.ActionPlan[0].ExpectedResult, sanitized.ActionNarratives["ACT_IP_CONSOLIDATION_AUDIT"].ExpectedResult);
        Assert.Equal(ctx.FenixLaw.SummaryText, sanitized.FenixLawRecommendation);
    }

    [Theory]
    [InlineData(null, 85)]
    [InlineData(null, 65)]
    [InlineData(null, 35)]
    [InlineData("none", 85)]
    [InlineData("none", 65)]
    [InlineData("none", 35)]
    [InlineData("within_12m", 85)]
    [InlineData("within_12m", 65)]
    [InlineData("within_12m", 35)]
    public void DeterministicFallback_IsGroundedAtAllScoreTiersWithoutPlannedRound(string? timing, int score)
    {
        var ctx = CreateContext(timing);
        ctx.Overall.Score = score;
        if (score >= 80) ctx.AllFindings.Clear();
        var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        var texts = new[]
        {
            narratives.ExecutiveConclusion,
            narratives.ModuleNarratives["ip"].Summary,
            narratives.ModuleNarratives["ip"].PracticalMeaning,
            narratives.ActionNarratives["ACT_IP_CONSOLIDATION_AUDIT"].WhyNow,
            DeterministicFallbackNarratives.GenerateModuleFallback(ctx, "team").PracticalMeaning
        };
        foreach (var text in texts)
            Assert.True(ReportQualityGate.IsFactuallyGrounded(text, ctx), text);
        Assert.DoesNotContain("инвестор", string.Join(" ", texts), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("раунд", string.Join(" ", texts), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    [InlineData("within_12m")]
    public void UnifiedActionPlan_UsesBusinessConsequencesWithoutInventingRound(string? timing)
    {
        var findings = ActionLibrary.All.Select(def => new RiskFinding
        {
            Code = def.SupportedFindingCodes.First(),
            RecommendedActionId = def.ActionId,
            SectionId = def.SectionId,
            Title = def.Title,
            Severity = RiskSeverity.High,
            Priority = def.DefaultPriority
        }).ToList();
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "one";
        if (timing != null) facts.Facts["investment.timing"] = timing;
        var ctx = CreateContext(timing, priorInvestment: true);
        var actions = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, facts);

        Assert.Equal(ActionLibrary.All.Count, actions.Count);
        foreach (var action in actions)
        {
            var text = string.Join(" ", action.Title, action.WhatToDo, action.WhyNow, action.ExpectedResult);
            Assert.True(ReportQualityGate.IsFactuallyGrounded(text, ctx), $"{action.ActionId}: {text}");
            Assert.DoesNotContain("раунд", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ДО ИНВЕСТИЦИОННОГО РАУНДА", action.PriorityGroup);
            Assert.Equal(findings.Single(f => f.RecommendedActionId == action.ActionId).Priority, action.Priority);
            Assert.Single(action.CoveredFindingCodes);
        }
        Assert.Contains(actions, a => a.ActionId == "ACT_INVEST_CAP_TABLE_PREPARATION"
            && a.WhatToDo.Contains("прошлыми инвесторами", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("within_12m")]
    public void AssembledReport_DoesNotLeakOriginalInvestmentConsequenceIntoRootSummary(string timing)
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "one";
        facts.Facts["investment.timing"] = timing;
        facts.Facts["investment.priorInvestment"] = false;
        var result = new ScoreResult
        {
            Overall = 65,
            InvestmentReadiness = new() { Applicable = false },
            Sections = new()
            {
                new() { SectionId = "ip", Title = "Интеллектуальная собственность", Score = 65 },
                new() { SectionId = "investment", Title = "Инвестиции", Score = null, Status = ApplicabilityStatus.NotApplicable }
            },
            Risks = new()
            {
                new()
                {
                    Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
                    RootCauseGroup = "IP_ASSIGNMENT",
                    SectionId = "ip",
                    Title = "Права на продукт не подтверждены",
                    Finding = "",
                    WhyItMatters = "Это снижает инвестиционную привлекательность и может затянуть новый раунд.",
                    Recommendation = "Подтвердить переход прав на продукт.",
                    Severity = RiskSeverity.High,
                    Priority = RiskPriority.ThirtyDays
                }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "grounding-scenario", "Компания");
        var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        Assert.True(ReportQualityGate.IsFactuallyGrounded(ctx.TopFindings.Single().ShortSummary, ctx));
        Assert.All(narratives.RootCauseSummaries.Values,
            text => Assert.True(ReportQualityGate.IsFactuallyGrounded(text, ctx), text));
        Assert.Equal(65, ctx.Overall.Score);
        Assert.Equal(RiskSeverity.High, ctx.AllFindings.Single().Severity);
        Assert.Equal(RiskPriority.ThirtyDays, ctx.AllFindings.Single().Priority);
    }

    private static ReportContext CreateContext(string? timing, bool priorInvestment = false)
    {
        var ctx = new ReportContext
        {
            InvestmentReadiness = new()
            {
                InvestmentTiming = timing,
                NewRoundPlanned = timing is "6_12m" or "3_6m" or "active_search" or "specific_investor" or "terms_received",
                NewRoundPossible = timing == "within_12m",
                HasPriorInvestment = priorInvestment
            },
            Overall = new() { Score = 65, LevelTitle = "Требует внимания" },
            Profile = new() { ConfigurationNarrative = "Компания создала продукт и работает с клиентами." },
            ExecutiveConclusion = "Необходимо подтвердить права на продукт и актуализировать документы компании.",
            FenixLaw = new() { RequiresLegalWork = true, SummaryText = "Юрист поможет оформить передачу прав на продукт." },
            TopFindings = new()
            {
                new() { FindingCode = "IP_PRODUCT_RIGHTS_UNCONFIRMED", RootCauseCode = "IP_ASSIGNMENT", DetailSectionId = "ip", ShortSummary = "Передача прав на продукт не подтверждена." }
            },
            AllFindings = new()
            {
                new() { Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED", SectionId = "ip", Severity = RiskSeverity.High }
            },
            FocusModules = new()
            {
                new()
                {
                    SectionId = "ip", Title = "Интеллектуальная собственность", Score = 65,
                    PracticalMeaning = "Подтверждение прав защищает использование и развитие продукта.",
                    Findings = new()
                    {
                        new()
                        {
                            FindingCode = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
                            WhyFound = "Передача прав на продукт не подтверждена.",
                            WhyItMatters = "Компания может столкнуться с ограничениями использования продукта.",
                            Recommendation = "Подтвердить передачу прав.",
                            Recommendations = new() { "Подтвердить передачу прав." }
                        }
                    }
                }
            },
            ActionPlan = new()
            {
                new()
                {
                    ActionId = "ACT_IP_CONSOLIDATION_AUDIT", Title = "Подтвердить права на продукт",
                    WhyNow = "Подтверждение прав защищает использование и развитие продукта.",
                    ExpectedResult = "Сформирован реестр активов и подтверждено право компании использовать продукт."
                }
            }
        };
        return ctx;
    }
}
