using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FenixLegalOs.Data;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class ReportEngineVNextTests
{
    [Fact(DisplayName = "Regression A: Healthy Scenario (Score 100) has no negative profile facts or priority-remediation narrative")]
    public void HealthyScenario_Has_No_Contradictory_Negative_Facts()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "registered";
        facts.Facts["founders.count"] = "2";
        facts.Facts["founders.disputes"] = "none";
        facts.Facts["ip.creators"] = "founders_only";
        facts.Facts["ip.assignedToCompany"] = "yes";

        var result = new ScoreResult
        {
            Overall = 100,
            Level = LegalScoreLevel.Strong,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 100, Status = ApplicabilityStatus.Applicable, Title = "Основатели" },
                new() { SectionId = "corporate", Score = 100, Status = ApplicabilityStatus.Applicable, Title = "Корпоративная структура" },
                new() { SectionId = "ip", Score = 100, Status = ApplicabilityStatus.Applicable, Title = "Интеллектуальная собственность" }
            },
            Risks = new List<RiskFinding>()
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-healthy");

        Assert.Equal(100, ctx.Overall.Score);
        Assert.Empty(ctx.TopFindings);
        Assert.Empty(ctx.ActionPlan);
        Assert.False(ctx.FenixLaw.RequiresLegalWork);
        Assert.Contains("не выявлена", ctx.FenixLaw.SummaryText);
    }

    [Fact(DisplayName = "Regression B: Medium Scenario with Data & AI = N/A produces no Data/AI Fenix Law recommendations")]
    public void MediumScenario_NaDataModule_Produces_No_Data_FenixLaw_Recommendation()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "registered";

        var result = new ScoreResult
        {
            Overall = 65,
            Level = LegalScoreLevel.Attention,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 50, Status = ApplicabilityStatus.Applicable, Title = "Основатели" },
                new() { SectionId = "data", Score = null, Status = ApplicabilityStatus.NotApplicable, Title = "Данные и ИИ" }
            },
            Risks = new List<RiskFinding>
            {
                new()
                {
                    Code = "FND_DEADLOCK_RISK",
                    SectionId = "founders",
                    Severity = RiskSeverity.Critical,
                    Priority = RiskPriority.Now,
                    Title = "Риск тупика 50/50",
                    Recommendation = "Закрепить правила разрешения deadlock"
                }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-medium");

        Assert.True(ctx.FenixLaw.RequiresLegalWork);
        Assert.DoesNotContain(ctx.FenixLaw.ServiceCards, c => c.Title.Contains("Данные", StringComparison.OrdinalIgnoreCase) || c.Title.Contains("ИИ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "Regression C: Registered Entity never receives pre-incorporation language")]
    public void RegisteredEntity_Never_Receives_PreIncorporation_Language()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "registered";

        var result = new ScoreResult
        {
            Overall = 45,
            Level = LegalScoreLevel.MaterialGaps,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "ip", Score = 40, Status = ApplicabilityStatus.Applicable, Title = "Интеллектуальная собственность" }
            },
            Risks = new List<RiskFinding>
            {
                new()
                {
                    Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED",
                    SectionId = "ip",
                    Severity = RiskSeverity.Critical,
                    Priority = RiskPriority.Now,
                    Title = "Права на код не переданы",
                    Recommendation = "Оформить IP Assignment в компанию"
                }
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-registered");

        foreach (var action in ctx.ActionPlan)
        {
            Assert.DoesNotContain("в создаваемую компанию", action.WhatToDo, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("в создаваемую компанию", action.ExpectedResult, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact(DisplayName = "Regression D: Severe Scenario ActionPlan merges redundant workstreams without losing findings")]
    public void SevereScenario_Merges_Redundant_Workstreams_With_Zero_Uncovered()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "not_incorporated";

        var risks = new List<RiskFinding>
        {
            new() { Code = "FND_DEADLOCK_RISK", SectionId = "founders", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Title = "Тупик 50/50" },
            new() { Code = "FND_GOVERNANCE_GAP", SectionId = "founders", Severity = RiskSeverity.High, Priority = RiskPriority.ThirtyDays, Title = "Пробелы корпоративного договора" },
            new() { Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", SectionId = "ip", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Title = "Права фаундеров не переданы" },
            new() { Code = "IP_CONTRACTOR_RIGHTS_GAP", SectionId = "ip", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Title = "Права подрядчиков не переданы" },
            new() { Code = "TEAM_NO_WRITTEN_CONTRACTS", SectionId = "team", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now, Title = "Нет договоров с командой" },
            new() { Code = "AI_SENSITIVE_DATA_TRANSFER", SectionId = "data", Severity = RiskSeverity.High, Priority = RiskPriority.Now, Title = "Передача данных в ИИ" }
        };

        var result = new ScoreResult
        {
            Overall = 32,
            Level = LegalScoreLevel.StructuralRisks,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 30, Status = ApplicabilityStatus.Applicable, Title = "Основатели" },
                new() { SectionId = "ip", Score = 25, Status = ApplicabilityStatus.Applicable, Title = "Интеллектуальная собственность" },
                new() { SectionId = "team", Score = 35, Status = ApplicabilityStatus.Applicable, Title = "Команда" },
                new() { SectionId = "data", Score = 40, Status = ApplicabilityStatus.Applicable, Title = "Данные и ИИ" }
            },
            Risks = risks
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-severe");

        var allCoveredCodes = ctx.ActionPlan.SelectMany(a => a.CoveredFindingCodes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in risks)
        {
            Assert.Contains(r.Code, allCoveredCodes);
        }

        // Action count should be coherent (< 10 distinct workstreams)
        Assert.InRange(ctx.ActionPlan.Count, 4, 8);
    }

    [Fact(DisplayName = "Investment Readiness: 2-Layer Model captures Cross-Module Blockers even when Base Score is High")]
    public void InvestmentReadiness_TwoLayer_Model_Captures_CrossModule_Blockers()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "registered";

        var result = new ScoreResult
        {
            Overall = 65,
            Level = LegalScoreLevel.Attention,
            Sections = new List<SectionScore>
            {
                new() { SectionId = "founders", Score = 30, Status = ApplicabilityStatus.Applicable, Title = "Основатели" },
                new() { SectionId = "investment", Score = 85, Status = ApplicabilityStatus.Applicable, Title = "Готовность к инвестициям" }
            },
            Risks = new List<RiskFinding>
            {
                new()
                {
                    Code = "FND_DEADLOCK_RISK",
                    SectionId = "founders",
                    Severity = RiskSeverity.Blocker,
                    Priority = RiskPriority.Now,
                    Title = "Корпоративный тупик 50/50",
                    WhyItMatters = "Блокирует принятие решений по раунду"
                }
            },
            InvestmentReadiness = new InvestmentReadinessOverlay
            {
                Applicable = true,
                ReadinessScore = 85,
                Blockers = new List<string>()
            }
        };

        var ctx = ReportEngine.AssembleReportContext(result, facts, "test-inv-blockers");

        Assert.NotNull(ctx.InvestmentReadiness);
        Assert.True(ctx.InvestmentReadiness.IsApplicable);
        Assert.Equal(85, ctx.InvestmentReadiness.BaseScore);
        Assert.True(ctx.InvestmentReadiness.HasCrossModuleBlockers);
        Assert.Single(ctx.InvestmentReadiness.CrossModuleBlockers);
        Assert.Equal("FND_DEADLOCK_RISK", ctx.InvestmentReadiness.CrossModuleBlockers[0].FindingCode);
        Assert.True(ctx.InvestmentReadiness.ReadinessScore <= 45, "Readiness score must be constrained by cross-module blocker");
    }
}
