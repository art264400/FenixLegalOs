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
            if (factStore.Facts.TryGetValue(rule.QuestionId, out var factVal) && factVal != null)
            {
                if (factVal is bool b)
                {
                    if (op == ConditionalOperator.In)
                        return RuleValueContains(rule.Value, b);
                    if (op == ConditionalOperator.NotIn)
                        return !RuleValueContains(rule.Value, b);

                    bool targetBool = false;
                    bool isBool = rule.Value is bool rb ? (targetBool = rb, true).Item2 : bool.TryParse(rule.Value?.ToString(), out targetBool);

                    if (isBool)
                    {
                        if (op == ConditionalOperator.Eq) return b == targetBool;
                        if (op == ConditionalOperator.Neq) return b != targetBool;
                    }
                    else
                    {
                        var bStr = b ? "true" : "false";
                        var rStr = rule.Value?.ToString() ?? "";
                        if (op == ConditionalOperator.Eq) return bStr.Equals(rStr, StringComparison.OrdinalIgnoreCase);
                        if (op == ConditionalOperator.Neq) return !bStr.Equals(rStr, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                }
                if (factVal is IEnumerable<string> strList)
                {
                    return op switch
                    {
                        ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                            strList.Any(x => RuleValueContains(rule.Value, x)),
                        ConditionalOperator.NotContains or ConditionalOperator.Neq or ConditionalOperator.NotIn =>
                            !strList.Any(x => RuleValueContains(rule.Value, x)),
                        _ => throw new InvalidOperationException($"Unsupported collection conditional operator: '{op}' for key '{rule.QuestionId}'")
                    };
                }
                return EvaluateOp(op, factVal.ToString() ?? "", rule.Value, factVal, rule.QuestionId);
            }

            // Fact is absent (null) in factStore
            return op switch
            {
                ConditionalOperator.Neq or ConditionalOperator.NotIn or ConditionalOperator.NotContains => true,
                _ => false
            };
        }

        if (!answers.TryGetValue(rule.QuestionId, out var rawVal) || rawVal == null)
        {
            return op switch
            {
                ConditionalOperator.Neq or ConditionalOperator.NotIn or ConditionalOperator.NotContains => true,
                _ => false
            };
        }

        if (rawVal is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var arrVals = new List<string>();
            foreach (var el in je.EnumerateArray()) arrVals.Add(el.ToString());
            return op switch
            {
                ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                    arrVals.Any(x => RuleValueContains(rule.Value, x)),
                ConditionalOperator.NotContains or ConditionalOperator.Neq or ConditionalOperator.NotIn =>
                    !arrVals.Any(x => RuleValueContains(rule.Value, x)),
                _ => throw new InvalidOperationException($"Unsupported array conditional operator: '{op}' for question '{rule.QuestionId}'")
            };
        }

        if (rawVal is IEnumerable<string> listVal)
        {
            return op switch
            {
                ConditionalOperator.Contains or ConditionalOperator.Eq or ConditionalOperator.In =>
                    listVal.Any(x => RuleValueContains(rule.Value, x)),
                ConditionalOperator.NotContains or ConditionalOperator.Neq or ConditionalOperator.NotIn =>
                    !listVal.Any(x => RuleValueContains(rule.Value, x)),
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

    private static bool RuleValueContains(object? ruleVal, object? val)
    {
        if (ruleVal == null || val == null) return false;

        if (ruleVal is IEnumerable<object> objEnum && ruleVal is not string)
        {
            foreach (var item in objEnum)
            {
                if (item is bool itemBool && val is bool valBool)
                {
                    if (itemBool == valBool) return true;
                }
                else if (string.Equals(item?.ToString(), val.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        if (ruleVal is IEnumerable<string> strEnum && ruleVal is not string)
        {
            var valStr = val.ToString() ?? "";
            return strEnum.Any(x => x.Equals(valStr, StringComparison.OrdinalIgnoreCase));
        }
        if (ruleVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (val is bool b && (item.ValueKind == JsonValueKind.True || item.ValueKind == JsonValueKind.False))
                    {
                        if (item.GetBoolean() == b) return true;
                    }
                    else if (item.ToString().Equals(val.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                return false;
            }
            if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                if (str.Contains(','))
                {
                    return str.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                        .Any(x => x.Equals(val.ToString(), StringComparison.OrdinalIgnoreCase));
                }
                return str.Equals(val.ToString(), StringComparison.OrdinalIgnoreCase);
            }
        }
        if (ruleVal is string s)
        {
            if (s.Contains(','))
            {
                return s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Any(x => x.Equals(val.ToString(), StringComparison.OrdinalIgnoreCase));
            }
            return s.Equals(val.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
