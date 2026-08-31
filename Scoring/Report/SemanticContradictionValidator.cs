using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;

namespace FenixLegalOs.Scoring.Report;

/// <summary>
/// Universal Semantic Contradiction Validator for FENIX SLS Report Engine.
/// Enforces all 13 Cross-Report Invariants fail-closed.
/// </summary>
public static class SemanticContradictionValidator
{
    public static void Validate(ScoreResult result, SharedFactStore facts, ReportContext ctx)
    {
        if (result == null) throw new InvalidOperationException("[SemanticContradictionValidator] ScoreResult is null.");
        if (facts == null) throw new InvalidOperationException("[SemanticContradictionValidator] SharedFactStore is null.");
        if (ctx == null) throw new InvalidOperationException("[SemanticContradictionValidator] ReportContext is null.");

        AssertNoPositiveFactorContradictsActiveFinding(result, ctx);
        AssertNoUniqueRootFindingLostInReport(result, ctx);
        AssertEveryActionableHighCriticalBlockerHasActionPlanCoverage(result, ctx);
        AssertApplicableModulesHaveExplainableScores(result, ctx);
        AssertNaModulesDoNotAffectScore(result, ctx);
        AssertFocusMarkerMatchesDetailedDestination(ctx);
        AssertRequiresLegalWorkConsistentWithFindingsAndActions(result, ctx);
        AssertNoDuplicateRootCausePresentation(ctx);
        AssertScoreBandNeverUsedAsRiskSeverity(ctx);
        AssertNoInternalValuesInUserFacingReport(ctx);
        AssertAllEightZonesAlwaysRepresented(result, ctx);
        AssertHealthyScenarioDoesNotInventRisks(result, ctx);
        AssertSevereScenarioDoesNotHideRisks(result, ctx);
        AssertHealthyScenarioHasNoUnresolvedNegativeProfileFacts(result, ctx);
        AssertPerfectScoreHasNoPriorityRemediationNarrative(result, ctx);
        AssertNoNaModuleLeaksIntoFenixLawRecommendation(result, ctx);
        AssertRegisteredEntityNeverUsesFutureCompanyLanguage(facts, ctx);
        AssertEveryActionableFindingHasActionCoverage(result, ctx);
        AssertNoDuplicateActionPlanWorkstreams(ctx);
        AssertActionPlanPreservesFindingTraceability(result, ctx);
        AssertActionsHaveDeterministicResolutionMode(ctx);
        AssertNoGenericOutcomesForUnrelatedActions(ctx);
        AssertSLSIsNotDescribedAsAudit(ctx);
    }

    public static void AssertNoPositiveFactorContradictsActiveFinding(ScoreResult result, ReportContext ctx)
    {
        var findings = result.Risks;

        foreach (var pos in ctx.PositiveFactors)
        {
            var title = pos.Title ?? "";

            // 1. Founder Dispute / Litigation Contradiction
            if (title.Contains("Отсутствие споров", StringComparison.OrdinalIgnoreCase) ||
                (title.Contains("спор", StringComparison.OrdinalIgnoreCase) && !title.Contains("интерес", StringComparison.OrdinalIgnoreCase)))
            {
                var disputeRisks = findings.Where(r =>
                    r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High &&
                    (r.RootCauseGroup is "FOUNDER_CONFLICT" or "OWNERSHIP" or "FOUNDER_EQUITY" ||
                     r.Code.Contains("DISPUTE", StringComparison.OrdinalIgnoreCase))).ToList();

                if (disputeRisks.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"[Invariant 1 Violation] Positive factor '{title}' directly contradicts active dispute risk(s): {string.Join(", ", disputeRisks.Select(r => r.Code))}.");
                }
            }

            // 2. Conflict of Interest Contradiction
            if (title.Contains("конфликт", StringComparison.OrdinalIgnoreCase) && title.Contains("интерес", StringComparison.OrdinalIgnoreCase))
            {
                var coiRisks = findings.Where(r =>
                    r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High &&
                    (r.Code.Contains("OUTSIDE", StringComparison.OrdinalIgnoreCase) ||
                     r.Code.Contains("CONFLICT_OF_INTEREST", StringComparison.OrdinalIgnoreCase))).ToList();

                if (coiRisks.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"[Invariant 1 Violation] Positive factor '{title}' contradicts active conflict of interest risk(s): {string.Join(", ", coiRisks.Select(r => r.Code))}.");
                }
            }

