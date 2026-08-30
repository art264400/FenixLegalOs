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
}
