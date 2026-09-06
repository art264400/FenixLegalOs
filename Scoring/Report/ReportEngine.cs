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
        ctx.AllFindings = result.Risks.ToList();
        ctx.TopFindings = RootCauseMerger.ExtractTopRootCauses(result.Risks, maxCount: 8);

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
                        WhyItMatters = r.WhyItMatters,
                        Recommendation = r.Recommendation,
                        Recommendations = r.Recommendations != null && r.Recommendations.Count > 0
                            ? r.Recommendations
                            : (!string.IsNullOrWhiteSpace(r.Recommendation) ? new List<string> { r.Recommendation } : new List<string>()),
                        Priority = r.Priority,
                        PriorityLabel = r.Priority switch
                        {
                            RiskPriority.Now => "В первую очередь",
                            RiskPriority.ThirtyDays => "В течение 30 дней",
                            RiskPriority.BeforeRound => "До раунда / сделки",
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
                if (!fullReason.Contains("не влияет на общий Score", StringComparison.OrdinalIgnoreCase))
                {
                    fullReason = $"{fullReason.TrimEnd('.')} (не влияет на общий Score).";
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

        var allBlockerTitles = crossBlockers.Select(cb => $"{cb.ModuleTitle}: {cb.Title}").ToList();
        var canonicalBlockerTitles = crossBlockers
            .Select(cb => cb.Title.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (result.InvestmentReadiness?.Blockers != null && result.InvestmentReadiness.Blockers.Count > 0)
        {
            foreach (var blockerTitle in result.InvestmentReadiness.Blockers)
            {
                var canonicalTitle = blockerTitle.Trim();
                if (canonicalBlockerTitles.Add(canonicalTitle))
                {
                    allBlockerTitles.Add(canonicalTitle);
                }
            }
        }
        var displayedBlockerCount = allBlockerTitles.Count;
        var displayedBlockerPhrase = displayedBlockerCount == 1
            ? "1 критическим блокером или существенным риском"
            : $"{displayedBlockerCount} критическими блокерами и существенными рисками";

        var dealScore = isInvApplicable ? (crossBlockers.Count > 0 ? Math.Min(baseScore, 45) : baseScore) : 0;
        var dealCategory = !isInvApplicable 
            ? "Не применимо" 
            : crossBlockers.Count > 0 
                ? "Сквозные юридические блокеры" 
                : baseCategory;

        string roundVerdict;
        string summaryDesc;

        if (!isInvApplicable)
        {
            roundVerdict = "Привлечение инвестиций не заявлено как активная цель текущего этапа.";
            summaryDesc = "Привлечение инвестиций не заявлено как активная цель текущего этапа.";
        }
        else if (baseScore >= 75)
        {
            if (displayedBlockerCount > 0)
            {
                roundVerdict = "Общая готовность к раунду: требует устранения юридических блокеров перед закрытием сделки.";
                summaryDesc = $"Параметры раунда и инвест-документы проработаны хорошо ({baseScore}/100), однако закрытие сделки ограничено {displayedBlockerPhrase} (структура, права, договоренности). Итоговая проходимость проверки (Due Diligence): {dealScore}/100.";
            }
            else
            {
                roundVerdict = "Общая готовность к раунду: высокая, проект готов к юридической проверке и открытию Data Room.";
                summaryDesc = $"Оценка инвестиционной готовности компании составляет {baseScore} / 100 ({baseCategory}). Критичных сквозных юридических блокеров не выявлено. Итоговая проходимость проверки (Due Diligence): {dealScore}/100.";
            }
        }
        else if (baseScore >= 50)
        {
            if (displayedBlockerCount > 0)
            {
                roundVerdict = "Общая готовность к раунду: требует доработки документов и устранения рисков в смежных зонах.";
                summaryDesc = $"Параметры раунда и инвест-документы проработаны частично ({baseScore}/100), при этом закрытие сделки осложнено {displayedBlockerPhrase} (структура, права, обязательства). Итоговая проходимость проверки (Due Diligence): {dealScore}/100.";
            }
            else
            {
                roundVerdict = "Общая готовность к раунду: средняя, требуется доработка инвестиционных документов.";
                summaryDesc = $"Базовая готовность инвестиционного блока составляет {baseScore} / 100 ({baseCategory}). Сквозных блокеров из других зон не выявлено, однако инвест-пакет требует структурирования перед открытием Data Room.";
            }
        }
        else
        {
            if (displayedBlockerCount > 0)
            {
                roundVerdict = "Общая готовность к раунду: критические риски в инвестиционном блоке и смежных зонах компании.";
                summaryDesc = $"Собственная готовность инвестиционного модуля находится на критическом уровне ({baseScore}/100): не сформированы базовые документы и параметры раунда. Кроме того, закрытие сделки блокируется {displayedBlockerPhrase}. Итоговая проходимость проверки (Due Diligence): {dealScore}/100.";
            }
            else
            {
                roundVerdict = "Общая готовность к раунду: не готов, требуется фундаментальная подготовка инвест-пакета.";
                summaryDesc = $"Собственная готовность инвестиционного модуля находится на критическом уровне ({baseScore}/100): отсутствуют ключевые инвестиционные документы и параметры раунда.";
            }
        }

        ctx.InvestmentReadiness = new InvestmentReadinessReportDto
        {
            IsApplicable = isInvApplicable,
            ReadinessScore = dealScore,
            BaseScore = baseScore,
            BaseCategory = isInvApplicable ? baseCategory : "Не применимо",
            Category = dealCategory,
            RoundVerdict = roundVerdict,
            UnresolvedBlockersCount = allBlockerTitles.Count,
            BlockerTitles = allBlockerTitles,
            CatalystTitles = result.InvestmentReadiness?.Blockers != null ? result.Strengths : new List<string>(),
            CrossModuleBlockers = crossBlockers,
            SummaryDescription = summaryDesc
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
        "founders" => "Пока договоренности и доли между основателями не зафиксированы юридически, любое разногласие грозит корпоративным тупиком (deadlock) и остановкой операционной деятельности. При входе инвестора отсутствие фаундерского соглашения станет прямым препятствием к раунду, а разрешение конфликтов постфактум обойдется несоизмеримо дороже.",
        "corporate" => isUnincorporated
            ? "Пока коммерческая деятельность ведется без регистрации юридического лица, основатели несут персональную ответственность всем своим имуществом по любым обязательствам. Выручка и база пользователей не капитализируются в стоимость бизнеса, а привлечение инвестиций заблокировано до создания компании."
            : "Пока корпоративная структура и регламенты принятия решений не выстроены, любые стратегические сделки и распределение прибыли остаются уязвимыми для оспаривания. На этапе Due Diligence несогласованность в структуре владения затянет инвестиционный раунд и потребует экстренной перерегистрации.",
        "ip" => isUnincorporated
            ? "Пока права на ключевые результаты находятся у создателей, при инкорпорации и привлечении инвестора придется отдельно подтверждать историю разработки и оформлять недостающие документы о правах. Чем дольше продукт развивается в текущей конструкции, тем больше объем документов придется восстанавливать позднее."
            : "Пока исключительные права на продукт и кодовую базу не консолидированы на балансе компании по договорам и актам, сохраняется риск претензий от разработчиков или подрядчиков. При юридической проверке инвестором отсутствие сквозной цепочки прав приведет к требованию экстренно подписывать документы с авторами на их условиях либо сорвет раунд.",
        "team" => "Пока отношения с разработчиками и ключевыми специалистами не закрыты договорами с передачей прав и NDA, созданный актив юридически остается за авторами, а переквалификация отношений грозит доначислениями налогов. При росте команды уход любого ключевого участника с наработками поставит бизнес под прямой удар.",
        "product" => "Пока работа с пользователями ведется без актуальной публичной оферты и правил сервиса, проект уязвим для потребительских исков, требований о возврате средств и блокировок эквайрингом. По мере роста аудитории цена каждого неурегулированного инцидента возрастает кратно, создавая регуляторные риски до первого раунда.",
        "data" => "Пока процессы сбора персональных данных и интеграции с внешними сервисами или AI не обвязаны соглашениями и политиками, компания рискует получить предписания регулятора, блокировку домена или штрафы за утечки. Инвесторы оценивают чистоту данных в первую очередь, так как претензии регуляторов могут парализовать работу сервиса.",
        "contracts" => "Пока договоры с клиентами и подрядчиками не содержат четких лимитов ответственности и условий расторжения, компания несет неконтролируемые финансовые риски по обязательствам контрагентов. При кассовых разрывах или спорах нехватка юридических рычагов приведет к потере выручки и длительным судебным издержкам.",
        "investment" => "Пока не сформирован базовый юридический инвест-пакет и не сняты блокеры в смежных зонах, компания теряет переговорную позицию перед инвесторами. Каждое несоответствие в документах затягивает Due Diligence, увеличивает дисконт к оценке бизнеса или дает инвестору повод пересмотреть условия сделки в неблагоприятную сторону.",
        _ => "Пока правовая основа направления не формализована, компания накапливает скрытые юридические обязательства. При масштабировании или внешнем аудите неурегулированные вопросы потребуют срочного устранения на невыгодных условиях."
    };
}
