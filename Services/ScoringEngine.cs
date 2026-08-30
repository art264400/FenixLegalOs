using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Interfaces;
using FenixLegalOs.Scoring.Modules.Corporate;
using FenixLegalOs.Scoring.Modules.Founders;
using FenixLegalOs.Scoring.Modules.IP;
using FenixLegalOs.Scoring.Modules.Product;
using FenixLegalOs.Scoring.Modules.DataAi;
using FenixLegalOs.Scoring.Modules.Contracts;
using FenixLegalOs.Scoring.Modules.Investment;
using FenixLegalOs.Scoring.Modules.Team;

namespace FenixLegalOs.Services;

public class ScoringEngine
{
    private readonly QuestionRepository _repository;
    private readonly List<IModuleRuleEngine> _moduleRuleEngines;

    public ScoringEngine(QuestionRepository repository)
    {
        _repository = repository;
        _moduleRuleEngines = new List<IModuleRuleEngine>
        {
            new FoundersRuleEngine(),
            new CorporateRuleEngine(),
            new IpRuleEngine(),
            new TeamRuleEngine(),
            new ProductRuleEngine(),
            new DataAiRuleEngine(),
            new ContractRuleEngine(),
            new InvestmentRuleEngine()
        };
    }

    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var allSections = _repository.GetSections().OrderBy(s => s.Order).ToList();
        var allQuestions = _repository.GetQuestions();
        var allRisks = _repository.GetRisks();

        // ─── Effective Answers Trust Boundary (Architecture A) ───────────────
        // Fixed-point convergence: hidden/stale answers are filtered out iteratively,
        // preventing orphaned or self-resurrecting visibility of downstream questions.
        var (visibleQs, effectiveAnswers, factStore) = ResolveEffectiveState(allQuestions, answers);
        // ─────────────────────────────────────────────────────────────────────

        var sections = new List<SectionScore>();
        double totalApplicableModuleWeight = 0;
        double weightedModuleScoreSum = 0;

        var confidenceTracker = new ConfidenceTracker();
        var allDimensionScores = new List<DimensionScore>();

        // 3. Dimension & Module Scoring (using effectiveAnswers + clean factStore)
        foreach (var sec in allSections)
        {
            var sectionQs = visibleQs.Where(q => q.SectionId == sec.Id).ToList();
            bool isApplicable = ModuleScorer.IsModuleApplicable(sec.Id, factStore, sectionQs);

            if (!isApplicable)
            {
                sections.Add(new SectionScore
                {
                    SectionId = sec.Id,
                    Title = sec.Title,
                    Score = null,
                    Weight = sec.Weight,
                    Status = ApplicabilityStatus.NotApplicable,
                    Confidence = 100,
                    Findings = new List<string>(),
                    Dimensions = new List<DimensionScore>()
                });
                continue;
            }

            var dimResult = DimensionScorer.ComputeDimensions(sectionQs, effectiveAnswers, confidenceTracker);
            allDimensionScores.AddRange(dimResult.Dimensions);

            var sectionScore = ModuleScorer.ComputeSectionScore(
                sec,
                dimResult.TotalApplicableDimensionWeight,
                dimResult.WeightedDimensionScoreSum,
                factStore);

            if (sectionScore.HasValue)
            {
                totalApplicableModuleWeight += sec.Weight;
                weightedModuleScoreSum += sectionScore.Value * sec.Weight;
            }

            sections.Add(new SectionScore
            {
                SectionId = sec.Id,
                Title = sec.Title,
                Score = sectionScore,
                Weight = sec.Weight,
                Status = ApplicabilityStatus.Applicable,
                Confidence = 100,
                Dimensions = dimResult.Dimensions
            });
        }

        // 4. Overall Score & Confidence Calculation
        int overallScore = OverallScorer.ComputeOverallScore(totalApplicableModuleWeight, weightedModuleScoreSum);
        int overallConfidence = confidenceTracker.ComputeOverallConfidence();

