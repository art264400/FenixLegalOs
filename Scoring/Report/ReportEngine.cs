using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Investment;

namespace FenixLegalOs.Scoring.Report;

public static class ReportEngine
{
    public static ReportContext AssembleReportContext(
        ScoreResult result,
        SharedFactStore facts,
        string sessionId,
        string? projectName = null)
    {
        var ctx = new ReportContext
        {
            SessionId = sessionId,
            ReportNumber = $"SLS-{DateTime.UtcNow:yyMMdd}-{sessionId[..Math.Min(6, sessionId.Length)].ToUpperInvariant()}",
            ProjectName = string.IsNullOrWhiteSpace(projectName) ? "Проект" : projectName
        };

        // 1. Profile
        ctx.Profile = ProjectProfileExtractor.ExtractProfile(facts, ctx.ProjectName);
        ctx.ProjectStage = ctx.Profile.KeyFacts.FirstOrDefault(f => f.Key == "stage")?.Value ?? "Не указано";

        // 2. Overall Score & Scale
        var bandTitle = result.Overall >= 80 ? "Хорошая готовность"
            : result.Overall >= 60 ? "Требует внимания"
            : result.Overall >= 40 ? "Существенные пробелы"
            : "Критические пробелы";

        var bandText = result.Overall >= 80 ? "Базовая юридическая конструкция и ключевые договоренности оформлены на высоком уровне."
            : result.Overall >= 60 ? "Базовые элементы структуры присутствуют, но есть отдельные уязвимые зоны."
            : result.Overall >= 40 ? "Обнаружены пробелы в защите прав или оформлении структуры, создающие уязвимости для бизнеса."
            : "Критическая юридическая уязвимость, требующая первоочередного вмешательства.";

        // Calculate top score drivers (e.g. lowest scoring applicable sections)
        var lowestSections = result.Sections
            .Where(s => s.Status == ApplicabilityStatus.Applicable && s.Score.HasValue)
            .OrderBy(s => s.Score!.Value)
            .Take(3)
            .Select(s => s.Title)
            .ToList();

        string driversExplanation;
        if (lowestSections.Count == 1)
        {
            driversExplanation = $"На итоговую оценку сильнее всего повлияло направление «{lowestSections[0]}».";
        }
        else if (lowestSections.Count == 2)
        {
            driversExplanation = $"На итоговую оценку сильнее всего повлияли направления «{lowestSections[0]}» и «{lowestSections[1]}».";
        }
        else if (lowestSections.Count >= 3)
        {
            var initial = string.Join(", ", lowestSections.Take(lowestSections.Count - 1).Select(t => $"«{t}»"));
            driversExplanation = $"На итоговую оценку сильнее всего повлияли направления {initial} и «{lowestSections.Last()}».";
        }
        else
        {
            driversExplanation = "Оценка сформирована по всем применимым направлениям.";
        }

        // Data completeness: proportion of reachable diagnostic questions answered
        int completeness = result.Confidence > 0 ? result.Confidence : result.AnsweredCount > 0 ? 100 : 0;

        string? confExplanation = null;
        if (completeness < 100 && result.UnknownMaterialFacts.Count > 0)
        {
            confExplanation = !string.IsNullOrWhiteSpace(result.ConfidenceExplanation)
                ? result.ConfidenceExplanation
                : ConfidenceCalculator.GetConfidenceExplanation(completeness, result.UnknownMaterialSections);
        }

        ctx.Overall = new OverallScoreDto
        {
            Score = result.Overall,
            Band = result.Level.ToString(),
            LevelTitle = bandTitle,
            LevelText = bandText,
            Confidence = completeness,
            ConfidenceText = result.ConfidenceText,
            ConfidenceExplanation = confExplanation,
            UnknownMaterialFacts = result.UnknownMaterialFacts,
            TopDrivers = lowestSections,
            BottomExplanation = driversExplanation
        };

        // 3. Top Root Causes (Section 04) - Lossless all Critical/Blocker + grouped High

        // 4. Positive Factors (Section 05)
        ctx.PositiveFactors = FactorBreakdownEvaluator.ExtractGlobalPositiveFactors(result);

        // New-round plans and existing investor obligations are separate contexts.
        var investmentTiming = facts.Facts.GetValueOrDefault("investment.timing")?.ToString();
        var priorInvestment = facts.Facts.GetValueOrDefault("investment.priorInvestment");
        ctx.InvestmentReadiness = new InvestmentReadinessReportDto
        {
            InvestmentTiming = investmentTiming,
            PriorInvestmentStatus = priorInvestment?.ToString()?.ToLowerInvariant(),
            NewRoundPlanned = investmentTiming is "6_12m" or "3_6m" or "active_search" or "specific_investor" or "terms_received",
            NewRoundPossible = investmentTiming == "within_12m",
            RoundIsCloseOrActive = InvestmentTimingClassifier.IsCloseOrActiveRound(facts),
            HasPriorInvestment = priorInvestment is true || priorInvestment?.ToString() == "unknown"
        };
        var reportFindings = result.Risks.Select(r => CopyFindingForReport(r, ctx.InvestmentReadiness.NewRoundPlanned)).ToList();
        ctx.AllFindings = reportFindings;
        ctx.TopFindings = RootCauseMerger.ExtractTopRootCauses(reportFindings, maxCount: 8);

        // 6. 8-Zone Classification & Detail (Section 06, 07, 08, 09)
        var renderModes = RenderModeClassifier.ClassifyRenderModes(result);
        var isUnincorporated = !facts.Facts.TryGetValue("company.entityStatus", out var esObj) || esObj?.ToString() is "none" or "not_incorporated" or "no_entity" or "";

        int orderIdx = 1;
        foreach (var s in result.Sections)
        {
            var mode = renderModes.GetValueOrDefault(s.SectionId, ReportRenderMode.Compact);
            var sectionRisks = reportFindings.Where(r => r.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            var maxSev = sectionRisks.Count > 0 ? sectionRisks.Max(r => r.Severity) : (RiskSeverity?)null;

            var maxSevLabel = maxSev switch
            {
                RiskSeverity.Blocker => "Блокирующий",
                RiskSeverity.Critical => "Критический",
                RiskSeverity.High => "Высокий",
                RiskSeverity.Medium => "Умеренный",
                _ => "Не выявлен"
            };

            var scoreVal = s.Score ?? 0;
            var statusText = s.Status != ApplicabilityStatus.Applicable ? "Не применимо"
                : scoreVal >= 80 ? "Устойчиво"
                : scoreVal >= 60 ? "Требует внимания"
                : scoreVal >= 40 ? "Существенные пробелы"
                : "Критические пробелы";

            var hasContractors = facts.Facts.TryGetValue("ip.creators", out var cVal) &&
                                 (cVal?.ToString()?.Contains("contractor") == true || cVal?.ToString()?.Contains("studio") == true || cVal?.ToString()?.Contains("both") == true);
            var hasCorporateRisk = sectionRisks.Any(r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");

            var reasonIfNa = s.SectionId switch
            {
                "corporate" => hasCorporateRisk
                    ? "Юридическое лицо пока не зарегистрировано (детальная корпоративная структура еще не создана). При этом ведение фактической деятельности без юрлица формирует отдельный риск."
                    : "Юридическое лицо пока не зарегистрировано.",
                "contracts" => "Договорные отношения с клиентами и подрядчиками пока не ведутся.",
                "team" => hasContractors
                    ? "Штатные сотрудники и постоянная команда пока не привлекались (привлекаются разовые подрядчики/фрилансеры)."
                    : "Постоянная команда и сотрудники пока не привлекались.",
                "data" => "Обработка персональных данных и AI пока не осуществляется.",
                "investment" => investmentTiming == "none"
                    ? "Привлечение инвестиций в настоящий момент не планируется."
                    : "Планы привлечения инвестиций не указаны.",
                _ => "Раздел не применим к текущей конфигурации проекта."
            };

            var triggerIfNa = s.SectionId switch
            {
                "corporate" => "Станет применимым после начала регистрации юридического лица.",
                "contracts" => "Станет применимым при заключении первых коммерческих контрактов.",
                "team" => "Станет применимым при найме штатных сотрудников или постоянных подрядчиков.",
                "data" => "Станет применимым при начале сбора персональных данных или интеграции AI.",
                "investment" => "Станет применимым при появлении планов привлечения инвестиций или сведений об обязательствах перед прежними инвесторами.",
                _ => "Станет применимым при изменении параметров деятельности."
            };

            // Add to 8-Zone Grid (Applicable module MUST have a valid score)
            ctx.ModuleCards.Add(new ModuleCardDto
            {
                SectionId = s.SectionId,
                Order = orderIdx,
                Title = s.Title,
                Score = s.Status == ApplicabilityStatus.Applicable ? scoreVal : null,
                MaxSeverity = maxSev,
                MaxSeverityLabel = maxSevLabel,
                RenderMode = mode,
                StatusText = statusText,
                ReasonIfNa = s.Status != ApplicabilityStatus.Applicable ? reasonIfNa : null,
                TriggerIfNa = s.Status != ApplicabilityStatus.Applicable ? triggerIfNa : null,
                Icon = GetSectionIcon(s.SectionId)
            });

            // If Focus Mode
            if (mode == ReportRenderMode.Focus)
            {
                var (neg, att, pos, table) = FactorBreakdownEvaluator.EvaluateSectionDriversAndTable(s, sectionRisks);
                if (scoreVal < 40 && neg.Count == 0 && sectionRisks.Count == 0 && table.Count == 0)
                {
                    neg.Add($"Низкий уровень юридической готовности по направлению «{s.Title}».");
                }

                var findingCards = sectionRisks.Select(r =>
                {
                    var resolvingAction = Data.ActionLibrary.ActionLibrary.ResolveActionForFinding(r);
                    var effectiveMode = resolvingAction?.ResolutionMode ?? r.ResolutionMode;
                    r.ResolutionMode = effectiveMode;

                    return new ReportFindingCardDto
                    {
                        FindingCode = r.Code,
                        Title = r.Title,
                        Severity = r.Severity,
                        SeverityLabel = r.Severity switch
                        {
                            RiskSeverity.Blocker => "Блокирующий",
                            RiskSeverity.Critical => "Критический",
                            RiskSeverity.High => "Высокий",
                            RiskSeverity.Medium => "Умеренный",
                            _ => "Низкий"
                        },
                        WhyFound = !string.IsNullOrWhiteSpace(r.Finding) ? r.Finding : r.Title,
                        WhyItMatters = InvestmentNarrativePresentation.GetWhyItMatters(r.Code, r.WhyItMatters, ctx.InvestmentReadiness.NewRoundPlanned),
                        Recommendation = InvestmentNarrativePresentation.GetRecommendation(r.Code, r.Recommendation, ctx.InvestmentReadiness.NewRoundPlanned),
                        Recommendations = InvestmentNarrativePresentation.GetRecommendations(r.Code,
                            r.Recommendations != null && r.Recommendations.Count > 0
                                ? r.Recommendations
                                : (!string.IsNullOrWhiteSpace(r.Recommendation) ? new List<string> { r.Recommendation } : new List<string>()),
                            ctx.InvestmentReadiness.NewRoundPlanned),
                        Priority = r.Priority,
                        PriorityLabel = r.Priority switch
                        {
                            RiskPriority.Now => "В первую очередь",
                            RiskPriority.ThirtyDays => "В течение 30 дней",
                            RiskPriority.BeforeRound => ctx.InvestmentReadiness.NewRoundPlanned ? "До раунда / сделки" : "Плановое улучшение",
                            _ => "Плановое улучшение"
                        },
                        ResolutionMode = effectiveMode,
                        ResolutionFormat = effectiveMode switch
                        {
                            ResolutionMode.InternalAction => "Внутреннее действие команды",
                            ResolutionMode.LegalReview => "Юридическая проверка",
                            ResolutionMode.LegalWork => "Требуется юридическая работа",
                            ResolutionMode.LegalAndProduct => "Юридическая и техническая доработка",
                            _ => "Требуется юридическая работа"
                        }
                    };
                }).ToList();

                ctx.FocusModules.Add(new FocusModuleDetailDto
                {
                    SectionId = s.SectionId,
                    Order = orderIdx,
                    Title = s.Title,
                    Score = scoreVal,
                    ScoreBand = statusText,
                    MaxSeverity = maxSev ?? RiskSeverity.Medium,
                    MaxSeverityLabel = maxSevLabel,
                    SubtitleNarrative = $"Направление «{s.Title}» требует первоочередного внимания: выявлены уязвимости, влияющие на общую оценку готовности компании.",
                    PracticalMeaning = GetSectionPracticalMeaning(s.SectionId, isUnincorporated, ctx.InvestmentReadiness),
                    NegativeDrivers = neg,
                    AttentionDrivers = att,
                    PositiveDrivers = pos,
                    FactorBreakdown = table,
                    Findings = findingCards
                });
            }
            // If Compact Mode (Exclude investment when dedicated investment section is rendered)
            else if (mode == ReportRenderMode.Compact && !s.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase))
            {
                var (neg, att, pos, _) = FactorBreakdownEvaluator.EvaluateSectionDriversAndTable(s, sectionRisks);

                var compSummary = scoreVal switch
                {
                    >= 80 => $"Базовые договоренности в направлении «{s.Title}» соблюдаются, существенных блокеров не выявлено.",
                    >= 60 => $"Направление «{s.Title}» требует внимания и точечной доработки отдельных условий.",
                    >= 40 => $"В направлении «{s.Title}» обнаружены существенные пробелы в оформлении, создающие повышенные риски.",
                    _ => $"В направлении «{s.Title}» выявлены критические уязвимости, требующие первоочередного вмешательства."
                };

                var nextStep = sectionRisks.Count > 0
                    ? (!string.IsNullOrWhiteSpace(sectionRisks.OrderByDescending(r => r.Severity).First().Recommendation) 
                        ? sectionRisks.OrderByDescending(r => r.Severity).First().Recommendation 
                        : $"Систематизировать подтверждающие документы по направлению «{s.Title}».")
                    : $"Поддерживать актуальность документов и регламентов по направлению «{s.Title}».";

                ctx.CompactModules.Add(new CompactModuleDto
                {
                    SectionId = s.SectionId,
                    Order = orderIdx,
                    Title = s.Title,
                    Score = scoreVal,
                    StatusText = statusText,
                    Summary = compSummary,
                    NegativePoints = neg.Concat(att).Take(2).ToList(),
                    PositivePoints = pos.Take(2).ToList(),
                    NextStep = nextStep
                });
            }
            // If N/A Mode
            else if (mode == ReportRenderMode.NotApplicable)
            {
                var fullReason = string.IsNullOrWhiteSpace(reasonIfNa)
                    ? "Раздел не применим на текущем этапе развития компании."
                    : reasonIfNa;
                if (!fullReason.Contains("не влияет на результат юридической оценки", StringComparison.OrdinalIgnoreCase))
                {
                    fullReason = $"{fullReason.TrimEnd('.')} (не влияет на результат юридической оценки).";
                }

                ctx.NotApplicableModules.Add(new NotApplicableModuleDto
                {
                    SectionId = s.SectionId,
                    Order = orderIdx,
                    Title = s.Title,
                    ReasonText = fullReason,
                    TriggerEventText = triggerIfNa
                });
            }

            orderIdx++;
        }

        // Populate vNext Collections
        ctx.RootCauses = result.Risks
            .GroupBy(r => !string.IsNullOrWhiteSpace(r.RootCauseGroup) ? r.RootCauseGroup : "GENERAL", StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var maxSev = g.Max(r => r.Severity);
                var minPrio = g.Min(r => r.Priority);
                var topR = g.OrderByDescending(r => r.Severity).First();
                return new RootCauseSummaryDto
                {
                    Code = g.Key,
                    Title = topR.Title,
                    SectionId = topR.SectionId,
                    MaxSeverity = maxSev,
                    Priority = minPrio,
                    FindingCodes = g.Select(r => r.Code).Distinct().ToList(),
                    Description = topR.Finding
                };
            })
            .OrderByDescending(rc => rc.MaxSeverity)
            .ThenBy(rc => rc.Priority)
            .ToList();

        // 7. Investment Readiness: present the canonical scoring result without a second cap.
        var invSec = result.Sections.FirstOrDefault(s => s.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase));
        var investment = ctx.InvestmentReadiness;
        var canonicalReadiness = result.InvestmentReadiness;
        var isInvApplicable = invSec?.Status == ApplicabilityStatus.Applicable && canonicalReadiness.Applicable;
        int baseScore = invSec?.Score ?? 0;
        int readinessScore = isInvApplicable ? canonicalReadiness.ReadinessScore ?? baseScore : 0;
        var baseCategory = GetInvestmentReadinessCategory(baseScore);
        var readinessCategory = GetInvestmentReadinessCategory(readinessScore);

