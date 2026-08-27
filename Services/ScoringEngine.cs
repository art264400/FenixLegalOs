using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;

namespace FenixLegalOs.Services;

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
                        _ => b == targetBool
                    };
                }
                if (factVal is IEnumerable<string> strList)
                {
                    var targetStr = rule.Value?.ToString() ?? "";
                    return rule.Op switch
                    {
                        "contains" or "eq" or "in" => strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        "notContains" or "neq" => !strList.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                        _ => true
                    };
                }
                if (factVal != null)
                {
                    return EvaluateOp(rule.Op, factVal.ToString() ?? "", rule.Value, factVal);
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
                _ => true
            };
        }

        if (rawVal is IEnumerable<string> listVal)
        {
            var targetStr = rule.Value?.ToString() ?? "";
            return rule.Op switch
            {
                "contains" or "eq" or "in" => listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                "notContains" or "neq" => !listVal.Any(x => x.Equals(targetStr, StringComparison.OrdinalIgnoreCase)),
                _ => true
            };
        }

        var valStr = rawVal.ToString() ?? "";
        return EvaluateOp(rule.Op, valStr, rule.Value, rawVal);
    }

    private static bool EvaluateOp(string? op, string valStr, object? ruleValue, object? rawVal = null)
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
            _ => true
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

