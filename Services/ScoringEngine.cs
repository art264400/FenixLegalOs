using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Interfaces;
using FenixLegalOs.Scoring.Modules.Corporate;
using FenixLegalOs.Scoring.Modules.Founders;
using FenixLegalOs.Scoring.Modules.IP;

namespace FenixLegalOs.Services;

// Forwarding aliases for backward compatibility with existing tests/controllers
public class ConditionsEvaluator : FenixLegalOs.Scoring.Core.ConditionsEvaluator { }
public class FactNormalizer : FenixLegalOs.Scoring.Core.FactNormalizer { }

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
            new IpRuleEngine()
        };
    }

    public ScoreResult ComputeResult(Dictionary<string, object> answers)
    {
        var allSections = _repository.GetSections().OrderBy(s => s.Order).ToList();
        var allQuestions = _repository.GetQuestions();
        var allRisks = _repository.GetRisks();

        // 1. Fact Normalization
        var factStore = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answers);

        // 2. Visible Questions Filtering
        var visibleQs = allQuestions
            .Where(q => FenixLegalOs.Scoring.Core.ConditionsEvaluator.IsVisible(q.ShowIf, answers, factStore))
            .ToList();

        var sections = new List<SectionScore>();
        double totalApplicableModuleWeight = 0;
        double weightedModuleScoreSum = 0;

        var confidenceTracker = new ConfidenceTracker();
        var allDimensionScores = new List<DimensionScore>();

        // 3. Dimension & Module Scoring
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
                    Status = "N_A",
                    Confidence = 100,
                    Findings = new List<string>(),
                    Dimensions = new List<DimensionScore>()
                });
                continue;
            }

            var dimResult = DimensionScorer.ComputeDimensions(sectionQs, answers, confidenceTracker);
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
                Status = "APPLICABLE",
                Confidence = 100,
                Dimensions = dimResult.Dimensions
            });
        }

        // 4. Overall Score & Confidence Calculation
        int overallScore = OverallScorer.ComputeOverallScore(totalApplicableModuleWeight, weightedModuleScoreSum);
        int overallConfidence = confidenceTracker.ComputeOverallConfidence();

        // 5. Findings Collection & Suppression
        var rawFindings = FindingProcessor.CollectRawFindings(factStore, allRisks, _moduleRuleEngines);
        var mergedFindings = FindingProcessor.MergeAndSuppressFindings(rawFindings, factStore);

        // 6. Strong Areas Calculation
        var strongAreas = StrongAreasCalculator.CalculateStrongAreas(allDimensionScores, mergedFindings);

        // 7. Investment Readiness & Consulting Overlays
        var investmentOverlay = InvestmentReadinessEvaluator.Calculate(answers, factStore, mergedFindings);
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
            CriticalCount = mergedFindings.Count(r => r.Severity is "CRITICAL" or "BLOCKER"),
            HighCount = mergedFindings.Count(r => r.Severity == "HIGH"),
            MediumCount = mergedFindings.Count(r => r.Severity == "MEDIUM"),
            Strengths = strongAreas,
            AnsweredCount = visibleQs.Count(q => answers.ContainsKey(q.Id)),
            InvestmentReadiness = investmentOverlay,
            Consulting = consulting,
            Versions = new ScoreVersions(),
            ComputedAt = DateTime.UtcNow.ToString("o")
        };
    }

    // Static forwarders to ensure 100% backward compatibility
    public static List<string> GetAffectedDimensions(string riskCode) => StrongAreasCalculator.GetAffectedDimensions(riskCode);
    public static string GetLevel(int score) => OverallScorer.GetLevel(score);
    public static string GetLevelTitle(string level) => OverallScorer.GetLevelTitle(level);
    public static string GetLevelText(string level) => OverallScorer.GetLevelText(level);
    public static string GetConfidenceText(int conf) => ConfidenceCalculator.GetConfidenceText(conf);
}