        // 5. Findings Collection & Suppression (clean factStore — no stale-answer artifacts)
        var rawFindings = FindingProcessor.CollectRawFindings(factStore, allRisks, _moduleRuleEngines);
        var mergedFindings = FindingProcessor.MergeAndSuppressFindings(rawFindings, factStore);

        // 6. Strong Areas Calculation
        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(allDimensionScores, mergedFindings);

        // 7. Investment Readiness & Consulting Overlays
        var investmentOverlay = InvestmentReadinessEvaluator.Calculate(effectiveAnswers, factStore, mergedFindings);
        var consulting = ConsultingEvaluator.Calculate(mergedFindings, factStore, overallScore);

        var level = OverallScorer.GetLevel(overallScore);

        return new ScoreResult
        {
            Overall = overallScore,
            Confidence = overallConfidence,
            ConfidenceText = ConfidenceCalculator.GetConfidenceText(overallConfidence),
            Level = level,
            LevelTitle = OverallScorer.GetLevelTitle(level),
            LevelText = OverallScorer.GetLevelText(level),
            Sections = sections,
            Risks = mergedFindings,
            CriticalCount = mergedFindings.Count(r => r.Severity is RiskSeverity.Critical or RiskSeverity.Blocker),
            HighCount = mergedFindings.Count(r => r.Severity == RiskSeverity.High),
            MediumCount = mergedFindings.Count(r => r.Severity == RiskSeverity.Medium),
            Strengths = strongAreas,
            AnsweredCount = visibleQs.Count(q => effectiveAnswers.ContainsKey(q.Id)),
            InvestmentReadiness = investmentOverlay,
            Consulting = consulting,
            Versions = new ScoreVersions(),
            ComputedAt = DateTime.UtcNow.ToString("o")
        };
    }

    // ─── Architecture A — Server-Driven Navigation ───────────────────────────

    /// <summary>
    /// Computes authoritative NavigationState from draft answers and an optional current question ID.
    /// Backend is the sole authority for current/next/previous question routing.
    /// </summary>
    public NavigationState GetNavigationState(
        Dictionary<string, object> answers,
        string? currentQuestionId = null,
        string? answeredQuestionId = null)
    {
        var allQuestions = _repository.GetQuestions();
        var (visibleQs, _, _) = ResolveEffectiveState(allQuestions, answers);
        var visibleIds = visibleQs.Select(q => q.Id).ToList();

        int total = visibleIds.Count;

        if (total == 0)
        {
            return new NavigationState
            {
                VisibleQuestionIds = visibleIds,
                CurrentQuestionId = null,
                PreviousQuestionId = null,
                NextQuestionId = null,
                Current = 0,
                TotalVisible = 0
            };
        }

        int currentIndex = -1;

        // 1. If answeredQuestionId is provided, advance to the question strictly following it in the updated visible list
        if (!string.IsNullOrEmpty(answeredQuestionId))
        {
            int answeredIndex = visibleIds.IndexOf(answeredQuestionId);
            if (answeredIndex >= 0)
            {
                int nextIdx = answeredIndex + 1;
                if (nextIdx < total)
                {
                    currentIndex = nextIdx;
                }
                else
                {
                    // Questionnaire completed!
                    return new NavigationState
                    {
                        VisibleQuestionIds = visibleIds,
                        CurrentQuestionId = null,
                        PreviousQuestionId = visibleIds[total - 1],
                        NextQuestionId = null,
                        Current = total + 1,
                        TotalVisible = total
                    };
                }
            }
        }

        // 2. If currentQuestionId is provided, look it up in visibleIds
        if (currentIndex < 0 && !string.IsNullOrEmpty(currentQuestionId))
        {
            currentIndex = visibleIds.IndexOf(currentQuestionId);
        }

        // 3. Fallback: snap to first visible question
        if (currentIndex < 0) currentIndex = 0;

        return new NavigationState
        {
            VisibleQuestionIds = visibleIds,
            CurrentQuestionId = visibleIds[currentIndex],
            PreviousQuestionId = currentIndex > 0 ? visibleIds[currentIndex - 1] : null,
            NextQuestionId = currentIndex < total - 1 ? visibleIds[currentIndex + 1] : null,
            Current = currentIndex + 1, // 1-based
            TotalVisible = total
        };
    }

    private static readonly Dictionary<string, int> SectionOrderMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["founders"] = 1,
        ["corporate"] = 2,
        ["ip"] = 3,
        ["team"] = 4,
        ["product"] = 5,
        ["data"] = 6,
        ["contracts"] = 7,
        ["investment"] = 8
    };

    public static int GetGlobalQuestionRank(DiagnosticQuestion q)
    {
        int secRank = SectionOrderMap.TryGetValue(q.SectionId ?? "", out var s) ? s : 99;
        return secRank * 1000 + q.Order;
    }

    /// <summary>
    /// Computes the authoritative list of visible questions and effective answers via forward topological evaluation.
    /// Ensures that hidden/stale answers can NEVER participate in establishing their own visibility,
    /// establish upstream visibility, or leak into derived canonical facts.
    /// </summary>
    public static (List<DiagnosticQuestion> VisibleQuestions, Dictionary<string, object> EffectiveAnswers, SharedFactStore FactStore)
        ResolveEffectiveState(List<DiagnosticQuestion> allQuestions, Dictionary<string, object> rawAnswers)
    {
        var enabledQuestions = allQuestions
            .Where(q => q.Enabled != false)
            .OrderBy(GetGlobalQuestionRank)
            .ToList();

        var effectiveAnswers = new Dictionary<string, object>(StringComparer.Ordinal);
        var visibleQs = new List<DiagnosticQuestion>();

        foreach (var q in enabledQuestions)
        {
            // 1. Derive routing facts strictly from already-authorized upstream effective answers
            var routingFactStore = FactNormalizer.NormalizeFacts(effectiveAnswers);

            // 2. Evaluate question visibility strictly against upstream effective state
            bool isVisible = ConditionsEvaluator.IsVisible(q.ShowIf, effectiveAnswers, routingFactStore);

            if (isVisible)
            {
                visibleQs.Add(q);
                // 3. Raw answer becomes effective ONLY AFTER question is proven visible by upstream authority
                if (rawAnswers.TryGetValue(q.Id, out var userVal) && userVal != null)
                {
                    effectiveAnswers[q.Id] = userVal;
                }
            }
        }

        // 4. Recompute final canonical factStore from the completed set of authorized EffectiveAnswers
        var finalFactStore = FactNormalizer.NormalizeFacts(effectiveAnswers);
        return (visibleQs, effectiveAnswers, finalFactStore);
    }

    // ─── Deprecated: use GetNavigationState instead ──────────────────────────

    /// <summary>
    /// Returns visible question IDs. Deprecated — use GetNavigationState for full navigation contract.
    /// Retained for backward compatibility with existing tests.
    /// </summary>
    public List<string> GetVisibleQuestionIds(Dictionary<string, object> answers)
        => GetNavigationState(answers).VisibleQuestionIds.ToList();

    // ─── Static forwarders ────────────────────────────────────────────────────
    public static List<string> GetAffectedDimensions(string riskCode) => StrongAreasCalculator.GetAffectedDimensions(riskCode);
    public static LegalScoreLevel GetLevel(int score) => OverallScorer.GetLevel(score);
    public static string GetLevelTitle(LegalScoreLevel level) => OverallScorer.GetLevelTitle(level);
    public static string GetLevelText(LegalScoreLevel level) => OverallScorer.GetLevelText(level);
    public static string GetConfidenceText(int conf) => ConfidenceCalculator.GetConfidenceText(conf);
}
