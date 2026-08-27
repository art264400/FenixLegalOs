using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly string _tempDbPath;

    public ScoringEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
    }

    [Fact]
    public void Deadlock_50_50_Should_Trigger_Critical_Risk()
    {
        // Arrange: 2 founders, verbal agreements, dispute on equity, no deadlock exit mechanism
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "dispute",
            ["FND-C04"] = "none",
            ["FND-01"] = "active_conflict",
            ["FND-02"] = "dispute",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "none",
            ["COR-C01"] = "none"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall <= 40, $"Expected very low overall score for critical conflict & deadlock, got: {result.Overall}");
        Assert.True(result.CriticalCount >= 1, $"Expected at least 1 critical risk, got {result.CriticalCount}");
        
        var hasDeadlockRisk = result.Risks.Any(r => 
            r.Severity is "CRITICAL" or "HIGH" &&
            (r.Code.Contains("FOUNDERS", StringComparison.OrdinalIgnoreCase) || 
             r.Title.Contains("доли", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("тупик", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("основател", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasDeadlockRisk, "Expected risk related to co-founders/deadlock to be present");
    }

    [Fact]
    public void Solo_Founder_Should_Mark_Founders_Section_Applicable_Or_Skipped()
    {
        // Arrange: Single founder
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "kz_llp",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 0);
        var foundersSec = result.Sections.FirstOrDefault(s => s.SectionId == "founders");
        Assert.NotNull(foundersSec);
        Assert.Equal("APPLICABLE", foundersSec.Status);
        Assert.Equal(100, foundersSec.Score);
    }

    [Fact]
    public void Solo_Founder_With_Multiple_Entities_Should_Score_Correctly()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "kz",
            ["COR-C02B"] = "3",
            ["COR-01"] = "dispute",
            ["COR-02"] = "none",
            ["COR-03"] = "unclear_terms",
            ["COR-04"] = "missing",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-08"] = "organized",
            ["COR-07_GROUP"] = "minor_exceptions",
            ["COR-T01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.True(result.Overall > 0, $"Expected Overall > 0, got {result.Overall}");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("APPLICABLE", corpSec.Status);
        Assert.NotNull(corpSec.Score);
    }

    [Fact]
    public void Pre_Incorporation_Idea_Stage_Should_Not_Trigger_No_Entity_Risk()
    {
        // Solo founder, no company, but no active commercial activity
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Risks, r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("N_A", corpSec.Status);
    }

    [Fact]
    public void Pre_Incorporation_With_Active_Revenue_Or_Team_Should_Trigger_COR_NO_ENTITY_FOR_ACTIVITY()
    {
        // No company, but operates with team/revenue
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["TEAM-C01"] = "contractors",
            ["REV-01"] = "active"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        Assert.NotNull(finding);
        Assert.Equal("HIGH", finding.Severity);
        Assert.Equal("ENTITY_ALIGNMENT", finding.RootCauseGroup);
    }

    [Fact]
    public void All_Best_Practices_Should_Produce_High_Score()
    {
        // Arrange: Fully compliant co-founders with signed SHA, vesting, deadlock exit
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "3",
            ["FND-C03"] = "none",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "mechanism",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 75, $"Expected overall score >= 75, got {result.Overall}");
        Assert.Equal(0, result.CriticalCount);
    }

    [Fact]
    public void Single_Company_Builds_Single_Narrative_And_Calculates_COR07()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, facts["company.entityCount"]);
        Assert.Equal(false, facts["company.groupStructure"]);
        Assert.Equal("kz", facts["company.primaryJurisdiction"]);
        
        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("Казахстан", narrative);
        Assert.Contains("одну компанию", narrative);
    }

    [Fact]
    public void Group_Structure_Builds_Detailed_Narrative_With_Roles()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "aifc",
            ["COR-C02B"] = "2",
            ["COR-C02C"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["jurisdiction"] = "kz",
                    ["roles"] = new List<string> { "clients", "payments" }
                }
            },
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07_GROUP"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, facts["company.entityCount"]);
        Assert.Equal(true, facts["company.groupStructure"]);
        Assert.Equal("aifc", facts["company.primaryJurisdiction"]);

        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("МФЦА", narrative);
        Assert.Contains("2 компаний", narrative);
    }

    [Fact]
    public void IP_Happy_Path_Fully_Protected_Should_Score_100()
    {
        // Arrange: Fully compliant IP setup with assigned rights, clean domain and company accounts
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "mechanism",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "signed",
            ["COR-04"] = "complete",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07_AIFC"] = "clean",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",

            // IP Module Answers
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "app", "web", "brand", "domain" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "all",       // overall_rights: 1.0
            ["IP-05"] = "assigned",  // founder_rights: 1.0
            ["IP-07"] = "all",       // external_creators: 1.0
            ["IP-10"] = "no",        // external_employer: 1.0
            ["IP-11"] = "no",        // 3rd party context
            ["IP-12"] = "no",        // external dependency: 1.0
            ["IP-13"] = "company",   // technical control: 1.0
            ["IP-14"] = "company",   // brand & domain: 1.0
            ["IP-15"] = "clear"      // content provenance: 1.0
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");
        Assert.NotNull(ipSec);
        Assert.Equal("APPLICABLE", ipSec.Status);
        Assert.Equal(100, ipSec.Score);
        Assert.DoesNotContain(result.Risks, r => r.SectionId == "ip" && r.Severity is "CRITICAL" or "HIGH");
    }

    [Fact]
    public void IP_Unconfirmed_Product_Rights_Should_Trigger_Critical_And_Suppress_Gaps()
    {
        // Arrange: Incorporated entity, ready product, but no documents confirming ownership (IP-04 = none)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders", "contractors", "studio" },
            ["IP-04"] = "none",              // Trigger IP_PRODUCT_RIGHTS_UNCONFIRMED (CRITICAL)
            ["IP-05"] = "agreed",            // IP_FOUNDER_RIGHTS_NOT_TRANSFERRED (should be suppressed)
            ["IP-07"] = "unclear_clause",    // IP_CONTRACTOR_RIGHTS_GAP (should be suppressed)
            ["IP-09"] = "unknown_chain"      // IP_STUDIO_RIGHTS_GAP (should be suppressed)
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var criticalRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.NotNull(criticalRisk);
        Assert.Equal("CRITICAL", criticalRisk.Severity);
        Assert.Equal("IP_OWNERSHIP", criticalRisk.RootCauseGroup);

        // Verify Canonical Suppressions
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_CONTRACTOR_RIGHTS_GAP");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_STUDIO_RIGHTS_GAP");
    }

    [Fact]
    public void IP_Former_Developer_Dispute_Should_Trigger_Critical_Risk()
    {
        // Arrange: Former developer with open dispute
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders", "former" },
            ["IP-04"] = "main",
            ["IP-08"] = "dispute" // Trigger IP_FORMER_DEVELOPER_GAP (CRITICAL)
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var formerRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_FORMER_DEVELOPER_GAP");
        Assert.NotNull(formerRisk);
        Assert.Equal("CRITICAL", formerRisk.Severity);
        Assert.Equal("KEY_DEVELOPER", formerRisk.RootCauseGroup);
    }

    [Fact]
    public void IP_Moonlighting_With_Employer_Resources_Should_Trigger_Critical_Employer_Risk()
    {
        // Arrange: Founder created product while employed and used employer resources
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-10"] = "not_reviewed",
            ["IP-10A"] = "yes" // Resources used -> CRITICAL
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var employerRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
        Assert.NotNull(employerRisk);
        Assert.Equal("CRITICAL", employerRisk.Severity);
        Assert.Equal("IP_EMPLOYER", employerRisk.RootCauseGroup);
    }

    [Fact]
    public void IP_Idea_Stage_Should_Handle_Light_Path_Gracefully()
    {
        // Arrange: Pure idea stage (IP-01 = idea)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Risks, r => r.SectionId == "ip" && r.Severity is "CRITICAL" or "HIGH");
    }

    [Fact]
    public void IP_Brand_Not_Registered_Should_Not_Penalize_Score_And_Create_Info_Risk()
    {
        // Arrange: Everything clean, but brand is not registered yet
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "brand" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "no",
            ["IP-11"] = "no",
            ["IP-12"] = "no",
            ["IP-13"] = "company",
            ["IP-14"] = "brand_not_registered", // Brand not registered
            ["IP-15"] = "clear"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");
        Assert.NotNull(ipSec);
        Assert.Equal(100, ipSec.Score); // Brand not registered does not penalize score
        var brandInfo = result.Risks.FirstOrDefault(r => r.Code == "IP_BRAND_REGISTRATION_INFO");
        Assert.NotNull(brandInfo);
        Assert.Equal("INFO", brandInfo.Severity);
    }

    [Fact]
    public void IP_Routing_IP10_To_IP10A_Should_Control_Visibility_And_Weights()
    {
        // When IP-10 == 'no', IP-10A should be hidden
        var q10A = DataBank.Questions.First(q => q.Id == "IP-10A");
        var answersNo = new Dictionary<string, object> { ["IP-10"] = "no" };
        Assert.False(ConditionsEvaluator.IsVisible(q10A.ShowIf, answersNo));

        // When IP-10 in [unrelated, lawyer_checked, not_reviewed, unknown], IP-10A should be visible
        foreach (var opt in new[] { "unrelated", "lawyer_checked", "not_reviewed", "unknown" })
        {
            var answersVisible = new Dictionary<string, object> { ["IP-10"] = opt };
            Assert.True(ConditionsEvaluator.IsVisible(q10A.ShowIf, answersVisible), $"Expected IP-10A visible when IP-10 = {opt}");
        }
    }

    [Fact]
    public void IP_Routing_IP11_To_IP11A_Should_Control_Visibility_And_Weights()
    {
        // When IP-11 == 'no', IP-11A should be hidden
        var q11A = DataBank.Questions.First(q => q.Id == "IP-11A");
        var answersNo = new Dictionary<string, object> { ["IP-11"] = "no" };
        Assert.False(ConditionsEvaluator.IsVisible(q11A.ShowIf, answersNo));

        // When IP-11 in [yes, likely, unknown], IP-11A should be visible
        foreach (var opt in new[] { "yes", "likely", "unknown" })
        {
            var answersVisible = new Dictionary<string, object> { ["IP-11"] = opt };
            Assert.True(ConditionsEvaluator.IsVisible(q11A.ShowIf, answersVisible), $"Expected IP-11A visible when IP-11 = {opt}");
        }
    }

    [Fact]
    public void IP_Employer_Risk_Rule_Engine_Boundary_Verification()
    {
        // 1. Unrelated employment + NO resources used -> NO IP_EMPLOYER_RISK
        var cleanAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "unrelated",
            ["IP-10A"] = "no"
        };
        var res1 = _engine.ComputeResult(cleanAnswers);
        Assert.DoesNotContain(res1.Risks, r => r.Code == "IP_EMPLOYER_RISK");

        // 2. Lawyer checked employment + resources used -> NO IP_EMPLOYER_RISK (formal lawyer consent exists)
        var lawyerCheckedAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "lawyer_checked",
            ["IP-10A"] = "yes"
        };
        var res2 = _engine.ComputeResult(lawyerCheckedAnswers);
        Assert.DoesNotContain(res2.Risks, r => r.Code == "IP_EMPLOYER_RISK");

        // 3. Unreviewed employment + resources used on ready product -> CRITICAL severity (CORE / resources used)
        var critAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "not_reviewed",
            ["IP-10A"] = "yes"
        };
        var res3 = _engine.ComputeResult(critAnswers);
        var rCrit = res3.Risks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
        Assert.NotNull(rCrit);
        Assert.Equal("CRITICAL", rCrit.Severity);
    }

    [Fact]
    public void IP_Third_Party_Components_Rule_Engine_Boundary_Verification()
    {
        // 1. Open Source used + systematic review -> NO risk
        var cleanAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-11"] = "yes",
            ["IP-11A"] = "yes"
        };
        var res1 = _engine.ComputeResult(cleanAnswers);
        Assert.DoesNotContain(res1.Risks, r => r.Code == "IP_THIRD_PARTY_COMPONENTS");

        // 2. Open Source used + no review -> Rule Engine activates MEDIUM finding
        var riskAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-11"] = "yes",
            ["IP-11A"] = "no"
        };
        var res2 = _engine.ComputeResult(riskAnswers);
        var rTp = res2.Risks.FirstOrDefault(r => r.Code == "IP_THIRD_PARTY_COMPONENTS");
        Assert.NotNull(rTp);
        Assert.Equal("MEDIUM", rTp.Severity);
    }
}
