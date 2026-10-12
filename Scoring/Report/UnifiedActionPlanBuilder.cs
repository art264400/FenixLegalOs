using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class UnifiedActionPlanBuilder
{
    public static List<UnifiedActionItemDto> BuildUnifiedActionPlan(List<RiskFinding> findings, SharedFactStore facts)
    {
        var actionList = new List<UnifiedActionItemDto>();
        if (findings == null || findings.Count == 0) return actionList;

        var entityStatus = "";
        if (facts.Facts.TryGetValue("company.entityStatus", out var esObj) && esObj != null)
            entityStatus = esObj.ToString() ?? "";
        bool isUnincorporated = entityStatus is "not_incorporated" or "none" or "no_entity" or "";
        var investmentTiming = facts.Facts.TryGetValue("investment.timing", out var timing) ? timing?.ToString() : null;
        var newRoundPlanned = investmentTiming is "6_12m" or "3_6m" or "active_search" or "specific_investor" or "terms_received";

        // 1. Map each finding to a canonical ActionDefinition from ActionLibrary
        var findingActionPairs = findings
            .Select(f => new
            {
                Finding = f,
                ActionDef = ActionLibrary.ResolveActionForFinding(f)
            })
            .ToList();

        // 2. Group findings by ActionId (merges same remediation actions)
        var groups = findingActionPairs
            .GroupBy(p => p.ActionDef.ActionId, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var actionDef = g.First().ActionDef;
                var coveredFindings = g.Select(p => p.Finding).ToList();

                // Compute highest priority among covered findings or default
                var maxPriority = coveredFindings.Count > 0
                    ? coveredFindings
                        .Select(f => f.Priority)
                        .OrderBy(p => p switch
                        {
                            RiskPriority.Now => 0,
                            RiskPriority.ThirtyDays => 1,
                            RiskPriority.BeforeRound => 2,
                            RiskPriority.Later => 3,
                            _ => 4
                        })
                        .First()
                    : actionDef.DefaultPriority;

                // Max severity among covered findings
                var maxSeverity = coveredFindings
                    .Select(f => f.Severity)
                    .OrderByDescending(s => s switch
                    {
                        RiskSeverity.Blocker => 4,
                        RiskSeverity.Critical => 3,
                        RiskSeverity.High => 2,
                        RiskSeverity.Medium => 1,
                        _ => 0
                    })
                    .FirstOrDefault();

                return new
                {
                    ActionDef = actionDef,
                    CoveredFindings = coveredFindings,
                    Priority = maxPriority,
                    MaxSeverity = maxSeverity
                };
            })
            // 3. Order actions deterministically: Priority -> Severity -> ActionId
            .OrderBy(g => g.Priority switch
            {
                RiskPriority.Now => 0,
                RiskPriority.ThirtyDays => 1,
                RiskPriority.BeforeRound => 2,
                RiskPriority.Later => 3,
                _ => 4
            })
            .ThenByDescending(g => g.MaxSeverity switch
            {
                RiskSeverity.Blocker => 4,
                RiskSeverity.Critical => 3,
                RiskSeverity.High => 2,
                RiskSeverity.Medium => 1,
                _ => 0
            })
            .ThenBy(g => g.ActionDef.ActionId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        int counter = 1;
        foreach (var item in groups)
        {
            var def = item.ActionDef;
            var pGroup = item.Priority switch
            {
                RiskPriority.Now => "В ПЕРВУЮ ОЧЕРЕДЬ",
                RiskPriority.ThirtyDays => "СЛЕДУЮЩИМ ЭТАПОМ",
                RiskPriority.BeforeRound => "ДО ИНВЕСТИЦИОННОГО РАУНДА",
                RiskPriority.Later => "ПЛАНОВОЕ УЛУЧШЕНИЕ",
                _ => "СЛЕДУЮЩИМ ЭТАПОМ"
            };

            var resolutionLabel = def.ResolutionMode switch
            {
                ResolutionMode.InternalAction => "Внутреннее действие команды",
                ResolutionMode.LegalReview => "Юридическая проверка",
                ResolutionMode.LegalWork => "Требуется юридическая работа",
                ResolutionMode.LegalAndProduct => "Юридическая и техническая доработка",
                _ => "Требуется юридическая работа"
            };

            // Tailor WhatToDo / ExpectedResult for pre-incorporation context if needed
            string title = def.Title;
            string whatToDo = def.WhatToDo;
            string expectedResult = def.RequiredOutcome;
            string businessReason = def.BusinessReason;

            if (isUnincorporated)
            {
                if (def.ActionId == "ACT_IP_FOUNDER_ASSIGNMENT")
                {
                    whatToDo = "Оформить соглашения между основателями о создании и последующей передаче прав на интеллектуальную собственность в создаваемую компанию.";
                    expectedResult = "Права на исходный код юридически зафиксированы за создателями с обязательством передачи в создаваемую компанию.";
                }
                else if (def.ActionId == "ACT_IP_CONSOLIDATION_AUDIT")
                {
                    whatToDo = "Провести инвентаризацию созданных компонентов и зафиксировать обязательства авторов и подрядчиков по отчуждению прав в создаваемую компанию.";
                    expectedResult = "Права на созданные результаты юридически зафиксированы за создателями с обязательством передачи в создаваемую компанию.";
                }
            }

            // Preserve the action identity and ordering, but do not let a broad library
            // package invent work for a targeted document finding or an unknown answer.
            var remediations = item.CoveredFindings.Select(f => FindingRemediationBuilder.Build(f, facts)).ToList();
            var useCanonicalRemediation = remediations.Any(r => r.RequiresClarification) ||
                item.CoveredFindings.Any(f => f.Code.StartsWith("PROD_", StringComparison.OrdinalIgnoreCase) ||
                    newRoundPlanned && f.Code is ("INVEST_PRIOR_INVESTMENT_UNCLEAR" or "INVEST_FUTURE_CAP_TABLE_UNCLEAR" or
                        "INVEST_DILUTION_NOT_MODELED" or "INVEST_DD_DOCS_NOT_READY" or "INVEST_SELF_AWARENESS_GAP") ||
                    f.Code is "DATA_PRIVACY_NOTICE_MISSING" or "DATA_PRIVACY_NOTICE_OUTDATED" or
                        "DATA_NO_PRIVACY_POLICY" or "DATA_PRIVACY_POLICY_INADEQUATE" or "DATA_PRIVACY_MISSING");
            if (useCanonicalRemediation)
            {
                static string Merge(IEnumerable<string> values, string separator = " ") =>
                    string.Join(separator, values.Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
                title = Merge(remediations.Select(r => r.Title), "; ");
                whatToDo = Merge(remediations.Select(r => r.WhatToDo));
                businessReason = Merge(remediations.Select(r => r.WhyNow));
                expectedResult = Merge(remediations.Select(r => r.ExpectedResult));
            }

            var coveredCodes = item.CoveredFindings.Select(f => f.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var action = new UnifiedActionItemDto
            {
                Number = counter++,
                ActionId = def.ActionId,
                ActionType = def.ActionType,
                ResolutionMode = def.ResolutionMode,
                Priority = item.Priority,
                PriorityGroup = pGroup,
                Title = title,
                UseCanonicalRemediation = useCanonicalRemediation,
                WhatToDo = whatToDo,
                WhyNow = businessReason,
                ExpectedResult = expectedResult,
                ResolutionFormat = resolutionLabel,
                Dependencies = def.Dependencies.ToList(),
                CoveredFindingCodes = coveredCodes
            };
            if (!useCanonicalRemediation)
                InvestmentNarrativePresentation.ApplyToAction(action, newRoundPlanned);
            else if (!newRoundPlanned && action.Priority == RiskPriority.BeforeRound)
                // The helper already adapts canonical text. Do not replace a check
                // or a targeted remediation with the broader action-library package.
                action.PriorityGroup = "ПЛАНОВОЕ УЛУЧШЕНИЕ";
            actionList.Add(action);
        }

        return actionList;
    }
}
