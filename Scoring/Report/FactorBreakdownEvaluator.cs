using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class FactorBreakdownEvaluator
{
    public static List<PositiveFactorDto> ExtractGlobalPositiveFactors(ScoreResult result)
    {
        var positiveFactors = new List<PositiveFactorDto>();
        var seenNormalized = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. From deterministic Strengths
        foreach (var s in result.Strengths)
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            if (IsStrengthConflictedWithRisks(s, result.Risks)) continue;

            var norm = s.Replace("Выстроено направление:", "").Replace("Выстроено:", "").Trim();
            if (seenNormalized.Add(norm))
            {
                positiveFactors.Add(new PositiveFactorDto
                {
                    Title = s,
                    Category = "General",
                    Icon = "check_circle"
                });
            }
        }

        // 2. High-score dimensions (>= 80 score with no findings in section and no conflicting risks)
        if (positiveFactors.Count < 6)
        {
            foreach (var section in result.Sections.Where(sec => sec.Status == ApplicabilityStatus.Applicable))
            {
                var sectionRisks = result.Risks.Where(r => r.SectionId.Equals(section.SectionId, StringComparison.OrdinalIgnoreCase)).ToList();
                if (sectionRisks.Any(r => r.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High))
                    continue; // Do not pull positive factors from deeply compromised sections

                foreach (var dim in section.Dimensions.Where(d => d.Score >= 80))
                {
                    if (Core.StrongAreasCalculator.IsDimensionConflictedWithRisks(dim.DimensionId, result.Risks))
                        continue;

                    var dimTitle = GetDimensionTitle(dim.DimensionId);
                    var norm = dimTitle.Replace("Выстроено направление:", "").Replace("Выстроено:", "").Trim();
                    if (seenNormalized.Add(norm))
                    {
                        positiveFactors.Add(new PositiveFactorDto
                        {
                            Title = dimTitle,
                            Category = section.SectionId,
                            Icon = "check_circle"
                        });
                    }
                    if (positiveFactors.Count >= 6) break;
                }
                if (positiveFactors.Count >= 6) break;
            }
        }

        return positiveFactors.Take(6).ToList();
    }

    public static (List<string> Negative, List<string> Attention, List<string> Positive, List<FactorBreakdownRowDto> Table) 
        EvaluateSectionDriversAndTable(SectionScore section, List<RiskFinding> sectionRisks)
    {
        var negative = new List<string>();
        var attention = new List<string>();
        var positive = new List<string>();
        var table = new List<FactorBreakdownRowDto>();

        // 1. Drivers from detected findings
        foreach (var risk in sectionRisks)
        {
            if (risk.Severity is RiskSeverity.Critical or RiskSeverity.Blocker or RiskSeverity.High)
            {
                negative.Add(risk.Title);
            }
            else if (risk.Severity == RiskSeverity.Medium)
            {
                attention.Add(risk.Title);
            }
        }

        // 2. Factor breakdown table from dimensions
        foreach (var dim in section.Dimensions.OrderBy(d => d.Score))
        {
            var factorName = GetDimensionTitle(dim.DimensionId);
            var dimRisks = sectionRisks.Where(r => r.AffectedDimensions.Contains(dim.DimensionId)).ToList();
            var maxDimSev = dimRisks.Count > 0 ? dimRisks.Max(r => r.Severity) : (RiskSeverity?)null;

            string statusText;
            string impactLevel;
            bool isPositive = false;

            // Status reflects actual detected risk severity or factor health status
            if (maxDimSev.HasValue)
            {
                statusText = maxDimSev.Value switch
                {
                    RiskSeverity.Blocker => "Блокирующий",
                    RiskSeverity.Critical => "Критический",
                    RiskSeverity.High => "Высокий",
                    RiskSeverity.Medium => "Умеренный",
                    _ => "Низкий"
                };
                impactLevel = maxDimSev.Value is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High ? "Высокое" : "Среднее";
            }
            else
            {
                // No active risk detected: reflect factor health status
                if (dim.Score >= 80)
                {
                    statusText = "В норме";
                    impactLevel = "Положительное";
                    isPositive = true;
                    positive.Add(factorName);
                }
                else if (dim.Score >= 60)
                {
                    statusText = "Требует доработки";
                    impactLevel = "Среднее";
                }
                else
                {
                    statusText = "Не оформлено";
                    impactLevel = "Высокое";
                }
            }

            table.Add(new FactorBreakdownRowDto
            {
                FactorName = factorName,
                StatusText = statusText,
                ImpactLevel = impactLevel,
                Severity = maxDimSev,
                IsPositive = isPositive
            });
        }

        if (negative.Count == 0 && sectionRisks.Count == 0 && (section.Score ?? 100) < 60)
        {
            negative.Add($"Низкий уровень юридической готовности по направлению «{section.Title}».");
        }

        return (
            negative.Distinct().ToList(),
            attention.Distinct().ToList(),
            positive.Distinct().ToList(),
            table
        );
    }

    public static string GetDimensionTitle(string dimensionId)
    {
        var name = DataBank.GetDimensionDisplayName(dimensionId);
        if (string.IsNullOrWhiteSpace(name) || name.Equals(dimensionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"[Dimension Configuration] Dimension '{dimensionId}' has no valid Russian DisplayName in DataBank.");
        }
        return name;
    }

    public static bool IsStrengthConflictedWithRisks(string title, IEnumerable<RiskFinding> findings)
    {
        if (findings == null || string.IsNullOrWhiteSpace(title)) return false;

        var severeRisks = findings
            .Where(r => r.Severity is RiskSeverity.Critical or RiskSeverity.High or RiskSeverity.Blocker)
            .ToList();

        if (title.Contains("Отсутствие споров", StringComparison.OrdinalIgnoreCase) ||
            (title.Contains("спор", StringComparison.OrdinalIgnoreCase) && !title.Contains("интерес", StringComparison.OrdinalIgnoreCase)))
        {
            if (severeRisks.Any(r => r.RootCauseGroup is "FOUNDER_CONFLICT" or "OWNERSHIP" or "FOUNDER_EQUITY" ||
                                     (r.Code != null && r.Code.Contains("DISPUTE", StringComparison.OrdinalIgnoreCase))))
                return true;
        }

        if (title.Contains("конфликт", StringComparison.OrdinalIgnoreCase) && title.Contains("интерес", StringComparison.OrdinalIgnoreCase))
        {
            if (severeRisks.Any(r => r.Code != null && (r.Code.Contains("OUTSIDE", StringComparison.OrdinalIgnoreCase) || r.Code.Contains("CONFLICT_OF_INTEREST", StringComparison.OrdinalIgnoreCase))))
                return true;
        }

        if (title.Contains("Распределение долей", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Соответствие долей", StringComparison.OrdinalIgnoreCase) ||
            (title.Contains("структур", StringComparison.OrdinalIgnoreCase) && title.Contains("капитал", StringComparison.OrdinalIgnoreCase)))
        {
            if (severeRisks.Any(r => r.RootCauseGroup is "FOUNDER_EQUITY" or "OWNERSHIP" or "EQUITY_PROMISE" ||
                                     (r.Code != null && (r.Code.Contains("CAP_TABLE", StringComparison.OrdinalIgnoreCase) || r.Code.Contains("EQUITY", StringComparison.OrdinalIgnoreCase)))))
                return true;
        }

        if (title.Contains("Оформление ИС", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Передача прав", StringComparison.OrdinalIgnoreCase) ||
            title.Contains("Интеллектуальная собственность", StringComparison.OrdinalIgnoreCase))
        {
            if (severeRisks.Any(r => r.RootCauseGroup is "IP_OWNERSHIP" or "IP_ASSIGNMENT" ||
                                     (r.Code != null && r.Code.StartsWith("IP_", StringComparison.OrdinalIgnoreCase))))
                return true;
        }

        return false;
    }
}
