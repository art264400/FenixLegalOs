using System;
using System.Collections.Generic;
using System.Text.Json;
using FenixLegalOs.Controllers;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class SessionsControllerTests
{
    private readonly SessionsController _controller;

    public SessionsControllerTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_ctrl_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var qRepo = new QuestionRepository(dbInit);
        var sRepo = new SessionRepository(dbInit);
        var lRepo = new LeadRepository(dbInit);
        var setRepo = new SettingsRepository(dbInit);
        var scoringEngine = new ScoringEngine(qRepo);
        
        var testEnv = new TestWebHostEnvironment();
        var aiReportService = new AiReportService(config);
        var pdfService = new TypstPdfService(testEnv, aiReportService);

        _controller = new SessionsController(sRepo, lRepo, scoringEngine, pdfService, aiReportService, setRepo, qRepo);
    }

    private class TestWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact(DisplayName = "1. Navigate endpoint returns full NavigationState with valid currentQuestionId on fresh session")]
    public void Navigate_Returns_Valid_CurrentQuestionId_On_Fresh_Session()
    {
        var createResult = _controller.CreateSession() as OkObjectResult;
        Assert.NotNull(createResult);
        var sessionId = createResult.Value?.GetType().GetProperty("id")?.GetValue(createResult.Value)?.ToString();
        Assert.NotNull(sessionId);

        // Call navigate with empty answers
        var body = JsonDocument.Parse("{\"answers\":{},\"currentQuestionId\":null}").RootElement;
        var navResult = _controller.Navigate(sessionId, body) as OkObjectResult;
        Assert.NotNull(navResult);

        var navState = navResult.Value as NavigationState;
        Assert.NotNull(navState);
        Assert.NotNull(navState.CurrentQuestionId);
        Assert.Equal("FND-C01", navState.CurrentQuestionId);
        Assert.NotEmpty(navState.VisibleQuestionIds);
        Assert.Equal(1, navState.Current);
        Assert.True(navState.TotalVisible > 0);
    }

    [Fact(DisplayName = "2. Sequential questionnaire ordering flows section by section")]
    public void Questionnaire_Flows_Section_By_Section()
    {
        var createResult = _controller.CreateSession() as OkObjectResult;
        var sessionId = createResult?.Value?.GetType().GetProperty("id")?.GetValue(createResult.Value)?.ToString()!;

        // Answer FND-C01 = two
        var body = JsonDocument.Parse("{\"answers\":{\"FND-C01\":\"two\"},\"currentQuestionId\":\"FND-C01\"}").RootElement;
        var navResult = _controller.Navigate(sessionId, body) as OkObjectResult;
        var navState = navResult?.Value as NavigationState;

        Assert.NotNull(navState);
        Assert.Equal("FND-C02", navState.NextQuestionId);

        // Verify visibleQuestionIds order: all FND questions before COR questions, before IP, before TEAM, before PROD
        var fndIndices = navState.VisibleQuestionIds.Select((id, idx) => (id, idx)).Where(x => x.id.StartsWith("FND-")).Select(x => x.idx).ToList();
        var corIndices = navState.VisibleQuestionIds.Select((id, idx) => (id, idx)).Where(x => x.id.StartsWith("COR-")).Select(x => x.idx).ToList();
        var ipIndices = navState.VisibleQuestionIds.Select((id, idx) => (id, idx)).Where(x => x.id.StartsWith("IP-")).Select(x => x.idx).ToList();
        var teamIndices = navState.VisibleQuestionIds.Select((id, idx) => (id, idx)).Where(x => x.id.StartsWith("TEAM-")).Select(x => x.idx).ToList();
        var prodIndices = navState.VisibleQuestionIds.Select((id, idx) => (id, idx)).Where(x => x.id.StartsWith("PROD-")).Select(x => x.idx).ToList();

        Assert.True(fndIndices.Max() < corIndices.Min(), "All Founders questions must precede Corporate questions");
        Assert.True(corIndices.Max() < ipIndices.Min(), "All Corporate questions must precede IP questions");
        Assert.True(ipIndices.Max() < teamIndices.Min(), "All IP questions must precede Team questions");
        Assert.True(teamIndices.Max() < prodIndices.Min(), "All Team questions must precede Product questions");
    }

    [Fact(DisplayName = "3. Answering FND-C01 as solo advances directly to COR-C01 without looping or snapping back")]
    public void Answering_Solo_Advances_Directly_To_Corporate()
    {
        var createResult = _controller.CreateSession() as OkObjectResult;
        var sessionId = createResult?.Value?.GetType().GetProperty("id")?.GetValue(createResult.Value)?.ToString()!;

        // Save answers: FND-C01 = solo with answeredQuestionId = FND-C01
        var body = JsonDocument.Parse("{\"answers\":{\"FND-C01\":\"solo\"},\"answeredQuestionId\":\"FND-C01\",\"lastSectionId\":\"founders\"}").RootElement;
        var saveResult = _controller.SaveAnswers(sessionId, body) as OkObjectResult;
        Assert.NotNull(saveResult);

        var navProp = saveResult.Value?.GetType().GetProperty("navigation")?.GetValue(saveResult.Value) as NavigationState;
        Assert.NotNull(navProp);
        Assert.Equal("COR-C01", navProp.CurrentQuestionId);
        Assert.DoesNotContain("FND-C02", navProp.VisibleQuestionIds);
    }

    [Fact(DisplayName = "4. AdminController TestBench PDF Generation returns valid non-empty PDF")]
    public async Task AdminController_TestBench_GeneratePdf_Returns_Valid_Pdf()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_admin_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var qRepo = new QuestionRepository(dbInit);
        var rRepo = new RiskRepository(dbInit);
        var lRepo = new LeadRepository(dbInit);
        var setRepo = new SettingsRepository(dbInit);
        var scoringEngine = new ScoringEngine(qRepo);
        var testEnv = new TestWebHostEnvironment();
        var aiReportService = new AiReportService(config);
        var pdfService = new TypstPdfService(testEnv, aiReportService, setRepo);

        var adminCtrl = new AdminController(lRepo, qRepo, rRepo, scoringEngine, aiReportService, setRepo, pdfService);
        var httpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        adminCtrl.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Login as admin
        var loginBody = JsonDocument.Parse("{\"password\":\"fenix2026\"}").RootElement;
        var loginRes = adminCtrl.Login(loginBody) as OkObjectResult;
        Assert.NotNull(loginRes);

        var tokensField = typeof(AdminController).GetField("AdminTokens", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var tokensSet = tokensField?.GetValue(null) as HashSet<string>;
        var validToken = tokensSet?.FirstOrDefault() ?? "test_token";
        if (tokensSet != null && !tokensSet.Contains(validToken)) tokensSet.Add(validToken);
        httpContext.Request.Headers["Cookie"] = $"fenix_admin={validToken}";

        var fullAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design", "app" }),
            ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders" }),
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
            ["PROD-01"] = "regular",
            ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "consumers" }),
            ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "app", "website" }),
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked",
            ["PROD-06"] = "mostly",
            ["PROD-10"] = "subscription",
            ["DATA-01"] = "yes",
            ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact", "account", "payment" }),
            ["DATA-03"] = "no",
            ["DATA-04"] = JsonSerializer.SerializeToElement(new[] { "user" }),
            ["DATA-05"] = "none",
            ["DATA-06"] = "preparing",
            ["AI-01"] = "yes",
            ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
            ["INVEST-01"] = "none"
        };

        var generateBody = JsonDocument.Parse(JsonSerializer.Serialize(new { answers = fullAnswers, projectName = "AdminTestCo" })).RootElement;
        var pdfResult = await adminCtrl.GenerateTestBenchPdf(generateBody) as FileContentResult;

        Assert.NotNull(pdfResult);
        Assert.Equal("application/pdf", pdfResult.ContentType);
        Assert.True(pdfResult.FileContents.Length > 1000, "PDF content must be non-empty");
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfResult.FileContents.Take(5).ToArray()));
    }
}

