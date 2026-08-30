using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FenixLegalOs.Controllers;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class UserJourneyE2ETests
{
    private readonly SessionsController _controller;
    private readonly QuestionRepository _questionRepo;
    private readonly ScoringEngine _scoringEngine;
    private readonly SessionRepository _sessionRepo;
    private readonly LeadRepository _leadRepo;

    public UserJourneyE2ETests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_e2e_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();

        var dbInit = new DbInitializer(config);
        dbInit.Initialize();

        _questionRepo = new QuestionRepository(dbInit);
        _sessionRepo = new SessionRepository(dbInit);
        _leadRepo = new LeadRepository(dbInit);
        var setRepo = new SettingsRepository(dbInit);
        _scoringEngine = new ScoringEngine(_questionRepo);

        var testEnv = new TestWebHostEnvironment();
        var pdfService = new TypstPdfService(testEnv);
        var aiReportService = new AiReportService(config);

        _controller = new SessionsController(_sessionRepo, _leadRepo, _scoringEngine, pdfService, aiReportService, setRepo, _questionRepo);
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

    private record JourneyResult(
        string SessionId,
        int StepCount,
        List<string> VisitedQuestionIds,
        Dictionary<string, object> FinalAnswers,
        ScoreResult Result);

    private async Task<JourneyResult> RunUserJourneyAsync(
        Dictionary<string, object> personaAnswers,
        Action<int, string, Dictionary<string, object>>? onStep = null)
    {
        // 1. Create Session
        var createResult = _controller.CreateSession() as OkObjectResult;
        Assert.NotNull(createResult);
        var sessionId = createResult.Value?.GetType().GetProperty("id")?.GetValue(createResult.Value)?.ToString()!;
        Assert.NotNull(sessionId);

        // 2. Start Navigation
        var initNavBody = JsonDocument.Parse("{\"answers\":{},\"currentQuestionId\":null}").RootElement;
        var navResult = _controller.Navigate(sessionId, initNavBody) as OkObjectResult;
        Assert.NotNull(navResult);
        var navState = navResult.Value as NavigationState;
        Assert.NotNull(navState);

        var currentAnswers = new Dictionary<string, object>(StringComparer.Ordinal);
        var visitedQuestions = new List<string>();
        string? currentQId = navState.CurrentQuestionId;
        int steps = 0;

        // 3. Step through questionnaire
        while (!string.IsNullOrEmpty(currentQId) && steps < 100)
        {
            steps++;
            visitedQuestions.Add(currentQId);

            // Optional mutation hook (e.g. user changes mind on step N)
            onStep?.Invoke(steps, currentQId, currentAnswers);

            // Get or generate answer for current question
            if (!currentAnswers.ContainsKey(currentQId))
            {
                if (personaAnswers.TryGetValue(currentQId, out var val))
                {
                    currentAnswers[currentQId] = val;
                }
                else
                {
                    currentAnswers[currentQId] = GenerateDefaultAnswerForQuestion(currentQId);
                }
            }

            var qObj = _questionRepo.GetQuestions().FirstOrDefault(q => q.Id == currentQId);
            string sectionId = qObj?.SectionId ?? "founders";

            var answersJson = JsonSerializer.Serialize(currentAnswers);
            var saveBody = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                answers = currentAnswers,
                lastSectionId = sectionId,
                answeredQuestionId = currentQId
            })).RootElement;

            var saveActionResult = _controller.SaveAnswers(sessionId, saveBody);
            if (saveActionResult is not OkObjectResult saveResult)
            {
                var badRes = saveActionResult as BadRequestObjectResult;
                var errJson = JsonSerializer.Serialize(badRes?.Value);
                throw new Exception($"SaveAnswers failed on step {steps} for Q='{currentQId}'. Response: {errJson}");
            }

            var nextNav = saveResult.Value?.GetType().GetProperty("navigation")?.GetValue(saveResult.Value) as NavigationState;
            Assert.NotNull(nextNav);

            currentQId = nextNav.CurrentQuestionId;
        }

        // 4. Complete Session
        var completeBody = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            answers = currentAnswers
        })).RootElement;

        var completeResult = _controller.CompleteSession(sessionId, completeBody) as OkObjectResult;
        Assert.NotNull(completeResult);

        var scoreResult = completeResult.Value?.GetType().GetProperty("result")?.GetValue(completeResult.Value) as ScoreResult;
        Assert.NotNull(scoreResult);

        return new JourneyResult(sessionId, steps, visitedQuestions, currentAnswers, scoreResult);
    }

    private object GenerateDefaultAnswerForQuestion(string questionId)
    {
        var q = _questionRepo.GetQuestions().FirstOrDefault(q => q.Id == questionId);
        if (q == null) return "none";

        if (q.Type == QuestionType.EquityInputs)
        {
            return new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 };
        }
        if (q.Type == QuestionType.EntityBuilder)
        {
            return new List<object>
            {
                new Dictionary<string, object> { ["index"] = 2, ["jurisdiction"] = "kz", ["roles"] = new List<string> { "holding" } }
            };
        }
        if (q.Type == QuestionType.Multiple)
        {
            var nonExcl = q.Options?.Where(o => !o.Exclusive && o.Id != "none" && o.Id != "nothing").ToList();
            if (nonExcl != null && nonExcl.Count > 0) return new List<string> { nonExcl[0].Id };
            return new List<string> { q.Options?[0].Id ?? "none" };
        }

        return q.Options?.Count > 0 ? q.Options[0].Id : "none";
    }

    // ─── 1. Solo Founder Short Journey ──────────────────────────────────────
    [Fact(DisplayName = "1. E2E: Solo Founder with no entity and idea stage finishes in short adaptive path")]
    public async Task Solo_Founder_Short_Journey_E2E()
    {
        var soloAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "prelaunch",
            ["PROD-02"] = new List<string> { "undecided" },
            ["PROD-03"] = new List<string> { "other" },
            ["PROD-04"] = "none",
            ["PROD-22"] = new List<string> { "none" },
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no",
            ["CONTRACT-01"] = new List<string> { "none" }
        };

        var journey = await RunUserJourneyAsync(soloAnswers);

        // Verify that journey was short (~19 questions instead of 133)
        Assert.True(journey.StepCount <= 25, $"Expected <= 25 steps, got {journey.StepCount}");
        Assert.Contains("FND-C01", journey.VisitedQuestionIds);
        Assert.Contains("COR-C01", journey.VisitedQuestionIds);
        Assert.Contains("IP-01", journey.VisitedQuestionIds);
        Assert.Contains("TEAM-01", journey.VisitedQuestionIds);
        Assert.Contains("CONTRACT-01", journey.VisitedQuestionIds);
        Assert.Contains("PROD-01", journey.VisitedQuestionIds);
        Assert.Contains("DATA-01", journey.VisitedQuestionIds);
        Assert.Contains("AI-01", journey.VisitedQuestionIds);

        // Multi-founder questions must never have been visited
        Assert.DoesNotContain("FND-C02", journey.VisitedQuestionIds);
        Assert.DoesNotContain("FND-01", journey.VisitedQuestionIds);
        Assert.DoesNotContain("TEAM-02", journey.VisitedQuestionIds);

        // Verify result
        Assert.NotNull(journey.Result);
        Assert.True(journey.Result.Overall >= 0 && journey.Result.Overall <= 100);
    }

    // ─── 2. Multi-Founder 50/50 Deadlock Journey ──────────────────────────
    [Fact(DisplayName = "2. E2E: 2 Founders 50/50 without deadlock agreement triggers FND_DEADLOCK finding")]
    public async Task Multi_Founder_50_50_Deadlock_E2E()
    {
        var multiAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "clear_oral",
            ["FND-03"] = "aligned",
            ["FND-04"] = "verbal",
            ["FND-05"] = "not_discussed",
            ["FND-05A"] = "none",
            ["FND-06"] = "none",
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none", // Deadlock: "Вопрос тупика вообще не продуман"
            ["FND-08"] = "none",
            ["FND-09"] = "none",
            ["FND-10"] = "none",
            ["FND-11"] = "aligned",

            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-03"] = "none",
            ["COR-04"] = "none",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",

            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",

            ["TEAM-01"] = new List<string> { "none" },

            ["PROD-01"] = "first",
            ["PROD-02"] = new List<string> { "companies" },
            ["PROD-03"] = new List<string> { "website" },
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-06"] = "clear",
            ["PROD-07"] = "company",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "free",
            ["PROD-14"] = "none",
            ["PROD-16"] = "none",
            ["PROD-17"] = "rules_cover",
            ["PROD-18"] = "no",
            ["PROD-20"] = "no",
            ["PROD-21"] = "one",
            ["PROD-22"] = new List<string> { "none" }
        };

        var journey = await RunUserJourneyAsync(multiAnswers);

        // Must visit multi-founder branch
        Assert.Contains("FND-C02", journey.VisitedQuestionIds);
        Assert.Contains("FND-07", journey.VisitedQuestionIds);

        // Must detect DEADLOCK
        Assert.True(journey.Result.CriticalCount >= 1, "Must contain at least 1 critical risk");
        Assert.Contains(journey.Result.Risks, r => r.Code == "FND_DEADLOCK");
    }

    // ─── 3. Answer Mutation: Changing Mind from Multi to Solo ──────────────
    [Fact(DisplayName = "3. E2E: Changing answer from 2 founders to solo mid-way cleans up downstream state")]
    public async Task Answer_Mutation_Changing_Mind_Cleans_Downstream_State_E2E()
    {
        var initialMultiAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "prelaunch",
            ["PROD-02"] = new List<string> { "undecided" },
            ["PROD-03"] = new List<string> { "other" },
            ["PROD-04"] = "none",
            ["PROD-22"] = new List<string> { "none" }
        };

        var journey = await RunUserJourneyAsync(initialMultiAnswers, onStep: (step, currentQ, currentAnswers) =>
        {
            // On step 4, simulate user changing mind and switching FND-C01 to "solo"
            if (step == 4)
            {
                currentAnswers["FND-C01"] = "solo";
            }
        });

        // After mutation to solo, effective state must isolate multi-founder answers
        Assert.NotNull(journey.Result);
        var corporateSection = journey.Result.Sections.FirstOrDefault(d => d.SectionId == "corporate");
        Assert.NotNull(corporateSection);
        Assert.Equal("NotApplicable", corporateSection.Status.ToString());

        var foundersSection = journey.Result.Sections.FirstOrDefault(d => d.SectionId == "founders");
        Assert.NotNull(foundersSection);
        Assert.Equal(100, foundersSection.Score);
    }

    // ─── 4. Random Walk Fuzzing: 30 Complete Valid Sessions ────────────────
    [Fact(DisplayName = "4. E2E: Fuzzing 30 complete randomized user sessions completes 100% successfully")]
    public async Task Random_Walk_Fuzzing_30_Sessions_E2E()
    {
        var rnd = new Random(42);

        for (int i = 0; i < 30; i++)
        {
            // Create Session
            var createResult = _controller.CreateSession() as OkObjectResult;
            var sessionId = createResult?.Value?.GetType().GetProperty("id")?.GetValue(createResult.Value)?.ToString()!;

            // Init navigation
            var initNavBody = JsonDocument.Parse("{\"answers\":{},\"currentQuestionId\":null}").RootElement;
            var navResult = _controller.Navigate(sessionId, initNavBody) as OkObjectResult;
            var navState = navResult?.Value as NavigationState;

            var currentAnswers = new Dictionary<string, object>(StringComparer.Ordinal);
            string? currentQId = navState?.CurrentQuestionId;
            int steps = 0;

            while (!string.IsNullOrEmpty(currentQId) && steps < 200)
            {
                steps++;
                var q = _questionRepo.GetQuestions().FirstOrDefault(item => item.Id == currentQId);
                Assert.NotNull(q);

                // Pick random valid answer
                object answerVal;
                if (q.Type == QuestionType.EquityInputs)
                {
                    answerVal = new Dictionary<string, object> { ["founder_1"] = 60, ["founder_2"] = 40 };
                }
                else if (q.Type == QuestionType.EntityBuilder)
                {
                    answerVal = new List<object>
                    {
                        new Dictionary<string, object> { ["index"] = 2, ["jurisdiction"] = "kz", ["roles"] = new List<string> { "holding" } }
                    };
                }
                else if (q.Type == QuestionType.Multiple)
                {
                    var nonExcl = q.Options?.Where(o => !o.Exclusive && o.Id != "none" && o.Id != "nothing").ToList();
                    if (nonExcl != null && nonExcl.Count > 0 && rnd.Next(2) == 0)
                    {
                        answerVal = new List<string> { nonExcl[rnd.Next(nonExcl.Count)].Id };
                    }
                    else
                    {
                        answerVal = new List<string> { q.Options?[rnd.Next(q.Options.Count)].Id ?? "none" };
                    }
                }
                else
                {
                    answerVal = q.Options?[rnd.Next(q.Options.Count)].Id ?? "none";
                }

                currentAnswers[currentQId] = answerVal;

                var saveBody = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    answers = currentAnswers,
                    lastSectionId = q.SectionId,
                    answeredQuestionId = currentQId
                })).RootElement;

                var saveResult = _controller.SaveAnswers(sessionId, saveBody) as OkObjectResult;
                Assert.NotNull(saveResult);

                var nextNav = saveResult.Value?.GetType().GetProperty("navigation")?.GetValue(saveResult.Value) as NavigationState;
                Assert.NotNull(nextNav);

                currentQId = nextNav.CurrentQuestionId;
            }

            // Must reach completion
            Assert.Null(currentQId);
            Assert.True(steps > 0, "Journey must perform at least 1 step");

            // Complete session
            var completeBody = JsonDocument.Parse(JsonSerializer.Serialize(new { answers = currentAnswers })).RootElement;
            var completeResult = _controller.CompleteSession(sessionId, completeBody) as OkObjectResult;
            Assert.NotNull(completeResult);

            var scoreResult = completeResult.Value?.GetType().GetProperty("result")?.GetValue(completeResult.Value) as ScoreResult;
            Assert.NotNull(scoreResult);
            Assert.InRange(scoreResult.Overall, 0, 100);
        }
    }

    // ─── 5. Seed Scale EntityBuilder & Vesting Journey ──────────────────────
    [Fact(DisplayName = "5. E2E: Scale startup with multiple entities, holding and subscription completes")]
    public async Task Seed_Scale_EntityBuilder_And_Vesting_E2E()
    {
        var scaleAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "3",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 30, ["founder_3"] = 20 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "none",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "aligned",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "defined",
            ["FND-06"] = "written",
            ["FND-06A"] = "different_thresholds",
            ["FND-07"] = "full",
            ["FND-08"] = "full",
            ["FND-09"] = "documented",
            ["FND-10"] = "none",
            ["FND-11"] = "aligned",

            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "us",
            ["COR-C02B"] = "2",
            ["COR-C02C"] = new List<object>
            {
                new Dictionary<string, object> { ["index"] = 2, ["jurisdiction"] = "kz", ["roles"] = new List<string> { "hiring", "payments" } }
            },
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-03"] = "documented_included",
            ["COR-04"] = "complete",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07_GROUP"] = "aligned",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",

            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "design" },
            ["IP-03"] = new List<string> { "founders", "employees" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-06"] = "all",
            ["IP-10"] = "no",

            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-02"] = "6_10",
            ["TEAM-03"] = "all",
            ["TEAM-04"] = "none",
            ["TEAM-06"] = "clear",
            ["TEAM-07"] = "all",
            ["TEAM-08"] = "yes",
            ["TEAM-08A"] = "all",
            ["TEAM-09"] = "controlled",
            ["TEAM-10"] = "company",
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "none",

            ["PROD-01"] = "regular",
            ["PROD-02"] = new List<string> { "consumers", "companies" },
            ["PROD-03"] = new List<string> { "website", "app" },
            ["PROD-04"] = "current",
            ["PROD-05"] = "yes",
            ["PROD-06"] = "clear",
            ["PROD-07"] = "company",
            ["PROD-08"] = "explicit",
            ["PROD-09"] = "versioned",
            ["PROD-10"] = "subscription",
            ["PROD-11"] = "clear",
            ["PROD-12"] = "published",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "clear",
            ["PROD-14"] = "self_service",
            ["PROD-15"] = "no_trial",
            ["PROD-16"] = "clear",
            ["PROD-17"] = "clear",
            ["PROD-18"] = "no",
            ["PROD-20"] = "no",
            ["PROD-21"] = "multiple",
            ["PROD-21A"] = "main_markets",
            ["PROD-22"] = new List<string> { "none" }
        };

        var journey = await RunUserJourneyAsync(scaleAnswers);

        Assert.NotNull(journey.Result);
        Assert.True(journey.Result.Overall >= 80, $"Expected strong Legal Score >= 80, got {journey.Result.Overall}");
        Assert.Equal(0, journey.Result.CriticalCount);
    }

    // ─── 6. Lead Generation from Session ──────────────────────────────────
    [Fact(DisplayName = "6. E2E: Diagnostic completion creates consultation lead with computed heat score")]
    public async Task Lead_Generation_From_Diagnostic_Session_E2E()
    {
        var soloAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "prelaunch",
            ["PROD-02"] = new List<string> { "undecided" },
            ["PROD-03"] = new List<string> { "other" },
            ["PROD-04"] = "none",
            ["PROD-22"] = new List<string> { "none" }
        };

        var journey = await RunUserJourneyAsync(soloAnswers);

        var leadsCtrl = new LeadsController(_sessionRepo, _leadRepo);
        var leadBody = JsonDocument.Parse(JsonSerializer.Serialize(new
        {
            sessionId = journey.SessionId,
            type = "consultation",
            name = "Алексей Иванов",
            email = "alexey@example.com",
            company = "FinTech Labs",
            interest = "Полный юридический аудит"
        })).RootElement;

        var leadResult = leadsCtrl.CreateLead(leadBody) as OkObjectResult;
        Assert.NotNull(leadResult);
        var leadId = leadResult.Value?.GetType().GetProperty("leadId")?.GetValue(leadResult.Value)?.ToString();
        Assert.NotNull(leadId);

        var lead = _leadRepo.GetLead(leadId);
        Assert.NotNull(lead);
        Assert.Equal("Алексей Иванов", lead.Name);
        Assert.Equal("alexey@example.com", lead.Email);
        Assert.True(lead.HeatScore >= 0);
    }
}
