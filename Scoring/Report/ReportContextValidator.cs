using System;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class ReportContextValidator
{
    public static void Validate(ReportContext ctx)
    {
        if (ctx == null)
            throw new InvalidOperationException("[ReportContext Integrity] ReportContext is null.");

        // INV-1: Applicable Module Score
        foreach (var card in ctx.ModuleCards.Where(c => c.RenderMode != ReportRenderMode.NotApplicable))
        {
            if (!card.Score.HasValue)
                throw new InvalidOperationException($"[ReportContext Integrity] Applicable module '{card.SectionId}' has null Score.");
            if (card.Score.Value < 0 || card.Score.Value > 100)
                throw new InvalidOperationException($"[ReportContext Integrity] Applicable module '{card.SectionId}' has invalid Score {card.Score.Value}.");
        }

        // INV-2: N/A Module Integrity
        foreach (var card in ctx.ModuleCards.Where(c => c.RenderMode == ReportRenderMode.NotApplicable))
        {
            if (card.Score.HasValue)
                throw new InvalidOperationException($"[ReportContext Integrity] N/A module '{card.SectionId}' must not have a Score.");
        }

        // INV-3: FOCUS Section Correspondence
        var focusCardIds = ctx.ModuleCards.Where(c => c.RenderMode == ReportRenderMode.Focus).Select(c => c.SectionId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var focusDetailIds = ctx.FocusModules.Select(f => f.SectionId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!focusCardIds.SetEquals(focusDetailIds))
        {
            throw new InvalidOperationException($"[ReportContext Integrity] FOCUS module mismatch between ModuleCards [{string.Join(",", focusCardIds)}] and FocusModules [{string.Join(",", focusDetailIds)}].");
        }

        // INV-4: Status-Aware Fallback Narratives
        foreach (var comp in ctx.CompactModules)
        {
            if (comp.Score < 40 && comp.Summary.Contains("блокеров не выявлено", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"[ReportContext Integrity] Critical-tier module '{comp.SectionId}' (Score={comp.Score}) cannot have a 'no blockers' narrative.");
            }
        }

        // INV-5: Overall Score Invariant
        if (ctx.Overall.Score < 0 || ctx.Overall.Score > 100)
            throw new InvalidOperationException($"[ReportContext Integrity] Overall score {ctx.Overall.Score} is outside valid 0..100 range.");

        // INV-6: Data Completeness Invariant
        if (ctx.Overall.Confidence < 0 || ctx.Overall.Confidence > 100)
            throw new InvalidOperationException($"[ReportContext Integrity] Data completeness {ctx.Overall.Confidence} is outside valid 0..100% range.");

        // INV-7: Action Plan Integrity (No fake items)
        foreach (var item in ctx.ActionPlan)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.PriorityGroup))
            {
                throw new InvalidOperationException("[ReportContext Integrity] ActionPlan item must have non-empty Title and PriorityGroup.");
            }
        }

        // INV-8: Fenix Law Recommendation Consistency
        if (!ctx.FenixLaw.RequiresLegalWork && ctx.FenixLaw.ServiceCards.Count > 0)
        {
            throw new InvalidOperationException("[ReportContext Integrity] ServiceCards cannot be present when RequiresLegalWork is false.");
        }

        // INV-9: Project Profile Semantic Coherence
        var entityFact = ctx.Profile.KeyFacts.FirstOrDefault(f => f.Key == "entity");
        if (entityFact != null && entityFact.Value.Contains("Не зарегистрировано", StringComparison.OrdinalIgnoreCase))
        {
            if (ctx.Profile.ConfigurationNarrative.Contains("осуществляет деятельность через структуру", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("[ReportContext Integrity] Profile narrative contradicts 'Unregistered entity' fact.");
            }
        }

        // INV-10: Legal Terms Non-Empty
        if (string.IsNullOrWhiteSpace(ctx.LegalTerms?.MethodologyText))
            throw new InvalidOperationException("[ReportContext Integrity] LegalTerms.MethodologyText must not be empty.");
        if (string.IsNullOrWhiteSpace(ctx.LegalTerms?.DisclaimerText))
            throw new InvalidOperationException("[ReportContext Integrity] LegalTerms.DisclaimerText must not be empty.");

        // INV-11: Overall Low Score & Finding Coherence
        if (ctx.Overall.Score < 60 && ctx.FocusModules.Any(f => f.Findings.Count > 0))
        {
            if (ctx.ActionPlan.Count == 0)
            {
                throw new InvalidOperationException("[ReportContext Integrity] Overall score < 60 with active focus findings must produce a non-empty ActionPlan.");
            }
        }

        // INV-12: Low Score Module Explanation
        foreach (var focus in ctx.FocusModules.Where(f => f.Score < 40))
        {
            if (focus.Findings.Count == 0 && focus.NegativeDrivers.Count == 0 && focus.FactorBreakdown.Count == 0)
            {
                throw new InvalidOperationException($"[ReportContext Integrity] Low-score focus module '{focus.SectionId}' (Score={focus.Score}) has no findings or negative drivers explaining the score.");
            }
        }

        // INV-13: Legal Work Consistency
        var hasLawyerRequiredRisks = ctx.FocusModules
            .SelectMany(f => f.Findings)
            .Any(f => (f.Severity is RiskSeverity.Critical or RiskSeverity.Blocker or RiskSeverity.High) &&
                      f.ResolutionFormat.Contains("Требуется юридическая работа", StringComparison.OrdinalIgnoreCase));

        if (hasLawyerRequiredRisks && !ctx.FenixLaw.RequiresLegalWork)
        {
            throw new InvalidOperationException("[ReportContext Integrity] High/Critical findings requiring legal work exist, but FenixLaw.RequiresLegalWork is false.");
        }
    }
}