public class FactNormalizer
{
    public static SharedFactStore NormalizeFacts(Dictionary<string, object> answers)
    {
        var store = new SharedFactStore();
        var f = store.Facts;

        // Company & Corporate Facts
        var corC01 = GetAnswerStr(answers, "COR-C01");
        f["company.entityStatus"] = corC01 switch
        {
            "one" => "single",
            "multiple" or "several" => "multiple",
            "registering" or "process" => "registering",
            "none" => "not_incorporated",
            _ => "unknown"
        };

        var isMultiple = corC01 is "multiple" or "several";
        var corC02B = GetAnswerStr(answers, "COR-C02B");
        int entityCount = corC01 switch
        {
            "one" or "registering" => 1,
            "multiple" or "several" => corC02B switch { "2" => 2, "3" => 3, "4plus" => 4, _ => 2 },
            "none" => 0,
            _ => 1
        };

        f["company.entityCount"] = entityCount;
        f["company.groupStructure"] = isMultiple;

        var primaryJurisdiction = GetAnswerStr(answers, "COR-C02A");
        if (string.IsNullOrEmpty(primaryJurisdiction)) primaryJurisdiction = GetAnswerStr(answers, "COR-C02");
        f["company.primaryJurisdiction"] = primaryJurisdiction;
        f["company.jurisdiction"] = primaryJurisdiction;

        var jurisdictionsList = new List<string>();
        if (!string.IsNullOrEmpty(primaryJurisdiction)) jurisdictionsList.Add(primaryJurisdiction);

        // Process Additional Entities (COR-C02C)
        var entitiesSummary = new List<string>();
        if (!string.IsNullOrEmpty(primaryJurisdiction))
        {
            entitiesSummary.Add($"Основная компания: {FormatJurisdictionName(primaryJurisdiction)}");
        }

        if (isMultiple && answers.TryGetValue("COR-C02C", out var rawC02C) && rawC02C != null)
        {
            f["company.additionalEntitiesRaw"] = rawC02C;
            // Parse entities array or object
            if (rawC02C is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                int cIdx = 2;
                foreach (var item in je.EnumerateArray())
                {
                    var jVal = item.TryGetProperty("jurisdiction", out var jp) ? jp.GetString() : null;
                    var rList = item.TryGetProperty("roles", out var rp) && rp.ValueKind == JsonValueKind.Array
                        ? rp.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                        : new List<string>();

                    if (!string.IsNullOrEmpty(jVal))
                    {
                        jurisdictionsList.Add(jVal);
                        var roleStr = rList.Count > 0 ? string.Join(", ", rList.Select(FormatRoleName)) : "операционная деятельность";
                        entitiesSummary.Add($"Компания {cIdx} ({FormatJurisdictionName(jVal)}): {roleStr}");
                    }
                    cIdx++;
                }
            }
            else if (rawC02C is string strC02C && !string.IsNullOrWhiteSpace(strC02C))
            {
                entitiesSummary.Add($"Дополнительные компании: {strC02C}");
            }
        }

        f["company.jurisdictions"] = jurisdictionsList.Distinct().ToList();

        // Build Human-Readable Corporate Structure Narrative
        string narrative;
        if (corC01 == "none")
        {
            narrative = "Проект пока работает без зарегистрированного юридического лица.";
        }
        else if (corC01 == "registering")
        {
            narrative = $"Компания находится в процессе регистрации ({FormatJurisdictionName(primaryJurisdiction)}).";
        }
        else if (!isMultiple)
        {
            narrative = $"Бизнес ведет деятельность через одну компанию в юрисдикции {FormatJurisdictionName(primaryJurisdiction)}.";
        }
        else
        {
            narrative = $"В структуре бизнеса используется {entityCount} компаний:\n- " + string.Join("\n- ", entitiesSummary);
        }

        f["company.structureNarrative"] = narrative;

        var cor04 = GetAnswerStr(answers, "COR-04");
        f["capital.historyChanges"] = cor04 is "complete" or "main_docs" or "partial" or "missing";

        // Founders Facts
        var fndC01 = GetAnswerStr(answers, "FND-C01");
        f["founders.count"] = fndC01 switch { "solo" => 1, "2" => 2, "3" => 3, "4plus" => 4, _ => 1 };
        f["founders.inactiveExists"] = fndC01 == "inactive_exist" || GetAnswerStr(answers, "FND-C03") != "none";

        var fndC02 = GetAnswerStr(answers, "FND-C02");
        f["founders.equityDistribution"] = fndC02;
        f["founders.isEqual5050"] = fndC02 == "equal_50_50";

        var fnd01 = GetAnswerStr(answers, "FND-01");
        f["founders.dispute"] = fnd01 == "active_conflict" || fnd01 == "formal_dispute";

        // IP Facts
        var ip01 = GetAnswerStr(answers, "IP-01");
        bool coreProductExists = ip01 != "idea" && !string.IsNullOrEmpty(ip01);
        f["ip.coreProductExists"] = coreProductExists;
        f["product.stage"] = ip01 switch
        {
            "idea" => "idea",
            "prototype" => "prototype",
            "ready" => "live_or_ready",
            "multiple" => "multiple_products",
            _ => "idea"
        };

        var ipAssets = GetAnswerList(answers, "IP-02");
        f["ip.assets"] = ipAssets;

        var ipCreators = GetAnswerList(answers, "IP-03");
        f["ip.creators"] = ipCreators;

        f["ip.overallRightsEvidence"] = GetAnswerStr(answers, "IP-04");
        f["ip.founderRights"] = GetAnswerStr(answers, "IP-05");
        f["ip.employeeRights"] = GetAnswerStr(answers, "IP-06");
        f["ip.contractorRights"] = GetAnswerStr(answers, "IP-07");
        f["ip.formerCreatorStatus"] = GetAnswerStr(answers, "IP-08");
        f["ip.studioRights"] = GetAnswerStr(answers, "IP-09");
        f["ip.externalEmployerCreation"] = GetAnswerStr(answers, "IP-10");

        var ip10A = GetAnswerStr(answers, "IP-10A");
        f["ip.employerResourcesUsed"] = ip10A switch
        {
            "yes" => true,
            "no" => false,
            "possible" => "possible",
            "unknown" => "unknown",
            _ => null
        };

        var ip11 = GetAnswerStr(answers, "IP-11");
        f["ip.thirdPartyComponentsUsed"] = ip11 switch
        {
            "yes" or "likely" => true,
            "no" => false,
            _ => "unknown"
        };
        var ip11A = GetAnswerStr(answers, "IP-11A");
        f["ip.thirdPartyTermsReview"] = ip11A switch
        {
            "yes" => "systematic",
            "main" => "main",
            "developers_only" => "developers_only",
            "no" => "none",
            "unknown" => "unknown",
            _ => null
        };
        f["ip.externalDependency"] = GetAnswerStr(answers, "IP-12");
        f["ip.criticalAccountsControl"] = GetAnswerStr(answers, "IP-13");
        f["ip.brandDomainControl"] = GetAnswerStr(answers, "IP-14");
        f["ip.brandRegistration"] = GetAnswerStr(answers, "IP-14") == "brand_not_registered" ? "not_registered" : "registered";
        f["ip.contentProvenance"] = GetAnswerStr(answers, "IP-15");

        // Team Facts
        var teamC01 = GetAnswerStr(answers, "TEAM-C01");
        f["team.hasNonFounderTeam"] = teamC01 != "founders_only" && !string.IsNullOrEmpty(teamC01);

        // Data & AI Facts
        var data01 = GetAnswerStr(answers, "DATA-01");
        var data02 = GetAnswerStr(answers, "DATA-02");
        f["data.personalDataProcessed"] = data01 == "yes" || (!string.IsNullOrEmpty(data02) && data02 != "none");

        var ai01 = GetAnswerStr(answers, "AI-01");
        f["ai.used"] = ai01 == "external" || ai01 == "own" || ai01 == "both";

        var ai02 = GetAnswerStr(answers, "AI-02");
        f["ai.sensitiveDataSent"] = ai02 == "sensitive";

        // Contracts Facts
        var contract01 = GetAnswerStr(answers, "CONTRACT-01");
        f["contracts.b2bRelevant"] = contract01 != "none" && !string.IsNullOrEmpty(contract01);

        // Investment Facts
        var invest01 = GetAnswerStr(answers, "INVEST-01");
        f["investment.timing"] = invest01 switch
        {
            "m3" or "m3_6" => "near_term",
            "m6_12" => "mid_term",
            "looking" or "discussing" or "terms" => "active",
            _ => "none"
        };
        var invest02 = GetAnswerStr(answers, "INVEST-02");
        f["investment.priorInvestment"] = invest02 != "none" && !string.IsNullOrEmpty(invest02);

        return store;
    }

