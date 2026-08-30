using FenixLegalOs.Data;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Team;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class TeamRuleEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public TeamRuleEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_team_rules_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1. [RiskLibrary] TeamRisks содержит ровно 13 канонических определений рисков")]
    public void TeamRisks_Contains_Exactly_13_Definitions()
    {
        Assert.Equal(13, TeamRisks.All.Count);

        var expectedCodes = new[]
        {
            "TEAM_NO_WRITTEN_AGREEMENTS",
            "TEAM_KEY_PERSON_UNDOCUMENTED",
            "TEAM_WORK_FORMAT_MISMATCH",
            "TEAM_UNCLEAR_TERMS",
            "TEAM_CONFIDENTIALITY_GAP",
            "TEAM_RIGHTS_TO_WORK_GAP",
            "TEAM_ACCESS_CONTROL_GAP",
            "TEAM_PERSONAL_ACCOUNT_DEPENDENCY",
            "TEAM_OFFBOARDING_GAP",
            "TEAM_FORMER_ACCESS_RISK",
            "TEAM_KEY_PERSON_DEPENDENCY",
            "TEAM_FOREIGN_TEAM_REVIEW",
            "TEAM_EQUITY_PROMISE"
        };

        var actualCodes = TeamRisks.All.Select(r => r.Code).ToList();
        Assert.Equal(expectedCodes.OrderBy(x => x), actualCodes.OrderBy(x => x));
    }

    [Fact(DisplayName = "2. [Unique Codes] Все 13 кодов рисков TEAM уникальны")]
    public void All_Team_Risk_Codes_Are_Unique()
    {
        var codes = TeamRisks.All.Select(r => r.Code).ToList();
        Assert.Equal(codes.Distinct().Count(), codes.Count);
    }

    [Fact(DisplayName = "3. [Canonical Metadata] Каждый TEAM RiskDefinition имеет валидный RootCauseGroup, ServiceCode и AffectedDimensions")]
    public void Team_RiskDefinitions_Have_Canonical_Metadata()
    {
        foreach (var def in TeamRisks.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(def.RootCauseGroup), $"Risk {def.Code} has empty RootCauseGroup");
            Assert.False(string.IsNullOrWhiteSpace(def.ServiceCode), $"Risk {def.Code} has empty ServiceCode");
            Assert.NotEmpty(def.AffectedDimensions);
            foreach (var dim in def.AffectedDimensions)
            {
                Assert.True(DataBank.Dimensions.Any(d => d.Id == dim),
                    $"Risk {def.Code} references non-existent dimension '{dim}'");
            }
        }
    }

    [Fact(DisplayName = "4. [TEAM_KEY_PERSON_UNDOCUMENTED] keyPersonExists=true + many_missing -> HIGH")]
    public void Key_Person_Undocumented_Triggers_On_Many_Missing()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-03"] = "many_missing",
            ["TEAM-04"] = "critical"
        };
        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_KEY_PERSON_UNDOCUMENTED" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "5. [TEAM_KEY_PERSON_UNDOCUMENTED] keyPersonExists=true + all agreements -> NO finding")]
    public void Key_Person_Undocumented_Does_Not_Trigger_When_Agreements_Signed()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-03"] = "all",
            ["TEAM-04"] = "critical"
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_KEY_PERSON_UNDOCUMENTED");
    }

    [Fact(DisplayName = "6. [TEAM_FORMER_ACCESS_RISK] retained -> CRITICAL")]
    public void Former_Access_Risk_Triggers_On_Retained()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "retained"
        };
        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK" && r.Severity == RiskSeverity.Critical);
    }

    [Fact(DisplayName = "7. [TEAM_FORMER_ACCESS_RISK] not_sure -> NOT TEAM_FORMER_ACCESS_RISK")]
    public void Former_Access_Risk_Does_Not_Trigger_On_Not_Sure()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "not_sure"
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK");
    }

    [Fact(DisplayName = "8. [TEAM_FORMER_ACCESS_RISK] conflict -> NOT TEAM_FORMER_ACCESS_RISK")]
    public void Former_Access_Risk_Does_Not_Trigger_On_Conflict()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "conflict"
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK");
    }

    [Fact(DisplayName = "9. [TEAM_FORMER_ACCESS_RISK] unknown -> NOT TEAM_FORMER_ACCESS_RISK")]
    public void Former_Access_Risk_Does_Not_Trigger_On_Unknown()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "unknown"
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK");
    }

    [Fact(DisplayName = "10. [TEAM_KEY_PERSON_DEPENDENCY] keyPersonDependency=critical -> HIGH")]
    public void Key_Person_Dependency_Triggers_On_Critical_Dependency()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-04"] = "critical"
        };
        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_KEY_PERSON_DEPENDENCY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "11. [TEAM_KEY_PERSON_DEPENDENCY] keyPersonContinuity=weak -> HIGH")]
    public void Key_Person_Dependency_Triggers_On_Weak_Continuity()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-04"] = "some", // keyPersonExists = true, dependency = some
            ["TEAM-13"] = "knowledge_only" // keyPersonContinuity = weak
        };
        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_KEY_PERSON_DEPENDENCY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "12. [TEAM_KEY_PERSON_DEPENDENCY] keyPersonContinuity=critical -> HIGH")]
    public void Key_Person_Dependency_Triggers_On_Critical_Continuity()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-04"] = "some",
            ["TEAM-13"] = "stop" // keyPersonContinuity = critical
        };
        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_KEY_PERSON_DEPENDENCY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "13. [TEAM_KEY_PERSON_DEPENDENCY] keyPersonExists=true alone -> NO finding")]
    public void Key_Person_Dependency_Does_Not_Trigger_On_Mitigated_Continuity()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-04"] = "mitigated", // keyPersonDependency = mitigated
            ["TEAM-13"] = "continuity" // keyPersonContinuity = good
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_KEY_PERSON_DEPENDENCY");
    }

    [Fact(DisplayName = "14. [Unknown Semantics] Explicit unknown facts do not create confirmed findings")]
    public void Unknown_Facts_Do_Not_Emit_Confirmed_Team_Findings()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-03"] = "unknown",
            ["TEAM-04"] = "unknown",
            ["TEAM-05"] = "unknown",
            ["TEAM-06"] = "unknown",
            ["TEAM-07"] = "unknown",
            ["TEAM-08"] = "unknown",
            ["TEAM-08A"] = "unknown",
            ["TEAM-09"] = "unknown",
            ["TEAM-10"] = "unknown",
            ["TEAM-11"] = "unknown",
            ["TEAM-12"] = "unknown",
            ["TEAM-13"] = "unknown",
            ["TEAM-14"] = "unknown",
            ["TEAM-14A"] = "unknown",
            ["TEAM-15"] = "unknown"
        };
        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code.StartsWith("TEAM_"));
    }

    [Fact(DisplayName = "15. [Invariant] Every emitted TEAM RiskCode resolves to DataBank.Risks")]
    public void Every_Emitted_Team_RiskCode_Resolves_To_DataBank_Risks()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees", "freelancers" },
            ["TEAM-03"] = "almost_none",
            ["TEAM-04"] = "critical",
            ["TEAM-05"] = "many",
            ["TEAM-06"] = "generic",
            ["TEAM-07"] = "none",
            ["TEAM-08"] = "yes",
            ["TEAM-08A"] = "no",
            ["TEAM-09"] = "unknown_access",
            ["TEAM-10"] = "critical",
            ["TEAM-11"] = "none",
            ["TEAM-12"] = "retained",
            ["TEAM-13"] = "stop",
            ["TEAM-14"] = "yes",
            ["TEAM-14A"] = "no_contract",
            ["TEAM-15"] = "oral"
        };

        var res = _engine.ComputeResult(answers);
        var teamFindings = res.Risks.Where(r => r.Code.StartsWith("TEAM_")).ToList();
        Assert.NotEmpty(teamFindings);

        foreach (var finding in teamFindings)
        {
            var def = DataBank.Risks.FirstOrDefault(r => r.Code == finding.Code);
            Assert.NotNull(def);
            Assert.Equal(def.RootCauseGroup, finding.RootCauseGroup);
            Assert.Equal(def.ServiceCode, finding.ServiceCode);
        }
    }

    [Fact(DisplayName = "16. [Invariant] Severe TEAM findings contain valid AffectedDimensions")]
    public void Severe_Team_Findings_Contain_Valid_AffectedDimensions()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees", "freelancers" },
            ["TEAM-03"] = "almost_none",
            ["TEAM-04"] = "critical",
            ["TEAM-12"] = "retained"
        };

        var res = _engine.ComputeResult(answers);
        var severeTeamFindings = res.Risks.Where(r => r.Code.StartsWith("TEAM_") && r.Severity is RiskSeverity.High or RiskSeverity.Critical).ToList();
        Assert.NotEmpty(severeTeamFindings);

        foreach (var finding in severeTeamFindings)
        {
            Assert.NotEmpty(finding.AffectedDimensions);
            foreach (var dim in finding.AffectedDimensions)
            {
                Assert.True(DataBank.Dimensions.Any(d => d.Id == dim),
                    $"Finding {finding.Code} references non-existent dimension '{dim}'");
            }
        }
    }

    [Fact(DisplayName = "17. [Invariant] StrongAreasCalculator contains zero TEAM_ literals")]
    public void StrongAreasCalculator_Contains_Zero_Team_Literals()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Scoring", "Core", "StrongAreasCalculator.cs");
        var fullPath = Path.GetFullPath(path);
        Assert.True(File.Exists(fullPath));

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("TEAM_", content);
    }
}
