using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class FenixLawRecommendationEvaluator
{
    public static FenixLawRecommendationReportDto EvaluateRecommendation(
        List<RiskFinding> findings, 
        ScoreResult result,
        List<UnifiedActionItemDto>? actionPlan = null)
    {
        // 1. Determine legal work requirement from ActionPlan (or findings fallback)
        List<UnifiedActionItemDto> legalActions;
        if (actionPlan != null)
        {
            legalActions = actionPlan
                .Where(a => a.ResolutionMode is ResolutionMode.LegalWork or ResolutionMode.LegalReview or ResolutionMode.LegalAndProduct)
                .ToList();
        }
        else
        {
            var legalFindingCodes = findings
                .Where(f => f.Severity is RiskSeverity.Critical or RiskSeverity.Blocker or RiskSeverity.High || f.LawyerRequired || f.Resolution == ResolutionType.LawyerRequired)
                .Select(f => f.Code)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            legalActions = findings
                .Where(f => legalFindingCodes.Contains(f.Code))
                .Select(f => new UnifiedActionItemDto
                {
                    ActionId = f.Code,
                    Title = f.Title,
                    ResolutionMode = ResolutionMode.LegalWork,
                    CoveredFindingCodes = new() { f.Code }
                })
                .ToList();
        }

        bool requiresLegalWork = legalActions.Count > 0;

        if (!requiresLegalWork)
        {
            return new FenixLawRecommendationReportDto
            {
                RequiresLegalWork = false,
                SummaryText = "Большинство выявленных вопросов можно устранить самостоятельно или внутри команды. Необходимость в отдельном юридическом сопровождении по результатам текущего скрининга не выявлена.",
                ServiceAreas = new List<string>(),
                ServiceCards = new List<FenixLawServiceCardDto>(),
                CtaTitle = "Сохранить отчет и план действий",
                CtaSubtext = "Используйте план действий для планомерного внедрения шаблонов и регламентов."
            };
        }

        // 2. Map every legal action to its Service Area and gather multi-signal prioritization metrics
        var serviceDomainData = new Dictionary<string, (string Title, string Description, string Icon, int MaxSevWeight, int MaxPrioWeight, bool HasInvBlocker, int Deficit)>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in legalActions)
        {
            var coveredFindings = findings.Where(f => action.CoveredFindingCodes.Contains(f.Code, StringComparer.OrdinalIgnoreCase)).ToList();
            
            // Determine section ID
            string secId = "";
            if (coveredFindings.Count > 0)
            {
                secId = coveredFindings.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f.SectionId))?.SectionId ?? "";
            }
            if (string.IsNullOrWhiteSpace(secId))
            {
                var actDef = Data.ActionLibrary.ActionLibrary.GetById(action.ActionId);
                secId = actDef?.SectionId ?? "";
            }
            if (string.IsNullOrWhiteSpace(secId))
            {
                secId = "corporate"; // Safe legal fallback
            }

            // Check applicability: never produce service cards for N/A sections
            var secScore = result.Sections.FirstOrDefault(s => s.SectionId.Equals(secId, StringComparison.OrdinalIgnoreCase));
            if (secScore != null && secScore.Status == ApplicabilityStatus.NotApplicable)
            {
                continue;
            }

            // Map Section to canonical Fenix Law Service Area
            string cardTitle, cardDesc, cardIcon;
            if (secId.Equals("contracts", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Договорная обвязка и контрагенты";
                cardDesc = "Разработка типовых форм коммерческих договоров, защита от кабальных условий, аллокация ответственности и порядка расторжения.";
                cardIcon = "file-text";
            }
            else if (secId.Equals("ip", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Права на продукт и интеллектуальную собственность";
                cardDesc = "Консолидация исключительных прав, договоры авторского заказа с разработчиками, служебные произведения и лицензионные соглашения.";
                cardIcon = "code";
            }
            else if (secId.Equals("team", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Команда и привлеченные специалисты";
                cardDesc = "Оформление трудовых и подрядных договоров, соглашения о неконкуренции, NDA и порядок передачи созданных результатов.";
                cardIcon = "user_check";
            }
            else if (secId.Equals("product", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Пользовательский контур и оферта";
                cardDesc = "Публичная оферта, пользовательское соглашение, правила сервиса и защита от потребительских претензий.";
                cardIcon = "rocket";
            }
            else if (secId.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Персональные данные и процессы ИИ";
                cardDesc = "Политика конфиденциальности, регламенты работы с персональными данными, трансграничная передача и правовая обвязка ИИ.";
                cardIcon = "database";
            }
            else if (secId.Equals("investment", StringComparison.OrdinalIgnoreCase))
            {
                cardTitle = "Подготовка к инвестициям (Data Room & DD)";
                cardDesc = "Устранение блокеров раунда, проверка инвестиционной истории, подготовка Term Sheet и юридического пакета для инвесторов.";
                cardIcon = "coins";
            }
            else // founders, corporate, or fallback
            {
                cardTitle = "Основатели и корпоративная структура";
                cardDesc = "Разработка фаундерского соглашения (SHA), правила принятия решений, вестинг, выход основателя и структурирование владения.";
                cardIcon = "shield";
            }

            // Compute signal weights
            var maxSev = coveredFindings.Count > 0 ? coveredFindings.Max(f => f.Severity) : RiskSeverity.High;
            int sevWeight = maxSev switch
            {
                RiskSeverity.Blocker => 100000,
                RiskSeverity.Critical => 10000,
                RiskSeverity.High => 1000,
                RiskSeverity.Medium => 100,
                _ => 10
            };

            int prioWeight = action.Priority switch
            {
                RiskPriority.Now => 5000,
                RiskPriority.ThirtyDays => 3000,
                RiskPriority.BeforeRound => 1000,
                _ => 100
            };

            bool hasInvBlocker = coveredFindings.Any(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical || f.Code.Contains("OWNERSHIP") || f.Code.Contains("DEADLOCK"));
            int deficit = 100 - (secScore?.Score ?? 100);

            if (serviceDomainData.TryGetValue(cardTitle, out var existing))
            {
                serviceDomainData[cardTitle] = (
                    cardTitle,
                    cardDesc,
                    cardIcon,
                    Math.Max(existing.MaxSevWeight, sevWeight),
                    Math.Max(existing.MaxPrioWeight, prioWeight),
                    existing.HasInvBlocker || hasInvBlocker,
                    Math.Max(existing.Deficit, deficit)
                );
            }
            else
            {
                serviceDomainData[cardTitle] = (cardTitle, cardDesc, cardIcon, sevWeight, prioWeight, hasInvBlocker, deficit);
            }
        }

        // Prioritize service areas using composite ranking:
        // 1. Highest covered finding severity (Weight: 100,000 / 10,000 / 1,000)
        // 2. Action priority (Weight: 5,000 / 3,000 / 1,000)
        // 3. Investment blocker relevance (Weight: 20,000)
        // 4. Module deficit as secondary signal (Weight: 0..100)
        var prioritizedDomains = serviceDomainData.Values
            .OrderByDescending(d => d.MaxSevWeight + d.MaxPrioWeight + (d.HasInvBlocker ? 20000 : 0) + d.Deficit)
            .ToList();

        var allEligibleTitles = prioritizedDomains.Select(d => d.Title).ToList();
        
        var visibleCards = prioritizedDomains
            .Take(4)
            .Select(d => new FenixLawServiceCardDto
            {
                Title = d.Title,
                Description = d.Description,
                Icon = d.Icon
            })
            .ToList();

        var topServiceTitlesLower = allEligibleTitles.Select(t => t.ToLowerInvariant()).ToList();
        var dynamicListStr = topServiceTitlesLower.Count switch
        {
            0 => "структурирование и защита бизнеса",
            1 => topServiceTitlesLower[0],
            2 => $"{topServiceTitlesLower[0]} и {topServiceTitlesLower[1]}",
            3 => $"{topServiceTitlesLower[0]}, {topServiceTitlesLower[1]} и {topServiceTitlesLower[2]}",
            _ => string.Join(", ", topServiceTitlesLower.Take(3)) + " и другие ключевые направления"
        };

        var summaryText = $"Ключевые задачи требуют профессиональной юридической работы: {dynamicListStr}. Fenix Law может подключиться к комплексному устранению этих вопросов на основании уже сформированной диагностики SLS.";

        return new FenixLawRecommendationReportDto
        {
            RequiresLegalWork = true,
            SummaryText = summaryText,
            ServiceAreas = allEligibleTitles,
            ServiceCards = visibleCards,
            CtaTitle = "Связаться с Fenix Law",
            CtaSubtext = "Для обсуждения устранения выявленных рисков и структурирования компании:",
            Telegram = "@fenixlaw",
            Website = "www.fenixlaw.org",
            Phone = "+7-700-559-1377",
            Email = "team@fenixlaw.org"
        };
    }
}
