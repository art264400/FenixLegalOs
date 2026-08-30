using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

/// <summary>
/// Architecture A Finalization Tests.
///
/// Validates:
/// 1. NavigationState fields (current/next/previous)
/// 2. Effective Answers trust boundary (stale hidden answers = zero effect)
/// 3. Back/edit-earlier-answer scenarios A–E
/// 4. Result card semantics (N_A vs Applicable+null)
/// 5. Production incident payload regression
/// </summary>
public class ArchitectureAFinalizationTests
{
    private readonly ScoringEngine _engine;

    public ArchitectureAFinalizationTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_arch_a_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _engine = new ScoringEngine(new QuestionRepository(dbInit));
    }

    // ─── 1. Navigation returns explicit current/next/previous ─────────────

    [Fact(DisplayName = "Nav.13 NavigationState has explicit currentQuestionId, previous, next")]
    public void Navigation_Returns_Current_Prev_Next()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" }
        };
        var nav = _engine.GetNavigationState(answers, "TEAM-02");

        Assert.Equal("TEAM-02", nav.CurrentQuestionId);
        Assert.NotNull(nav.PreviousQuestionId);
        Assert.NotNull(nav.NextQuestionId);
    }

    [Fact(DisplayName = "Nav.14 Initial navigation (empty answers) — first contextual question is current")]
    public void Initial_Navigation_Empty_Answers_Returns_First_Question()
    {
        var nav = _engine.GetNavigationState(new Dictionary<string, object>());

        Assert.NotNull(nav.CurrentQuestionId);
        Assert.True(nav.TotalVisible > 0);
        Assert.Equal(1, nav.Current);
        // First contextual question should have no previous
        Assert.Null(nav.PreviousQuestionId);
    }

    [Fact(DisplayName = "Nav.15 Changing TEAM-01 from contractors to employees removes contractor questions")]
    public void Scenario_A_ChangeTeam01_Removes_Contractor_Questions()
    {
        // Before: external_devs → TEAM-05 visible
        var before = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "external_devs" } });
        Assert.Contains("TEAM-05", before.VisibleQuestionIds);

        // After: employees only → TEAM-05 hidden
        var after = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "employees" } });
        Assert.DoesNotContain("TEAM-05", after.VisibleQuestionIds);
    }

    // ─── 2. Effective Answers — stale answers have zero effect ────────────

    [Fact(DisplayName = "Nav.16 Scenario D: stale TEAM-15=oral with TEAM-01=none → team.equityPromise absent")]
    public void Scenario_D_Stale_TEAM15_Has_Zero_Fact_Effect()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-15"] = "oral"  // stale — TEAM-15 hidden when no non-founder team
        };

        var result = _engine.ComputeResult(answers);

        // COR_UNDOCUMENTED_EQUITY must NOT fire because team.equityPromise should be absent
        Assert.DoesNotContain(result.Risks, r => r.Code == "COR_UNDOCUMENTED_EQUITY");
    }

    [Fact(DisplayName = "Nav.17 Stale hidden answers have zero scoring effect")]
    public void Stale_Hidden_Answers_Zero_Scoring_Effect()
    {
        // TEAM-01=none → all TEAM diagnostic questions hidden
        // Stale TEAM-03=many_missing should not contribute dimension score
        var withStale = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-03"] = "many_missing",  // stale
            ["TEAM-04"] = "senior_dev"     // stale
        };
        var withoutStale = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" }
        };

        var r1 = _engine.ComputeResult(withStale);
        var r2 = _engine.ComputeResult(withoutStale);

        // Team section should be N_A in both cases, overall scores identical
        Assert.Equal(r1.Overall, r2.Overall);
        var t1 = r1.Sections.FirstOrDefault(s => s.SectionId == "team");
        var t2 = r2.Sections.FirstOrDefault(s => s.SectionId == "team");
        Assert.Equal(t1?.Status, t2?.Status);
        Assert.Equal(t1?.Score, t2?.Score);
    }

    [Fact(DisplayName = "Nav.18 Stale hidden answers have zero finding effect")]
    public void Stale_Hidden_Answers_Zero_Finding_Effect()
    {
        // External devs + stale TEAM-05=many → should produce TEAM_WORK_FORMAT_MISMATCH
        // None + stale TEAM-05=many → should NOT produce any TEAM finding
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-05"] = "many"  // stale — TEAM-05 only visible for freelancers/external_devs
        };

        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "TEAM_WORK_FORMAT_MISMATCH");
    }

    [Fact(DisplayName = "Nav.19 Scenario E: stale TEAM-12=conflict with TEAM-01=none → IP_ACCESS_CONTROL not triggered")]
    public void Scenario_E_Stale_TEAM12_No_CrossModule_IP_Effect()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-12"] = "conflict"  // stale
        };

        var result = _engine.ComputeResult(answers);

        // IP_ACCESS_CONTROL must not be triggered from stale team.formerPersonConflict
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_ACCESS_CONTROL");
    }

    [Fact(DisplayName = "Nav.20 Scenario D (explicit): stale TEAM-15=oral → COR_UNDOCUMENTED_EQUITY NOT triggered")]
    public void Scenario_D_Stale_Equity_Promise_No_COR_Finding()
    {
        // COR-C01 = llc (registered) so COR module is applicable
        var answers = new Dictionary<string, object>
        {
            ["COR-C01"] = "llc",
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-15"] = "oral"  // stale — hidden when no non-founder team
        };

        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "COR_UNDOCUMENTED_EQUITY");
    }

    // ─── 3. Navigation determinism ────────────────────────────────────────

    [Fact(DisplayName = "Nav.21 Navigation deterministic after visible set EXPANDS")]
    public void Navigation_Deterministic_After_Visible_Set_Expands()
    {
        // Start: no non-founder team
        var small = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "none" } },
            "TEAM-01");

        // Expand: add employees
        var larger = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "employees" } },
            "TEAM-01");

        Assert.True(larger.TotalVisible > small.TotalVisible);
        Assert.Equal("TEAM-01", larger.CurrentQuestionId); // current stays valid
    }

    [Fact(DisplayName = "Nav.22 Navigation deterministic after visible set SHRINKS — snaps to first")]
    public void Navigation_Deterministic_After_Visible_Set_Shrinks()
    {
        // Was on TEAM-05 (contractor question)
        var before = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "external_devs" } },
            "TEAM-05");
        Assert.Equal("TEAM-05", before.CurrentQuestionId);

        // TEAM-05 becomes hidden after changing to employees-only
        var after = _engine.GetNavigationState(
            new Dictionary<string, object> { ["TEAM-01"] = new List<string> { "employees" } },
            "TEAM-05");

        // Must snap — TEAM-05 not in visible list, so current snaps to first
        Assert.NotEqual("TEAM-05", after.CurrentQuestionId);
        Assert.Equal(1, after.Current);
    }

    // ─── 4. Production incident payload ──────────────────────────────────

    [Fact(DisplayName = "Nav.23 Production incident: TEAM-01=external_devs+studios+advisors → Team.Status=Applicable, Team.Score=null")]
    public void Production_Incident_Payload_Team_Applicable_NullScore()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["IP-02"] = new List<string> { "other" },
            ["TEAM-01"] = new List<string> { "external_devs", "studios", "advisors" }
        };

        var result = _engine.ComputeResult(answers);
        var teamSection = result.Sections.First(s => s.SectionId == "team");

        Assert.Equal(ApplicabilityStatus.Applicable, teamSection.Status);
        Assert.Null(teamSection.Score); // No diagnostic answers → null score
    }

    // ─── 5. Scenario B & C ────────────────────────────────────────────────

    [Fact(DisplayName = "Nav.24 Scenario B: TEAM-08=yes→TEAM-08A=no, then TEAM-08=no → no TEAM_RIGHTS_TO_WORK_GAP")]
    public void Scenario_B_Stale_TEAM08A_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-08"] = "no",
            ["TEAM-08A"] = "no_contract"  // stale — TEAM-08A only visible when TEAM-08=yes
        };

        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "TEAM_RIGHTS_TO_WORK_GAP");
    }

    [Fact(DisplayName = "Nav.25 Scenario C: TEAM-14=no with stale TEAM-14A → no TEAM_FOREIGN_TEAM_REVIEW")]
    public void Scenario_C_Stale_TEAM14A_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-14"] = "no",
            ["TEAM-14A"] = "no_contract"  // stale
        };

        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "TEAM_FOREIGN_TEAM_REVIEW");
    }
}
