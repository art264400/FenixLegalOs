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
        var pdfService = new TypstPdfService(testEnv);
        var aiReportService = new AiReportService(config);

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
}
