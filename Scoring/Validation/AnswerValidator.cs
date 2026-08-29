using System.Text.Json;
using System.Text.RegularExpressions;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Core;

namespace FenixLegalOs.Scoring.Validation;

public class AnswerValidator
{
    private static readonly HashSet<string> AllowedJurisdictions = new(StringComparer.OrdinalIgnoreCase)
    {
        "kz", "aifc", "us", "uae", "cy", "sg", "uk", "eu", "kg", "uz", "other"
    };

    public static ValidationResult Validate(
        IReadOnlyDictionary<string, object> answers,
        IReadOnlyList<DiagnosticQuestion> allQuestions)
    {
        var result = new ValidationResult();
        var questionsById = allQuestions.ToDictionary(q => q.Id, q => q, StringComparer.OrdinalIgnoreCase);

        foreach (var (qId, rawVal) in answers)
        {
            if (!questionsById.TryGetValue(qId, out var q))
            {
                result.AddError(qId, "UNKNOWN_QUESTION", $"Question '{qId}' does not exist in the Question Bank.", rawVal);
                continue;
            }

            if (rawVal == null)
            {
                result.AddError(qId, "NULL_VALUE", $"Answer for '{qId}' cannot be null.", null);
                continue;
            }

            if (rawVal is string s && string.IsNullOrWhiteSpace(s))
            {
                result.AddError(qId, "EMPTY_VALUE", $"Answer for '{qId}' cannot be empty or whitespace.", s);
                continue;
            }

            if (rawVal is JsonElement jeNull && (jeNull.ValueKind == JsonValueKind.Null || jeNull.ValueKind == JsonValueKind.Undefined))
            {
                result.AddError(qId, "NULL_VALUE", $"Answer for '{qId}' cannot be null JSON.", null);
                continue;
            }

            switch (q.Type?.ToLowerInvariant())
            {
                case "single":
                    ValidateSingle(q, rawVal, result);
                    break;

                case "multiple":
                    ValidateMultiple(q, rawVal, result);
                    break;

                case "equity_split" or "equity_inputs":
                    ValidateEquitySplit(q, rawVal, result);
                    break;

                case "entity_builder":
                    ValidateEntityBuilder(q, rawVal, result);
                    break;

                default:
                    // Fallback to single option matching if options are present
                    if (q.Options != null && q.Options.Count > 0)
                    {
                        ValidateSingle(q, rawVal, result);
                    }
                    break;
            }
        }

        return result;
    }

    private static void ValidateSingle(DiagnosticQuestion q, object rawVal, ValidationResult result)
    {
        string? valStr = null;
        if (rawVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.String) valStr = je.GetString();
            else if (je.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) valStr = je.ToString();
            else
            {
                result.AddError(q.Id, "INVALID_TYPE", $"Question '{q.Id}' expects a scalar single-choice answer, got JSON {je.ValueKind}.", rawVal);
                return;
            }
        }
        else
        {
            valStr = rawVal.ToString();
        }

        if (string.IsNullOrWhiteSpace(valStr))
        {
            result.AddError(q.Id, "EMPTY_VALUE", $"Answer for '{q.Id}' cannot be empty or whitespace.", rawVal);
            return;
        }

