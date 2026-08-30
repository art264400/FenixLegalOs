using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class TeamCrossModuleIntegrationTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public TeamCrossModuleIntegrationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_team_cross_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1. [Suppression] IP_FORMER_DEVELOPER_GAP подавляет TEAM_FORMER_ACCESS_RISK")]
    public void Ip_Former_Developer_Gap_Suppresses_Team_Former_Access_Risk()
    {
        var answers = new Dictionary<string, object>
        {
            // IP-01 = prototype → ip.coreProductExists = true
            ["IP-01"] = "prototype",
            // IP-03 = ["former"] → ip.creators contains "former" → IP-08 visible
            ["IP-03"] = new List<string> { "former" },
            ["IP-04"] = "signed",
            ["IP-08"] = "dispute",   // triggers IP_FORMER_DEVELOPER_GAP (ShowIf: ip.creators contains "former")

            // Team answers triggering TEAM_FORMER_ACCESS_RISK
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "retained"
        };

        var res = _engine.ComputeResult(answers);

        // IP_FORMER_DEVELOPER_GAP is present
        Assert.Contains(res.Risks, r => r.Code == "IP_FORMER_DEVELOPER_GAP");

        // TEAM_FORMER_ACCESS_RISK is suppressed
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK");
    }

    [Fact(DisplayName = "2. [Suppression] IP_FORMER_DEVELOPER_GAP подавляет IP_CONTRACTOR_RIGHTS_GAP")]
    public void Ip_Former_Developer_Gap_Suppresses_Ip_Contractor_Rights_Gap()
    {
        var answers = new Dictionary<string, object>
        {
            // IP-01 = prototype → ip.coreProductExists = true
            ["IP-01"] = "prototype",
            // IP-03 = ["contractors", "former"] → makes IP-07 and IP-08 visible
            ["IP-03"] = new List<string> { "contractors", "former" },
            ["IP-04"] = "signed",
            ["IP-07"] = "payment_only",   // triggers IP_CONTRACTOR_RIGHTS_GAP (ShowIf: ip.creators contains "contractors")
            ["IP-08"] = "dispute"          // triggers IP_FORMER_DEVELOPER_GAP (ShowIf: ip.creators contains "former")
        };

        var res = _engine.ComputeResult(answers);

        Assert.Contains(res.Risks, r => r.Code == "IP_FORMER_DEVELOPER_GAP");
        Assert.DoesNotContain(res.Risks, r => r.Code == "IP_CONTRACTOR_RIGHTS_GAP");
    }

    [Fact(DisplayName = "3. [Isolation] TEAM-12 conflict НЕ создает TEAM_FORMER_ACCESS_RISK")]
    public void Team12_Conflict_Does_Not_Create_Team_Former_Access_Risk()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "conflict"
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_FORMER_ACCESS_RISK");
    }

    [Fact(DisplayName = "4. [Cross-Module IP] TEAM-12 conflict активирует IP_ACCESS_CONTROL при личном контроле аккаунтов")]
    public void Team12_Conflict_Triggers_Ip_Access_Control()
    {
        var answers = new Dictionary<string, object>
        {
            // Founders without dispute
            ["FND-C01"] = "2",
            ["FND-01"] = "none",

            // IP-01 = prototype → ip.coreProductExists = true → IP-13 visible
            ["IP-01"] = "prototype",
            ["IP-04"] = "signed",
            ["IP-13"] = "worker",   // triggers IP_ACCESS_CONTROL (ShowIf: ip.coreProductExists == true)

            // Team former person conflict
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-12"] = "conflict"
        };

        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "IP_ACCESS_CONTROL" && r.Severity == RiskSeverity.Critical);
    }

    [Fact(DisplayName = "5. [Cross-Module Corporate] TEAM-15 oral активирует COR_UNDOCUMENTED_EQUITY")]
    public void Team15_Oral_Triggers_Corporate_Undocumented_Equity()
    {
        var answers = new Dictionary<string, object>
        {
            // Corporate is incorporated, no direct COR-03 promises
            ["COR-C01"] = "one",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",

            // Team oral equity promises
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-15"] = "oral"
        };

        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_EQUITY_PROMISE");
        Assert.Contains(res.Risks, r => r.Code == "COR_UNDOCUMENTED_EQUITY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "6. [Cross-Module Corporate] TEAM-15 undefined активирует COR_UNDOCUMENTED_EQUITY")]
    public void Team15_Undefined_Triggers_Corporate_Undocumented_Equity()
    {
        var answers = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",

            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-15"] = "undefined"
        };

        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_EQUITY_PROMISE");
        Assert.Contains(res.Risks, r => r.Code == "COR_UNDOCUMENTED_EQUITY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "7. [Cross-Module Corporate] TEAM-15 formal НЕ активирует COR_UNDOCUMENTED_EQUITY")]
    public void Team15_Formal_Does_Not_Trigger_Corporate_Undocumented_Equity()
    {
        var answers = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",

            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-15"] = "formal"
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code == "TEAM_EQUITY_PROMISE");
        Assert.DoesNotContain(res.Risks, r => r.Code == "COR_UNDOCUMENTED_EQUITY");
    }

    [Fact(DisplayName = "8. [Cross-Module Corporate Activity] not_incorporated + team.hasNonFounderTeam -> COR_NO_ENTITY_FOR_ACTIVITY")]
    public void Not_Incorporated_With_Team_Triggers_Cor_No_Entity()
    {
        var answers = new Dictionary<string, object>
        {
            ["COR-C01"] = "none", // not incorporated
            ["TEAM-01"] = new List<string> { "employees" } // hasNonFounderTeam = true
        };

        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY" && r.Severity == RiskSeverity.High);
    }

    [Fact(DisplayName = "9. [No Duplicate Deep IP Finding] TEAM rights gap создает только TEAM_RIGHTS_TO_WORK_GAP")]
    public void Team_Rights_Gap_Does_Not_Emit_Artificial_Deep_IP_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["IP-01"] = new List<string> { "code" },
            ["IP-04"] = "signed", // IP itself clean

            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-08"] = "yes",
            ["TEAM-08A"] = "no" // workRightsClarity = none
        };

        var res = _engine.ComputeResult(answers);
        Assert.Contains(res.Risks, r => r.Code == "TEAM_RIGHTS_TO_WORK_GAP");
        Assert.DoesNotContain(res.Risks, r => r.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
    }

    [Fact(DisplayName = "10. [No Synthetic Facts] Нет синтетического факта departing")]
    public void No_Synthetic_Departing_Fact_Exists()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-04"] = "critical",
            ["TEAM-13"] = "stop"
        };

        var res = _engine.ComputeResult(answers);
        var keyDep = res.Risks.FirstOrDefault(r => r.Code == "TEAM_KEY_PERSON_DEPENDENCY");
        Assert.NotNull(keyDep);
        Assert.Equal(RiskSeverity.High, keyDep.Severity); // Baseline High, no synthetic Critical
    }

    [Fact(DisplayName = "11. [Invariants] Все SuppressCodes разрешаются без forward allowlist")]
    public void Every_SuppressCode_In_DataBank_Is_Resolvable()
    {
        var codes = DataBank.Risks.Select(r => r.Code).ToHashSet();
        foreach (var r in DataBank.Risks)
        {
            foreach (var s in r.SuppressCodes)
            {
                Assert.Contains(s, codes);
            }
        }
    }
}
