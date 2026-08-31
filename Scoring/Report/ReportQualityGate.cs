using System.Text.RegularExpressions;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class ReportQualityGate
{
    private static readonly Regex CodePattern = new(@"\b(FND|COR|IP|TEAM|PROD|DATA|AI|CONTRACTS|INVEST)_[A-Z0-9_]+\b", RegexOptions.Compiled);
    private static readonly Regex TechnicalTermsPattern = new(@"\b(FactStore|ShowIf|RuleEngine|AnswerValidator|ShowIfEvaluator)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AiMentionPattern = new(@"\b(искусственный интеллект|нейросеть|LLM|языковая модель|нейросеть|наш ИИ|алгоритм ИИ|чат-бот)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmojiPattern = new(@"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u26FF\u2700-\u27BF]", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new(@"\b(Почему это нужно сделать|Ожидаемый практический результат|Action Title|FINDING_CODE|Section Title)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ReportNarrativesDto ValidateAndSanitize(ReportNarrativesDto? rawNarratives, ReportContext ctx)
    {
        if (rawNarratives == null)
        {
            Console.WriteLine("[ReportQualityGate] Raw narratives is null -> Applying deterministic fallback.");
            return DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        }

        var sanitized = new ReportNarrativesDto();

        // 1. Validate & Sanitize Project Profile Narrative
        var profileText = rawNarratives.ProjectProfileNarrative?.Trim();
        if (string.IsNullOrWhiteSpace(profileText) || ContainsProhibitedContent(profileText) || !IsFactuallyGrounded(profileText, ctx))
        {
            sanitized.ProjectProfileNarrative = ctx.Profile.ConfigurationNarrative;
        }
        else
        {
            sanitized.ProjectProfileNarrative = SanitizeText(profileText);
        }

        // 2. Validate & Sanitize Executive Conclusion
        var execText = rawNarratives.ExecutiveConclusion?.Trim();
        if (string.IsNullOrWhiteSpace(execText) || execText.Length < 150 || ContainsProhibitedContent(execText) || !IsFactuallyGrounded(execText, ctx))
        {
            sanitized.ExecutiveConclusion = ctx.ExecutiveConclusion;
        }
        else
        {
            sanitized.ExecutiveConclusion = SanitizeText(execText);
        }

        // 3. Root Cause Summaries (Canonical Schema)
        var validDeterministicKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var top in ctx.TopFindings)
        {
            if (!string.IsNullOrWhiteSpace(top.RootCauseCode)) validDeterministicKeys.Add(top.RootCauseCode);
            if (!string.IsNullOrWhiteSpace(top.FindingCode)) validDeterministicKeys.Add(top.FindingCode);
        }

        foreach (var top in ctx.TopFindings)
        {
            string? sumText = null;
            if (rawNarratives.RootCauseSummaries.TryGetValue(top.RootCauseCode, out var rcSum) && !string.IsNullOrWhiteSpace(rcSum))
            {
                sumText = rcSum;
            }
            else if (rawNarratives.RootCauseSummaries.TryGetValue(top.FindingCode, out var fSum) && !string.IsNullOrWhiteSpace(fSum))
            {
                sumText = fSum;
            }
            else if (rawNarratives.TopRiskSummaries != null && rawNarratives.TopRiskSummaries.TryGetValue(top.FindingCode, out var legacySum) && !string.IsNullOrWhiteSpace(legacySum))
            {
                sumText = legacySum;
            }

            var canonicalKey = !string.IsNullOrWhiteSpace(top.RootCauseCode) ? top.RootCauseCode : top.FindingCode;
            if (!string.IsNullOrWhiteSpace(sumText) && !ContainsProhibitedContent(sumText) && IsFactuallyGrounded(sumText, ctx))
            {
                sanitized.RootCauseSummaries[canonicalKey] = SanitizeText(sumText);
            }
            else
            {
                sanitized.RootCauseSummaries[canonicalKey] = top.ShortSummary;
            }
        }

        // 4. Module Narratives
        foreach (var focus in ctx.FocusModules)
        {
            var fallbackFocus = DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx).ModuleNarratives[focus.SectionId];

            if (rawNarratives.ModuleNarratives.TryGetValue(focus.SectionId, out var modDto) && modDto != null)
            {
                var summary = !string.IsNullOrWhiteSpace(modDto.Summary) && !ContainsProhibitedContent(modDto.Summary) && IsFactuallyGrounded(modDto.Summary, ctx)
                    ? SanitizeText(modDto.Summary)
                    : fallbackFocus.Summary;

                var practical = !string.IsNullOrWhiteSpace(modDto.PracticalMeaning) && !ContainsProhibitedContent(modDto.PracticalMeaning) && IsFactuallyGrounded(modDto.PracticalMeaning, ctx)
                    ? SanitizeText(modDto.PracticalMeaning)
                    : fallbackFocus.PracticalMeaning;

                var findingNarratives = new Dictionary<string, FindingNarrativeDto>();
                foreach (var finding in focus.Findings)
                {
                    if (modDto.FindingNarratives.TryGetValue(finding.FindingCode, out var fNarrative) && fNarrative != null)
                    {
                        findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                        {
                            WhyFound = !string.IsNullOrWhiteSpace(fNarrative.WhyFound) && !ContainsProhibitedContent(fNarrative.WhyFound) && IsFactuallyGrounded(fNarrative.WhyFound, ctx)
                                ? SanitizeText(fNarrative.WhyFound) : finding.WhyFound,
                            WhyItMatters = !string.IsNullOrWhiteSpace(fNarrative.WhyItMatters) && !ContainsProhibitedContent(fNarrative.WhyItMatters) && IsFactuallyGrounded(fNarrative.WhyItMatters, ctx)
                                ? SanitizeText(fNarrative.WhyItMatters) : finding.WhyItMatters,
                            Recommendation = !string.IsNullOrWhiteSpace(fNarrative.Recommendation) && !ContainsProhibitedContent(fNarrative.Recommendation) && IsFactuallyGrounded(fNarrative.Recommendation, ctx)
                                ? SanitizeText(fNarrative.Recommendation) : finding.Recommendation
                        };
                    }
                    else
                    {
                        findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                        {
                            WhyFound = finding.WhyFound,
                            WhyItMatters = finding.WhyItMatters,
                            Recommendation = finding.Recommendation
                        };
                    }
                }

                sanitized.ModuleNarratives[focus.SectionId] = new ModuleNarrativeDto
                {
                    Summary = summary,
                    PracticalMeaning = practical,
                    FindingNarratives = findingNarratives
                };
            }
            else
            {
                sanitized.ModuleNarratives[focus.SectionId] = fallbackFocus;
            }
        }

        // 5. Action Narratives (Keyed ONLY by ActionId)
        foreach (var action in ctx.ActionPlan)
        {
            ActionNarrativeItemDto? actNarrative = null;
            if (rawNarratives.ActionNarratives.TryGetValue(action.ActionId, out var byId) && byId != null)
            {
                actNarrative = byId;
            }
            else if (rawNarratives.ActionNarratives.TryGetValue(action.Title, out var byTitle) && byTitle != null)
            {
                actNarrative = byTitle;
            }

            var whyNow = (actNarrative != null && !string.IsNullOrWhiteSpace(actNarrative.WhyNow) && !ContainsProhibitedContent(actNarrative.WhyNow) && IsFactuallyGrounded(actNarrative.WhyNow, ctx))
                ? SanitizeText(actNarrative.WhyNow) : action.WhyNow;

            var expectedResult = (actNarrative != null && !string.IsNullOrWhiteSpace(actNarrative.ExpectedResult) && !ContainsProhibitedContent(actNarrative.ExpectedResult) && IsFactuallyGrounded(actNarrative.ExpectedResult, ctx))
                ? SanitizeText(actNarrative.ExpectedResult) : action.ExpectedResult;

            var item = new ActionNarrativeItemDto
            {
                WhyNow = whyNow,
                ExpectedResult = expectedResult
            };

            // STRICT CONTRACT: Keyed ONLY by ActionId (No Title keys)
            sanitized.ActionNarratives[action.ActionId] = item;
        }

        // 6. Fenix Law Recommendation
        if (ctx.FenixLaw.RequiresLegalWork)
        {
            var flText = rawNarratives.FenixLawRecommendation?.Trim();
            sanitized.FenixLawRecommendation = !string.IsNullOrWhiteSpace(flText) && !ContainsProhibitedContent(flText) && IsFactuallyGrounded(flText, ctx)
                ? SanitizeText(flText)
                : ctx.FenixLaw.SummaryText;
        }
        else
        {
            // If deterministic engine says no legal work needed, enforce no upsell!
            sanitized.FenixLawRecommendation = ctx.FenixLaw.SummaryText;
        }

        return sanitized;
    }

    private static readonly Regex InventedDepartureRegex = new(@"(разработчик[а-я]*.*(покинул[а-я]*|ушел|ушл[а-я]*|уволил[а-я]*|бросил[а-я]*|уход[а-я]*)|уход[а-я]*.*разработчик[а-я]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ExtremeGuaranteeRegex = new(@"((институциональн[а-я]*\s+)?инвестор[а-я]*\s+(откажут[а-я]*|не\s+войдут|отвергнут[а-я]*|заблокируют)|гарантированн[а-я]*\s+отказ\s+инвестор[а-я]*|100%\s+срыв)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AbsoluteAbsenceRegex = new(@"((никогда\s+не\s+(существовал[а-я]*|заключал[а-я]*|подписывал[а-я]*|составлял[а-я]*))|акт[а-я]*.*(вовсе|полностью)\s+отсутствуют|договор[а-я]*.*никогда\s+не\s+составлялись)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InventedDisputeMotiveRegex = new(@"(конфликт[а-я]*.*из-за\s+(денег|невыплат[а-я]*|гонорар[а-я]*)|спор[а-я]*\s+из-за\s+оплат[а-я]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefinitiveNegativeOnUnknownRegex = new(@"(точно\s+отсутствуют|достоверно\s+не\s+ведется|гарантированно\s+нет)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static bool IsFactuallyGrounded(string? text, ReportContext ctx)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        // 1. Invented departure
        if (InventedDepartureRegex.IsMatch(text))
        {
            var hasDepartedContext = ctx.TopFindings.Any(f => f.FindingCode.Contains("DEPARTED") || f.FindingCode.Contains("FORMER") || f.ShortSummary.Contains("бывш") || f.ShortSummary.Contains("уход"))
                                  || ctx.FocusModules.Any(m => m.Findings.Any(f => f.FindingCode.Contains("DEPARTED") || f.FindingCode.Contains("FORMER") || f.WhyFound.Contains("бывш") || f.WhyFound.Contains("уход")));
            if (!hasDepartedContext) return false;
        }

        // 2. Extreme investor rejection guarantees
        if (ExtremeGuaranteeRegex.IsMatch(text))
        {
            var hasExtremeBlocker = ctx.InvestmentReadiness?.CrossModuleBlockers.Any(b => b.WhyItBlocksDueDiligence.Contains("институциональн", StringComparison.OrdinalIgnoreCase)) == true;
            if (!hasExtremeBlocker) return false;
        }

        // 3. Absolute non-existence claims on unconfirmed/partial items
        if (AbsoluteAbsenceRegex.IsMatch(text))
        {
            var hasAbsoluteAbsenceBasis = ctx.FocusModules.Any(m => m.Findings.Any(f => f.WhyFound.Contains("никогда", StringComparison.OrdinalIgnoreCase) || f.WhyFound.Contains("вовсе", StringComparison.OrdinalIgnoreCase)));
            if (!hasAbsoluteAbsenceBasis) return false;
        }

        // 4. Invented dispute motives
        if (InventedDisputeMotiveRegex.IsMatch(text))
        {
            var hasPaymentDisputeContext = ctx.FocusModules.Any(m => m.Findings.Any(f => f.WhyFound.Contains("оплат", StringComparison.OrdinalIgnoreCase) || f.WhyFound.Contains("невыплат", StringComparison.OrdinalIgnoreCase)));
            if (!hasPaymentDisputeContext) return false;
        }

        // 5. Definitive negatives
        if (DefinitiveNegativeOnUnknownRegex.IsMatch(text))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsProhibitedContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (CodePattern.IsMatch(text)) return true;
        if (TechnicalTermsPattern.IsMatch(text)) return true;
        if (AiMentionPattern.IsMatch(text)) return true;
        if (PlaceholderPattern.IsMatch(text)) return true;
        return false;
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var stripped = EmojiPattern.Replace(text, "");
        return stripped.Trim();
    }
}
