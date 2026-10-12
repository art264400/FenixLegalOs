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
        List<UnifiedActionItemDto>? actionPlan = null,
        InvestmentReadinessReportDto? investmentContext = null,
        SharedFactStore? facts = null)
    {
        // An action can describe legal work only when it points to an actual source finding.
        // Without an action plan, preserve the existing finding-level eligibility contract.
        var sources = new List<LegalSource>();
        foreach (var finding in findings)
        {
            if (actionPlan == null)
            {
                if (finding.Severity is RiskSeverity.Critical or RiskSeverity.Blocker or RiskSeverity.High ||
                    finding.LawyerRequired || finding.Resolution == ResolutionType.LawyerRequired)
                    sources.Add(new LegalSource(finding, finding.Priority));
                continue;
            }

            if (string.IsNullOrWhiteSpace(finding.Code)) continue;
            var relatedLegalActions = actionPlan
                .Where(a => a.ResolutionMode is ResolutionMode.LegalWork or ResolutionMode.LegalReview or ResolutionMode.LegalAndProduct)
                .Where(a => a.CoveredFindingCodes.Any(code => !string.IsNullOrWhiteSpace(code) &&
                    string.Equals(code, finding.Code, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (relatedLegalActions.Count == 0) continue;
            var priority = relatedLegalActions.OrderByDescending(a => PriorityWeight(a.Priority)).First().Priority;
            sources.Add(new LegalSource(finding, priority));
        }

        var domains = new Dictionary<string, ServiceDomain>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            var sectionId = ResolveSourceSection(source.Finding);
            var domain = DescribeDomain(sectionId, investmentContext);
            if (domain == null) continue;

            // A real finding still needs remediation when its detailed module is N/A:
            // COR_NO_ENTITY_FOR_ACTIVITY is one example. N/A itself never creates a service.
            var remediation = FindingRemediationBuilder.Build(source.Finding, facts);
            var recommendation = string.IsNullOrWhiteSpace(remediation.WhatToDo)
                ? "Уточнить исходные сведения по выявленному вопросу и определить необходимый следующий шаг."
                : remediation.WhatToDo;
            var item = new FenixLawServiceItemDto
            {
                FindingCode = source.Finding.Code,
                Title = string.IsNullOrWhiteSpace(remediation.Title) ? source.Finding.Title : remediation.Title,
                Recommendation = recommendation,
                RequiresClarification = remediation.RequiresClarification,
                HasConfirmedIssue = remediation.HasConfirmedIssue,
                SourceAnswers = source.Finding.Basis.Select(b => new RiskFindingBasis
                {
                    QuestionId = b.QuestionId,
                    AnswerId = b.AnswerId
                }).ToList()
            };
            if (string.IsNullOrWhiteSpace(item.Title))
                item.Title = "Уточнение выявленного вопроса";

            if (!domains.TryGetValue(domain.Value.Title, out var service))
            {
                service = new ServiceDomain(domain.Value.Title, domain.Value.Icon);
                domains.Add(domain.Value.Title, service);
            }

            // Blank codes in legacy callers do not identify a shared finding. Keep those
            // real source records separate, while deduplicating ordinary canonical codes.
            if (string.IsNullOrWhiteSpace(item.FindingCode) ||
                !service.Items.Any(existing => string.Equals(existing.FindingCode, item.FindingCode, StringComparison.OrdinalIgnoreCase)))
                service.Items.Add(item);
            service.MaxSeverityWeight = Math.Max(service.MaxSeverityWeight, SeverityWeight(source.Finding.Severity));
            service.MaxPriorityWeight = Math.Max(service.MaxPriorityWeight, PriorityWeight(source.Priority));
            service.HasRoundBlocker |= investmentContext?.RoundIsCloseOrActive == true &&
                investmentContext.CrossModuleBlockers.Any(b => string.Equals(b.FindingCode, source.Finding.Code, StringComparison.OrdinalIgnoreCase));
            var section = result.Sections.FirstOrDefault(s => string.Equals(s.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
            service.Deficit = Math.Max(service.Deficit, 100 - (section?.Score ?? 100));
        }

        var cards = domains.Values
            .OrderByDescending(d => d.MaxSeverityWeight + d.MaxPriorityWeight + (d.HasRoundBlocker ? 20000 : 0) + d.Deficit)
            .ThenBy(d => d.Title, StringComparer.Ordinal)
            .Select(d => new FenixLawServiceCardDto
            {
                Title = d.Title,
                Description = string.Join(" ", d.Items.Select(item => item.Recommendation).Distinct(StringComparer.Ordinal)),
                Icon = d.Icon,
                Items = d.Items,
                SourceFindingCodes = d.Items.Select(item => item.FindingCode)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            })
            .ToList();

        if (cards.Count == 0)
        {
            var summary = sources.Count > 0
                ? "Для вывода о конкретном юридическом сопровождении нужно уточнить направление и содержание выявленных вопросов. По доступным сведениям перечень юридических услуг не сформирован."
                : findings.Count == 0
                    ? "По результатам текущего скрининга необходимость в отдельном юридическом сопровождении не выявлена. Сохраните отчет для дальнейшего планирования."
                    : "По сформированному плану отдельная юридическая работа не предусмотрена. Текущие рекомендации можно выполнить самостоятельно или внутри команды.";
            return new FenixLawRecommendationReportDto
            {
                RequiresLegalWork = false,
                SummaryText = summary,
                ServiceAreas = new(),
                ServiceCards = new(),
                CtaTitle = "Сохранить отчет и план действий",
                CtaSubtext = "Используйте отчет для дальнейшего планирования и уточнения исходных сведений."
            };
        }

        var allItems = cards.SelectMany(card => card.Items).ToList();
        var allNeedClarification = allItems.All(item => item.RequiresClarification && !item.HasConfirmedIssue);
        var titles = cards.Select(card => card.Title.ToLowerInvariant()).ToList();
        var joinedTitles = titles.Count == 1 ? titles[0] : string.Join(", ", titles.Take(titles.Count - 1)) + " и " + titles.Last();
        var summaryText = allNeedClarification
            ? $"По результатам скрининга требуется уточнение сведений по направлениям: {joinedTitles}."
            : allItems.Count == 1
                ? $"По результатам скрининга один приоритетный вопрос требует юридической работы: {joinedTitles}."
                : $"По результатам скрининга несколько приоритетных вопросов требуют юридической работы: {joinedTitles}.";
        summaryText += allNeedClarification
            ? "\n\nПервый шаг — юридическая проверка и уточнение исходных сведений. Объем последующей работы определяется после этой проверки."
            : allItems.Any(item => item.RequiresClarification)
                ? "\n\nПо пунктам с неопределенными ответами сначала нужно проверить исходные сведения. Fenix Law может помочь с конкретными действиями, перечисленными ниже."
                : "\n\nFenix Law может помочь выполнить конкретные действия по выявленным вопросам, перечисленные ниже.";

        return new FenixLawRecommendationReportDto
        {
            RequiresLegalWork = true,
            SummaryText = summaryText,
            ServiceAreas = cards.Select(card => card.Title).ToList(),
            ServiceCards = cards,
            CtaTitle = allNeedClarification ? "Обсудить юридическую проверку" : "Связаться с Fenix Law",
            CtaSubtext = allNeedClarification
                ? "Для обсуждения проверки и уточнения выявленных вопросов:"
                : "Для обсуждения конкретных действий по выявленным вопросам:",
            Telegram = "@fenixlaw",
            Website = "www.fenixlaw.org",
            Phone = "+7-700-559-1377",
            Email = "team@fenixlaw.org"
        };
    }

    private static string ResolveSourceSection(RiskFinding finding)
    {
        if (!string.IsNullOrWhiteSpace(finding.SectionId)) return finding.SectionId.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(finding.Code))
        {
            var definition = Data.DataBank.Risks.FirstOrDefault(r => string.Equals(r.Code, finding.Code, StringComparison.OrdinalIgnoreCase));
            if (definition != null && !string.IsNullOrWhiteSpace(definition.SectionId))
                return definition.SectionId.Trim().ToLowerInvariant();
        }
        return finding.Modules.FirstOrDefault(module => DescribeDomain(module.Trim().ToLowerInvariant(), null) != null)?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static (string Title, string Icon)? DescribeDomain(string sectionId, InvestmentReadinessReportDto? investment) => sectionId switch
    {
        "founders" or "corporate" => ("Основатели и корпоративная структура", "shield"),
        "ip" => ("Права на продукт и интеллектуальную собственность", "code"),
        "team" => ("Команда и привлеченные специалисты", "user_check"),
        "product" => ("Пользовательский контур и оферта", "rocket"),
        "data" => ("Персональные данные и процессы ИИ", "database"),
        "contracts" => ("Договорная обвязка и контрагенты", "file-text"),
        "investment" => (investment?.NewRoundPlanned == true
            ? "Подготовка к инвестициям (Data Room & DD)"
            : investment?.NewRoundPossible == true ? "Инвестиционные планы и подготовка"
            : "Инвестиционная история и обязательства", "coins"),
        _ => null
    };

    private static int SeverityWeight(RiskSeverity severity) => severity switch
    {
        RiskSeverity.Blocker => 100000,
        RiskSeverity.Critical => 10000,
        RiskSeverity.High => 1000,
        RiskSeverity.Medium => 100,
        _ => 10
    };

    private static int PriorityWeight(RiskPriority priority) => priority switch
    {
        RiskPriority.Now => 5000,
        RiskPriority.ThirtyDays => 3000,
        RiskPriority.BeforeRound => 1000,
        _ => 100
    };

    private sealed record LegalSource(RiskFinding Finding, RiskPriority Priority);

    private sealed class ServiceDomain(string title, string icon)
    {
        public string Title { get; } = title;
        public string Icon { get; } = icon;
        public List<FenixLawServiceItemDto> Items { get; } = new();
        public int MaxSeverityWeight { get; set; }
        public int MaxPriorityWeight { get; set; }
        public bool HasRoundBlocker { get; set; }
        public int Deficit { get; set; }
    }
}