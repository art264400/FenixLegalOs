using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class ReportQualityGateTests
{
    [Fact]
    public void QualityGate_StripsEmojis_AndRejectsInternalCodes()
    {
        var ctx = new ReportContext
        {
            ExecutiveConclusion = "Детерминированный вывод по умолчанию.",
            Overall = new OverallScoreDto { Score = 44, Band = "material_gaps", LevelTitle = "Существенные пробелы" }
        };

        var raw = new ReportNarrativesDto
        {
            ProjectProfileNarrative = "Проект 🚀 работает без юрлица 🔴 и нарушает FND_04.",
            ExecutiveConclusion = "Полноценный синтез ситуации без запрещенных кодов и терминов для проверки валидации отчета.",
            FenixLawRecommendation = "Рекомендуется юрист."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        // Should replace profile with deterministic fallback because of internal code FND_04
        Assert.Equal(ctx.Profile.ConfigurationNarrative, sanitized.ProjectProfileNarrative);
        Assert.DoesNotContain("FND_04", sanitized.ProjectProfileNarrative);
        Assert.DoesNotContain("🚀", sanitized.ProjectProfileNarrative);
    }

    [Fact]
    public void QualityGate_RejectsAiMentions()
    {
        var ctx = new ReportContext
        {
            ExecutiveConclusion = "Детерминированный вывод по умолчанию."
        };

        var raw = new ReportNarrativesDto
        {
            ExecutiveConclusion = "Наш искусственный интеллект проанализировал риски и считает..."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        // Fallback applied because AI mention is prohibited
        Assert.Equal(ctx.ExecutiveConclusion, sanitized.ExecutiveConclusion);
        Assert.DoesNotContain("искусственный интеллект", sanitized.ExecutiveConclusion);
    }

    [Fact]
    public void QualityGate_HandlesNullGracefullyWithFallback()
    {
        var ctx = new ReportContext
        {
            Overall = new OverallScoreDto { Score = 55, LevelTitle = "Существенные пробелы" },
            ExecutiveConclusion = "Детерминированный вывод."
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(null, ctx);

        Assert.NotNull(sanitized);
        Assert.NotEmpty(sanitized.ExecutiveConclusion);
    }

    [Fact]
    public void QualityGate_RejectsPlaceholders_AndFallsBackToDeterministicActionPlan()
    {
        var ctx = new ReportContext
        {
            ActionPlan = new List<UnifiedActionItemDto>
            {
                new()
                {
                    ActionId = "ACT_FND_SPLIT",
                    Number = 1,
                    Title = "Закрепить доли сооснователей",
                    WhyNow = "Предотвращает корпоративный тупик (deadlock).",
                    ExpectedResult = "Будут защищены права основателей."
                }
            }
        };

        var raw = new ReportNarrativesDto
        {
            ActionNarratives = new Dictionary<string, ActionNarrativeItemDto>
            {
                ["ACT_FND_SPLIT"] = new()
                {
                    WhyNow = "Почему это нужно сделать на данном этапе",
                    ExpectedResult = "Ожидаемый практический результат"
                }
            }
        };

        var sanitized = ReportQualityGate.ValidateAndSanitize(raw, ctx);

        Assert.NotNull(sanitized.ActionNarratives["ACT_FND_SPLIT"]);
        Assert.Equal("Предотвращает корпоративный тупик (deadlock).", sanitized.ActionNarratives["ACT_FND_SPLIT"].WhyNow);
        Assert.Equal("Будут защищены права основателей.", sanitized.ActionNarratives["ACT_FND_SPLIT"].ExpectedResult);
    }
}