    private static string FormatJurisdictionName(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "Не указана";
        return code.ToLowerInvariant() switch
        {
            "kz" => "Казахстан (ТОО)",
            "aifc" => "МФЦА (AIFC)",
            "us" => "США (Delaware / C-Corp)",
            "uae" => "ОАЭ (Free Zone / Mainland)",
            "uk" => "Великобритания (Ltd)",
            "other" => "Другая юрисдикция",
            "unknown" => "Юрисдикция уточняется",
            _ => code
        };
    }

    private static string FormatRoleName(string? role)
    {
        if (string.IsNullOrWhiteSpace(role)) return "операционная деятельность";
        return role.ToLowerInvariant() switch
        {
            "holding" => "Владение долями (холдинг)",
            "clients" => "Работа с клиентами и договоры",
            "payments" => "Получение платежей и выручки",
            "ip_assets" => "Владение продуктом и IP-активами",
            "hiring" => "Найм команды",
            "other" => "Операционная деятельность",
            _ => role
        };
    }

    private static string GetAnswerStr(Dictionary<string, object> answers, string key)
    {
        return answers.TryGetValue(key, out var val) && val != null ? val.ToString() ?? "" : "";
    }

    private static List<string> GetAnswerList(Dictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return new();
        if (val is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            var res = new List<string>();
            foreach (var item in je.EnumerateArray()) res.Add(item.ToString());
            return res;
        }
        if (val is IEnumerable<string> strEnum) return strEnum.ToList();
        if (val is string s)
        {
            if (s.Contains(',')) return s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            return new List<string> { s };
        }
        return new();
    }
}

public class ScoringEngine
{
    private readonly QuestionRepository? _questionRepo;

    public ScoringEngine(QuestionRepository? questionRepo = null)
    {
        _questionRepo = questionRepo;
    }

    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var factStore = FactNormalizer.NormalizeFacts(answers);

        var allQuestions = _questionRepo != null ? _questionRepo.GetQuestions(enabledOnly: true) : DataBank.Questions.Where(q => q.Enabled).ToList();
        var allSections = _questionRepo != null ? _questionRepo.GetSections(enabledOnly: true) : DataBank.Sections.ToList();
        var allRisks = _questionRepo != null ? _questionRepo.GetRisks() : DataBank.Risks.ToList();

