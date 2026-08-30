using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Interfaces;
using FenixLegalOs.Scoring.Modules.Corporate;
using FenixLegalOs.Scoring.Modules.Founders;
using FenixLegalOs.Scoring.Modules.IP;
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
            new TeamRuleEngine()
        };
    }

    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var allSections = _repository.GetSections().OrderBy(s => s.Order).ToList();
        var allQuestions = _repository.GetQuestions();
        var allRisks = _repository.GetRisks();

        // ─── Effective Answers Trust Boundary (Architecture A) ───────────────
        // Phase 1: Determine visible questions using ALL submitted answers (for routing).
        var routingFactStore = FactNormalizer.NormalizeFacts(answers);
        var visibleQs = allQuestions
            .Where(q => q.Enabled != false
                && ConditionsEvaluator.IsVisible(q.ShowIf, answers, routingFactStore))
            .ToList();

        // Phase 2: Filter to EffectiveAnswers — only answers to visible questions.
        //          Hidden/stale/tampered answers are excluded BEFORE fact derivation.
        var visibleIds = visibleQs.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        var effectiveAnswers = answers
            .Where(kv => visibleIds.Contains(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        // Phase 3: Recompute canonical facts from EffectiveAnswers only.
        //          This is the clean factStore used by ALL downstream scoring and rules.
        var factStore = FactNormalizer.NormalizeFacts(effectiveAnswers);
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
        string? currentQuestionId = null)
    {
        var allQuestions = _repository.GetQuestions();

        // Use routing facts (all answers) to determine visibility — same as Phase 1 in ComputeResult.
        var routingFactStore = FactNormalizer.NormalizeFacts(answers);
        var visibleIds = allQuestions
            .Where(q => q.Enabled != false
                && ConditionsEvaluator.IsVisible(q.ShowIf, answers, routingFactStore))
            .OrderBy(q => q.Order)
            .Select(q => q.Id)
            .ToList();

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

        // Find current index: use requested currentQuestionId, or snap to first.
        int currentIndex = currentQuestionId != null
            ? visibleIds.IndexOf(currentQuestionId)
            : -1;

        // Snap to first if not found in visible list (question became hidden after earlier answer change).
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
