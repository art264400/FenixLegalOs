using System.Text.RegularExpressions;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class ReportQualityGate
{
    private static readonly Regex CodePattern = new(@"\b(FND|COR|IP|TEAM|PROD|DATA|AI|CONTRACTS|INVEST)_[A-Z0-9_]+\b", RegexOptions.Compiled);
    private static readonly Regex TechnicalTermsPattern = new(@"\b(FactStore|ShowIf|RuleEngine|AnswerValidator|ShowIfEvaluator)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AiMentionPattern = new(@"\b(искусственный интеллект|нейросеть|LLM|языковая модель|нейросеть|наш ИИ|алгоритм ИИ|чат-бот)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex EmojiPattern = new(@"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u26FF\u2700-\u27BF]", RegexOptions.Compiled);
    private static readonly Regex PlaceholderPattern = new(@"\b(Почему это нужно сделать|Ожидаемый практический результат|Action Title|FINDING_CODE|Section Title)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex TautologyPattern = new(@"(Для бизнеса это означает необходимость защиты|требует структурирования для защиты устойчивости бизнеса|важно оформить документы)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string ComputeContextFingerprint(ReportContext ctx)
    {
        var key = $"{ctx.Overall.Score}|{ctx.AllFindings.Count}|{string.Join(",", ctx.TopFindings.Select(f => f.FindingCode))}";
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..16];
    }

    public static ReportNarrativesDto ValidateAndSanitize(ReportNarrativesDto? rawNarratives, ReportContext ctx)
    {
        if (rawNarratives == null)
        {
            Console.WriteLine("[ReportQualityGate] Raw narratives is null -> Applying deterministic fallback.");
            return DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        }

        var expectedFingerprint = ComputeContextFingerprint(ctx);
        if (!string.IsNullOrWhiteSpace(rawNarratives.ContextFingerprint) &&
            !string.Equals(rawNarratives.ContextFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[ReportQualityGate] ContextFingerprint mismatch ('{rawNarratives.ContextFingerprint}' vs '{expectedFingerprint}') -> Discarding stale incompatible narratives and applying deterministic fallback.");
            return DeterministicFallbackNarratives.GenerateFallbackNarratives(ctx);
        }

        var sanitized = new ReportNarrativesDto
        {
            ContextFingerprint = expectedFingerprint,
            SchemaVersion = "2.0"
        };

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
        var isComplexScenario = ctx.AllFindings.Count(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical or RiskSeverity.High) >= 5;
        var minExecLen = isComplexScenario ? 300 : 150;

        if (string.IsNullOrWhiteSpace(execText) || execText.Length < minExecLen || ContainsProhibitedContent(execText) || !IsFactuallyGrounded(execText, ctx))
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
                        var rawRecs = (fNarrative.Recommendations != null && fNarrative.Recommendations.Count > 0)
                            ? fNarrative.Recommendations.Where(r => !string.IsNullOrWhiteSpace(r) && !ContainsProhibitedContent(r) && IsFactuallyGrounded(r, ctx)).Select(SanitizeText).ToList()
                            : (finding.Recommendations ?? new List<string>());

                        var recList = ChunkedNarrativeValidator.NormalizeRecommendations(rawRecs, finding.Recommendations, finding.Recommendation);

                        findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                        {
                            WhyFound = !string.IsNullOrWhiteSpace(fNarrative.WhyFound) && !ContainsProhibitedContent(fNarrative.WhyFound) && IsFactuallyGrounded(fNarrative.WhyFound, ctx)
                                ? SanitizeText(fNarrative.WhyFound) : finding.WhyFound,
                            WhyItMatters = !string.IsNullOrWhiteSpace(fNarrative.WhyItMatters) && !ContainsProhibitedContent(fNarrative.WhyItMatters) && IsFactuallyGrounded(fNarrative.WhyItMatters, ctx)
                                ? SanitizeText(fNarrative.WhyItMatters) : finding.WhyItMatters,
                            Recommendation = recList.FirstOrDefault() ?? (!string.IsNullOrWhiteSpace(fNarrative.Recommendation) && !ContainsProhibitedContent(fNarrative.Recommendation) && IsFactuallyGrounded(fNarrative.Recommendation, ctx)
                                ? SanitizeText(fNarrative.Recommendation) : finding.Recommendation),
                            Recommendations = recList
                        };
                    }
                    else
                    {
                        findingNarratives[finding.FindingCode] = new FindingNarrativeDto
                        {
                            WhyFound = finding.WhyFound,
                            WhyItMatters = finding.WhyItMatters,
                            Recommendation = finding.Recommendation,
                            Recommendations = finding.Recommendations
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

            // Invariant P0: ExpectedResult is canonical legal deliverable and protected against LLM semantic distortion
            var expectedResult = action.ExpectedResult;

            var item = new ActionNarrativeItemDto
            {
                WhyNow = whyNow,
                ExpectedResult = expectedResult
            };

            // STRICT CONTRACT: Keyed ONLY by ActionId (No Title keys)
            sanitized.ActionNarratives[action.ActionId] = item;
        }

        // The legal service scope must match the current source-grounded cards.
        sanitized.FenixLawRecommendation = ctx.FenixLaw.SummaryText;

        return sanitized;
    }

    private static readonly Regex InventedDepartureRegex = new(@"(разработчик[а-я]*.*(покинул[а-я]*|ушел|ушл[а-я]*|уволил[а-я]*|бросил[а-я]*|уход[а-я]*)|уход[а-я]*.*разработчик[а-я]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ExtremeGuaranteeRegex = new(@"((институциональн[а-я]*\s+)?инвестор[а-я]*\s+(откажут[а-я]*|не\s+войдут|отвергнут[а-я]*|заблокируют|приостанов[а-я]*|откаж[а-я]*)|гарантированн[а-я]*\s+отказ\s+инвестор[а-я]*|100%\s+срыв)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AbsoluteAbsenceRegex = new(@"((никогда\s+не\s+(существовал[а-я]*|заключал[а-я]*|подписывал[а-я]*|составлял[а-я]*))|акт[а-я]*.*(вовсе|полностью)\s+отсутствуют|договор[а-я]*.*никогда\s+не\s+составлялись)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex InventedDisputeMotiveRegex = new(@"(конфликт[а-я]*.*из-за\s+(денег|невыплат[а-я]*|гонорар[а-я]*)|спор[а-я]*\s+из-за\s+оплат[а-я]*)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex DefinitiveNegativeOnUnknownRegex = new(@"(точно\s+отсутствуют|достоверно\s+не\s+ведется|гарантированно\s+нет)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Reject consequences of an invented future round, without banning factual references
    // to past investors or investments supplied as part of the product's business model.
    private static readonly Regex FundraisingClaimRegex = new(
        @"(\bраунд[а-яё]*\b|(?:привлеч[а-яё]*|привлек[а-яё]*)\s+(?:венчурн[а-яё]*\s+)?(?:инвестици[а-яё]*|инвестор[а-яё]*)|венчурн[а-яё]*\s+финансировани[а-яё]*|инвестиционн[а-яё]*\s+привлекательност[а-яё]*|привлекательност[а-яё]*\s+для\s+инвестор[а-яё]*|инвестиционн[а-яё]*\s+проверк[а-яё]*|(?:инвестор[а-яё]*[^.!?;]{0,70}(?:due\s+diligence|дью\s*дилидженс)|(?:due\s+diligence|дью\s*дилидженс)[^.!?;]{0,70}инвестор[а-яё]*)|блокер[а-яё]*\s+(?:due\s+diligence|дью\s*дилидженс))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex NoRoundStatementRegex = new(
        @"((?:раунд[а-яё]*|привлечени[а-яё]*\s+инвестици[а-яё]*)[^,;.!?]{0,35}не\s+(?:планир[а-яё]*|заявлен[а-яё]*|предусмотрен[а-яё]*)|не\s+планир[а-яё]*[^,;.!?]{0,35}(?:раунд[а-яё]*|привлек[а-яё]*\s+инвестици[а-яё]*)|не\s+(?:как\s+)?(?:начавш[а-яё]*|активн[а-яё]*|заявленн[а-яё]*)\s+(?:инвестиционн[а-яё]*\s+)?раунд[а-яё]*|не\s+предполагает[^;.!?]{0,80}привлечени[а-яё]*\s+инвестици[а-яё]*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PastRoundStatementRegex = new(
        @"((?:прошл[а-яё]*|предыдущ[а-яё]*|состоявш[а-яё]*|ранее\s+закрыт[а-яё]*)\s+(?:инвестиционн[а-яё]*\s+)?раунд[а-яё]*|раунд[а-яё]*[^,;.!?]{0,20}ранее\s+(?:закрыт[а-яё]*|завершен[а-яё]*))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PastInvestmentCheckRegex = new(
        @"((?:прошл[а-яё]*|предыдущ[а-яё]*|ранее\s+проведенн[а-яё]*)\s+инвестиционн[а-яё]*\s+проверк[а-яё]*|инвестиционн[а-яё]*\s+проверк[а-яё]*[^,;.!?]{0,20}ранее\s+(?:проведен[а-яё]*|завершен[а-яё]*))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PastFundraisingStatementRegex = new(
        @"(?:ранее|в\s+прошлом|уже)\s+(?:(?:были|было|компанией)\s+)?(?:привлек(?:ла|ли)?|привлечен[а-яё]*)\s+(?:венчурн[а-яё]*\s+)?инвестици[а-яё]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex PossibleRoundStatementRegex = new(
        @"(?:возможн[а-яё]*|потенциальн[а-яё]*)\s+(?:будущ[а-яё]*\s+)?(?:инвестиционн[а-яё]*\s+)?раунд[а-яё]*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ConditionalFundraisingPrefixRegex = new(
        @"^\s*(?:если\s+(?:компания|проект|команда|бизнес|основатели)\s+(?:(?:решит|решат|будет|будут|планирует|планируют)\s+)?(?:привлечь|привлекать|привлекает|привлекают|привлечет|привлекут)\s+(?:венчурн[а-яё]*\s+)?инвестици[а-яё]*|если\s+(?:появятся|возникнут)\s+планы\s+(?:привлечь|привлекать)\s+инвестици[а-яё]*|в\s+случае\s+(?:решения\s+(?:привлечь|привлекать)|будущ[а-яё]*\s+привлечени[а-яё]*)\s+инвестици[а-яё]*|при\s+(?:будущ[а-яё]*|возможн[а-яё]*)\s+(?:привлечени[а-яё]*\s+инвестици[а-яё]*|(?:инвестиционн[а-яё]*\s+)?раунд[а-яё]*))",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IndependentClauseBoundaryRegex = new(
        @";|\s*[,—–]\s*(?:но|однако|а\s+(?:сейчас|также)|тем\s+временем|при\s+этом)\b|\b(?:но|однако)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static bool IsCoveredBy(Match claim, Regex allowedStatement, string sentence) =>
        allowedStatement.Matches(sentence).Cast<Match>()
            .Any(statement => statement.Index <= claim.Index &&
                              statement.Index + statement.Length >= claim.Index + claim.Length);

    private static bool IsConditionalFundraisingClaim(Match claim, string sentence)
    {
        // A fundraising condition can govern its consequences, but not an independent
        // assertion after "но", "однако" or a semicolon. An unrelated "если" is no basis.
        var boundary = IndependentClauseBoundaryRegex.Match(sentence);
        var scopeEnd = boundary.Success ? boundary.Index : sentence.Length;
        return claim.Index + claim.Length <= scopeEnd &&
               ConditionalFundraisingPrefixRegex.IsMatch(sentence[..scopeEnd]);
    }

    public static bool IsFactuallyGrounded(string? text, ReportContext ctx)
    {
        if (string.IsNullOrWhiteSpace(text)) return true;

        if (ctx.InvestmentReadiness?.NewRoundPlanned != true)
        {
            foreach (var sentence in Regex.Split(text, @"[.!?\n]+"))
            {
                foreach (Match claim in FundraisingClaimRegex.Matches(sentence))
                {
                    if (IsCoveredBy(claim, NoRoundStatementRegex, sentence)) continue;
                    if (ctx.InvestmentReadiness?.HasPriorInvestment == true &&
                        (IsCoveredBy(claim, PastRoundStatementRegex, sentence) ||
                         IsCoveredBy(claim, PastInvestmentCheckRegex, sentence) ||
                         IsCoveredBy(claim, PastFundraisingStatementRegex, sentence))) continue;
                    if (ctx.InvestmentReadiness?.NewRoundPossible == true &&
                        (IsCoveredBy(claim, PossibleRoundStatementRegex, sentence) ||
                         IsConditionalFundraisingClaim(claim, sentence))) continue;
                    return false;
                }
            }
        }

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
        if (TautologyPattern.IsMatch(text)) return true;
        return false;
    }

    private static string SanitizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var stripped = EmojiPattern.Replace(text, "");
        return stripped.Trim();
    }
}