        // Filter visible questions according to showIf and skipIf
        var visibleQs = allQuestions
            .Where(q => ConditionsEvaluator.IsVisible(q.ShowIf, answers, factStore))
            .Where(q => q.SkipIf == null || !ConditionsEvaluator.IsVisible(q.SkipIf, answers, factStore))
            .ToList();

        // 1. Calculate Section Scores & Confidence per section
        var sections = allSections.Select(s =>
        {
            var sectionQs = visibleQs.Where(q => q.SectionId == s.Id).ToList();
            bool isModuleApplicable = IsModuleApplicable(s.Id, factStore, sectionQs);

            // Special case for Solo Founder
            if (s.Id == "founders" && (int?)factStore.Facts.GetValueOrDefault("founders.count") == 1)
            {
                // Solo founder has clean co-founder structure (no partner deadlock/vesting risks)
                return new SectionScore
                {
                    SectionId = s.Id,
                    Title = s.Title,
                    Score = 100,
                    Weight = s.Weight,
                    Status = "APPLICABLE",
                    Confidence = 100
                };
            }

            if (!isModuleApplicable)
            {
                return new SectionScore
                {
                    SectionId = s.Id,
                    Title = s.Title,
                    Score = null,
                    Weight = s.Weight,
                    Status = "N_A",
                    Confidence = 100
                };
            }

            var diagnosticQs = sectionQs.Where(q => q.ScoreMode == "diagnostic" && q.Weight > 0).ToList();
            double totalWeight = 0;
            double weightedScoreSum = 0;
            double knownWeight = 0;

            foreach (var q in diagnosticQs)
            {
                if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null) continue;
                var opt = q.Options?.FirstOrDefault(o => o.Id == ansVal.ToString());
                if (opt == null) continue;

                totalWeight += q.Weight * q.WithinDimensionWeight;
                weightedScoreSum += opt.Score * (q.Weight * q.WithinDimensionWeight);

                if (opt.ConfidenceClass != "unknown")
                {
                    knownWeight += q.Weight * q.WithinDimensionWeight;
                }
            }

            int? finalScore = totalWeight > 0 ? (int)Math.Round((weightedScoreSum / totalWeight) * 100) : null;
            int confidence = totalWeight > 0 ? (int)Math.Round((knownWeight / totalWeight) * 100) : 100;

            return new SectionScore
            {
                SectionId = s.Id,
                Title = s.Title,
                Score = finalScore,
                Weight = s.Weight,
                Status = finalScore.HasValue ? "APPLICABLE" : "N_A",
                Confidence = confidence
            };
        }).ToList();

        // 2. Overall Legal Score & Confidence
        var applicableSections = sections.Where(s => s.Status == "APPLICABLE" && s.Score.HasValue).ToList();
        double totalSectionWeight = applicableSections.Sum(s => s.Weight);

        int overallScore = totalSectionWeight > 0
            ? (int)Math.Round(applicableSections.Sum(s => s.Score!.Value * s.Weight) / totalSectionWeight)
            : 0;

        int overallConfidence = applicableSections.Count > 0
            ? (int)Math.Round(applicableSections.Average(s => s.Confidence))
            : 85;

        // 3. Merged & Suppressed Findings
        var rawFindings = CollectRawFindings(visibleQs, answers, allRisks, factStore);
        var mergedFindings = MergeAndSuppressFindings(rawFindings, factStore);

        // 4. Investment Readiness Overlay
        var investmentOverlay = CalculateInvestmentReadiness(answers, factStore, mergedFindings);

        // 5. Consulting Recommendation
        var consulting = CalculateConsultingRecommendation(mergedFindings, factStore, overallScore);

        var level = GetLevel(overallScore);

        return new ScoreResult
        {
            Overall = overallScore,
            Confidence = overallConfidence,
            ConfidenceText = GetConfidenceText(overallConfidence),
            Level = level,
            LevelTitle = GetLevelTitle(level),
            LevelText = GetLevelText(level),
            Sections = sections,
            Risks = mergedFindings,
            CriticalCount = mergedFindings.Count(r => r.Severity is "CRITICAL" or "BLOCKER"),
            HighCount = mergedFindings.Count(r => r.Severity == "HIGH"),
            MediumCount = mergedFindings.Count(r => r.Severity == "MEDIUM"),
            Strengths = applicableSections.Where(s => s.Score >= 75).Select(s => s.Title).ToList(),
            AnsweredCount = visibleQs.Count(q => answers.ContainsKey(q.Id)),
            InvestmentReadiness = investmentOverlay,
            Consulting = consulting,
            Versions = new ScoreVersions(),
            ComputedAt = DateTime.UtcNow.ToString("o")
        };
    }

    private bool IsModuleApplicable(string sectionId, SharedFactStore facts, List<DiagnosticQuestion> sectionQs)
    {
        var f = facts.Facts;
        return sectionId switch
        {
            "founders" => true,
            "corporate" => (string?)f.GetValueOrDefault("company.entityStatus") is "single" or "multiple" or "incorporated" or "registering",
            "ip" => true,
            "team" => GetBoolFact(f, "team.hasNonFounderTeam"),
            "data" => GetBoolFact(f, "data.personalDataProcessed") || GetBoolFact(f, "ai.used"),
            "contracts" => GetBoolFact(f, "contracts.b2bRelevant"),
            "investment" => (string?)f.GetValueOrDefault("investment.timing") != "none" || GetBoolFact(f, "investment.priorInvestment"),
            _ => sectionQs.Count > 0
        };
    }

    private bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }

    private List<RiskFinding> CollectRawFindings(List<DiagnosticQuestion> visibleQs, Dictionary<string, object> answers, List<RiskDefinition> allRisks, SharedFactStore facts)
    {
        var list = new List<RiskFinding>();

        foreach (var q in visibleQs)
        {
            if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null || q.Options == null) continue;
            var strVal = ansVal.ToString();
            var opt = q.Options.FirstOrDefault(o => o.Id == strVal);
            if (opt?.RiskCode == null) continue;

            var def = allRisks.FirstOrDefault(r => r.Code == opt.RiskCode);
            if (def != null)
            {
                list.Add(new RiskFinding
                {
                    Code = def.Code,
                    RootCauseGroup = def.RootCauseGroup,
                    Severity = opt.Severity ?? def.Severity,
                    Priority = def.Priority,
                    SectionId = def.SectionId,
                    Title = def.Title,
                    Finding = def.Finding,
                    WhyItMatters = def.WhyItMatters,
                    Recommendation = def.Recommendation.Length > 0 ? def.Recommendation : (def.Recommendations.FirstOrDefault() ?? ""),
                    Recommendations = def.Recommendations.Count > 0 ? def.Recommendations : new List<string> { def.Recommendation },
                    Basis = new List<RiskFindingBasis> { new() { QuestionId = q.Id, AnswerId = strVal ?? "" } },
                    LawyerRequired = def.LawyerRequired,
                    Resolution = def.Resolution,
                    ServiceCode = def.ServiceCode,
                    Cta = def.Cta
                });
            }
        }

        // §27.2 Rule: COR_NO_ENTITY_FOR_ACTIVITY
        // Condition: company.entityStatus == not_incorporated AND (company.hasRevenue == true OR team.hasNonFounderTeam == true OR investment.priorInvestment == true)
        var entityStatus = (string?)facts.Facts.GetValueOrDefault("company.entityStatus");
        bool hasRevenue = GetBoolFact(facts.Facts, "company.hasRevenue") || GetBoolFact(facts.Facts, "revenue.exists") || answers.ContainsKey("REV-01") || answers.ContainsKey("REV-C01");
        bool hasNonFounderTeam = GetBoolFact(facts.Facts, "team.hasNonFounderTeam") || (answers.TryGetValue("TEAM-C01", out var teamVal) && teamVal?.ToString() != "solo_only" && teamVal?.ToString() != "none");
        bool priorInvestment = GetBoolFact(facts.Facts, "investment.priorInvestment") || (answers.TryGetValue("INV-C01", out var invVal) && invVal?.ToString() == "yes");

        if (entityStatus == "not_incorporated" && (hasRevenue || hasNonFounderTeam || priorInvestment))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
            if (def != null && !list.Any(f => f.Code == def.Code))
            {
                AddFinding(list, def, "COR-C01", "none", "HIGH");
            }
        }

        // §27.2 Rule: IP_PRODUCT_RIGHTS_UNCONFIRMED
        // Condition: ip.coreProductExists == true AND company.entityStatus in [incorporated, single, multiple] AND ip.overallRightsEvidence in [none, informal]
        bool coreProductExists = GetBoolFact(facts.Facts, "ip.coreProductExists");
        var overallRights = (string?)facts.Facts.GetValueOrDefault("ip.overallRightsEvidence");
        if (coreProductExists && entityStatus is "single" or "multiple" or "incorporated" && overallRights is "none" or "informal")
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
            if (def != null)
            {
                var existing = list.FirstOrDefault(f => f.Code == def.Code);
                if (existing != null) existing.Severity = "CRITICAL";
                else AddFinding(list, def, "IP-04", overallRights ?? "none", "CRITICAL");
            }
        }

        // §27.2 Rule: IP_FORMER_DEVELOPER_GAP
        // Condition: ip.formerCreatorStatus in [unresolved, dispute] OR (team.formerPeopleExist == true AND ip.contractorRights in [payment_only, no_contract, unclear_clause])
        var formerStatus = (string?)facts.Facts.GetValueOrDefault("ip.formerCreatorStatus");
        var contractorRights = (string?)facts.Facts.GetValueOrDefault("ip.contractorRights");
        var ipCreators = facts.Facts.GetValueOrDefault("ip.creators") as List<string>;
        bool formerPeopleExist = GetBoolFact(facts.Facts, "team.formerPeopleExist") || (ipCreators != null && ipCreators.Contains("former"));
        if (formerStatus is "unresolved" or "dispute" || (formerPeopleExist && contractorRights is "payment_only" or "no_contract" or "unclear_clause"))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "IP_FORMER_DEVELOPER_GAP");
            if (def != null)
            {
                var existing = list.FirstOrDefault(f => f.Code == def.Code);
                if (existing != null) existing.Severity = "CRITICAL";
                else AddFinding(list, def, "IP-08", formerStatus ?? "unresolved", "CRITICAL");
            }
        }

        // §27.2 Rule: IP_EMPLOYER_RISK
        // Condition: ip.externalEmployerCreation in [not_reviewed, unknown] AND ip.employerResourcesUsed in [true, possible, unknown]
        // Severity: HIGH_OR_CRITICAL_IF_CORE (CRITICAL if core product exists / resources used, HIGH otherwise)
        var extEmployer = (string?)facts.Facts.GetValueOrDefault("ip.externalEmployerCreation");
        var resUsed = facts.Facts.GetValueOrDefault("ip.employerResourcesUsed");
        if (extEmployer is "not_reviewed" or "unknown" && (resUsed is true or "possible" or "unknown"))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
            if (def != null)
            {
                bool isCore = resUsed is true || coreProductExists;
                string sev = isCore ? "CRITICAL" : "HIGH";
                var existing = list.FirstOrDefault(f => f.Code == def.Code);
                if (existing != null) existing.Severity = sev;
                else AddFinding(list, def, "IP-10A", resUsed?.ToString() ?? "possible", sev);
            }
        }

        // §27.2 Rule: IP_THIRD_PARTY_COMPONENTS
        // Condition: ip.thirdPartyComponentsUsed == true AND ip.thirdPartyTermsReview in [developers_only, none, unknown]
        // Severity: MEDIUM (Canonical default)
        var tpComponentsUsed = facts.Facts.GetValueOrDefault("ip.thirdPartyComponentsUsed");
        var tpReview = (string?)facts.Facts.GetValueOrDefault("ip.thirdPartyTermsReview");
        if (tpComponentsUsed is true && tpReview is "developers_only" or "none" or "unknown")
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "IP_THIRD_PARTY_COMPONENTS");
            if (def != null && !list.Any(f => f.Code == def.Code))
            {
                AddFinding(list, def, "IP-11A", tpReview ?? "none", "MEDIUM");
            }
        }

        // §27.2 Rule: IP_ACCESS_CONTROL
        // Condition: ip.criticalAccountsControl in [worker, one_founder] AND (founders.activeDispute == true OR team.formerPersonConflict == true OR personDeparting == true)
        var accControl = (string?)facts.Facts.GetValueOrDefault("ip.criticalAccountsControl");
        bool founderDispute = GetBoolFact(facts.Facts, "founders.dispute") || GetBoolFact(facts.Facts, "founders.activeDispute");
        bool teamConflict = GetBoolFact(facts.Facts, "team.formerPersonConflict");
        if (accControl is "worker" or "one_founder" && (founderDispute || teamConflict))
        {
            var def = allRisks.FirstOrDefault(r => r.Code == "IP_ACCESS_CONTROL");
            if (def != null)
            {
                var existing = list.FirstOrDefault(f => f.Code == def.Code);
                if (existing != null) existing.Severity = "CRITICAL";
                else AddFinding(list, def, "IP-13", accControl ?? "worker", "CRITICAL");
            }
        }

        return list;
    }

    private void AddFinding(List<RiskFinding> list, RiskDefinition def, string qId, string ansId, string severity)
    {
        list.Add(new RiskFinding
        {
            Code = def.Code,
            RootCauseGroup = def.RootCauseGroup,
            Severity = severity,
            Priority = def.Priority,
            SectionId = def.SectionId,
            Title = def.Title,
            Finding = def.Finding,
            WhyItMatters = def.WhyItMatters,
            Recommendation = def.Recommendation.Length > 0 ? def.Recommendation : (def.Recommendations.FirstOrDefault() ?? ""),
            Recommendations = def.Recommendations.Count > 0 ? def.Recommendations : new List<string> { def.Recommendation },
            Basis = new List<RiskFindingBasis> { new() { QuestionId = qId, AnswerId = ansId } },
            LawyerRequired = def.LawyerRequired,
            Resolution = def.Resolution,
            ServiceCode = def.ServiceCode,
            Cta = def.Cta
        });
    }

    private List<RiskFinding> MergeAndSuppressFindings(List<RiskFinding> rawFindings, SharedFactStore facts)
    {
        var suppressedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Canonical Cross-Finding Suppressions (§25)
        if (rawFindings.Any(f => f.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED"))
        {
            suppressedCodes.Add("IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");
            suppressedCodes.Add("IP_CONTRACTOR_RIGHTS_GAP");
            suppressedCodes.Add("IP_STUDIO_RIGHTS_GAP");
        }

        if (rawFindings.Any(f => f.Code == "IP_FORMER_DEVELOPER_GAP"))
        {
            suppressedCodes.Add("IP_CONTRACTOR_RIGHTS_GAP");
            suppressedCodes.Add("TEAM_FORMER_ACCESS_RISK");
        }

        var activeFindings = rawFindings.Where(f => !suppressedCodes.Contains(f.Code)).ToList();

        var grouped = activeFindings.GroupBy(f => f.RootCauseGroup).ToList();
        var merged = new List<RiskFinding>();

        foreach (var group in grouped)
        {
            var highestSeverity = group.OrderBy(f => GetSeverityOrder(f.Severity)).First();

            // Collect all unique recommendations and bases
            var allRecs = group.SelectMany(f => f.Recommendations).Distinct().ToList();
            var allBases = group.SelectMany(f => f.Basis).DistinctBy(b => b.QuestionId).ToList();

            highestSeverity.Recommendations = allRecs;
            highestSeverity.Basis = allBases;

            merged.Add(highestSeverity);
        }

        return merged.OrderBy(r => GetSeverityOrder(r.Severity)).ToList();
    }

    private InvestmentReadinessOverlay CalculateInvestmentReadiness(Dictionary<string, object> answers, SharedFactStore facts, List<RiskFinding> findings)
    {
        bool applicable = (string)facts.Facts["investment.timing"]! != "none" || (bool)facts.Facts["investment.priorInvestment"]!;
        if (!applicable) return new InvestmentReadinessOverlay { Applicable = false, ReadinessScore = 100 };

        var blockers = findings
            .Where(f => f.Severity is "CRITICAL" or "BLOCKER")
            .Select(f => f.Title)
            .ToList();

        int readiness = 85;
        if (blockers.Count >= 2) readiness = 35;
        else if (blockers.Count == 1) readiness = 55;

        return new InvestmentReadinessOverlay
        {
            Applicable = true,
            ReadinessScore = readiness,
            Blockers = blockers
        };
    }

    private ConsultingRecommendation CalculateConsultingRecommendation(List<RiskFinding> findings, SharedFactStore facts, int overallScore)
    {
        int opportunityScore = 30;
        if (findings.Any(f => f.Severity == "BLOCKER")) opportunityScore += 25;
        if (findings.Any(f => f.Severity == "CRITICAL")) opportunityScore += 20;

        string primary = "FULL_LEGAL_ARCHITECTURE";
        string primaryCta = "Провести полный юридический аудит компании";
        string? secondary = null;
        string? secondaryCta = null;

        var topFinding = findings.FirstOrDefault(f => !string.IsNullOrEmpty(f.ServiceCode));
        if (topFinding != null && !string.IsNullOrEmpty(topFinding.ServiceCode))
        {
            primary = topFinding.ServiceCode;
            primaryCta = topFinding.Cta ?? GetServiceCta(topFinding.ServiceCode);
            secondary = "FULL_LEGAL_ARCHITECTURE";
            secondaryCta = "Провести полный юридический аудит компании";
        }

        return new ConsultingRecommendation
        {
            PrimaryServiceCode = primary,
            PrimaryCta = primaryCta,
            SecondaryServiceCode = secondary,
            SecondaryCta = secondaryCta,
            ConsultingOpportunityScore = Math.Min(100, opportunityScore)
        };
    }

    private string GetServiceCta(string code) => code switch
    {
        "FOUNDERS_REVIEW" => "Разобрать структуру между основателями",
        "CORPORATE_CLEANUP" => "Привести корпоративную структуру в порядок",
        "IP_RIGHTS_REVIEW" => "Проверить права на продукт",
        "TEAM_LEGAL_REVIEW" => "Проверить юридическую конструкцию команды",
        "PRODUCT_LEGAL_REVIEW" => "Проверить юридическую модель продукта",
        "DATA_AI_REVIEW" => "Разобрать модель работы с данными и ИИ",
        "CONTRACTS_REVIEW" => "Проверить ключевые договоры",
        "INVESTOR_READINESS" => "Подготовить компанию к проверке инвестором",
        "DEAL_SUPPORT" => "Проверить и сопроводить инвестиционную сделку",
        _ => "Провести полный юридический аудит компании"
    };

    private int GetSeverityOrder(string sev) => sev switch
    {
        "BLOCKER" => 0,
        "CRITICAL" or "critical" => 1,
        "HIGH" or "high" => 2,
        "MEDIUM" or "medium" => 3,
        _ => 4
    };

    public static string GetConfidenceText(int confidence) => confidence switch
    {
        >= 80 => "Высокая определенность ответов.",
        >= 60 => "Оценка достаточно надежна, но часть вопросов требует подтверждения.",
        _ => "Оценка ограничена недостатком определенности ответов."
    };

    public static string GetLevel(int score) => score switch
    {
        >= 80 => "strong",
        >= 60 => "attention",
        >= 40 => "material_gaps",
        _ => "structural_risks"
    };

    public static string GetLevelTitle(string level) => level switch
    {
        "strong" => "Сильная основа",
        "attention" => "Есть вопросы, требующие внимания",
        "material_gaps" => "Существенные пробелы",
        _ => "Структурные вопросы"
    };

    public static string GetLevelText(string level) => level switch
    {
        "strong" => "Ваша компания имеет относительно сильную юридическую основу.",
        "attention" => "Основа сформирована частично. Некоторые вопросы требуют внимания.",
        "material_gaps" => "Диагностика выявила несколько значимых пробелов в юридической конструкции.",
        _ => "Юридическая основа бизнеса пока сформирована фрагментарно."
    };
}
