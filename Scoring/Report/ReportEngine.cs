using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;

namespace FenixLegalOs.Scoring.Report;

public static class ReportEngine
{
    public static ReportContext AssembleReportContext(
        ScoreResult result,
        SharedFactStore facts,
        string sessionId,
        string? projectName = null)
    {
        var rawStage = (string?)facts.Facts.GetValueOrDefault("product.stage") ?? "";
        var stageDisplayName = rawStage switch
        {
            "idea" => "Идея",
            "prototype" => "Прототип",
            "mvp" or "first" or "live_or_ready" => "MVP / Первые пользователи",
            "commercial" or "scaling" or "regular" or "large" => "Коммерческий запуск",
            _ => "Ранняя стадия"
        };

        var ctx = new ReportContext
        {
            SessionId = sessionId,
            ReportNumber = $"SLS-{DateTime.UtcNow:yyMMdd}-{sessionId[..Math.Min(6, sessionId.Length)].ToUpperInvariant()}",
            ProjectName = string.IsNullOrWhiteSpace(projectName) ? "Проект" : projectName,
            ProjectStage = stageDisplayName
        };

        // 1. Profile
        ctx.Profile = ProjectProfileExtractor.ExtractProfile(facts, ctx.ProjectName);

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

        ctx.Overall = new OverallScoreDto
        {
            Score = result.Overall,
            Band = result.Level.ToString(),
            LevelTitle = bandTitle,
            LevelText = bandText,
            Confidence = completeness,
            ConfidenceText = result.ConfidenceText,
            TopDrivers = lowestSections,
            BottomExplanation = driversExplanation
        };

        // 3. Top Root Causes (Section 04) - Max 5
        ctx.TopFindings = RootCauseMerger.ExtractTopRootCauses(result.Risks, maxCount: 5);

        // 4. Positive Factors (Section 05)
        ctx.PositiveFactors = FactorBreakdownEvaluator.ExtractGlobalPositiveFactors(result);

        // 5. Executive Conclusion (Section 03)
        ctx.ExecutiveConclusion = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx).ExecutiveConclusion;

        // 6. 8-Zone Classification & Detail (Section 06, 07, 08, 09)
        var renderModes = RenderModeClassifier.ClassifyRenderModes(result);
        var isUnincorporated = !facts.Facts.TryGetValue("company.entityStatus", out var esObj) || esObj?.ToString() is "none" or "not_incorporated" or "no_entity" or "";

        int orderIdx = 1;
        foreach (var s in result.Sections)
        {
            var mode = renderModes.GetValueOrDefault(s.SectionId, ReportRenderMode.Compact);
            var sectionRisks = result.Risks.Where(r => r.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
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
                "investment" => "Привлечение инвестиций в настоящий момент не планируется.",
                _ => "Раздел не применим к текущей конфигурации проекта."
            };

            var triggerIfNa = s.SectionId switch
            {
                "corporate" => "Станет применимым после начала регистрации юридического лица.",
                "contracts" => "Станет применимым при заключении первых коммерческих контрактов.",
                "team" => "Станет применимым при найме штатных сотрудников или постоянных подрядчиков.",
                "data" => "Станет применимым при начале сбора персональных данных или интеграции AI.",
                "investment" => "Станет применимым при планировании выхода на инвестиционный раунд.",
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

                var findingCards = sectionRisks.Select(r => new ReportFindingCardDto
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
                    WhyItMatters = r.WhyItMatters,
                    Recommendation = r.Recommendation,
                    Priority = r.Priority,
                    PriorityLabel = r.Priority switch
                    {
                        RiskPriority.Now => "В первую очередь",
                        RiskPriority.ThirtyDays => "В течение 30 дней",
                        RiskPriority.BeforeRound => "До раунда / сделки",
                        _ => "Плановое улучшение"
                    },
                    ResolutionFormat = r.Resolution switch
                    {
                        ResolutionType.SelfService => "Можно исправить самостоятельно",
                        ResolutionType.CheckWithLawyer => "Желательно проверить с юристом",
                        ResolutionType.LawyerRequired => "Требуется юридическая работа",
                        _ => "Желательно проверить с юристом"
                    }
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
                    PracticalMeaning = GetSectionPracticalMeaning(s.SectionId, isUnincorporated),
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

                ctx.CompactModules.Add(new CompactModuleDto
                {
                    SectionId = s.SectionId,
                    Order = orderIdx,
                    Title = s.Title,
                    Score = scoreVal,
                    StatusText = statusText,
                    Summary = compSummary,
                    NegativePoints = neg.Concat(att).Take(2).ToList(),
                    PositivePoints = pos.Take(2).ToList()
                });
            }
            // If N/A Mode
            else if (mode == ReportRenderMode.NotApplicable)
            {
                ctx.NotApplicableModules.Add(new NotApplicableModuleDto
                {
                    SectionId = s.SectionId,
                    Order = orderIdx,
                    Title = s.Title,
                    ReasonText = reasonIfNa,
                    TriggerEventText = triggerIfNa
                });
            }

            orderIdx++;
        }

        // Populate vNext Collections
        ctx.AllFindings = result.Risks.ToList();
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

        // 7. Investment Readiness (2-Layer Architecture: Base + Cross-Module Blockers)
        var invSec = result.Sections.FirstOrDefault(s => s.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase));
        var isInvApplicable = invSec != null && invSec.Status == ApplicabilityStatus.Applicable;
        int baseScore = invSec?.Score ?? 0;
        var baseCategory = baseScore >= 80 ? "Высокая готовность" : baseScore >= 60 ? "Умеренная готовность" : baseScore >= 40 ? "Требуется подготовка" : "Критические блокеры";

