using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;

namespace FenixLegalOs.Services;

public class ConditionsEvaluator
{
    public static bool IsVisible(List<ConditionalRule>? rules, Dictionary<string, object> answers)
    {
        if (rules == null || rules.Count == 0) return true;
        return rules.All(r => EvaluateRule(r, answers));
    }

    public static bool EvaluateRule(ConditionalRule rule, Dictionary<string, object> answers)
    {
        if (rule.All != null && rule.All.Count > 0)
            return rule.All.All(r => EvaluateRule(r, answers));

        if (rule.Any != null && rule.Any.Count > 0)
            return rule.Any.Any(r => EvaluateRule(r, answers));

        if (string.IsNullOrEmpty(rule.QuestionId)) return true;

        if (!answers.TryGetValue(rule.QuestionId, out var rawVal) || rawVal == null)
            return rule.Op == "neq";

        var valStr = rawVal.ToString() ?? "";

        return rule.Op switch
        {
            "eq" => valStr.Equals(rule.Value?.ToString(), StringComparison.OrdinalIgnoreCase),
            "neq" => !valStr.Equals(rule.Value?.ToString(), StringComparison.OrdinalIgnoreCase),
            "in" => RuleValueContains(rule.Value, valStr),
            "notIn" => !RuleValueContains(rule.Value, valStr),
            "answered" => !string.IsNullOrEmpty(valStr),
            _ => true
        };
    }

    private static bool RuleValueContains(object? ruleVal, string val)
    {
        if (ruleVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ToString().Equals(val, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        if (ruleVal is List<string> list)
        {
            return list.Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
        }
        return false;
    }
}

public class ScoringEngine
{
    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var visibleQs = DataBank.Questions
            .Where(q => q.Enabled)
            .Where(q => ConditionsEvaluator.IsVisible(q.ShowIf, answers))
            .ToList();

        var sections = DataBank.Sections.Select(s =>
        {
            var qs = visibleQs.Where(q => q.SectionId == s.Id && q.Weight > 0).ToList();
            double weightSum = 0;
            double scoreSum = 0;

            foreach (var q in qs)
            {
                if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null) continue;
                var sc = GetAnswerScore(q, ansVal);
                if (sc == null) continue;
                weightSum += q.Weight;
                scoreSum += sc.Value * q.Weight;
            }

            int? finalScore = weightSum > 0 ? (int)Math.Round((scoreSum / weightSum) * 100) : null;
            return new SectionScore { SectionId = s.Id, Title = s.Title, Score = finalScore, Weight = s.Weight };
        }).ToList();

        var applicable = sections.Where(s => s.Score.HasValue).ToList();
        double totalWeight = applicable.Sum(s => s.Weight);
        int overall = totalWeight > 0
            ? (int)Math.Round(applicable.Sum(s => s.Score!.Value * s.Weight) / totalWeight)
            : 0;

        var riskMap = CollectOptionRisks(visibleQs, answers);

        var risks = riskMap.Values.OrderBy(r => GetSeverityOrder(r.Severity)).ToList();
        var level = GetLevel(overall);

        return new ScoreResult
        {
            Overall = overall,
            Level = level,
            LevelTitle = GetLevelTitle(level),
            LevelText = GetLevelText(level),
            Sections = sections,
            Risks = risks,
            CriticalCount = risks.Count(r => r.Severity == "critical"),
            HighCount = risks.Count(r => r.Severity == "high"),
            MediumCount = risks.Count(r => r.Severity == "medium"),
            Strengths = applicable.Where(s => s.Score >= 75).Select(s => s.Title).ToList(),
            AnsweredCount = visibleQs.Count(q => answers.ContainsKey(q.Id)),
            Versions = new ScoreVersions(),
            ComputedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private double? GetAnswerScore(DiagnosticQuestion q, object answer)
    {
        if (q.Options == null || q.Options.Count == 0) return null;
        var str = answer.ToString();
        var opt = q.Options.FirstOrDefault(o => o.Id == str);
        return opt?.Score;
    }

    private Dictionary<string, RiskFinding> CollectOptionRisks(List<DiagnosticQuestion> visibleQs, Dictionary<string, object> answers)
    {
        var found = new Dictionary<string, RiskFinding>();
        foreach (var q in visibleQs)
        {
            if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null || q.Options == null) continue;
            var strVal = ansVal.ToString();
            var opt = q.Options.FirstOrDefault(o => o.Id == strVal);
            if (opt?.RiskCode != null && !found.ContainsKey(opt.RiskCode))
            {
                var def = DataBank.Risks.FirstOrDefault(r => r.Code == opt.RiskCode);
                if (def != null)
                {
                    found[def.Code] = new RiskFinding
                    {
                        Code = def.Code, Severity = def.Severity, SectionId = def.SectionId,
                        Title = def.Title, Finding = def.Finding, WhyItMatters = def.WhyItMatters,
                        Recommendation = def.Recommendation, Resolution = def.Resolution, Cta = def.Cta
                    };
                }
            }
        }
        return found;
    }

    private int GetSeverityOrder(string sev) => sev switch { "critical" => 0, "high" => 1, _ => 2 };

    private string GetLevel(int score) => score switch
    {
        >= 80 => "strong",
        >= 60 => "attention",
        >= 40 => "material_gaps",
        _ => "structural_risks"
    };

    private string GetLevelTitle(string level) => level switch
    {
        "strong" => "Сильная основа",
        "attention" => "Есть вопросы, требующие внимания",
        "material_gaps" => "Существенные пробелы",
        _ => "Структурные вопросы"
    };

    private string GetLevelText(string level) => level switch
    {
        "strong" => "Ваша компания имеет относительно сильную юридическую основу.",
        "attention" => "Основа сформирована частично. Некоторые вопросы требуют внимания.",
        "material_gaps" => "Диагностика выявила несколько значимых пробелов в юридической конструкции.",
        _ => "Юридическая основа бизнеса пока сформирована фрагментарно."
    };
}
