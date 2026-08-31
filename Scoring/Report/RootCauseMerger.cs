using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class RootCauseMerger
{
    public static List<TopFindingSummaryDto> ExtractTopRootCauses(List<RiskFinding> findings, int maxCount = 5)
    {
        var topList = new List<TopFindingSummaryDto>();
        if (findings == null || findings.Count == 0) return topList;

        // Group findings by canonical RootCauseGroup (or Code if group is unset)
        var groups = findings
            .GroupBy(f => !string.IsNullOrWhiteSpace(f.RootCauseGroup) ? f.RootCauseGroup : f.Code, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var groupFindings = g.OrderByDescending(f => f.Severity switch
                {
                    RiskSeverity.Blocker => 4,
                    RiskSeverity.Critical => 3,
                    RiskSeverity.High => 2,
                    RiskSeverity.Medium => 1,
                    _ => 0
                }).ThenByDescending(f => f.Priority == RiskPriority.Now ? 2 : f.Priority == RiskPriority.BeforeRound ? 1 : 0).ToList();

                var dominant = groupFindings.First();
                return new
                {
                    RootCauseKey = g.Key,
                    DominantFinding = dominant,
                    AllFindings = groupFindings,
                    MaxSeverity = dominant.Severity,
                    Priority = dominant.Priority
                };
            })
            // Ordered deterministic ranking of root-cause clusters
            .OrderByDescending(g => g.MaxSeverity switch
            {
                RiskSeverity.Blocker => 4,
                RiskSeverity.Critical => 3,
                RiskSeverity.High => 2,
                RiskSeverity.Medium => 1,
                _ => 0
            })
            .ThenByDescending(g => g.Priority == RiskPriority.Now ? 2 : g.Priority == RiskPriority.BeforeRound ? 1 : 0)
            .Take(Math.Min(maxCount, 5))
            .ToList();

        int idx = 1;
        foreach (var grp in groups)
        {
            var dom = grp.DominantFinding;
            var sevLabel = dom.Severity switch
            {
                RiskSeverity.Blocker => "Блокирующий риск",
                RiskSeverity.Critical => "Критический риск",
                RiskSeverity.High => "Высокий риск",
                RiskSeverity.Medium => "Умеренный риск",
                _ => "Низкий риск"
            };

            // Summary derived directly from canonical dominant finding without truncation
            var summary = !string.IsNullOrWhiteSpace(dom.Finding) ? dom.Finding : dom.WhyItMatters;

            topList.Add(new TopFindingSummaryDto
            {
                Index = idx++,
                FindingCode = dom.Code,
                RootCauseCode = grp.RootCauseKey,
                Title = dom.Title,
                Severity = dom.Severity,
                SeverityLabel = sevLabel,
                ShortSummary = summary,
                DetailSectionId = dom.SectionId
            });
        }

        return topList;
    }
}
