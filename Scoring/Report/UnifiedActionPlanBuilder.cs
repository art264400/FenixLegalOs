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
                ResolutionMode.InternalAction => "Можно сделать внутри команды",
                ResolutionMode.LegalReview => "Желательно проверить с юристом",
                ResolutionMode.LegalWork => "Требуется юридическая работа",
                ResolutionMode.LegalAndProduct => "Юридическая работа + доработка продукта",
                _ => "Требуется юридическая работа"
            };

            // Tailor WhatToDo / ExpectedResult for pre-incorporation context if needed
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
                else if (def.ActionId == "ACT_IP_CONSOLIDATION_AUDIT" || def.SectionId == "ip")
                {
                    whatToDo = "Провести инвентаризацию созданных компонентов и зафиксировать обязательства авторов и подрядчиков по отчуждению прав в создаваемую компанию.";
                    expectedResult = "Права на созданные результаты юридически зафиксированы за создателями с обязательством передачи в создаваемую компанию.";
                }
            }

            var coveredCodes = item.CoveredFindings.Select(f => f.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            actionList.Add(new UnifiedActionItemDto
            {
                Number = counter++,
                ActionId = def.ActionId,
                ActionType = def.ActionType,
                ResolutionMode = def.ResolutionMode,
                Priority = item.Priority,
                PriorityGroup = pGroup,
                Title = def.Title,
                WhatToDo = whatToDo,
                WhyNow = businessReason,
                ExpectedResult = expectedResult,
                ResolutionFormat = resolutionLabel,
                Dependencies = def.Dependencies.ToList(),
                CoveredFindingCodes = coveredCodes
            });
        }

        return actionList;
    }
}
