using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FenixLegalOs.Scoring.Report;
using Xunit;

namespace FenixLegalOs.Tests;

public class FenixLawRecommendationPdfTests
{
    [Fact]
    public async Task GenerateFiveGroundedFenixLawPdfs_WithActualAnswersAndNoExternalModel()
    {
        using var fixture = new FenixLawScenarioFixture();
        var directory = Path.Combine(fixture.Root, "tmp", "pdfs", "fenix-law-grounded");
        Directory.CreateDirectory(directory);
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };
        foreach (var scenario in new[] { "all-built", "real-problem", "mixed", "unknown", "no-problems" })
        {
            var (answers, result, facts, context) = fixture.Assemble(scenario);
            var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
            // The service must keep the independently assembled scope even when generated prose adds unrelated services.
            const string unrelatedServices = "Нужно разработать новое фаундерское соглашение и новую политику конфиденциальности. Требуется полный юридический аудит и новая оферта.";
            narratives.FenixLawRecommendation = unrelatedServices;
            var sanitized = ReportQualityGate.ValidateAndSanitize(narratives, context);
            Assert.Equal(context.FenixLaw.SummaryText, sanitized.FenixLawRecommendation);
            Assert.DoesNotContain(unrelatedServices, sanitized.FenixLawRecommendation);
            var markup = fixture.Pdf.BuildTypstMarkup(context);
            Assert.DoesNotContain(unrelatedServices, markup);
            Assert.All(context.FenixLaw.ServiceCards.SelectMany(card => card.Items), item => Assert.Contains(item.Title, markup));
            var pdf = await fixture.Pdf.GeneratePdfAsync(result, facts, context.SessionId, context.ProjectName, rawNarratives: narratives);
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 10000, $"PDF too small for {scenario}");
            Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Take(5).ToArray()));
            await File.WriteAllBytesAsync(Path.Combine(directory, scenario + ".pdf"), pdf);
            await File.WriteAllTextAsync(Path.Combine(directory, scenario + ".typ"), markup);
            await File.WriteAllTextAsync(Path.Combine(directory, scenario + ".json"), JsonSerializer.Serialize(new { scenario, answers, result, facts, context, narratives, sanitized }, jsonOptions));
        }
    }
}