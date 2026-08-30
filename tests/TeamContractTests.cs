using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Team;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class TeamContractTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public TeamContractTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_team_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1. [Structure] Ровно 17 вопросов TEAM: 4 контекстных и 13 диагностических")]
    public void Team_Questions_Structure_And_Counts()
    {
        var questions = TeamQuestions.All;
        Assert.Equal(17, questions.Count);

        var expectedIds = new[]
        {
            "TEAM-01", "TEAM-02", "TEAM-03", "TEAM-04", "TEAM-05",
            "TEAM-06", "TEAM-07", "TEAM-08", "TEAM-08A", "TEAM-09",
            "TEAM-10", "TEAM-11", "TEAM-12", "TEAM-13", "TEAM-14",
            "TEAM-14A", "TEAM-15"
        };
        Assert.Equal(expectedIds, questions.Select(q => q.Id).ToArray());

        var contextIds = new[] { "TEAM-01", "TEAM-02", "TEAM-08", "TEAM-14" };
        var diagIds = expectedIds.Except(contextIds).ToArray();

        foreach (var cId in contextIds)
        {
            var q = questions.First(x => x.Id == cId);
            Assert.Equal(ScoreMode.Context, q.ScoreMode);
            Assert.Equal(0, q.Weight);
        }

        foreach (var dId in diagIds)
        {
            var q = questions.First(x => x.Id == dId);
            Assert.Equal(ScoreMode.Diagnostic, q.ScoreMode);
            Assert.True(q.Weight > 0, $"Question {dId} should have positive weight.");
        }
    }

    [Fact(DisplayName = "2. [Dimensions] 11 измерений TEAM с суммой весов ровно 100% и 100% внутри каждого измерения")]
    public void Team_Dimensions_And_Weights_Sum_To_100()
    {
        var dimensions = TeamDimensions.All;
        Assert.Equal(11, dimensions.Count);

        var expectedWeights = new Dictionary<string, double>
        {
            ["written_agreements"] = 18.0,
            ["key_person_dependency"] = 7.0,
            ["work_format"] = 15.0,
            ["terms_clarity"] = 8.0,
            ["confidentiality"] = 8.0,
            ["work_rights"] = 10.0,
            ["access_accounts"] = 12.0,
            ["offboarding"] = 12.0,
            ["former_people"] = 7.0,
            ["foreign_team"] = 1.5,
            ["team_equity"] = 1.5
        };

        double totalDimWeight = expectedWeights.Values.Sum();
        Assert.Equal(100.0, totalDimWeight);

        var diagQs = TeamQuestions.All.Where(q => q.ScoreMode == ScoreMode.Diagnostic).ToList();
        var grouped = diagQs.GroupBy(q => q.DimensionId!).ToList();

        Assert.Equal(11, grouped.Count);
        foreach (var grp in grouped)
        {
            var dimId = grp.Key;
            Assert.True(expectedWeights.ContainsKey(dimId), $"Unknown dimension '{dimId}'");
            double expectedWeight = expectedWeights[dimId];

            foreach (var q in grp)
            {
                Assert.Equal(expectedWeight, q.DimensionWeight);
            }

            double insideSum = grp.Sum(q => q.WithinDimensionWeight);
            Assert.Equal(100.0, insideSum);
        }
    }

    [Fact(DisplayName = "3. [Routing] Точные условия видимости (ShowIf) для всех 17 вопросов TEAM")]
    public void Team_Questions_Routing_Rules()
    {
        var qs = TeamQuestions.All.ToDictionary(q => q.Id);

        // TEAM-01: ALWAYS
        Assert.Null(qs["TEAM-01"].ShowIf);

        // ALWAYS with hasNonFounderTeam: TEAM-02, 03, 04, 06, 07, 08, 09, 10, 11, 12, 14, 15
        var nonFounderDependents = new[] { "TEAM-02", "TEAM-03", "TEAM-04", "TEAM-06", "TEAM-07", "TEAM-08", "TEAM-09", "TEAM-10", "TEAM-11", "TEAM-12", "TEAM-14", "TEAM-15" };
        foreach (var id in nonFounderDependents)
        {
            var showIf = qs[id].ShowIf;
            Assert.NotNull(showIf);
            Assert.Single(showIf);
            Assert.Equal("team.hasNonFounderTeam", showIf[0].QuestionId);
            Assert.Equal("true", showIf[0].Value);
        }

        // TEAM-05: freelancers OR external_devs
        var t05 = qs["TEAM-05"].ShowIf;
        Assert.NotNull(t05);
        Assert.NotNull(t05[0].Any);
        Assert.Equal(2, t05[0].Any!.Count);

        // TEAM-08A: createsImportantWork true OR unknown
        var t08a = qs["TEAM-08A"].ShowIf;
        Assert.NotNull(t08a);
        Assert.NotNull(t08a[0].Any);
        Assert.Equal(2, t08a[0].Any!.Count);

        // TEAM-13: keyPersonExists == true
        var t13 = qs["TEAM-13"].ShowIf;
        Assert.NotNull(t13);
        Assert.Equal("team.keyPersonExists", t13[0].QuestionId);

        // TEAM-14A: foreignWorkers true OR unknown
        var t14a = qs["TEAM-14A"].ShowIf;
        Assert.NotNull(t14a);
        Assert.NotNull(t14a[0].Any);
        Assert.Equal(2, t14a[0].Any!.Count);
    }

    [Fact(DisplayName = "4. [Normalization] TEAM-01 Multiple select и взаимное исключение 'none'")]
    public void Team01_Normalization_And_None_Mutual_Exclusion()
    {
        var factsWithTeam = new SharedFactStore();
        var normalizer = new TeamFactNormalizer();

        // 1. Несколько работников без none -> hasNonFounderTeam = true
        normalizer.Normalize(new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees", "freelancers" }
        }, factsWithTeam);

        Assert.True((bool)factsWithTeam.Facts["team.hasNonFounderTeam"]!);
        var workerTypes = (List<string>)factsWithTeam.Facts["team.workerTypes"]!;
        Assert.Contains("employees", workerTypes);
        Assert.Contains("freelancers", workerTypes);

        // 2. Только none -> hasNonFounderTeam = false
        var factsNone = new SharedFactStore();
        normalizer.Normalize(new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "none" }
        }, factsNone);

        Assert.False((bool)factsNone.Facts["team.hasNonFounderTeam"]!);
        Assert.Empty((List<string>)factsNone.Facts["team.workerTypes"]!);
    }

    [Fact(DisplayName = "5. [Unknown Semantics] Строгая нормативная семантика unknown/absence для TEAM-04, TEAM-12")]
    public void Team_Unknown_And_Absence_Semantics()
    {
        var normalizer = new TeamFactNormalizer();

        // TEAM-04 unknown: keyPersonDependency = "unknown", but keyPersonExists is NOT set
        var f4 = new SharedFactStore();
        normalizer.Normalize(new Dictionary<string, object> { ["TEAM-04"] = "unknown" }, f4);
        Assert.Equal("unknown", f4.Facts["team.keyPersonDependency"]);
        Assert.False(f4.Facts.ContainsKey("team.keyPersonExists"));

        // TEAM-12 unknown: formerAccessStatus = "unknown", but formerPeopleExist is NOT set
        var f12Unknown = new SharedFactStore();
        normalizer.Normalize(new Dictionary<string, object> { ["TEAM-12"] = "unknown" }, f12Unknown);
        Assert.Equal("unknown", f12Unknown.Facts["team.formerAccessStatus"]);
        Assert.False(f12Unknown.Facts.ContainsKey("team.formerPeopleExist"));

        // TEAM-12 conflict: formerPeopleExist = true, formerPersonConflict = true, formerAccessStatus is NOT set
        var f12Conflict = new SharedFactStore();
        normalizer.Normalize(new Dictionary<string, object> { ["TEAM-12"] = "conflict" }, f12Conflict);
        Assert.Equal(true, f12Conflict.Facts["team.formerPeopleExist"]);
        Assert.Equal(true, f12Conflict.Facts["team.formerPersonConflict"]);
        Assert.False(f12Conflict.Facts.ContainsKey("team.formerAccessStatus"));
    }

    [Fact(DisplayName = "6. [Applicability] TEAM модуль применим только при team.hasNonFounderTeam == true")]
    public void Team_Module_Applicability_And_Score_Weight()
    {
        // 1. Without team (solo / founders only) -> TEAM is NotApplicable, score null
        var answersSolo = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["TEAM-01"] = new List<string> { "none" }
        };
        var resSolo = _engine.ComputeResult(answersSolo);
        var teamSecSolo = resSolo.Sections.FirstOrDefault(s => s.SectionId == "team");
        Assert.NotNull(teamSecSolo);
        Assert.Equal(ApplicabilityStatus.NotApplicable, teamSecSolo.Status);
        Assert.Null(teamSecSolo.Score);

        // 2. With team -> TEAM is Applicable
        var answersTeam = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-03"] = "all",
            ["TEAM-04"] = "none",
            ["TEAM-06"] = "clear",
            ["TEAM-07"] = "all",
            ["TEAM-08"] = "no",
            ["TEAM-09"] = "controlled",
            ["TEAM-10"] = "company",
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "closed",
            ["TEAM-14"] = "no",
            ["TEAM-15"] = "formal"
        };
        var resTeam = _engine.ComputeResult(answersTeam);
        var teamSec = resTeam.Sections.FirstOrDefault(s => s.SectionId == "team");
        Assert.NotNull(teamSec);
        Assert.Equal(ApplicabilityStatus.Applicable, teamSec.Status);
        Assert.NotNull(teamSec.Score);
        Assert.Equal(100, teamSec.Score);
    }

    [Fact(DisplayName = "7. [Clean Answers] При идеальных ответах никаких находок TEAM_* не генерируется")]
    public void No_Team_Findings_Generated_On_Clean_Answers()
    {
        var answers = new Dictionary<string, object>
        {
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-03"] = "all",
            ["TEAM-04"] = "none",
            ["TEAM-06"] = "clear",
            ["TEAM-07"] = "all",
            ["TEAM-08"] = "no",
            ["TEAM-09"] = "controlled",
            ["TEAM-10"] = "company",
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "closed",
            ["TEAM-14"] = "no",
            ["TEAM-15"] = "formal"
        };

        var res = _engine.ComputeResult(answers);
        Assert.DoesNotContain(res.Risks, r => r.Code.StartsWith("TEAM_"));
    }
}
