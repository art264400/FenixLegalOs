using System.Text.Json;
using System.Text.RegularExpressions;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
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
                result.AddError(qId, ValidationErrorCode.UnknownQuestion, $"Question '{qId}' does not exist in the Question Bank.", rawVal);
                continue;
            }

            if (rawVal == null)
            {
                result.AddError(qId, ValidationErrorCode.NullValue, $"Answer for '{qId}' cannot be null.", null);
                continue;
            }

            if (rawVal is string s && string.IsNullOrWhiteSpace(s))
            {
                result.AddError(qId, ValidationErrorCode.EmptyValue, $"Answer for '{qId}' cannot be empty or whitespace.", s);
                continue;
            }

            if (rawVal is JsonElement jeNull && (jeNull.ValueKind == JsonValueKind.Null || jeNull.ValueKind == JsonValueKind.Undefined))
            {
                result.AddError(qId, ValidationErrorCode.NullValue, $"Answer for '{qId}' cannot be null JSON.", null);
                continue;
            }

            switch (q.Type)
            {
                case QuestionType.Single or QuestionType.Boolean:
                    ValidateSingle(q, rawVal, result);
                    break;

                case QuestionType.Multiple:
                    ValidateMultiple(q, rawVal, result);
                    break;

                case QuestionType.EquityInputs:
                    ValidateEquityInputs(q, rawVal, result);
                    break;

                case QuestionType.EntityBuilder:
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
                result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Question '{q.Id}' expects a scalar single-choice answer, got JSON {je.ValueKind}.", rawVal);
                return;
            }
        }
        else
        {
            valStr = rawVal.ToString();
        }

        if (string.IsNullOrWhiteSpace(valStr))
        {
            result.AddError(q.Id, ValidationErrorCode.EmptyValue, $"Answer for '{q.Id}' cannot be empty or whitespace.", rawVal);
            return;
        }

        var allowedOptions = q.Options?.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        if (allowedOptions.Count > 0 && !allowedOptions.Contains(valStr))
        {
            result.AddError(q.Id, ValidationErrorCode.InvalidOption, $"Value '{valStr}' is not a valid option for question '{q.Id}'. Allowed: [{string.Join(", ", allowedOptions)}]", valStr);
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
                result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Question '{q.Id}' expects a multi-select array, got JSON {je.ValueKind}.", rawVal);
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
            result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Question '{q.Id}' expects a multi-select array, got {rawVal.GetType().Name}.", rawVal);
            return;
        }

        if (items.Count == 0)
        {
            result.AddError(q.Id, ValidationErrorCode.EmptySelection, $"Multi-select question '{q.Id}' must contain at least one selection.", rawVal);
            return;
        }

        var allowedOptions = q.Options?.Select(o => o.Id).ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();

        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                result.AddError(q.Id, ValidationErrorCode.EmptyItem, $"Multi-select question '{q.Id}' contains empty item.", items);
                continue;
            }

            if (allowedOptions.Count > 0 && !allowedOptions.Contains(item))
            {
                result.AddError(q.Id, ValidationErrorCode.InvalidOption, $"Item '{item}' is not a valid option for question '{q.Id}'. Allowed: [{string.Join(", ", allowedOptions)}]", item);
            }
        }

        // Check for mutually exclusive options like 'none'
        if (items.Count > 1)
        {
            bool hasNone = items.Any(x => x.Equals("none", StringComparison.OrdinalIgnoreCase));
            if (hasNone)
            {
                result.AddError(q.Id, ValidationErrorCode.MutuallyExclusiveConflict, $"Option 'none' in '{q.Id}' cannot be combined with other selections.", items);
            }
        }
    }

    private static void ValidateEquityInputs(DiagnosticQuestion q, object rawVal, ValidationResult result)
    {
        var shares = new List<double>();
        if (rawVal is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in je.EnumerateObject())
                {
                    if (prop.Value.TryGetDouble(out var d)) shares.Add(d);
                    else if (double.TryParse(prop.Value.GetString(), out var ps)) shares.Add(ps);
                    else
                    {
                        result.AddError(q.Id, ValidationErrorCode.InvalidNumber, $"Share value for '{prop.Name}' is not a valid number.", prop.Value.ToString());
                    }
                }
            }
            else
            {
                result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Equity inputs '{q.Id}' expects a JSON object map of founder shares.", rawVal);
                return;
            }
        }
        else if (rawVal is IDictionary<string, double> dictDouble)
        {
            shares.AddRange(dictDouble.Values);
        }
        else if (rawVal is IDictionary<string, int> dictInt)
        {
            shares.AddRange(dictInt.Values.Select(v => (double)v));
        }
        else if (rawVal is IDictionary<string, float> dictFloat)
        {
            shares.AddRange(dictFloat.Values.Select(v => (double)v));
        }
        else if (rawVal is IDictionary<string, object> dictObj)
        {
            foreach (var kvp in dictObj)
            {
                if (double.TryParse(kvp.Value?.ToString(), out var d)) shares.Add(d);
                else result.AddError(q.Id, ValidationErrorCode.InvalidNumber, $"Share value for '{kvp.Key}' is not a valid number.", kvp.Value);
            }
        }
        else
        {
            result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Equity inputs '{q.Id}' expects a JSON object map of founder shares.", rawVal);
            return;
        }

        if (shares.Count == 0 && result.Errors.All(e => e.QuestionId != q.Id))
        {
            result.AddError(q.Id, ValidationErrorCode.EmptyShares, $"Question '{q.Id}' must contain at least one share value.", rawVal);
            return;
        }

        foreach (var share in shares)
        {
            if (double.IsNaN(share) || double.IsInfinity(share) || share <= 0 || share > 100)
            {
                result.AddError(q.Id, ValidationErrorCode.OutOfRangeShare, $"Share percentage {share}% is invalid. Must be > 0 and <= 100.", share);
            }
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
                        result.AddError(q.Id, ValidationErrorCode.InvalidEntityFormat, $"Entity element in '{q.Id}' must be an object.", item.ToString());
                        continue;
                    }

                    if (item.TryGetProperty("jurisdiction", out var jProp))
                    {
                        var jCode = jProp.GetString();
                        if (string.IsNullOrWhiteSpace(jCode) || !AllowedJurisdictions.Contains(jCode))
                        {
                            result.AddError(q.Id, ValidationErrorCode.InvalidJurisdiction, $"Jurisdiction code '{jCode}' is not supported. Allowed: [{string.Join(", ", AllowedJurisdictions)}]", jCode);
                        }
                    }
                }
            }
            else if (je.ValueKind == JsonValueKind.String)
            {
                var str = je.GetString();
                if (string.IsNullOrWhiteSpace(str))
                {
                    result.AddError(q.Id, ValidationErrorCode.EmptyValue, $"Entity builder answer for '{q.Id}' cannot be empty whitespace.", str);
                }
            }
            else
            {
                result.AddError(q.Id, ValidationErrorCode.InvalidType, $"Question '{q.Id}' expects array of entities or string.", rawVal);
            }
        }
        else if (rawVal is IEnumerable<object> objList && rawVal is not string)
        {
            foreach (var item in objList)
            {
                if (item is IDictionary<string, object> dict)
                {
                    if (dict.TryGetValue("jurisdiction", out var jVal) && jVal != null)
                    {
                        var jCode = jVal.ToString();
                        if (string.IsNullOrWhiteSpace(jCode) || !AllowedJurisdictions.Contains(jCode))
                        {
                            result.AddError(q.Id, ValidationErrorCode.InvalidJurisdiction, $"Jurisdiction code '{jCode}' is not supported. Allowed: [{string.Join(", ", AllowedJurisdictions)}]", jCode);
                        }
                    }
                }
                else if (item is not null)
                {
                    result.AddError(q.Id, ValidationErrorCode.InvalidEntityFormat, $"Entity element in '{q.Id}' must be an object.", item.ToString());
                }
            }
        }
        else if (rawVal is string strVal && string.IsNullOrWhiteSpace(strVal))
        {
            result.AddError(q.Id, ValidationErrorCode.EmptyValue, $"Entity builder answer for '{q.Id}' cannot be empty whitespace.", strVal);
        }
    }
}
