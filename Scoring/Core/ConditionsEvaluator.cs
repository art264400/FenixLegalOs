using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

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

        var op = rule.Op ?? ConditionalOperator.Eq;

        // Check if QuestionId refers to a FactStore key
        if (factStore != null && (rule.QuestionId.Contains('.') || factStore.Facts.ContainsKey(rule.QuestionId)))
        {
            if (factStore.Facts.TryGetValue(rule.QuestionId, out var factVal))
            {
                if (factVal is bool b && bool.TryParse(rule.Value?.ToString(), out var targetBool))
                {
                    return op switch
                    {
                        ConditionalOperator.Eq => b == targetBool,
                        ConditionalOperator.Neq => b != targetBool,
                        _ => throw new InvalidOperationException($"Unsupported boolean conditional operator: '{op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal is IEnumerable<string> strList)
                {
                    var targetStr = rule.Value?.ToString() ?? "";
                    return op switch
                    {
                        ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                            strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        ConditionalOperator.NotContains or ConditionalOperator.Neq =>
                            !strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        _ => throw new InvalidOperationException($"Unsupported collection conditional operator: '{op}' for key '{rule.QuestionId}'")
                    };
                }
                if (factVal != null)
                {
                    return EvaluateOp(op, factVal.ToString() ?? "", rule.Value, factVal, rule.QuestionId);
                }
            }
        }

        if (!answers.TryGetValue(rule.QuestionId, out var rawVal) || rawVal == null)
            return false;

        if (rawVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var arrVals = new List<string>();
            foreach (var el in je.EnumerateArray()) arrVals.Add(el.ToString());
            var targetStr = rule.Value?.ToString() ?? "";
            return op switch
            {
                ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                    arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                ConditionalOperator.NotContains or ConditionalOperator.Neq =>
                    !arrVals.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported array conditional operator: '{op}' for question '{rule.QuestionId}'")
            };
        }

        if (rawVal is IEnumerable<string> listVal)
        {
            var targetStr = rule.Value?.ToString() ?? "";
            return op switch
            {
                ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                    listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                ConditionalOperator.NotContains or ConditionalOperator.Neq =>
                    !listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => throw new InvalidOperationException($"Unsupported list conditional operator: '{op}' for question '{rule.QuestionId}'")
            };
        }

        var valStr = rawVal.ToString() ?? "";
        return EvaluateOp(op, valStr, rule.Value, rawVal, rule.QuestionId);
    }

    private static bool EvaluateOp(ConditionalOperator op, string valStr, object? ruleValue, object? rawVal, string questionId)
    {
        return op switch
        {
            ConditionalOperator.Eq => valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            ConditionalOperator.Neq => !valStr.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase),
            ConditionalOperator.In => RuleValueContains(ruleValue, valStr),
            ConditionalOperator.NotIn => !RuleValueContains(ruleValue, valStr),
            ConditionalOperator.Contains => valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            ConditionalOperator.NotContains => !valStr.Split(',', StringSplitOptions.TrimEntries).Any(x => x.Equals(ruleValue?.ToString(), StringComparison.OrdinalIgnoreCase)),
            ConditionalOperator.Answered => !string.IsNullOrEmpty(valStr),
            ConditionalOperator.Gte => double.TryParse(valStr, out var v1) && double.TryParse(ruleValue?.ToString(), out var v2) && v1 >= v2,
            ConditionalOperator.Lte => double.TryParse(valStr, out var v3) && double.TryParse(ruleValue?.ToString(), out var v4) && v3 <= v4,
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
