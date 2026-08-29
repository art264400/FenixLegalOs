using System.Text.Json;
using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Core;

public class ConditionsEvaluator
{
    public static bool IsVisible(List<ConditionalRule>? rules, Dictionary<string, object> answers, SharedFactStore? factStore = null)
    {
        if (rules == null || rules.Count == 0) return true;
        return rules.All(r => EvaluateRule(r, answers, factStore));
    }

    public static bool EvaluateRule(ConditionalRule rule, Dictionary<string, object> answers, SharedFactStore? factStore = null)
    {
        if (rule.All != null && rule.All.Count > 0)
            return rule.All.All(r => EvaluateRule(r, answers, factStore));

        if (rule.Any != null && rule.Any.Count > 0)
            return rule.Any.Any(r => EvaluateRule(r, answers, factStore));

        if (string.IsNullOrEmpty(rule.QuestionId)) return true;

        // Check if QuestionId refers to a FactStore key
        if (factStore != null && (rule.QuestionId.Contains('.') || factStore.Facts.ContainsKey(rule.QuestionId)))
        {
            if (factStore.Facts.TryGetValue(rule.QuestionId, out var factVal))
            {
                if (factVal is bool b && bool.TryParse(rule.Value?.ToString(), out var targetBool))
                {
                    return rule.Op switch
                    {
                        "eq" or null => b == targetBool,
                        "neq" => b != targetBool,
                        _ => throw new InvalidOperationException($"Unsupported boolean conditional operator: '{rule.Op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal is IEnumerable<string> strList)
                {
                    var targetStr = rule.Value?.ToString() ?? "";
                    return rule.Op switch
                    {
                        "contains" or "eq" or "in" => strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        "notContains" or "neq" => !strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        _ => throw new InvalidOperationException($"Unsupported collection conditional operator: '{rule.Op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal != null)
                {
                    return EvaluateOp(rule.Op, factVal.ToString() ?? "", rule.Value, factVal, rule.QuestionId);
                }
            }
        }

        if (!answers.TryGetValue(rule.QuestionId, out var rawVal) || rawVal == null)
            return rule.Op == "neq" || rule.Op == "notIn" || rule.Op == "notContains";

        if (rawVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var arrVals = new List<string>();
            foreach (var el in je.EnumerateArray()) arrVals.Add(el.ToString());
            var targetStr = rule.Value?.ToString() ?? "";
            return rule.Op switch
            {
                "contains" or "eq" or "in" => arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                "notContains" or "neq" => !arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported array conditional operator: '{rule.Op}' for question '{rule.QuestionId}'")
            };
        }

        if (rawVal is IEnumerable<string> listVal)
        {
            var targetStr = rule.Value?.ToString() ?? "";
            return rule.Op switch
            {
                "contains" or "eq" or "in" => listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                "notContains" or "neq" => !listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported list conditional operator: '{rule.Op}' for question '{rule.QuestionId}'")
            };
        }

        var valStr = rawVal.ToString() ?? "";
        return EvaluateOp(rule.Op, valStr, rule.Value, rawVal, rule.QuestionId);
    }

    private static bool EvaluateOp(string? op, string valStr, object? ruleValue, object? rawVal, string questionId)
    {
        return op switch
        {
            "eq" => valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            "neq" => !valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            "in" => RuleValueContains(ruleValue, valStr),
            "notIn" => !RuleValueContains(ruleValue, valStr),
            "contains" => valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            "notContains" => !valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            "answered" => !string.IsNullOrEmpty(valStr),
            "gte" => double.TryParse(valStr, out var v1) && double.TryParse(ruleValue?.ToString(), out var v2) && v1 >= v2,
            "lte" => double.TryParse(valStr, out var v3) && double.TryParse(ruleValue?.ToString(), out var v4) && v3 <= v4,
            _ => throw new InvalidOperationException($"Unsupported conditional operator: '{op}' for question '{questionId}'")
        };
    }

    private static bool RuleValueContains(object? ruleVal, string val)
    {
        if (ruleVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ToString().Equals(val, StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                if (str.Contains(','))
                {
                    return str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
                }
                return str.Equals(val, StringComparison.OrdinalIgnoreCase);
            }
        }
        if (ruleVal is List<string> list)
        {
            return list.Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
        }
        if (ruleVal is string s)
        {
            if (s.Contains(','))
            {
                return s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(x => x.Equals(val, StringComparison.OrdinalIgnoreCase));
            }
            return s.Equals(val, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