            // 3. Equity & Cap Table Contradiction
            if (title.Contains("Распределение долей", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Соответствие долей", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("структур", StringComparison.OrdinalIgnoreCase) && title.Contains("капитал", StringComparison.OrdinalIgnoreCase))
            {
                var equityRisks = findings.Where(r =>
                    r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High &&
                    (r.RootCauseGroup is "FOUNDER_EQUITY" or "OWNERSHIP" or "EQUITY_PROMISE" ||
                     r.Code.Contains("EQUITY", StringComparison.OrdinalIgnoreCase) ||
                     r.Code.Contains("CAP_TABLE", StringComparison.OrdinalIgnoreCase))).ToList();

                if (equityRisks.Count > 0)
                {
                    throw new InvalidOperationException(
                        $"[Invariant 1 Violation] Positive factor '{title}' contradicts active equity risk(s): {string.Join(", ", equityRisks.Select(r => r.Code))}.");
                }
            }
        }
    }

    public static void AssertNoUniqueRootFindingLostInReport(ScoreResult result, ReportContext ctx)
    {
        foreach (var focus in ctx.FocusModules)
        {
            var expectedSectionRisks = result.Risks.Where(r => r.SectionId.Equals(focus.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            if (expectedSectionRisks.Count != focus.Findings.Count)
            {
                throw new InvalidOperationException(
                    $"[Invariant 2 Violation] Section '{focus.SectionId}' has {expectedSectionRisks.Count} active findings in ScoreResult, but FocusModule contains {focus.Findings.Count}.");
            }
        }
    }

    public static void AssertEveryActionableHighCriticalBlockerHasActionPlanCoverage(ScoreResult result, ReportContext ctx)
    {
        var seriousRisks = result.Risks.Where(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High).ToList();
        if (seriousRisks.Count > 0 && ctx.ActionPlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"[Invariant 3 Violation] Found {seriousRisks.Count} High/Critical/Blocker findings, but UnifiedActionPlan is completely empty.");
        }
    }

    public static void AssertApplicableModulesHaveExplainableScores(ScoreResult result, ReportContext ctx)
    {
        foreach (var sec in result.Sections.Where(s => s.Status == ApplicabilityStatus.Applicable))
        {
            if (!sec.Score.HasValue)
            {
                throw new InvalidOperationException($"[Invariant 4 Violation] Applicable module '{sec.SectionId}' has null Score.");
            }

            if (sec.Score.Value < 0 || sec.Score.Value > 100)
            {
                throw new InvalidOperationException($"[Invariant 4 Violation] Applicable module '{sec.SectionId}' has invalid Score {sec.Score.Value}.");
            }
        }
    }

    public static void AssertNaModulesDoNotAffectScore(ScoreResult result, ReportContext ctx)
    {
        foreach (var sec in result.Sections.Where(s => s.Status == ApplicabilityStatus.NotApplicable))
        {
            if (sec.Score.HasValue)
            {
                throw new InvalidOperationException($"[Invariant 5 Violation] N/A module '{sec.SectionId}' must have null Score, but got {sec.Score.Value}.");
            }
        }

        foreach (var card in ctx.ModuleCards.Where(c => c.RenderMode == ReportRenderMode.NotApplicable))
        {
            if (card.Score.HasValue)
            {
                throw new InvalidOperationException($"[Invariant 5 Violation] N/A ModuleCard '{card.SectionId}' must have null Score.");
            }
        }
    }

    public static void AssertFocusMarkerMatchesDetailedDestination(ReportContext ctx)
    {
        foreach (var card in ctx.ModuleCards)
        {
            if (card.RenderMode == ReportRenderMode.Focus)
            {
                bool hasFocus = ctx.FocusModules.Any(f => f.SectionId.Equals(card.SectionId, StringComparison.OrdinalIgnoreCase));
                if (!hasFocus)
                {
                    throw new InvalidOperationException($"[Invariant 6 Violation] Card '{card.SectionId}' is FOCUS, but no FocusModuleDetailDto exists.");
                }
            }
        }
    }

    public static void AssertRequiresLegalWorkConsistentWithFindingsAndActions(ScoreResult result, ReportContext ctx)
    {
        bool hasSerious = result.Risks.Any(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical || r.Resolution == ResolutionType.LawyerRequired);
        if (hasSerious && !ctx.FenixLaw.RequiresLegalWork)
        {
            throw new InvalidOperationException("[Invariant 7 Violation] Blocker/Critical or LawyerRequired findings exist, but RequiresLegalWork is FALSE.");
        }

        if (!ctx.FenixLaw.RequiresLegalWork && ctx.FenixLaw.ServiceCards.Count > 0)
        {
            throw new InvalidOperationException("[Invariant 7 Violation] ServiceCards present when RequiresLegalWork is FALSE.");
        }
    }

    public static void AssertNoDuplicateRootCausePresentation(ReportContext ctx)
    {
        var seenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var top in ctx.TopFindings)
        {
            if (!string.IsNullOrWhiteSpace(top.RootCauseCode))
            {
                if (!seenRoots.Add(top.RootCauseCode))
                {
                    throw new InvalidOperationException($"[Invariant 8 Violation] Duplicate RootCauseGroup '{top.RootCauseCode}' in Executive Summary TopFindings.");
                }
            }
        }
    }

