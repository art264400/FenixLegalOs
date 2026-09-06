using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FenixLegalOs.Data;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace FenixLegalOs.Tests;

public class VerificationPdfRunner
{
    private class TestEnv : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = @"C:\Users\Arthur\Desktop\fenix-legal-os-20260809T173328Z-1-001\fenix-legal-os-dotnet\wwwroot";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string ContentRootPath { get; set; } = @"C:\Users\Arthur\Desktop\fenix-legal-os-20260809T173328Z-1-001\fenix-legal-os-dotnet";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact(DisplayName = "Generate Full Official Review PDF Report for Verification")]
    public async Task GenerateOfficialReviewPdfReport()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.entityStatus"] = "incorporated";
        facts.Facts["company.primaryJurisdiction"] = "kz";
        facts.Facts["founders.count"] = "2";
        facts.Facts["founders.isEqual5050"] = true;
        facts.Facts["product.stage"] = "prototype";
        facts.Facts["ip.creators"] = "both";
        facts.Facts["ip.overallRightsEvidence"] = "partial";
        facts.Facts["investment.timing"] = "round_1yr";

        var foundersRisks = FoundersRisks.All.Where(r => r.Code is "FND_DEADLOCK" or "FND_NO_VESTING" or "FND_DOCUMENTATION_GAP" or "FND_EQUITY_NOT_FORMALIZED" or "FND_EXIT_RULES_MISSING")
            .Select(r => new RiskFinding
            {
                Code = r.Code,
                SectionId = r.SectionId,
                Title = r.Title,
                Severity = r.Severity,
                Priority = r.Priority,
                ResolutionMode = r.ResolutionMode,
                Finding = r.Finding,
                WhyItMatters = r.WhyItMatters,
                Recommendation = r.Recommendation,
                Recommendations = r.Recommendations?.ToList() ?? new List<string>(),
                RootCauseGroup = r.RootCauseGroup,
                AffectedDimensions = r.AffectedDimensions.ToList()
            }).ToList();

        var corpRisks = CorporateRisks.All.Where(r => r.Code is "COR_CAP_TABLE_UNRELIABLE" or "COR_UNDOCUMENTED_EQUITY")
            .Select(r => new RiskFinding
            {
                Code = r.Code,
                SectionId = r.SectionId,
                Title = r.Title,
                Severity = r.Severity,
                Priority = r.Priority,
                ResolutionMode = r.ResolutionMode,
                Finding = r.Finding,
                WhyItMatters = r.WhyItMatters,
                Recommendation = r.Recommendation,
                Recommendations = r.Recommendations?.ToList() ?? new List<string>(),
                RootCauseGroup = r.RootCauseGroup,
                AffectedDimensions = r.AffectedDimensions.ToList()
            }).ToList();

        var ipRisks = IpRisks.All.Where(r => r.Code is "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" or "IP_CONTRACTOR_RIGHTS_GAP" or "IP_PRODUCT_RIGHTS_UNCONFIRMED")
            .Select(r => new RiskFinding
            {
                Code = r.Code,
                SectionId = r.SectionId,
                Title = r.Title,
                Severity = r.Severity,
                Priority = r.Priority,
                ResolutionMode = r.ResolutionMode,
                Finding = r.Finding,
                WhyItMatters = r.WhyItMatters,
                Recommendation = r.Recommendation,
                Recommendations = r.Recommendations?.ToList() ?? new List<string>(),
                RootCauseGroup = r.RootCauseGroup,
                AffectedDimensions = r.AffectedDimensions.ToList()
            }).ToList();

        var dataRisks = DataAiRisks.All.Where(r => r.Code is "DATA_PRIVACY_NOTICE_OUTDATED" or "DATA_AI_TRAINING_UNCHECKED" or "AI_USER_DATA_TRANSFER" or "DATA_MAP_INCOMPLETE")
            .Select(r => new RiskFinding
            {
                Code = r.Code,
                SectionId = r.SectionId,
                Title = r.Title,
                Severity = r.Severity,
                Priority = r.Priority,
                ResolutionMode = r.ResolutionMode,
                Finding = r.Finding,
                WhyItMatters = r.WhyItMatters,
                Recommendation = r.Recommendation,
                Recommendations = r.Recommendations?.ToList() ?? new List<string>(),
                RootCauseGroup = r.RootCauseGroup,
                AffectedDimensions = r.AffectedDimensions.ToList()
            }).ToList();

        var allRisks = foundersRisks.Concat(corpRisks).Concat(ipRisks).Concat(dataRisks).ToList();

        var scoreResult = new ScoreResult
        {
            Overall = 48,
            Confidence = 73,
            AnsweredCount = 28,
            Level = LegalScoreLevel.MaterialGaps,
            Risks = allRisks,
            Strengths = new List<string> { "Регистрация юридического лица", "Отсутствие корпоративных споров" },
            Sections = new List<SectionScore>
            {
                new()
                {
                    SectionId = "founders",
                    Title = "Сооснователи",
                    Score = 41,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "deadlock", Score = 30 },
                        new() { DimensionId = "early_exit_equity", Score = 35 },
                        new() { DimensionId = "governance", Score = 55 }
                    }
                },
                new()
                {
                    SectionId = "corporate",
                    Title = "Корпоративная структура",
                    Score = 55,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "ownership_accuracy", Score = 90 },
                        new() { DimensionId = "cap_table", Score = 40 }
                    }
                },
                new()
                {
                    SectionId = "ip",
                    Title = "Интеллектуальная собственность",
                    Score = 38,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "founder_rights", Score = 30 },
                        new() { DimensionId = "external_creators", Score = 40 }
                    }
                },
                new()
                {
                    SectionId = "data",
                    Title = "Данные и ИИ",
                    Score = 44,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "privacy_notice", Score = 40 },
                        new() { DimensionId = "ai_external_data", Score = 45 }
                    }
                },
                new()
                {
                    SectionId = "team",
                    Title = "Команда и сотрудники",
                    Score = 50,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "written_agreements", Score = 50 }
                    }
                },
                new()
                {
                    SectionId = "contracts",
                    Title = "Договоры и контрагенты",
                    Score = 65,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "written_form", Score = 65 }
                    }
                },
                new()
                {
                    SectionId = "product",
                    Title = "Продукт и пользователи",
                    Score = 60,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "rules_presence", Score = 60 }
                    }
                },
                new()
                {
                    SectionId = "investment",
                    Title = "Инвестиционная готовность",
                    Score = 78,
                    Status = ApplicabilityStatus.Applicable,
                    Dimensions = new List<DimensionScore>
                    {
                        new() { DimensionId = "dd_documents", Score = 78 }
                    }
                }
            }
        };

        ReportNarrativesDto? parsedNarratives = null;
        string outputLlmPath = @"C:\Users\Arthur\Downloads\outputLLM.txt";
        if (File.Exists(outputLlmPath))
        {
            var rawText = await File.ReadAllTextAsync(outputLlmPath);
            parsedNarratives = JsonSerializer.Deserialize<ReportNarrativesDto>(rawText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var testEnv = new TestEnv();
        var aiService = new AiReportService(new ConfigurationBuilder().Build());
        var pdfService = new TypstPdfService(testEnv, aiService);

        var pdfBytes = await pdfService.GeneratePdfAsync(scoreResult, facts, "43a9709b-dc75-4c19-a151-2ea0bcc1fa78", "Стартап", parsedNarratives);

        Assert.NotNull(pdfBytes);
        Assert.True(pdfBytes.Length > 50000, "PDF should be successfully rendered and non-empty");

        var artifactDir = @"C:\Users\Arthur\.gemini\antigravity\brain\60cb8fcd-d78f-4143-ba82-e5c7356400bd";
        var outPdfPath = Path.Combine(artifactDir, "Fenix_SLS_Report_Verified_P0.pdf");
        await File.WriteAllBytesAsync(outPdfPath, pdfBytes);

        var previewDir = Path.Combine(artifactDir, "verif_preview");
        Directory.CreateDirectory(previewDir);

        var markup = pdfService.BuildTypstMarkup(ReportEngine.AssembleReportContext(scoreResult, facts, "43a9709b-dc75-4c19-a151-2ea0bcc1fa78", "Стартап"));
        var typFile = Path.Combine(testEnv.ContentRootPath, "verif_report.typ");
        await File.WriteAllTextAsync(typFile, markup);

        var typstExe = Path.Combine(testEnv.ContentRootPath, "typst.exe");
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = typstExe,
            Arguments = $"compile --root \"{testEnv.ContentRootPath}\" --format png --ppi 144 \"{typFile}\" \"{Path.Combine(previewDir, "page-{n}.png")}\"",
            WorkingDirectory = testEnv.ContentRootPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using (var proc = System.Diagnostics.Process.Start(psi))
        {
            proc?.WaitForExit();
        }

        Assert.True(File.Exists(outPdfPath), "PDF file should be created in artifacts directory");
    }
}
