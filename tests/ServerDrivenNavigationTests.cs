using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

/// <summary>
/// Architecture A — Server-Driven Routing invariant tests.
///
/// Invariant: GetVisibleQuestionIds is the single server-side source of truth for question routing.
/// Adding a new module requires ZERO changes to frontend routing logic.
/// </summary>
public class ServerDrivenNavigationTests
{
    private readonly ScoringEngine _engine;

    public ServerDrivenNavigationTests()
    {
        var tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_nav_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDbPath
        }).Build();

        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
    }

    // ─── TEAM ShowIf routing ──────────────────────────────────────────────

    [Fact(DisplayName = "Nav.1 TEAM-01=['external_devs'] → TEAM-03 is visible")]
    public void TEAM01_ExternalDevs_Makes_TEAM03_Visible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "external_devs" }
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.Contains("TEAM-03", ids);
    }

    [Fact(DisplayName = "Nav.2 TEAM-01=['none'] → TEAM-03 is NOT visible")]
    public void TEAM01_None_Makes_TEAM03_NotVisible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" }
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.DoesNotContain("TEAM-03", ids);
    }

    [Fact(DisplayName = "Nav.3 TEAM-01=['freelancers'] → TEAM-05 visible (contractors exist)")]
    public void TEAM01_Freelancers_Makes_TEAM05_Visible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "freelancers" }
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.Contains("TEAM-05", ids);
    }

    [Fact(DisplayName = "Nav.4 TEAM-01=['employees'] → TEAM-05 NOT visible (no contractors)")]
    public void TEAM01_EmployeesOnly_Makes_TEAM05_NotVisible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" }
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.DoesNotContain("TEAM-05", ids);
    }

    [Fact(DisplayName = "Nav.5 TEAM-08='no' → TEAM-08A is NOT visible")]
    public void TEAM08_No_Makes_TEAM08A_NotVisible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-08"] = "no"
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.DoesNotContain("TEAM-08A", ids);
    }

    [Fact(DisplayName = "Nav.6 TEAM-08='yes' → TEAM-08A is visible")]
    public void TEAM08_Yes_Makes_TEAM08A_Visible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-08"] = "yes"
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.Contains("TEAM-08A", ids);
    }

    [Fact(DisplayName = "Nav.7 TEAM-14='no' → TEAM-14A is NOT visible")]
    public void TEAM14_No_Makes_TEAM14A_NotVisible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-14"] = "no"
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.DoesNotContain("TEAM-14A", ids);
    }

    [Fact(DisplayName = "Nav.8 TEAM-14='yes' → TEAM-14A is visible")]
    public void TEAM14_Yes_Makes_TEAM14A_Visible()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-14"] = "yes"
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.Contains("TEAM-14A", ids);
    }

    // ─── IP ShowIf routing ────────────────────────────────────────────────

    [Fact(DisplayName = "Nav.9 IP-01='idea' → IP-03 NOT visible (no core product yet)")]
    public void IP01_Idea_Makes_IP03_NotVisible()
    {
        var answers = new Dictionary<string, object> { ["IP-01"] = "idea" };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.DoesNotContain("IP-03", ids);
    }

    [Fact(DisplayName = "Nav.10 IP-01='prototype' → IP-03 visible (core product exists)")]
    public void IP01_Prototype_Makes_IP03_Visible()
    {
        var answers = new Dictionary<string, object> { ["IP-01"] = "prototype" };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.Contains("IP-03", ids);
    }

    // ─── Empty answers ────────────────────────────────────────────────────

    [Fact(DisplayName = "Nav.11 Empty answers → only unconditional questions visible")]
    public void Empty_Answers_Returns_Only_Unconditional_Questions()
    {
        var ids = _engine.GetVisibleQuestionIds(new Dictionary<string, object>());
        Assert.Contains("FND-C01", ids);
        Assert.Contains("COR-C01", ids);
        Assert.Contains("IP-01", ids);
        Assert.Contains("TEAM-01", ids);
        Assert.DoesNotContain("TEAM-03", ids);
        Assert.DoesNotContain("TEAM-05", ids);
        Assert.DoesNotContain("IP-03", ids);
    }

    // ─── Module-agnostic invariant ────────────────────────────────────────

    [Fact(DisplayName = "Nav.12 All returned IDs are unique and non-empty")]
    public void All_Visible_IDs_Are_Unique()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees", "freelancers" },
            ["TEAM-08"] = "yes",
            ["TEAM-14"] = "yes",
            ["IP-01"] = "prototype"
        };
        var ids = _engine.GetVisibleQuestionIds(answers);
        Assert.NotEmpty(ids);
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