    public static void AssertScoreBandNeverUsedAsRiskSeverity(ReportContext ctx)
    {
        var forbiddenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Хорошая готовность", "Требует внимания", "Существенные пробелы", "Критические пробелы", "Устойчиво"
        };

        foreach (var focus in ctx.FocusModules)
        {
            foreach (var f in focus.Findings)
            {
                if (forbiddenLabels.Contains(f.SeverityLabel))
                {
                    throw new InvalidOperationException(
                        $"[Invariant 9 Violation] Finding '{f.Title}' uses ScoreBand text '{f.SeverityLabel}' as SeverityLabel.");
                }
            }
        }
    }

    public static void AssertNoInternalValuesInUserFacingReport(ReportContext ctx)
    {
        var rawPatterns = new[] { "live_or_ready", "product.stage", "kz_llp", "FND_DEADLOCK", "COR_OWNERSHIP", "not_incorporated" };

        void CheckString(string? text, string location)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            foreach (var pat in rawPatterns)
            {
                if (text.Contains(pat, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[Invariant 10 Violation] Found internal raw string '{pat}' in {location}: '{text}'");
                }
            }
        }

        CheckString(ctx.ProjectName, "ProjectName");
        CheckString(ctx.ProjectStage, "ProjectStage");
        CheckString(ctx.ExecutiveConclusion, "ExecutiveConclusion");
        CheckString(ctx.Profile.ConfigurationNarrative, "Profile.ConfigurationNarrative");

        foreach (var card in ctx.ModuleCards)
        {
            CheckString(card.ReasonIfNa, $"ModuleCard[{card.SectionId}].ReasonIfNa");
            CheckString(card.TriggerIfNa, $"ModuleCard[{card.SectionId}].TriggerIfNa");
        }

        foreach (var focus in ctx.FocusModules)
        {
            CheckString(focus.SubtitleNarrative, $"Focus[{focus.SectionId}].Subtitle");
            CheckString(focus.PracticalMeaning, $"Focus[{focus.SectionId}].PracticalMeaning");
            foreach (var f in focus.Findings)
            {
                CheckString(f.WhyFound, $"Finding[{f.FindingCode}].WhyFound");
                CheckString(f.WhyItMatters, $"Finding[{f.FindingCode}].WhyItMatters");
                CheckString(f.Recommendation, $"Finding[{f.FindingCode}].Recommendation");
            }
        }
    }

    public static void AssertAllEightZonesAlwaysRepresented(ScoreResult result, ReportContext ctx)
    {
        if (result.Sections.Count == 8 && ctx.ModuleCards.Count != 8)
        {
            throw new InvalidOperationException($"[Invariant 11 Violation] ModuleCards count is {ctx.ModuleCards.Count}, expected exactly 8.");
        }
    }

    public static void AssertHealthyScenarioDoesNotInventRisks(ScoreResult result, ReportContext ctx)
    {
        if (result.Overall >= 95 && result.Risks.Count == 0)
        {
            if (ctx.FenixLaw.RequiresLegalWork)
            {
                throw new InvalidOperationException("[Invariant 12 Violation] Healthy scenario (Score >= 95, 0 findings) should not require legal work.");
            }
            if (ctx.ActionPlan.Count > 0)
            {
                throw new InvalidOperationException("[Invariant 12 Violation] Healthy scenario with 0 findings should not produce ActionPlan items.");
            }
        }
    }

    public static void AssertSevereScenarioDoesNotHideRisks(ScoreResult result, ReportContext ctx)
    {
        if (result.Sections.Count == 8 && result.Overall < 40 && result.Risks.Count > 0)
        {
            if (ctx.FocusModules.Count == 0)
            {
                throw new InvalidOperationException("[Invariant 13 Violation] Severe scenario (Score < 40) has 0 FocusModules.");
            }
            if (ctx.ActionPlan.Count == 0)
            {
                throw new InvalidOperationException("[Invariant 13 Violation] Severe scenario (Score < 40) has 0 ActionPlan items.");
            }
            if (!ctx.FenixLaw.RequiresLegalWork)
            {
                throw new InvalidOperationException("[Invariant 13 Violation] Severe scenario (Score < 40) must require legal work.");
            }
        }
    }

    public static void AssertHealthyScenarioHasNoUnresolvedNegativeProfileFacts(ScoreResult result, ReportContext ctx)
    {
        if (result.Overall >= 95 && result.Risks.Count == 0)
        {
            var narr = ctx.Profile.ConfigurationNarrative ?? "";
            if (narr.Contains("не полностью", StringComparison.OrdinalIgnoreCase) ||
                narr.Contains("не оформлены", StringComparison.OrdinalIgnoreCase) ||
                narr.Contains("пробелы", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"[Invariant Violation] Healthy scenario contains negative profile narrative: '{narr}'");
            }

            foreach (var fact in ctx.Profile.KeyFacts)
            {
                if (fact.Value.Contains("не полностью", StringComparison.OrdinalIgnoreCase) ||
                    fact.Value.Contains("не оформлены", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"[Invariant Violation] Healthy scenario fact '{fact.Label}' has negative value: '{fact.Value}'");
                }
            }
        }
    }

    public static void AssertPerfectScoreHasNoPriorityRemediationNarrative(ScoreResult result, ReportContext ctx)
    {
        if (result.Overall >= 95 && result.Risks.Count == 0)
        {
            var exec = ctx.ExecutiveConclusion ?? "";
            if (exec.Contains("первоочередного", StringComparison.OrdinalIgnoreCase) ||
                exec.Contains("сохраняются правовые уязвимости", StringComparison.OrdinalIgnoreCase) ||
                exec.Contains("факторами снижения оценки", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"[Invariant Violation] Perfect score (100) scenario contains priority remediation narrative: '{exec}'");
            }
        }
    }

    public static void AssertNoNaModuleLeaksIntoFenixLawRecommendation(ScoreResult result, ReportContext ctx)
    {
        foreach (var naSec in result.Sections.Where(s => s.Status == ApplicabilityStatus.NotApplicable))
        {
            var secId = naSec.SectionId.ToLowerInvariant();
            bool hasActiveFinding = result.Risks.Any(r => r.SectionId.Equals(secId, StringComparison.OrdinalIgnoreCase));
            if (!hasActiveFinding)
            {
                var prohibitedPhrases = secId switch
                {
                    "data" => new[] { "персональных данных", "процессов ИИ", "правовой обвязки ИИ", "AI" },
                    "team" => new[] { "оформления отношений с командой" },
                    "contracts" => new[] { "договорной обвязки с контрагентами" },
                    "investment" => new[] { "подготовки к инвестиционным раундам" },
                    _ => Array.Empty<string>()
                };

                foreach (var phrase in prohibitedPhrases)
                {
                    if (ctx.FenixLaw.SummaryText.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"[Invariant Violation] N/A module '{secId}' leaked into FenixLaw Summary: '{phrase}' found in '{ctx.FenixLaw.SummaryText}'");
                    }

                    foreach (var card in ctx.FenixLaw.ServiceCards)
                    {
                        if (card.Title.Contains(phrase, StringComparison.OrdinalIgnoreCase) || card.Description.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"[Invariant Violation] N/A module '{secId}' leaked into FenixLaw ServiceCard '{card.Title}': '{phrase}'");
                        }
                    }
                }
            }
        }
    }

    public static void AssertRegisteredEntityNeverUsesFutureCompanyLanguage(SharedFactStore facts, ReportContext ctx)
    {
        var entityStatus = (string?)facts.Facts.GetValueOrDefault("company.entityStatus") ?? "";
        bool isRegistered = entityStatus is "one" or "multiple" or "holding" or "registered";

        if (isRegistered)
        {
            var prohibited = new[] { "создаваемую компанию", "создаваемой компании", "будущую компанию", "создаваемом юридическом лице" };

            foreach (var action in ctx.ActionPlan)
            {
                foreach (var p in prohibited)
                {
                    if (action.WhatToDo.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                        action.ExpectedResult.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                        action.WhyNow.Contains(p, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"[Invariant Violation] Registered entity uses future company language '{p}' in ActionPlan '{action.Title}'");
                    }
                }
            }

            foreach (var focus in ctx.FocusModules)
            {
                foreach (var f in focus.Findings)
                {
                    foreach (var p in prohibited)
                    {
                        if (f.Recommendation.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                            f.WhyFound.Contains(p, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"[Invariant Violation] Registered entity uses future company language '{p}' in Finding '{f.Title}'");
                        }
                    }
                }
            }
        }
    }

    public static void AssertEveryActionableFindingHasActionCoverage(ScoreResult result, ReportContext ctx)
    {
        var actionableRisks = result.Risks
            .Where(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High || r.Priority == RiskPriority.Now)
            .ToList();

        if (actionableRisks.Count > 0)
        {
            var coveredCodes = ctx.ActionPlan.SelectMany(a => a.CoveredFindingCodes).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var uncovered = actionableRisks.Where(r => !coveredCodes.Contains(r.Code)).ToList();

            if (uncovered.Count > 0)
            {
                throw new InvalidOperationException(
                    $"[Invariant Violation] {uncovered.Count} actionable findings not covered in ActionPlan: {string.Join(", ", uncovered.Select(u => u.Code))}");
            }
        }
    }

    public static void AssertNoDuplicateActionPlanWorkstreams(ReportContext ctx)
    {
        var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in ctx.ActionPlan)
        {
            if (!seenTitles.Add(action.Title))
            {
                throw new InvalidOperationException(
                    $"[Invariant Violation] Duplicate ActionPlan item title: '{action.Title}'");
            }
        }
    }

    public static void AssertActionPlanPreservesFindingTraceability(ScoreResult result, ReportContext ctx)
    {
        var riskCodes = result.Risks.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var action in ctx.ActionPlan)
        {
            foreach (var code in action.CoveredFindingCodes)
            {
                if (!riskCodes.Contains(code))
                {
                    throw new InvalidOperationException(
                        $"[Invariant Violation] ActionPlan item '{action.Title}' references untraceable finding code '{code}'");
                }
            }
        }
    }

    public static void AssertActionsHaveDeterministicResolutionMode(ReportContext ctx)
    {
        foreach (var action in ctx.ActionPlan)
        {
            if (string.IsNullOrWhiteSpace(action.ActionId))
            {
                throw new InvalidOperationException(
                    $"[QA Gate Violation] Action '{action.Title}' does not have a stable ActionId.");
            }

            if (!Enum.IsDefined(typeof(ResolutionMode), action.ResolutionMode) || (int)action.ResolutionMode == 0)
            {
                throw new InvalidOperationException(
                    $"[QA Gate Violation] Action '{action.ActionId}' has invalid ResolutionMode: {action.ResolutionMode}.");
            }
        }
    }

    public static void AssertNoGenericOutcomesForUnrelatedActions(ReportContext ctx)
    {
        var outcomeToActionIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var action in ctx.ActionPlan)
        {
            if (string.IsNullOrWhiteSpace(action.ExpectedResult)) continue;

            if (outcomeToActionIds.TryGetValue(action.ExpectedResult, out var existingActionId) &&
                !existingActionId.Equals(action.ActionId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"[QA Gate Violation] Actions '{existingActionId}' and '{action.ActionId}' reuse identical ExpectedResult outcome: '{action.ExpectedResult}'.");
            }
            outcomeToActionIds[action.ExpectedResult] = action.ActionId;
        }
    }

    public static void AssertSLSIsNotDescribedAsAudit(ReportContext ctx)
    {
        foreach (var action in ctx.ActionPlan)
        {
            if (action.WhatToDo.Contains("рекомендациями юридического аудита", StringComparison.OrdinalIgnoreCase) ||
                action.ExpectedResult.Contains("рекомендациями юридического аудита", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"[QA Gate Violation] Action '{action.ActionId}' incorrectly refers to SLS as 'юридический аудит'.");
            }
        }
    }
}