        // Only blockers identified by the canonical calculator belong in round readiness.
        // Past investments alone do not turn ordinary operational risks into DD blockers.
        var allBlockerTitles = isInvApplicable && investment.RoundIsCloseOrActive
            ? canonicalReadiness.Blockers.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();
        var canonicalBlockerTitles = allBlockerTitles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockerFindings = result.Risks
            .Where(r => canonicalBlockerTitles.Contains(r.Title.Trim()))
            .ToList();
        var crossBlockers = blockerFindings
            .Where(r => !r.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase))
            .Select(r => new CrossModuleInvestmentBlockerDto
            {
                ModuleTitle = result.Sections.FirstOrDefault(s => s.SectionId.Equals(r.SectionId, StringComparison.OrdinalIgnoreCase))?.Title ?? r.SectionId,
                SectionId = r.SectionId,
                FindingCode = r.Code,
                Title = r.Title,
                Severity = r.Severity,
                WhyItBlocksDueDiligence = !string.IsNullOrWhiteSpace(r.WhyItMatters) ? r.WhyItMatters : r.Finding
            })
            .ToList();
        var unresolvedBlockersCount = blockerFindings
            .Select(r => r.RootCauseGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        string roundVerdict;
        string summaryDesc;
        if (!isInvApplicable)
        {
            roundVerdict = investmentTiming == "none"
                ? "Новый инвестиционный раунд не планируется."
                : "Планы нового инвестиционного раунда не указаны.";
            summaryDesc = "Инвестиционный блок не применяется к текущей диагностике и не влияет на общую юридическую оценку.";
        }
        else if (!investment.NewRoundPlanned)
        {
            roundVerdict = investment.NewRoundPossible
                ? "Привлечение инвестиций рассматривается как возможность, новый раунд пока не запланирован."
                : "Новый инвестиционный раунд не заявлен; оценены сведения о прошлых инвестициях.";
            summaryDesc = investment.PriorInvestmentStatus == "unknown"
                ? $"Оценка инвестиционного блока: {readinessScore}/100 ({readinessCategory}). Наличие или условия прошлых инвестиций не определены; их следует уточнить. Новый раунд не заявлен как запланированный."
                : investment.HasPriorInvestment
                ? $"Оценка оформления инвестиционных отношений: {readinessScore}/100 ({readinessCategory}). Учитываются документы и обязательства перед прежними инвесторами. Блокеры нового раунда не определяются, поскольку он не запланирован."
                : $"Базовая готовность инвестиционного блока: {readinessScore}/100 ({readinessCategory}). Возможность будущего привлечения не означает, что раунд уже начался; блокеры активной сделки не определяются.";
        }
        else if (unresolvedBlockersCount > 0)
        {
            roundVerdict = "Подготовка к заявленному раунду требует устранения выявленных юридических блокеров.";
            summaryDesc = $"Базовая оценка инвестиционных документов: {baseScore}/100. С учетом выявленных блокеров оценка готовности к заявленному раунду составляет {readinessScore}/100 ({readinessCategory}). Она совпадает с результатом расчета диагностики.";
        }
        else
        {
            roundVerdict = $"Готовность к заявленному раунду: {readinessCategory.ToLowerInvariant()}.";
            summaryDesc = $"Оценка инвестиционной готовности: {readinessScore}/100 ({readinessCategory}). Учитываются заявленные сроки и ответы об инвестиционных документах. Блокеры близкого или активного раунда по правилам диагностики не выявлены.";
        }

        investment.IsApplicable = isInvApplicable;
        investment.ReadinessScore = readinessScore;
        investment.BaseScore = baseScore;
        investment.BaseCategory = isInvApplicable ? baseCategory : "Не применимо";
        investment.Category = isInvApplicable ? readinessCategory : "Не применимо";
        investment.RoundVerdict = roundVerdict;
        investment.UnresolvedBlockersCount = unresolvedBlockersCount;
        investment.BlockerTitles = allBlockerTitles;
        investment.CatalystTitles = isInvApplicable ? result.Strengths : new List<string>();
        investment.CrossModuleBlockers = crossBlockers;
        investment.SummaryDescription = summaryDesc;

        // 8. Unified Action Plan (Section 11)
        ctx.ActionPlan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(result.Risks, facts);

        // 9. Fenix Law Recommendation (Section 12)
        ctx.FenixLaw = FenixLawRecommendationEvaluator.EvaluateRecommendation(result.Risks, result, ctx.ActionPlan, ctx.InvestmentReadiness);

        // 10. Legal Terms & Methodology (Sections 13-14)
        ctx.LegalTerms = ReportStaticContent.GetLegalTerms();

        // Executive fallback consumes the fully assembled investment and operational context.
        ctx.ExecutiveConclusion = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx).ExecutiveConclusion;

        // Enforce Strict Deterministic Semantic Invariants (Fail-Closed)
        ReportContextValidator.Validate(ctx);
        SemanticContradictionValidator.Validate(result, facts, ctx);

        return ctx;
    }

    private static RiskFinding CopyFindingForReport(RiskFinding finding, bool newRoundPlanned) => new()
    {
        Code = finding.Code,
        RootCauseGroup = finding.RootCauseGroup,
        Severity = finding.Severity,
        Priority = finding.Priority,
        SectionId = finding.SectionId,
        Modules = finding.Modules.ToList(),
        Title = finding.Title,
        Finding = finding.Finding,
        WhyItMatters = InvestmentNarrativePresentation.GetWhyItMatters(finding.Code, finding.WhyItMatters, newRoundPlanned),
        Recommendations = InvestmentNarrativePresentation.GetRecommendations(finding.Code, finding.Recommendations, newRoundPlanned),
        Recommendation = InvestmentNarrativePresentation.GetRecommendation(finding.Code, finding.Recommendation, newRoundPlanned),
        Basis = finding.Basis.ToList(),
        LawyerRequired = finding.LawyerRequired,
        Resolution = finding.Resolution,
        ResolutionMode = finding.ResolutionMode,
        RecommendedActionId = finding.RecommendedActionId,
        Facts = finding.Facts.ToList(),
        BusinessImpact = finding.BusinessImpact.ToList(),
        ServiceCode = finding.ServiceCode,
        Cta = finding.Cta,
        AffectedDimensions = finding.AffectedDimensions.ToList()
    };

    private static string GetSectionIcon(string sectionId) => sectionId switch
    {
        "founders" => "users",
        "corporate" => "building",
        "ip" => "shield",
        "team" => "user_check",
        "product" => "rocket",
        "data" => "cpu",
        "contracts" => "file_text",
        "investment" => "coins",
        _ => "circle"
    };

    private static string GetInvestmentReadinessCategory(int score) => score >= 80
        ? "Высокая готовность"
        : score >= 60 ? "Умеренная готовность"
        : score >= 40 ? "Требуется подготовка"
        : "Критические пробелы";

    private static string GetSectionPracticalMeaning(string sectionId, bool isUnincorporated, InvestmentReadinessReportDto investment)
    {
        var operationalMeaning = sectionId switch
        {
            "founders" => "Пока договоренности и доли между основателями не зафиксированы юридически, любое разногласие грозит корпоративным тупиком (deadlock) и остановкой операционной деятельности. Разрешение конфликтов постфактум потребует дополнительных затрат и может затруднить управление компанией.",
            "corporate" => isUnincorporated
                ? "При коммерческой деятельности без зарегистрированного юридического лица обязательства приходится оформлять на основателей. Это затрудняет разграничение личной и деловой ответственности, заключение договоров и передачу активов компании."
                : "Пока корпоративная структура и регламенты принятия решений не выстроены, стратегические сделки и распределение прибыли остаются уязвимыми для оспаривания. Несогласованность структуры владения может затруднить управление и исполнение обязательств компании.",
            "ip" => isUnincorporated
                ? "Пока права на ключевые результаты находятся у создателей, при регистрации компании придется отдельно подтверждать историю разработки и оформлять недостающие документы о правах. Чем дольше продукт развивается в текущей конструкции, тем больше объем документов придется восстанавливать позднее."
                : "Пока исключительные права на продукт и кодовую базу не подтверждены договорами и актами, сохраняется риск претензий от разработчиков или подрядчиков. Пробелы в цепочке прав могут осложнить использование продукта, продажу лицензий и заключение договоров с клиентами.",
            "team" => "Пока отношения с разработчиками и ключевыми специалистами не закрыты договорами с передачей прав и NDA, созданный актив юридически остается за авторами, а переквалификация отношений грозит доначислениями налогов. При росте команды уход ключевого участника с наработками поставит бизнес под прямой удар.",
            "product" => "Пока работа с пользователями ведется без актуальной публичной оферты и правил сервиса, проект уязвим для потребительских исков, требований о возврате средств и ограничений со стороны платежных сервисов. По мере роста аудитории увеличиваются возможные затраты на урегулирование претензий и устранение нарушений.",
            "data" => "Пока процессы сбора персональных данных и интеграции с внешними сервисами или AI не обеспечены соглашениями и политиками, компания рискует получить предписания регулятора, ограничения работы сервиса или штрафы за утечки. Претензии пользователей и регуляторов могут нарушить работу продукта и потребовать дополнительных затрат.",
            "contracts" => "Пока договоры с клиентами и подрядчиками не содержат четких лимитов ответственности и условий расторжения, компания несет неконтролируемые финансовые риски по обязательствам контрагентов. При кассовых разрывах или спорах нехватка юридических рычагов приведет к потере выручки и длительным судебным издержкам.",
            "investment" => investment.NewRoundPlanned
                ? "Подготовка к заявленному раунду включает оформление инвестиционных документов и устранение подтвержденных юридических рисков. Несоответствия в документах могут осложнить юридическую проверку и согласование условий сделки."
                : investment.HasPriorInvestment
                    ? "Сведения о прошлых инвестициях и обязательствах перед инвесторами должны однозначно подтверждать права сторон. Неясные условия могут привести к спорам о долях, возврате средств и исполнении договоренностей."
                    : investment.NewRoundPossible
                        ? "Если компания решит привлекать инвестиции, потребуется подтвердить структуру долей, права на продукт и условия сделки. Сейчас инвестиции рассматриваются как возможность, а не как начавшийся раунд."
                        : "Планы нового инвестиционного раунда не заявлены. Оценка не предполагает, что компания готовится к привлечению инвестиций.",
            _ => "Пока правовая основа направления не формализована, компания накапливает скрытые юридические обязательства. При масштабировании или внешней проверке неурегулированные вопросы потребуют срочного устранения."
        };
        if (!investment.NewRoundPlanned || sectionId == "investment") return operationalMeaning;

        var fundraisingMeaning = sectionId switch
        {
            "founders" => " При подготовке к заявленному инвестиционному раунду потребуется подтвердить договоренности основателей и структуру долей.",
            "corporate" => " Для заявленного инвестиционного раунда необходимо подтвердить юридическую структуру и полномочия участников сделки.",
            "ip" => " Для заявленного раунда потребуется подтвердить непрерывную цепочку прав на продукт при юридической проверке инвестором.",
            "product" => " При подготовке к заявленному раунду выявленные проблемы взаимодействия с пользователями также потребуют раскрытия и урегулирования.",
            "data" => " При юридической проверке в рамках заявленного раунда потребуется подтвердить правовые основания обработки данных и использования внешних сервисов.",
            _ => string.Empty
        };
        return operationalMeaning + fundraisingMeaning;
    }
}
