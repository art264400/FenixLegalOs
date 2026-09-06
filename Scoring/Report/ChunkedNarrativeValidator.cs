using System.Text.RegularExpressions;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class ChunkedNarrativeValidator
{
    private static readonly Regex CodePattern = new(@"\b(FND|COR|IP|TEAM|PROD|DATA|AI|CONTRACTS|INVEST)_[A-Z0-9_]+\b", RegexOptions.Compiled);
    private static readonly Regex TechnicalTermsPattern = new(@"\b(FactStore|ShowIf|RuleEngine|AnswerValidator|ShowIfEvaluator)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AiMentionPattern = new(@"\b(искусственный интеллект|нейросеть|LLM|языковая модель|нейросеть|наш ИИ|алгоритм ИИ|чат-бот)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmojiPattern = new(@"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u26FF\u2700-\u27BF]", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new(@"\b(Почему это нужно сделать|Ожидаемый практический результат|Action Title|FINDING_CODE|Section Title)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static (bool IsValid, ModuleNarrativeDto Result, string? Error) ValidateAndSanitizeModule(
        ModuleNarrativeResponseDto? raw,
        FocusModuleDetailDto expectedModule,
        ReportContext ctx)
    {
        var fallback = DeterministicFallbackNarratives.GenerateModuleFallback(ctx, expectedModule.SectionId);

        if (raw == null)
        {
            return (false, fallback, "Module response is null");
        }

        if (string.IsNullOrWhiteSpace(raw.SectionId) ||
            !string.Equals(raw.SectionId.Trim(), expectedModule.SectionId, StringComparison.OrdinalIgnoreCase))
        {
            return (false, fallback, $"SectionId mismatch: expected '{expectedModule.SectionId}', got '{raw.SectionId}'");
        }

        var validFindingCodes = new HashSet<string>(expectedModule.Findings.Select(f => f.FindingCode), StringComparer.OrdinalIgnoreCase);

        // Rule: LLM must not inject unknown FindingCode
        if (raw.FindingNarratives != null)
        {
            foreach (var key in raw.FindingNarratives.Keys)
            {
                if (!validFindingCodes.Contains(key))
                {
                    // Unknown FindingCode injected by LLM!
                    return (false, fallback, $"Unknown FindingCode '{key}' injected by LLM in section '{expectedModule.SectionId}'");
                }
            }
        }

        var summary = !string.IsNullOrWhiteSpace(raw.Summary) &&
                      !ContainsProhibitedContent(raw.Summary) &&
                      ReportQualityGate.IsFactuallyGrounded(raw.Summary, ctx)
            ? SanitizeText(raw.Summary)
            : fallback.Summary;

        var practicalMeaning = !string.IsNullOrWhiteSpace(raw.PracticalMeaning) &&
                               !ContainsProhibitedContent(raw.PracticalMeaning) &&
                               ReportQualityGate.IsFactuallyGrounded(raw.PracticalMeaning, ctx)
            ? SanitizeText(raw.PracticalMeaning)
            : fallback.PracticalMeaning;

        var findingNarratives = new Dictionary<string, FindingNarrativeDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in expectedModule.Findings)
        {
            if (raw.FindingNarratives != null &&
                raw.FindingNarratives.TryGetValue(finding.FindingCode, out var fNarrative) &&
                fNarrative != null)
            {
                var whyFound = !string.IsNullOrWhiteSpace(fNarrative.WhyFound) &&
                               !ContainsProhibitedContent(fNarrative.WhyFound) &&
                               ReportQualityGate.IsFactuallyGrounded(fNarrative.WhyFound, ctx)
                    ? SanitizeText(fNarrative.WhyFound)
                    : finding.WhyFound;

                var whyItMatters = !string.IsNullOrWhiteSpace(fNarrative.WhyItMatters) &&
                                   !ContainsProhibitedContent(fNarrative.WhyItMatters) &&
                                   ReportQualityGate.IsFactuallyGrounded(fNarrative.WhyItMatters, ctx)
                    ? SanitizeText(fNarrative.WhyItMatters)
                    : finding.WhyItMatters;

                var recList = (fNarrative.Recommendations != null && fNarrative.Recommendations.Count > 0)
                    ? fNarrative.Recommendations
                        .Where(r => !string.IsNullOrWhiteSpace(r) && !ContainsProhibitedContent(r) && ReportQualityGate.IsFactuallyGrounded(r, ctx))
                        .Select(SanitizeText)
                        .ToList()
                    : new List<string>();

                recList = NormalizeRecommendations(recList, finding.Recommendations, finding.Recommendation);

                findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                {
                    WhyFound = whyFound,
                    WhyItMatters = whyItMatters,
                    Recommendation = recList.FirstOrDefault() ?? finding.Recommendation,
                    Recommendations = recList
                };
            }
            else
            {
                // Missing FindingCode handled gracefully according to contract with deterministic fallback
                var fallbackRecommendations = NormalizeRecommendations(
                    new List<string>(), finding.Recommendations, finding.Recommendation);
                findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                {
                    WhyFound = finding.WhyFound,
                    WhyItMatters = finding.WhyItMatters,
                    Recommendations = fallbackRecommendations,
                    Recommendation = fallbackRecommendations.FirstOrDefault() ?? finding.Recommendation
                };
            }
        }

        var result = new ModuleNarrativeDto
        {
            Summary = summary,
            PracticalMeaning = practicalMeaning,
            FindingNarratives = findingNarratives
        };

        return (true, result, null);
    }

    public static List<string> NormalizeRecommendations(
        IEnumerable<string>? generated,
        IEnumerable<string>? canonical,
        string? legacyPrimary)
    {
        var result = new List<string>();

        void AddDistinct(IEnumerable<string>? items)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (result.Count >= 3) return;
                var clean = SanitizeText(item ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(clean)) continue;
                if (result.Any(existing => string.Equals(existing, clean, StringComparison.OrdinalIgnoreCase) || IsSubstantialDuplicate(existing, clean))) continue;
                result.Add(clean);
                if (result.Count >= 3) return;
            }
        }

        AddDistinct(generated);
        AddDistinct(canonical);
        if (result.Count < 3 && !string.IsNullOrWhiteSpace(legacyPrimary))
        {
            AddDistinct(new[] { legacyPrimary });
        }
        return result.Take(3).ToList();
    }

    private static bool IsSubstantialDuplicate(string a, string b)
    {
        var cleanA = a.Trim().TrimEnd('.', ' ', ',');
        var cleanB = b.Trim().TrimEnd('.', ' ', ',');
        if (cleanA.StartsWith(cleanB, StringComparison.OrdinalIgnoreCase) ||
            cleanB.StartsWith(cleanA, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        return false;
    }


    public static (bool IsValid, Dictionary<string, ActionNarrativeItemDto> Result, string? Error) ValidateAndSanitizeActionBatch(
        ActionBatchResponseDto? raw,
        IEnumerable<UnifiedActionItemDto> expectedActions,
        ReportContext ctx)
    {
        var actionList = expectedActions.ToList();
        var fallback = DeterministicFallbackNarratives.GenerateActionBatchFallback(ctx, actionList.Select(a => a.ActionId));

        if (raw == null || raw.Actions == null)
        {
            return (false, fallback, "Action batch response is null");
        }

        var expectedActionIds = new HashSet<string>(actionList.Select(a => a.ActionId), StringComparer.OrdinalIgnoreCase);

        // Rule: returned ActionIds must not contain unknown ActionIds
        foreach (var key in raw.Actions.Keys)
        {
            if (!expectedActionIds.Contains(key))
            {
                return (false, fallback, $"Unknown ActionId '{key}' injected by LLM");
            }
        }

        var result = new Dictionary<string, ActionNarrativeItemDto>(StringComparer.OrdinalIgnoreCase);

        foreach (var action in actionList)
        {
            if (raw.Actions.TryGetValue(action.ActionId, out var item) && item != null)
            {
                var whyNow = !string.IsNullOrWhiteSpace(item.WhyNow) &&
                             !ContainsProhibitedContent(item.WhyNow) &&
                             ReportQualityGate.IsFactuallyGrounded(item.WhyNow, ctx)
                    ? SanitizeText(item.WhyNow)
                    : action.WhyNow;

                // Invariant P0: ExpectedResult is canonical legal deliverable and protected against distortion
                result[action.ActionId] = new ActionNarrativeItemDto
                {
                    WhyNow = whyNow,
                    ExpectedResult = action.ExpectedResult
                };
            }
            else
            {
                result[action.ActionId] = new ActionNarrativeItemDto
                {
                    WhyNow = action.WhyNow,
                    ExpectedResult = action.ExpectedResult
                };
            }
        }

        return (true, result, null);
    }

    public static (bool IsValid, ExecutiveSynthesisResponseDto Result, string? Error) ValidateAndSanitizeExecutive(
        ExecutiveSynthesisResponseDto? raw,
        ReportContext ctx)
    {
        var fallback = DeterministicFallbackNarratives.GenerateExecutiveFallback(ctx);

        if (raw == null)
        {
            return (false, fallback, "Executive synthesis response is null");
        }

        var execText = raw.ExecutiveConclusion?.Trim();
        var isComplexScenario = ctx.AllFindings.Count(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High) >= 5;
        var minExecLen = isComplexScenario ? 300 : 150;

        var execConclusion = !string.IsNullOrWhiteSpace(execText) &&
                             execText.Length >= minExecLen &&
                             !ContainsProhibitedContent(execText) &&
                             ReportQualityGate.IsFactuallyGrounded(execText, ctx)
            ? SanitizeText(execText)
            : fallback.ExecutiveConclusion;

        var profileText = raw.ProjectProfileNarrative?.Trim();
        var profileNarrative = !string.IsNullOrWhiteSpace(profileText) &&
                               !ContainsProhibitedContent(profileText) &&
                               ReportQualityGate.IsFactuallyGrounded(profileText, ctx)
            ? SanitizeText(profileText)
            : fallback.ProjectProfileNarrative;

        var rootCauses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var validDeterministicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var top in ctx.TopFindings)
        {
            if (!string.IsNullOrWhiteSpace(top.RootCauseCode)) validDeterministicKeys.Add(top.RootCauseCode);
            if (!string.IsNullOrWhiteSpace(top.FindingCode)) validDeterministicKeys.Add(top.FindingCode);
        }

        foreach (var top in ctx.TopFindings)
        {
            var canonicalKey = !string.IsNullOrWhiteSpace(top.RootCauseCode) ? top.RootCauseCode : top.FindingCode;
            string? sumText = null;

            if (raw.RootCauseSummaries != null)
            {
                if (raw.RootCauseSummaries.TryGetValue(top.RootCauseCode, out var rcSum) && !string.IsNullOrWhiteSpace(rcSum))
                    sumText = rcSum;
                else if (raw.RootCauseSummaries.TryGetValue(top.FindingCode, out var fSum) && !string.IsNullOrWhiteSpace(fSum))
                    sumText = fSum;
            }

            if (!string.IsNullOrWhiteSpace(sumText) &&
                !ContainsProhibitedContent(sumText) &&
                ReportQualityGate.IsFactuallyGrounded(sumText, ctx))
            {
                rootCauses[canonicalKey] = SanitizeText(sumText);
            }
            else
            {
                rootCauses[canonicalKey] = top.ShortSummary;
            }
        }

        string fenixLawRec;
        if (ctx.FenixLaw.RequiresLegalWork)
        {
            var flText = raw.FenixLawRecommendation?.Trim();
            fenixLawRec = !string.IsNullOrWhiteSpace(flText) &&
                          !ContainsProhibitedContent(flText) &&
                          ReportQualityGate.IsFactuallyGrounded(flText, ctx)
                ? SanitizeText(flText)
                : fallback.FenixLawRecommendation;
        }
        else
        {
            fenixLawRec = fallback.FenixLawRecommendation;
        }

        var result = new ExecutiveSynthesisResponseDto
        {
            ProjectProfileNarrative = profileNarrative,
            ExecutiveConclusion = execConclusion,
            RootCauseSummaries = rootCauses,
            FenixLawRecommendation = fenixLawRec
        };

        return (true, result, null);
    }

    public static bool ContainsProhibitedContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (CodePattern.IsMatch(text)) return true;
        if (TechnicalTermsPattern.IsMatch(text)) return true;
        if (AiMentionPattern.IsMatch(text)) return true;
        if (PlaceholderPattern.IsMatch(text)) return true;
        return false;
    }

    public static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var stripped = EmojiPattern.Replace(text, "");
        return stripped.Trim();
    }
}
