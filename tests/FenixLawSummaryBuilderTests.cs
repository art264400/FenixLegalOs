using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class FenixLawSummaryBuilderTests
{
    [Fact]
    public void UserReportDomains_KeepActualSubjectsWithoutRepeatingEveryFinding()
    {
        var cards = new[]
        {
            Card("Права на продукт и интеллектуальную собственность", "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", "IP_DOMAIN_BRAND_CONTROL", "IP_EXTERNAL_DEPENDENCY", "IP_THIRD_PARTY_COMPONENTS"),
            Card("Основатели и корпоративная структура", "FND_GOVERNANCE_AMBIGUITY", "FND_NO_DEADLOCK_PROTECTION", "FND_NO_VESTING", "FND_DOCUMENTATION_GAP", "FND_EQUITY_NOT_FORMALIZED", "FND_EXIT_RULES_MISSING", "FND_INCOMPLETE_LEAVER_RULES"),
            Card("Подготовка к инвестициям (Data Room & DD)", "INVEST_DD_DOCS_NOT_READY", "INVEST_FUTURE_CAP_TABLE_UNCLEAR", "INVEST_DILUTION_NOT_MODELED", "INVEST_SELF_AWARENESS_GAP"),
            Card("Пользовательский контур и оферта", "PROD_MULTI_COUNTRY_REVIEW")
        };
        var source = JsonSerializer.Serialize(cards);
        var rows = cards.Select(FenixLawSummaryBuilder.Build).ToArray();
        Assert.Equal(4, rows.Length);
        Assert.Equal(16, cards.Sum(card => card.Items.Count));
        AssertBounded(rows);
        Assert.All(new[] { "прав", "бренд", "зависимост", "лицензи" }, word => Assert.Contains(word, rows[0].Task, StringComparison.OrdinalIgnoreCase));
        Assert.All(new[] { "управлен", "тупик", "участи", "дол", "уход", "договоренност" }, word => Assert.Contains(word, rows[1].Task, StringComparison.OrdinalIgnoreCase));
        Assert.All(new[] { "капитал", "дол", "документ", "юридическ" }, word => Assert.Contains(word, rows[2].Task, StringComparison.OrdinalIgnoreCase));
        Assert.Contains("стран пользователей", rows[3].Task);
        Assert.DoesNotContain("оферт", rows[3].Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("роли", rows[3].Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Подготовить", rows[3].Task, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(source, JsonSerializer.Serialize(cards));
    }

    [Fact]
    public void ActualExistingProductRules_OnlyNeedUpdating()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, _, _, context) = fixture.Assemble("real-problem");
        var card = Assert.Single(context.FenixLaw.ServiceCards);
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.Contains("Обновить пользовательские условия", row.Task);
        Assert.DoesNotContain("Подготовить", row.Task);
        Assert.DoesNotContain("оферт", row.Task, StringComparison.OrdinalIgnoreCase);
        AssertBounded(new[] { row });
    }

    [Theory]
    [InlineData("DATA_PRIVACY_NOTICE_OUTDATED", "Сопоставить существующую политику конфиденциальности с фактическими потоками данных и обновить расходящиеся положения.", "Обновить")]
    [InlineData("DATA_PRIVACY_NOTICE_MISSING", "Подготовить политику конфиденциальности по фактическим потокам данных и сделать ее доступной пользователям.", "Подготовить")]
    [InlineData("DATA_PRIVACY_NOTICE_MISSING", "Завершить уже начатую подготовку политики конфиденциальности по фактическим потокам данных и сделать ее доступной пользователям.", "Завершить подготовку")]
    public void PolicyStatus_DistinguishesUpdatingCreatingAndFinishing(string code, string recommendation, string expectedAction)
    {
        var card = Card("Персональные данные и процессы ИИ", code);
        card.Items[0].Recommendation = recommendation;
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.StartsWith(expectedAction + " политику конфиденциальности", row.Task);
        Assert.DoesNotContain("оферт", row.Task, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActualUnknownAnswers_StayConditionalInsteadOfCreatingServices()
    {
        using var fixture = new FenixLawScenarioFixture();
        var (_, _, _, context) = fixture.Assemble("unknown");
        Assert.NotEmpty(context.FenixLaw.ServiceCards);
        var rows = context.FenixLaw.ServiceCards.Select(FenixLawSummaryBuilder.Build).ToArray();
        AssertBounded(rows);
        Assert.All(rows, row =>
        {
            Assert.StartsWith("Уточнить ", row.Task);
            Assert.Contains("только по подтвержденным пробелам", row.Task);
            Assert.DoesNotContain("Подготовить", row.Task, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("отсутств", row.Task, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void MixedConfirmedAndUnknownItems_KeepRemediationAndVerification()
    {
        var card = Card("Персональные данные и процессы ИИ", "DATA_PRIVACY_NOTICE_OUTDATED", "AI_SENSITIVE_DATA_TRANSFER");
        card.Items[0].Recommendation = "Сопоставить существующую политику конфиденциальности с фактическими потоками данных и обновить расходящиеся положения.";
        card.Items[1].HasConfirmedIssue = false;
        card.Items[1].RequiresClarification = true;
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.Contains("Обновить", row.Task);
        Assert.Contains("Уточнить передачи данных в ИИ", row.Task, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("только подтвержденные недостатки", row.Task);
        AssertBounded(new[] { row });
    }

    [Theory]
    [InlineData("partial", true)]
    [InlineData("formal", false)]
    public void ActualPastInvestmentsWithoutNewRound_DoNotBecomeNewRoundPreparation(string priorStatus, bool documentationGap)
    {
        using var fixture = new FenixLawScenarioFixture();
        var answers = FenixLawScenarioFixtures.Answers("all-built");
        answers["INVEST-02"] = priorStatus;
        answers["INVEST-02A"] = "unknown";
        var (_, _, _, context) = fixture.Assemble(answers, "summary-existing-investments");
        var card = Assert.Single(context.FenixLaw.ServiceCards.Where(card => card.Items.Any(item => item.FindingCode == "INVEST_PRIOR_INVESTMENT_UNCLEAR")));
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.Equal("Инвестиционная история", row.Title);
        Assert.Contains("прошл", row.Task, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("инвестор", row.Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("раунд", row.Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Data Room", row.Task, StringComparison.OrdinalIgnoreCase);
        if (documentationGap)
        {
            Assert.Contains("Завершить оформление", row.Task);
            Assert.Contains("проверить", row.Task, StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            Assert.StartsWith("Уточнить ", row.Task);
            Assert.DoesNotContain("Завершить оформление", row.Task);
        }
        AssertBounded(new[] { row });
    }

    [Theory]
    [InlineData("ip", "Права на продукт и интеллектуальную собственность")]
    [InlineData("founders", "Основатели и корпоративная структура")]
    [InlineData("team", "Команда и привлеченные специалисты")]
    [InlineData("product", "Пользовательский контур и оферта")]
    [InlineData("data", "Персональные данные и процессы ИИ")]
    [InlineData("contracts", "Договорная обвязка и контрагенты")]
    [InlineData("investment", "Подготовка к инвестициям (Data Room & DD)")]
    public void EvenFullDomainRiskLists_FitWithoutDiscardingSourceItems(string section, string title)
    {
        var codes = DataBank.Risks.Where(risk => risk.SectionId == section || section == "founders" && risk.SectionId == "corporate").Select(risk => risk.Code).ToArray();
        var card = Card(title, codes);
        var before = JsonSerializer.Serialize(card);
        var row = FenixLawSummaryBuilder.Build(card);
        AssertBounded(new[] { row });
        Assert.NotEmpty(row.Task);
        Assert.Equal(codes.Length, card.Items.Count);
        Assert.Equal(before, JsonSerializer.Serialize(card));
        // Dense sections use an explicit complete-plan reference rather than clipping text.
        if (row.Task.Contains("этого направления"))
            Assert.Contains(codes.Length.ToString(), row.Task);
    }

    [Fact]
    public void LegacyDescription_IsBoundedAtACompleteSentence()
    {
        var card = new FenixLawServiceCardDto { Title = "Прочее", Description = "Проверить действующий договор. " + new string('я', 400) };
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.Equal("Проверить действующий договор.", row.Task);
        card.Description = "Проверить " + new string('я', 400);
        row = FenixLawSummaryBuilder.Build(card);
        Assert.Contains("плану отчета", row.Task);
        Assert.DoesNotContain("яя", row.Task);
        card.Description = "Все договоры отсутствуют и нужна разработка полного комплекта.";
        row = FenixLawSummaryBuilder.Build(card);
        Assert.StartsWith("Уточнить", row.Task);
        Assert.DoesNotContain("отсутствуют", row.Task);
        AssertBounded(new[] { row });
    }

    [Fact]
    public void UnknownLegacyCode_DoesNotInventAPackageOrTruncateAnUnfinishedInstruction()
    {
        var card = new FenixLawServiceCardDto
        {
            Title = "Прочее",
            Items = new() { new() { FindingCode = "CUSTOM_CASE", Recommendation = "Проверить " + new string('я', 400) } }
        };
        var row = FenixLawSummaryBuilder.Build(card);
        Assert.Equal("Выполнить адресные рекомендации из плана отчета.", row.Task);
        Assert.DoesNotContain("оферт", row.Task, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("учред", row.Task, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertBounded(IEnumerable<FenixLawSummaryRow> rows) =>
        Assert.All(rows, row => Assert.InRange(row.Task.Length, 1, FenixLawSummaryBuilder.MaximumTaskLength));

    private static FenixLawServiceCardDto Card(string title, params string[] codes) => new()
    {
        Title = title,
        SourceFindingCodes = codes.ToList(),
        Items = codes.Select(code =>
        {
            var risk = DataBank.Risks.Single(risk => risk.Code == code);
            return new FenixLawServiceItemDto { FindingCode = code, Title = risk.Title, Recommendation = string.Join(" ", risk.Recommendations), HasConfirmedIssue = true };
        }).ToList()
    };
}
