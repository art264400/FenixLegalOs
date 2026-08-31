using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class RenderModeClassifier
{
    public static Dictionary<string, ReportRenderMode> ClassifyRenderModes(ScoreResult result)
    {
        var modes = new Dictionary<string, ReportRenderMode>(StringComparer.OrdinalIgnoreCase);
        var applicableCandidates = new List<ModuleCandidate>();

        foreach (var s in result.Sections)
        {
            if (s.Status != ApplicabilityStatus.Applicable)
            {
                modes[s.SectionId] = ReportRenderMode.NotApplicable;
                continue;
            }

            var sectionRisks = result.Risks.Where(r => r.SectionId.Equals(s.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
            bool hasBlocker = sectionRisks.Any(r => r.Severity == RiskSeverity.Blocker);
            int critCount = sectionRisks.Count(r => r.Severity == RiskSeverity.Critical);
            int highCount = sectionRisks.Count(r => r.Severity == RiskSeverity.High);
            int scoreVal = s.Score ?? 100;
            double scoreDrag = (100.0 - scoreVal) * (s.Weight > 0 ? s.Weight : 1.0);

            // A module qualifies for FOCUS if it exhibits real legal vulnerabilities
            bool hasVulnerabilities = hasBlocker || critCount > 0 || highCount > 0 || scoreVal < 80;

            applicableCandidates.Add(new ModuleCandidate
            {
                Section = s,
                HasBlocker = hasBlocker,
                CriticalCount = critCount,
                HighCount = highCount,
                ActiveFindingCount = sectionRisks.Count,
                ScoreDrag = scoreDrag,
                Score = scoreVal,
                HasVulnerabilities = hasVulnerabilities
            });
        }

        // Ordered deterministic ranking based on canonical engine outputs:
        // 1. Primary Top Drivers of the report (strongest score drag)
        // 2. Investment Blocker
        // 3. Count of Critical findings
        // 4. Count of High findings
        // 5. Overall score drag (Score deficit * Section weight)
        // 6. Lower module Score
        // Note: 'investment' section deep-dive is dedicated to Section 10 and is not duplicated in FOCUS
        // Determine top score drivers (e.g. lowest score / highest score drag among modules with vulnerabilities)
        var topDriverSectionIds = applicableCandidates
            .Where(c => c.Score < 80 || c.HasVulnerabilities)
            .OrderByDescending(c => c.ScoreDrag)
            .ThenBy(c => c.Score)
            .Take(3)
            .Select(c => c.Section.SectionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // A module receives detailed FOCUS analysis whenever explainability requires it:
        // A. It contains a Blocker finding
        // B. It contains a Critical finding
        // C. It contains a High finding
        // D. It is an Overall TopDriver
        // E. It contains active findings requiring remediation
        // F. Its score reflects readiness deficits (< 80)
        // (Note: 'investment' section deep-dive is dedicated to Section 10/11 and is not duplicated in FOCUS)
        foreach (var candidate in applicableCandidates)
        {
            var secId = candidate.Section.SectionId;
            if (secId.Equals("investment", StringComparison.OrdinalIgnoreCase))
            {
                // Investment has a dedicated specialized section, so not rendered in generic Focus loop
                modes[secId] = ReportRenderMode.Compact;
                continue;
            }

            bool requiresFocus = candidate.HasBlocker ||
                                 candidate.CriticalCount > 0 ||
                                 candidate.HighCount > 0 ||
                                 candidate.ActiveFindingCount > 0 ||
                                 topDriverSectionIds.Contains(secId) ||
                                 candidate.Score < 80;

            modes[secId] = requiresFocus ? ReportRenderMode.Focus : ReportRenderMode.Compact;
        }

        return modes;
    }

    private class ModuleCandidate
    {
        public SectionScore Section { get; set; } = null!;
        public bool HasBlocker { get; set; }
        public int CriticalCount { get; set; }
        public int HighCount { get; set; }
        public int ActiveFindingCount { get; set; }
        public double ScoreDrag { get; set; }
        public int Score { get; set; }
        public bool HasVulnerabilities { get; set; }
    }
}
