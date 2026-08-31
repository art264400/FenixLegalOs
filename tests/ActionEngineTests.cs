using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.ActionLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class ActionEngineTests
{
    [Fact(DisplayName = "1. ActionLibrary: Все ActionId уникальны и не пусты")]
    public void ActionLibrary_ActionIds_Are_Unique_And_NonEmpty()
    {
        var actionIds = ActionLibrary.All.Select(a => a.ActionId).ToList();
        Assert.NotEmpty(actionIds);

        var duplicates = actionIds
            .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        Assert.Empty(duplicates);

        foreach (var action in ActionLibrary.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(action.ActionId), "ActionId не может быть пустым");
            Assert.False(string.IsNullOrWhiteSpace(action.Title), $"Title действия '{action.ActionId}' пуст");
            Assert.False(string.IsNullOrWhiteSpace(action.BusinessReason), $"BusinessReason действия '{action.ActionId}' пуст");
            Assert.False(string.IsNullOrWhiteSpace(action.RequiredOutcome), $"RequiredOutcome действия '{action.ActionId}' пуст");
            Assert.False(string.IsNullOrWhiteSpace(action.WhatToDo), $"WhatToDo действия '{action.ActionId}' пуст");
        }
    }

    [Fact(DisplayName = "2. ActionDependencies: Все зависимости ссылаются на реально существующие ActionId")]
    public void ActionDependencies_Reference_Existing_Actions()
    {
        var existingActionIds = ActionLibrary.All.Select(a => a.ActionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var action in ActionLibrary.All)
        {
            foreach (var dep in action.Dependencies)
            {
                Assert.True(existingActionIds.Contains(dep),
                    $"Действие '{action.ActionId}' ссылается на несуществующую зависимость '{dep}'.");
            }
        }
    }

    [Fact(DisplayName = "3. Traceability: Каждый риск из RiskLibrary детерминированно разрешается в ActionDefinition")]
    public void Every_Risk_In_RiskLibrary_Resolves_To_Valid_Action()
    {
        foreach (var risk in DataBank.Risks)
        {
            var dummyFinding = new RiskFinding
            {
                Code = risk.Code,
                SectionId = risk.SectionId,
                Severity = risk.Severity,
                Priority = risk.Priority,
                Title = risk.Title,
                RootCauseGroup = risk.RootCauseGroup,
                RecommendedActionId = risk.RecommendedActionId
            };

            var resolvedAction = ActionLibrary.ResolveActionForFinding(dummyFinding);
            Assert.NotNull(resolvedAction);
            Assert.False(string.IsNullOrWhiteSpace(resolvedAction.ActionId));
            Assert.False(string.IsNullOrWhiteSpace(resolvedAction.RequiredOutcome));
        }
    }

    [Fact(DisplayName = "4. Privacy Policy: Разработка Политики конфиденциальности всегда требует LEGAL_WORK")]
    public void PrivacyPolicy_Creation_Requires_LegalWork()
    {
        var privacyAction = ActionLibrary.GetById("ACT_DATA_PRIVACY_POLICY_CREATE");
        Assert.NotNull(privacyAction);
        Assert.Equal(ResolutionMode.LegalWork, privacyAction.ResolutionMode);

        var dataFinding = new RiskFinding
        {
            Code = "DATA_PRIVACY_NOTICE_MISSING",
            SectionId = "data",
            Severity = RiskSeverity.High
        };

        var action = ActionLibrary.ResolveActionForFinding(dataFinding);
        Assert.Equal(ResolutionMode.LegalWork, action.ResolutionMode);
    }

    [Fact(DisplayName = "5. NoGenericOutcome: Разные действия имеют уникальные содержательные RequiredOutcome")]
    public void No_Generic_Outcome_For_Unrelated_Actions()
    {
        var aiAction = ActionLibrary.GetById("ACT_DATA_AI_PROVIDER_REVIEW");
        var privacyAction = ActionLibrary.GetById("ACT_DATA_PRIVACY_POLICY_CREATE");
        var deletionAction = ActionLibrary.GetById("ACT_DATA_RETENTION_DELETION");

        Assert.NotNull(aiAction);
        Assert.NotNull(privacyAction);
        Assert.NotNull(deletionAction);

        Assert.NotEqual(aiAction.RequiredOutcome, privacyAction.RequiredOutcome);
        Assert.NotEqual(privacyAction.RequiredOutcome, deletionAction.RequiredOutcome);
        Assert.NotEqual(aiAction.BusinessReason, deletionAction.BusinessReason);
    }

    [Fact(DisplayName = "6. Aggregation: Every actionable material finding has ActionPlan coverage and valid CoveredFindingCodes")]
    public void Every_Actionable_Finding_Has_Action_Coverage()
    {
        var findings = new List<RiskFinding>
        {
            new() { Code = "FND_DEADLOCK_RISK", SectionId = "founders", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now },
            new() { Code = "FND_GOVERNANCE_GAP", SectionId = "founders", Severity = RiskSeverity.High, Priority = RiskPriority.ThirtyDays },
            new() { Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", SectionId = "ip", Severity = RiskSeverity.Critical, Priority = RiskPriority.Now },
            new() { Code = "AI_SENSITIVE_DATA_TRANSFER", SectionId = "data", Severity = RiskSeverity.High, Priority = RiskPriority.Now }
        };

        var facts = new SharedFactStore();
        var actionPlan = UnifiedActionPlanBuilder.BuildUnifiedActionPlan(findings, facts);

        Assert.NotEmpty(actionPlan);

        // All findings are covered
        var allCoveredCodes = actionPlan.SelectMany(a => a.CoveredFindingCodes).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var f in findings)
        {
            Assert.Contains(f.Code, allCoveredCodes);
        }

        // Deadlock and Governance gap merged into ACT_FOUNDER_DEADLOCK_RESOLVE
        var deadlockAction = actionPlan.FirstOrDefault(a => a.ActionId == "ACT_FOUNDER_DEADLOCK_RESOLVE");
        Assert.NotNull(deadlockAction);
        Assert.Contains("FND_DEADLOCK_RISK", deadlockAction.CoveredFindingCodes);
        Assert.Contains("FND_GOVERNANCE_GAP", deadlockAction.CoveredFindingCodes);
    }
}