        var allowedOptions = q.Options?.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        if (allowedOptions.Count > 0 && !allowedOptions.Contains(valStr))
        {
            result.AddError(q.Id, "INVALID_OPTION", $"Value '{valStr}' is not a valid option for question '{q.Id}'. Allowed: [{string.Join(", ", allowedOptions)}]", valStr);
        }
    }

    private static void ValidateMultiple(DiagnosticQuestion q, object rawVal, ValidationResult result)
    {
        var items = new List<string>();

        if (rawVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in je.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String) items.Add(el.GetString() ?? "");
                    else items.Add(el.ToString());
                }
            }
            else if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                if (!string.IsNullOrWhiteSpace(str)) items.Add(str);
            }
            else
            {
                result.AddError(q.Id, "INVALID_TYPE", $"Question '{q.Id}' expects a multi-select array, got JSON {je.ValueKind}.", rawVal);
                return;
            }
        }
        else if (rawVal is IEnumerable<string> strEnum)
        {
            items.AddRange(strEnum);
        }
        else if (rawVal is string singleStr)
        {
            if (!string.IsNullOrWhiteSpace(singleStr)) items.Add(singleStr);
        }
        else
        {
            result.AddError(q.Id, "INVALID_TYPE", $"Question '{q.Id}' expects a multi-select array, got {rawVal.GetType().Name}.", rawVal);
            return;
        }

        if (items.Count == 0)
        {
            result.AddError(q.Id, "EMPTY_SELECTION", $"Multi-select question '{q.Id}' must contain at least one selection.", rawVal);
            return;
        }

        var allowedOptions = q.Options?.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                result.AddError(q.Id, "EMPTY_ITEM", $"Multi-select question '{q.Id}' contains empty item.", items);
                continue;
            }

            if (allowedOptions.Count > 0 && !allowedOptions.Contains(item))
            {
                result.AddError(q.Id, "INVALID_OPTION", $"Item '{item}' is not a valid option for question '{q.Id}'. Allowed: [{string.Join(", ", allowedOptions)}]", item);
            }
        }

        // Check for mutually exclusive options like 'none'
        if (items.Count > 1)
        {
            bool hasNone = items.Any(x => x.Equals("none", StringComparison.OrdinalIgnoreCase));
            if (hasNone)
            {
                result.AddError(q.Id, "MUTUALLY_EXCLUSIVE_CONFLICT", $"Option 'none' in '{q.Id}' cannot be combined with other selections.", items);
            }
        }
    }

    private static void ValidateEquitySplit(DiagnosticQuestion q, object rawVal, ValidationResult result)
    {
        var shares = new List<double>();

        if (rawVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.TryGetDouble(out var d)) shares.Add(d);
                    else if (double.TryParse(item.GetString(), out var ps)) shares.Add(ps);
                    else
                    {
                        result.AddError(q.Id, "INVALID_NUMBER", $"Element '{item}' in shares array is not a valid number.", item.ToString());
                    }
                }
            }
            else if (je.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in je.EnumerateObject())
                {
                    if (prop.Value.TryGetDouble(out var d)) shares.Add(d);
                    else if (double.TryParse(prop.Value.GetString(), out var ps)) shares.Add(ps);
                    else
                    {
                        result.AddError(q.Id, "INVALID_NUMBER", $"Share value for '{prop.Name}' is not a valid number.", prop.Value.ToString());
                    }
                }
            }
            else if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString() ?? "";
                ExtractSharesFromString(str, shares);
            }
            else
            {
                result.AddError(q.Id, "INVALID_TYPE", $"Equity split '{q.Id}' expects array, object, or string of shares.", rawVal);
                return;
            }
        }
        else if (rawVal is IEnumerable<double> enumDouble)
        {
            shares.AddRange(enumDouble);
        }
        else if (rawVal is IEnumerable<int> enumInt)
        {
            shares.AddRange(enumInt.Select(i => (double)i));
        }
        else if (rawVal is IDictionary<string, double> dictDouble)
        {
            shares.AddRange(dictDouble.Values);
        }
        else if (rawVal is IDictionary<string, object> dictObj)
        {
            foreach (var v in dictObj.Values)
            {
                if (double.TryParse(v?.ToString(), out var d)) shares.Add(d);
                else result.AddError(q.Id, "INVALID_NUMBER", $"Share value '{v}' is not a valid number.", v);
            }
        }
        else if (rawVal is string strVal)
        {
            ExtractSharesFromString(strVal, shares);
        }

        if (shares.Count == 0 && result.Errors.All(e => e.QuestionId != q.Id))
        {
            result.AddError(q.Id, "EMPTY_SHARES", $"Question '{q.Id}' must contain at least one share value.", rawVal);
            return;
        }

        foreach (var share in shares)
        {
            if (double.IsNaN(share) || double.IsInfinity(share) || share <= 0 || share > 100)
            {
                result.AddError(q.Id, "OUT_OF_RANGE_SHARE", $"Share percentage {share}% is invalid. Must be > 0 and <= 100.", share);
            }
        }
    }

    private static void ExtractSharesFromString(string str, List<double> shares)
    {
        if (string.IsNullOrWhiteSpace(str)) return;
        var matches = Regex.Matches(str, @"\b\d+(?:\.\d+)?\b");
        foreach (Match m in matches)
        {
            if (double.TryParse(m.Value, out var val)) shares.Add(val);
        }
    }

    private static void ValidateEntityBuilder(DiagnosticQuestion q, object rawVal, ValidationResult result)
    {
        if (rawVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in je.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        result.AddError(q.Id, "INVALID_ENTITY_FORMAT", $"Entity element in '{q.Id}' must be an object.", item.ToString());
                        continue;
                    }

                    if (item.TryGetProperty("jurisdiction", out var jProp))
                    {
                        var jCode = jProp.GetString();
                        if (string.IsNullOrWhiteSpace(jCode) || !AllowedJurisdictions.Contains(jCode))
                        {
                            result.AddError(q.Id, "INVALID_JURISDICTION", $"Jurisdiction code '{jCode}' is not supported. Allowed: [{string.Join(", ", AllowedJurisdictions)}]", jCode);
                        }
                    }
                }
            }
            else if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    result.AddError(q.Id, "EMPTY_VALUE", $"Entity builder answer for '{q.Id}' cannot be empty whitespace.", str);
                }
            }
            else
            {
                result.AddError(q.Id, "INVALID_TYPE", $"Question '{q.Id}' expects array of entities or string.", rawVal);
            }
        }
        else if (rawVal is string strVal && string.IsNullOrWhiteSpace(strVal))
        {
            result.AddError(q.Id, "EMPTY_VALUE", $"Entity builder answer for '{q.Id}' cannot be empty whitespace.", strVal);
        }
    }
}
