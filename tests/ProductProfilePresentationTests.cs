using FenixLegalOs.Models;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class ProductProfilePresentationTests
{
    public static IEnumerable<object[]> ProductAndUsageAnswers()
    {
        var products = new[]
        {
            (Answer: "idea", State: "Идея"),
            (Answer: "prototype", State: "Прототип / тестовая версия"),
            (Answer: "ready", State: "Готовый продукт"),
            (Answer: "multiple", State: "Несколько продуктов")
        };
        var usage = new[]
        {
            (Answer: "prelaunch", Description: "Пользователей пока нет"),
            (Answer: "first", Description: "Есть первые пользователи"),
            (Answer: "regular", Description: "Есть постоянные пользователи"),
            (Answer: "large", Description: "Используется большим количеством пользователей")
        };
        foreach (var product in products)
        foreach (var userStage in usage)
            yield return new object[] { product.Answer, product.State, userStage.Answer, userStage.Description };
    }

    [Theory]
    [MemberData(nameof(ProductAndUsageAnswers))]
    public void QuestionnaireAnswers_KeepProductReadinessAndUsageSeparateAcrossReport(
        string productAnswer, string expectedState, string usageAnswer, string expectedUsage)
    {
        var facts = FactNormalizer.NormalizeFacts(new Dictionary<string, object>
        {
            ["IP-01"] = productAnswer,
            ["PROD-01"] = usageAnswer,
            ["PROD-02"] = new[] { "companies", "client_employees" }
        });
        var context = Assemble(facts);
        var productBlock = context.Profile.ConfigurationBlocks.Single(block => block.Label == "Продукт и стадия");
        var noUsers = usageAnswer == "prelaunch";
        var expectedAudience = noUsers ? "Пользователей пока нет" : "Компании, Сотрудники компаний-клиентов";
        var expectedBlock = $"Продукт: {expectedState}.\nИспользование: {expectedUsage}."
            + (noUsers ? string.Empty : $"\nАудитория: {expectedAudience}.");

        Assert.Equal(expectedState, Fact(context.Profile, "stage"));
        Assert.Equal(expectedUsage, Fact(context.Profile, "usage"));
        Assert.Equal(expectedAudience, Fact(context.Profile, "users"));
        Assert.Equal(expectedBlock, productBlock.Value);
        Assert.Equal(expectedState, context.ProjectStage);
        Assert.Contains($"Продукт: {expectedState}.", context.Profile.ConfigurationNarrative);
        Assert.Contains($"Использование: {expectedUsage}.", context.Profile.ConfigurationNarrative);
        Assert.DoesNotContain("MVP", context.Profile.ConfigurationNarrative);
        Assert.DoesNotContain("Коммерческий запуск", context.Profile.ConfigurationNarrative);
        if (noUsers)
        {
            Assert.DoesNotContain("Аудитория:", productBlock.Value);
            Assert.DoesNotContain("Сотрудники компаний-клиентов", context.Profile.ConfigurationNarrative);
        }
        else
        {
            Assert.Contains($"Аудитория: {expectedAudience}.", context.Profile.ConfigurationNarrative);
        }

        var compact = ContextSelector.ExtractCompactSharedContext(context);
        Assert.Contains(expectedState, compact.ProductStage);
        Assert.Contains(expectedUsage, compact.ProductStage);
        Assert.Equal(expectedAudience, compact.Users);
        Assert.DoesNotContain("MVP", compact.ProductStage);
    }

    [Theory]
    [InlineData("ready", "Готовый продукт")]
    [InlineData("live_or_ready", "Готовый продукт")]
    [InlineData("multiple", "Несколько продуктов")]
    [InlineData("multiple_products", "Несколько продуктов")]
    public void StoredProductStateAliases_UseTheSameTextInHeaderAndProfile(string storedState, string expected)
    {
        var facts = new SharedFactStore();
        facts.Facts["product.stage"] = storedState;
        var context = Assemble(facts);

        Assert.Equal(expected, context.ProjectStage);
        Assert.Equal(expected, Fact(context.Profile, "stage"));
        Assert.Contains($"Продукт: {expected}.", context.Profile.ConfigurationNarrative);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("mvp")]
    [InlineData("first")]
    [InlineData("commercial")]
    [InlineData("scaling")]
    [InlineData("regular")]
    [InlineData("large")]
    public void MissingOrUnsupportedProductState_IsNotInferredFromUsage(string? storedState)
    {
        var facts = new SharedFactStore();
        if (storedState != null) facts.Facts["product.stage"] = storedState;
        facts.Facts["product.userStage"] = "large";
        facts.Facts["product.liveUsers"] = true;
        var context = Assemble(facts);

        Assert.Equal("Не указано", context.ProjectStage);
        Assert.Equal("Не указано", Fact(context.Profile, "stage"));
        Assert.Contains("Продукт: Не указано.", context.Profile.ConfigurationNarrative);
        Assert.Equal("Используется большим количеством пользователей", Fact(context.Profile, "usage"));
        Assert.DoesNotContain("MVP", context.Profile.ConfigurationNarrative);
        Assert.DoesNotContain("Коммерческий запуск", context.ProjectStage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void MissingOrUnsupportedUsage_IsExplicitlyUnknown(string? storedUsage)
    {
        var facts = new SharedFactStore();
        facts.Facts["product.stage"] = "live_or_ready";
        if (storedUsage != null) facts.Facts["product.userStage"] = storedUsage;
        var profile = ProjectProfileExtractor.ExtractProfile(facts);

        Assert.Equal("Не указано", Fact(profile, "usage"));
        Assert.Equal("Не указано", Fact(profile, "users"));
        Assert.Contains("Использование: Не указано.", profile.ConfigurationBlocks.Single(block => block.Label == "Продукт и стадия").Value);
        Assert.Contains("Использование: Не указано.", profile.ConfigurationNarrative);
    }

    [Fact]
    public void ExplicitlyNoLiveUsers_SuppressesStaleAudienceEvenWithoutUsageStage()
    {
        var facts = new SharedFactStore();
        facts.Facts["product.stage"] = "live_or_ready";
        facts.Facts["product.liveUsers"] = false;
        facts.Facts["product.userTypes"] = new[] { "companies", "consumers" };
        facts.Facts["product.targetAudience"] = "b2b";
        var profile = ProjectProfileExtractor.ExtractProfile(facts);
        var block = profile.ConfigurationBlocks.Single(item => item.Label == "Продукт и стадия");

        Assert.Equal("Пользователей пока нет", Fact(profile, "usage"));
        Assert.Equal("Пользователей пока нет", Fact(profile, "users"));
        Assert.Equal("Продукт: Готовый продукт.\nИспользование: Пользователей пока нет.", block.Value);
        Assert.DoesNotContain("Аудитория:", profile.ConfigurationNarrative);
        Assert.DoesNotContain("Компании", block.Value);
        Assert.DoesNotContain("Обычные физические лица", block.Value);
    }

    [Fact]
    public void UnknownAudienceCategory_DoesNotInventAnAudience()
    {
        var facts = FactNormalizer.NormalizeFacts(new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["PROD-01"] = "regular",
            ["PROD-02"] = new[] { "unsupported_category" }
        });
        var profile = ProjectProfileExtractor.ExtractProfile(facts);

        Assert.Equal("Не указано", Fact(profile, "users"));
        Assert.Contains("Аудитория: Не указано.", profile.ConfigurationBlocks.Single(block => block.Label == "Продукт и стадия").Value);
        Assert.DoesNotContain("Другая категория", profile.ConfigurationNarrative);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("registering")]
    [InlineData("single")]
    public void EntityRegistration_DoesNotDetermineProductReadiness(string entityStatus)
    {
        var facts = FactNormalizer.NormalizeFacts(new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["PROD-01"] = "regular"
        });
        facts.Facts["company.entityStatus"] = entityStatus;
        var context = Assemble(facts);

        Assert.Equal("Готовый продукт", context.ProjectStage);
        Assert.Equal("Готовый продукт", Fact(context.Profile, "stage"));
        Assert.Contains("Использование: Есть постоянные пользователи.", context.Profile.ConfigurationNarrative);
        Assert.DoesNotContain("ранней стадии", context.Profile.ConfigurationNarrative);
    }

    [Fact]
    public void UsageFirstAlias_PreservesExplicitMeaningWithoutMvpInference()
    {
        var facts = new SharedFactStore();
        facts.Facts["product.stage"] = "prototype";
        facts.Facts["product.userStage"] = "first";
        var context = Assemble(facts);

        Assert.Equal("Прототип / тестовая версия", context.ProjectStage);
        Assert.Equal("Есть первые пользователи", Fact(context.Profile, "usage"));
        Assert.DoesNotContain("MVP", context.Profile.ConfigurationNarrative);
    }

    private static string Fact(ProjectProfileDto profile, string key) =>
        profile.KeyFacts.Single(fact => fact.Key == key).Value;

    private static ReportContext Assemble(SharedFactStore facts) =>
        ReportEngine.AssembleReportContext(new ScoreResult { Overall = 75 }, facts, "product-profile-test", "Тестовый проект");
}
