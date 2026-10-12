using FenixLegalOs.Data;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class RecommendationNormalizationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GeneratedList_KeepsItsLength_WithoutCanonicalOrLegacyPadding(int count)
    {
        var generated = new[] { "Первый самостоятельный шаг.", "Второй самостоятельный шаг.", "Третий самостоятельный шаг." }.Take(count).ToList();
        var canonical = new[] { "Дополнительное действие из справочника.", "Еще одно действие из справочника." };

        var result = ChunkedNarrativeValidator.NormalizeRecommendations(generated, canonical, "Общее описание рекомендации.");

        Assert.Equal(generated, result);
    }

    [Fact]
    public void DeadlockCanonicalList_StaysAtTwo_WithoutLegacySummaryAsThirdStep()
    {
        var finding = DataBank.Risks.Single(r => r.Code == "FND_NO_DEADLOCK_PROTECTION");

        var result = ChunkedNarrativeValidator.NormalizeRecommendations(null, finding.Recommendations, finding.Recommendation);

        Assert.Equal(2, result.Count);
        Assert.Equal(finding.Recommendations, result);
        Assert.DoesNotContain(finding.Recommendation, result);
    }

    [Fact]
    public void DuplicateGeneratedItems_DoNotTriggerPaddingFromFallbackSources()
    {
        var result = ChunkedNarrativeValidator.NormalizeRecommendations(
            new[] { "Оформить согласованный механизм.", "оформить согласованный механизм.", "Оформить согласованный механизм. После согласования." },
            new[] { "Другой шаг из справочника." },
            "Общая рекомендация.");

        Assert.Equal(new[] { "Оформить согласованный механизм." }, result);
    }

    [Fact]
    public void EmptyGeneratedItems_UseCanonicalList_WithoutLegacyPadding()
    {
        var canonical = new[] { "Согласовать механизм.", "Закрепить механизм письменно." };

        var result = ChunkedNarrativeValidator.NormalizeRecommendations(new[] { "", "  " }, canonical, "Общий совет.");

        Assert.Equal(canonical, result);
    }

    [Fact]
    public void MissingLists_UseLegacyRecommendationAsOneItem()
    {
        var result = ChunkedNarrativeValidator.NormalizeRecommendations(null, null, "Оформить согласованный механизм.");

        Assert.Equal(new[] { "Оформить согласованный механизм." }, result);
    }

    [Fact]
    public void EmptySources_StayEmpty()
    {
        var result = ChunkedNarrativeValidator.NormalizeRecommendations(null, Array.Empty<string>(), " ");

        Assert.Empty(result);
    }

    [Fact]
    public void LongGeneratedList_RemainsLimitedToThreeItems()
    {
        var generated = new[] { "Согласовать структуру.", "Проверить документы.", "Закрепить результат.", "Подготовить дополнительные материалы." };

        var result = ChunkedNarrativeValidator.NormalizeRecommendations(generated, null, null);

        Assert.Equal(generated.Take(3), result);
    }
}