        // Collect cross-module blockers from other sections
        var crossBlockers = result.Risks
            .Where(r => !r.SectionId.Equals("investment", StringComparison.OrdinalIgnoreCase) && 
                        (r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical || 
                         r.Code.Contains("DEADLOCK") || r.Code.Contains("DISPUTE") || r.Code.Contains("OWNERSHIP")))
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

        ctx.InvestmentReadiness = new InvestmentReadinessReportDto
        {
            IsApplicable = isInvApplicable,
            ReadinessScore = isInvApplicable ? (crossBlockers.Count > 0 ? Math.Min(baseScore, 45) : baseScore) : 0,
            BaseScore = baseScore,
            BaseCategory = isInvApplicable ? baseCategory : "Не применимо",
            Category = !isInvApplicable ? "Не применимо" : crossBlockers.Count > 0 ? "Сквозные юридические блокеры" : baseCategory,
            UnresolvedBlockersCount = crossBlockers.Count + (result.InvestmentReadiness?.Blockers.Count ?? 0),
            BlockerTitles = result.InvestmentReadiness?.Blockers ?? new List<string>(),
            CrossModuleBlockers = crossBlockers,
            SummaryDescription = !isInvApplicable 
                ? "Привлечение инвестиций не заявлено как активная цель текущего этапа."
                : crossBlockers.Count > 0 
                    ? $"Базовая готовность инвест-блока составляет {baseScore}/100, однако общая готовность к сделке ограничена {crossBlockers.Count} критическими блокерами в смежных направлениях (структура, права, договоренности)."
                    : $"Оценка инвестиционной готовности компании составляет {baseScore} / 100 ({baseCategory}). Критичных сквозных юридических блокеров не выявлено."
        };

        // 8. Unified Action Plan (Section 11)
        ctx.ActionPlan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(result.Risks, facts);

        // 9. Fenix Law Recommendation (Section 12)
        ctx.FenixLaw = FenixLawRecommendationEvaluator.EvaluateRecommendation(result.Risks, result, ctx.ActionPlan);

        // 10. Legal Terms & Methodology (Sections 13-14)
        ctx.LegalTerms = ReportStaticContent.GetLegalTerms();

        // Enforce Strict Deterministic Semantic Invariants (Fail-Closed)
        ReportContextValidator.Validate(ctx);
        SemanticContradictionValidator.Validate(result, facts, ctx);

        return ctx;
    }

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

    private static string GetSectionPracticalMeaning(string sectionId, bool isUnincorporated) => sectionId switch
    {
        "founders" => "Неопределенность в договоренностях между основателями создает риск корпоративного тупика (deadlock) при принятии стратегических решений.",
        "corporate" => isUnincorporated
            ? "Отсутствие зарегистрированного юридического лица при ведении коммерческой деятельности создает персональную ответственность основателей."
            : "Отсутствие надлежащей структуры ограничивает заключение договоров и создает персональную ответственность участников.",
        "ip" => isUnincorporated
            ? "Отсутствие письменных договоров с создателями о передаче прав создает риск утраты контроля над продуктом до создания компании."
            : "Отсутствие надлежащей передачи исключительных прав от разработчиков и подрядчиков ставит под угрозу права на продукт.",
        "team" => "Неоформленные трудовые и подрядные отношения создают риски претензий по созданным разработкам и налоговых доначислений.",
        "product" => "Работа с пользователями без выстроенной публичной оферты создает риски потребительских споров и регуляторных предписаний.",
        "data" => "Передача персональных данных или использование AI без правовой основы влечет риски блокировок сервиса и регуляторных штрафов.",
        "contracts" => "Несбалансированные договоры с контрагентами повышают финансовые риски и не защищают ключевую выручку компании.",
        "investment" => "Наличие правовых блокеров существенно затягивает юридическую проверку (Due Diligence) инвестором или приводит к отказу от сделки.",
        _ => "Выявленные правовые факторы требуют структурирования для защиты устойчивости бизнеса."
    };
}
