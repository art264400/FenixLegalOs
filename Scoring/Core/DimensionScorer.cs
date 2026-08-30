using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Core;

public class DimensionScorerResult
{
    public List<DimensionScore> Dimensions { get; set; } = new();
    public double TotalApplicableDimensionWeight { get; set; }
    public double WeightedDimensionScoreSum { get; set; }
}

public class DimensionScorer
{
    public static DimensionScorerResult ComputeDimensions(
        List<DiagnosticQuestion> sectionQuestions,
        Dictionary<string, object> answers,
        ConfidenceTracker? confidenceTracker = null)
    {
        var diagnosticQs = sectionQuestions.Where(q => q.ScoreMode == ScoreMode.Diagnostic).ToList();
        var dimensionGroups = diagnosticQs.GroupBy(q => !string.IsNullOrEmpty(q.DimensionId) ? q.DimensionId : q.Id).ToList();

        var sectionDimensions = new List<DimensionScore>();
        double totalApplicableDimWeight = 0;
        double weightedDimScoreSum = 0;

        foreach (var dimGroup in dimensionGroups)
        {
            var dimId = dimGroup.Key;
            var dimQuestions = dimGroup.ToList();
            double firstDimWeight = dimQuestions.First().DimensionWeight;
            if (firstDimWeight <= 0) firstDimWeight = dimQuestions.First().Weight;

            double applicableWithinDimWeightSum = 0;
            double weightedQuestionScoreSum = 0;

            foreach (var q in dimQuestions)
            {
                if (!answers.TryGetValue(q.Id, out var ansVal) || ansVal == null) continue;
                var opt = q.Options?.FirstOrDefault(o => o.Id == ansVal.ToString());
                if (opt == null) continue;

                double withinWeight = q.WithinDimensionWeight > 0 ? q.WithinDimensionWeight : 100.0;
                applicableWithinDimWeightSum += withinWeight;
                weightedQuestionScoreSum += opt.Score * withinWeight;

                // Track question-level confidence
                confidenceTracker?.TrackQuestion(opt.ConfidenceClass, firstDimWeight, withinWeight);
            }

            if (applicableWithinDimWeightSum > 0)
            {
                int dimScore = (int)Math.Round((weightedQuestionScoreSum / applicableWithinDimWeightSum) * 100.0);
                var dimModel = new DimensionScore
                {
                    DimensionId = dimId,
                    Score = dimScore,
                    Weight = firstDimWeight,
                    IsApplicable = true
                };
                sectionDimensions.Add(dimModel);

                totalApplicableDimWeight += firstDimWeight;
                weightedDimScoreSum += dimScore * firstDimWeight;
            }
        }

        return new DimensionScorerResult
        {
            Dimensions = sectionDimensions,
            TotalApplicableDimensionWeight = totalApplicableDimWeight,
            WeightedDimensionScoreSum = weightedDimScoreSum
        };
    }
}
