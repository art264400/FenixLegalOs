using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class RecommendationPdfRegressionTests
{
    [Fact]
    public async Task DeadlockFinding_WithTwoCanonicalRecommendations_DoesNotAppendLegacySummaryInPdf()
    {
        const string code = "FND_NO_DEADLOCK_PROTECTION";
        const string sessionId = "two-recommendations";
        const string company = "Проверка · две рекомендации без повторов";
        var definition = DataBank.Risks.Single(risk => risk.Code == code);
        Assert.Equal(new[] { "Согласовать финальный способ выхода из тупика.", "Закрепить механизм письменно." }, definition.Recommendations);
        Assert.NotEmpty(definition.Recommendation);
        Assert.DoesNotContain(definition.Recommendation, definition.Recommendations);

        var risk = new RiskFinding
        {
            Code = definition.Code,
            RootCauseGroup = definition.RootCauseGroup,
            Severity = definition.Severity,
            Priority = definition.Priority,
            SectionId = definition.SectionId,
            Modules = definition.Modules.ToList(),
            Title = definition.Title,
            Finding = definition.Finding,
            WhyItMatters = definition.WhyItMatters,
            Recommendations = definition.Recommendations.ToList(),
            Recommendation = definition.Recommendation,
            Basis = new() { new() { QuestionId = "FND-07", AnswerId = "only_agree" } },
            LawyerRequired = definition.LawyerRequired,
            Resolution = definition.Resolution,
            ResolutionMode = definition.ResolutionMode,
            ServiceCode = definition.ServiceCode,
            AffectedDimensions = definition.AffectedDimensions.ToList()
        };
        var result = new ScoreResult
        {
            Overall = 65,
            Confidence = 100,
            HighCount = 1,
            Risks = new() { risk },
            Sections = DataBank.Sections.Select(section => new SectionScore
            {
                SectionId = section.Id,
                Title = section.Title,
                Weight = section.Weight,
                Score = section.Id == "founders" ? 65 : null,
                Status = section.Id == "founders" ? ApplicabilityStatus.Applicable : ApplicabilityStatus.NotApplicable
            }).ToList(),
            InvestmentReadiness = new() { Applicable = false }
        };
        var facts = new SharedFactStore
        {
            Facts = new()
            {
                ["founders.count"] = "2",
                ["founders.isEqual5050"] = true,
                ["company.entityStatus"] = "none",
                ["company.primaryJurisdiction"] = "kz",
                ["product.stage"] = "prototype",
                ["product.usage"] = "no",
                ["investment.timing"] = "none",
                ["investment.priorInvestment"] = false
            }
        };
        var context = ReportEngine.AssembleReportContext(result, facts, sessionId, company);
        var finding = Assert.Single(Assert.Single(context.FocusModules).Findings);
        Assert.Equal(code, finding.FindingCode);
        Assert.Equal(definition.Recommendations, finding.Recommendations);
        var narratives = DeterministicFallbackNarratives.GenerateFallbackNarratives(context);
        var sanitized = ReportQualityGate.ValidateAndSanitize(narratives, context);
        var sanitizedFinding = sanitized.ModuleNarratives["founders"].FindingNarratives[code];
        var recommendations = Assert.IsType<List<string>>(sanitizedFinding.Recommendations);
        Assert.Equal(2, recommendations.Count);
        Assert.Equal(definition.Recommendations, recommendations);
        Assert.DoesNotContain(definition.Recommendation, recommendations);

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AiSettings:StrictLlm"] = "false"
        }).Build();
        var pdfService = new TypstPdfService(new TestWebHostEnvironment(root), new AiReportService(config, new NoNetworkLlmClient()));
        var pdf = await pdfService.GeneratePdfAsync(result, facts, sessionId, company, rawNarratives: narratives);
        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 10000);
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(pdf.Take(5).ToArray()));

        // Apply the same sanitized recommendation list for the inspectable Typst artifact.
        finding.Recommendation = recommendations[0];
        finding.Recommendations = recommendations;
        var markup = pdfService.BuildTypstMarkup(context);
        Assert.DoesNotContain(definition.Recommendation, markup);
        var directory = Path.Combine(root, "tmp", "pdfs", "recommendation-count");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, sessionId + ".pdf"), pdf);
        await File.WriteAllTextAsync(Path.Combine(directory, sessionId + ".typ"), markup);
        await File.WriteAllTextAsync(Path.Combine(directory, sessionId + ".json"), JsonSerializer.Serialize(new { definition, result, facts, context, narratives, sanitized }, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }));
    }

    private sealed class TestWebHostEnvironment(string root) : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Path.Combine(root, "wwwroot");
        public string ContentRootPath { get; set; } = root;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private sealed class NoNetworkLlmClient : ILlmNarrativeClient
    {
        public bool IsConfigured => false;
        public Task<(ModuleNarrativeResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateModuleNarrativeAsync(ModuleNarrativeRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF regression must never call a model.");
        public Task<(ActionBatchResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateActionBatchNarrativeAsync(ActionBatchRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF regression must never call a model.");
        public Task<(ExecutiveSynthesisResponseDto? Response, NarrativeGenerationMetrics Metrics)> GenerateExecutiveSynthesisAsync(ExecutiveSynthesisRequestDto request, CancellationToken ct = default) => throw new InvalidOperationException("PDF regression must never call a model.");
    }
}