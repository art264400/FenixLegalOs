using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class TeamRoutingIncidentHardeningTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public TeamRoutingIncidentHardeningTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_team_incident_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1.1 [Applicability] team.hasNonFounderTeam == true -> TEAM ApplicabilityStatus is APPLICABLE")]
    public void Team_With_NonFounder_Members_Is_Applicable()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "external_devs", "studios", "advisors" }
        };

        var res = _engine.ComputeResult(answers);
        var teamSec = res.Sections.FirstOrDefault(s => s.SectionId == "team");

        Assert.NotNull(teamSec);
        Assert.Equal(ApplicabilityStatus.Applicable, teamSec.Status);
        Assert.NotEqual(ApplicabilityStatus.NotApplicable, teamSec.Status);
    }

    [Fact(DisplayName = "1.2 [Applicability] team.hasNonFounderTeam == false -> TEAM ApplicabilityStatus is NotApplicable")]
    public void Team_With_Solo_Or_None_Is_NotApplicable()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" }
        };

        var res = _engine.ComputeResult(answers);
        var teamSec = res.Sections.FirstOrDefault(s => s.SectionId == "team");

        Assert.NotNull(teamSec);
        Assert.Equal(ApplicabilityStatus.NotApplicable, teamSec.Status);
        Assert.Null(teamSec.Score);
    }

    [Fact(DisplayName = "2. [Missing Diagnostics] Отсутствие ответов на диагностические вопросы не создает ложных фактов или находок")]
    public void Missing_Diagnostic_Answers_Does_Not_Create_False_Findings_Or_Scores()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" }
            // TEAM-02 ... TEAM-15 not answered
        };

        var res = _engine.ComputeResult(answers);
        var teamSec = res.Sections.FirstOrDefault(s => s.SectionId == "team");

        Assert.NotNull(teamSec);
        Assert.Equal(ApplicabilityStatus.Applicable, teamSec.Status);
        Assert.Null(teamSec.Score); // Score is null, not synthetic 0 or 100
        Assert.Empty(teamSec.Dimensions);
        Assert.DoesNotContain(res.Risks, r => r.Code.StartsWith("TEAM_")); // No false findings
    }

    [Fact(DisplayName = "3.A [Server Routing] TEAM-01 = none + tampered TEAM-03 -> TEAM N_A and no findings")]
    public void Server_Routing_Ignores_Tampered_TEAM03_When_Team01_Is_None()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" },
            ["TEAM-03"] = "almost_none" // Tampered or stale answer
        };

        var res = _engine.ComputeResult(answers);
        var teamSec = res.Sections.FirstOrDefault(s => s.SectionId == "team");

        Assert.NotNull(teamSec);
        Assert.Equal(ApplicabilityStatus.NotApplicable, teamSec.Status);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_NO_WRITTEN_AGREEMENTS");
    }

    [Fact(DisplayName = "3.B [Server Routing] TEAM-01 = employees + tampered TEAM-05 -> TEAM-05 hidden and ignored")]
    public void Server_Routing_Ignores_Tampered_TEAM05_When_No_Contractors()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" }, // only employees, no freelancers/external_devs
            ["TEAM-05"] = "many" // Tampered answer to hidden question
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_WORK_FORMAT_MISMATCH");
    }

    [Fact(DisplayName = "3.C [Server Routing] TEAM-08 = no + tampered TEAM-08A -> TEAM-08A hidden and ignored")]
    public void Server_Routing_Ignores_Tampered_TEAM08A_When_TEAM08_Is_No()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-08"] = "no",
            ["TEAM-08A"] = "no" // workRightsClarity = none, but question is hidden because TEAM-08 is no
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_RIGHTS_TO_WORK_GAP");
    }

    [Fact(DisplayName = "3.D [Server Routing] TEAM-14 = no + tampered TEAM-14A -> TEAM-14A hidden and ignored")]
    public void Server_Routing_Ignores_Tampered_TEAM14A_When_TEAM14_Is_No()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-14"] = "no",
            ["TEAM-14A"] = "no_contract" // hidden question
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FOREIGN_TEAM_REVIEW");
    }

    [Fact(DisplayName = "5. [Root Cause Regression] Exact Production Payload -> TEAM is Applicable")]
    public void Exact_Production_Payload_From_Incident_Has_Team_Applicable()
    {
        // Exact payload submitted in session 94aa4df8-9c95-4ffc-a59f-a0f264eddf6b
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["IP-02"] = new List<string> { "other" },
            ["TEAM-01"] = new List<string> { "external_devs", "studios", "advisors" }
        };

        var res = _engine.ComputeResult(answers);

        var foundersSec = res.Sections.First(s => s.SectionId == "founders");
        var corpSec = res.Sections.First(s => s.SectionId == "corporate");
        var ipSec = res.Sections.First(s => s.SectionId == "ip");
        var teamSec = res.Sections.First(s => s.SectionId == "team");

        // 1. Founders: solo -> 100%
        Assert.Equal(ApplicabilityStatus.Applicable, foundersSec.Status);
        Assert.Equal(100, foundersSec.Score);

        // 2. Corporate: none -> N_A
        Assert.Equal(ApplicabilityStatus.NotApplicable, corpSec.Status);
        Assert.Null(corpSec.Score);

        // 3. IP: idea -> Applicable
        Assert.Equal(ApplicabilityStatus.Applicable, ipSec.Status);

        // 4. Team: non-founder team present -> Applicable (NOT NotApplicable!)
        Assert.Equal(ApplicabilityStatus.Applicable, teamSec.Status);
        Assert.NotEqual(ApplicabilityStatus.NotApplicable, teamSec.Status);
    }
}
