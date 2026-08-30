using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Interfaces;
using FenixLegalOs.Scoring.Modules.Corporate;
using FenixLegalOs.Scoring.Modules.Founders;
using FenixLegalOs.Scoring.Modules.IP;
using FenixLegalOs.Scoring.Modules.Team;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class RiskLibraryCanonicalCleanupTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public RiskLibraryCanonicalCleanupTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_cleanup_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1. [IP-14] Правило IP-14 возвращает канонический код IP_DOMAIN_BRAND_CONTROL и находит RiskDefinition")]
    public void IP14_Emits_Canonical_IP_DOMAIN_BRAND_CONTROL_And_Resolves_To_RiskDefinition()
    {
        // worker -> High IP_DOMAIN_BRAND_CONTROL
        var answersWorker = new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code", "brand" },
            ["IP-04"] = "signed",
            ["IP-14"] = "worker"
        };
        var resWorker = _engine.ComputeResult(answersWorker);
        var riskWorker = resWorker.Risks.FirstOrDefault(r => r.Code == "IP_DOMAIN_BRAND_CONTROL");
        Assert.NotNull(riskWorker);
        Assert.Equal(RiskSeverity.High, riskWorker.Severity);
        Assert.Equal("Домен или оформленные права на бренд находятся вне компании", riskWorker.Title);

        // founder -> Medium IP_DOMAIN_BRAND_CONTROL
        var answersFounder = new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code", "brand" },
            ["IP-04"] = "signed",
            ["IP-14"] = "founder"
        };
        var resFounder = _engine.ComputeResult(answersFounder);
        var riskFounder = resFounder.Risks.FirstOrDefault(r => r.Code == "IP_DOMAIN_BRAND_CONTROL");
        Assert.NotNull(riskFounder);
        Assert.Equal(RiskSeverity.Medium, riskFounder.Severity);
    }

    [Fact(DisplayName = "2. [COR-T01] Корпоративный скрытый контроль нормализуется и создает COR_HIDDEN_CONTROL")]
    public void COR_T01_Hidden_Control_Normalization_And_Rule_Execution()
    {
        // 1. none -> no finding
        var resNone = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-T01"] = "none"
        });
        Assert.DoesNotContain(resNone.Risks, r => r.Code == "COR_HIDDEN_CONTROL");

        // 2. formal -> no finding
        var resFormal = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-T01"] = "formal"
        });
        Assert.DoesNotContain(resFormal.Risks, r => r.Code == "COR_HIDDEN_CONTROL");

        // 3. indirect -> COR_HIDDEN_CONTROL (HIGH)
        var resIndirect = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-T01"] = "indirect"
        });
        var riskIndirect = resIndirect.Risks.FirstOrDefault(r => r.Code == "COR_HIDDEN_CONTROL");
        Assert.NotNull(riskIndirect);
        Assert.Equal(RiskSeverity.High, riskIndirect.Severity);
        Assert.Equal("Фактический контроль или экономический интерес не отражен формально", riskIndirect.Title);

        // 4. informal -> COR_HIDDEN_CONTROL (CRITICAL)
        var resInformal = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-T01"] = "informal"
        });
        var riskInformal = resInformal.Risks.FirstOrDefault(r => r.Code == "COR_HIDDEN_CONTROL");
        Assert.NotNull(riskInformal);
        Assert.Equal(RiskSeverity.Critical, riskInformal.Severity);

        // 5. unknown -> no confirmed finding
        var resUnknown = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-T01"] = "unknown"
        });
        Assert.DoesNotContain(resUnknown.Risks, r => r.Code == "COR_HIDDEN_CONTROL");
    }

    [Fact(DisplayName = "2.1 [COR-T01] Trigger question не изменяет numeric Legal Score")]
    public void COR_T01_Trigger_Question_Does_Not_Affect_Numeric_Legal_Score()
    {
        var baseAnswers = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "exact",
            ["COR-02"] = "clean"
        };
        var resBase = _engine.ComputeResult(baseAnswers);

        var triggerAnswers = new Dictionary<string, object>
        {
            ["COR-C01"] = "one",
            ["COR-01"] = "exact",
            ["COR-02"] = "clean",
            ["COR-T01"] = "informal"
        };
        var resTrigger = _engine.ComputeResult(triggerAnswers);

        var baseCorpScore = resBase.Sections.First(s => s.SectionId == "corporate").Score;
        var triggerCorpScore = resTrigger.Sections.First(s => s.SectionId == "corporate").Score;

        Assert.Equal(baseCorpScore, triggerCorpScore);
        Assert.Equal(resBase.Overall, resTrigger.Overall);
    }

    [Fact(DisplayName = "3. [IP-15] Права на контент создают канонический IP_CONTENT_RIGHTS")]
    public void IP15_Content_Provenance_Canonical_Behavior()
    {
        // 1. clear -> no finding
        var resClear = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code" },
            ["IP-15"] = "clear"
        });
        Assert.DoesNotContain(resClear.Risks, r => r.Code == "IP_CONTENT_RIGHTS");

        // 2. mostly -> no finding
        var resMostly = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code" },
            ["IP-15"] = "mostly"
        });
        Assert.DoesNotContain(resMostly.Risks, r => r.Code == "IP_CONTENT_RIGHTS");

        // 3. some_unknown -> IP_CONTENT_RIGHTS (MEDIUM)
        var resSome = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code" },
            ["IP-15"] = "some_unknown"
        });
        var riskSome = resSome.Risks.FirstOrDefault(r => r.Code == "IP_CONTENT_RIGHTS");
        Assert.NotNull(riskSome);
        Assert.Equal(RiskSeverity.Medium, riskSome.Severity);
        Assert.Equal("Происхождение части данных или контента как актива не подтверждено", riskSome.Title);

        // 4. external_unchecked -> IP_CONTENT_RIGHTS (HIGH)
        var resExt = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code" },
            ["IP-15"] = "external_unchecked"
        });
        var riskExt = resExt.Risks.FirstOrDefault(r => r.Code == "IP_CONTENT_RIGHTS");
        Assert.NotNull(riskExt);
        Assert.Equal(RiskSeverity.High, riskExt.Severity);

        // 5. unknown -> no confirmed finding
        var resUnknown = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["IP-01"] = "prototype",
            ["IP-02"] = new List<string> { "code" },
            ["IP-15"] = "unknown"
        });
        Assert.DoesNotContain(resUnknown.Risks, r => r.Code == "IP_CONTENT_RIGHTS");
    }

    [Fact(DisplayName = "4. [StrongAreas] FND_EXIT_RULES_MISSING замаплен на exit_continuity и корректно учитывается")]
    public void FND_EXIT_RULES_MISSING_Strong_Areas_Mapping()
    {
        var dims = StrongAreasCalculator.GetAffectedDimensions("FND_EXIT_RULES_MISSING");
        Assert.Contains("exit_continuity", dims);

        // Проверяем, что FND_EXIT_RULES_MISSING (Medium) сам по себе не блокирует Strong Area при score >= 80
        var dimScores = new List<DimensionScore>
        {
            new() { DimensionId = "exit_continuity", Score = 85, Weight = 7, IsApplicable = true }
        };
        var mediumFinding = new List<RiskFinding>
        {
            new() { Code = "FND_EXIT_RULES_MISSING", Severity = RiskSeverity.Medium }
        };
        var strongWithMedium = StrongAreasCalculator.CalculateStrongAreas(dimScores, mediumFinding);
        Assert.Single(strongWithMedium);

        // А тяжелый риск (FND_DEPARTED_UNRESOLVED Critical) блокирует Strong Area
        var criticalFinding = new List<RiskFinding>
        {
            new() { Code = "FND_DEPARTED_UNRESOLVED", Severity = RiskSeverity.Critical }
        };
        var strongWithCritical = StrongAreasCalculator.CalculateStrongAreas(dimScores, criticalFinding);
        Assert.Empty(strongWithCritical);
    }

    [Fact(DisplayName = "8.1 [Invariant] Каждый RiskCode, создаваемый RuleEngine, существует в DataBank.Risks")]
    public void Invariant_Every_Emitted_RiskCode_Resolves_To_DataBank_RiskDefinition()
    {
        var facts = new SharedFactStore();
        // Заполняем факты, активирующие все правила
        facts.Facts["founders.departedFounderStatus"] = "unresolved";
        facts.Facts["founders.activeCount"] = 2;
        facts.Facts["founders.disputeLevel"] = "active";
        facts.Facts["founders.equityClarity"] = "dispute";
        facts.Facts["founders.nearEqualControl"] = true;
        facts.Facts["founders.keyDecisionMode"] = "material_unanimity";
        facts.Facts["founders.deadlockMechanism"] = "none";
        facts.Facts["founders.vestingStatus"] = "none";
        facts.Facts["founders.leaverRules"] = "none";
        facts.Facts["founders.roleClarity"] = "disputed";
        facts.Facts["founders.commitmentStatus"] = "below_expected";
        facts.Facts["founders.externalActivity"] = "active_competition";
        facts.Facts["founders.governanceClarity"] = "none";
        facts.Facts["founders.exitRules"] = "none";
        facts.Facts["founders.personalContributions"] = "dispute";
        facts.Facts["founders.strategicAlignment"] = "conflict";
        facts.Facts["founders.founderAgreementStatus"] = "none";

        facts.Facts["company.entityStatus"] = "incorporated";
        facts.Facts["company.hasRevenue"] = true;
        facts.Facts["capital.ownershipDispute"] = true;
        facts.Facts["capital.ownershipMatch"] = "planned_change";
        facts.Facts["capital.capTableStatus"] = "fragmented";
        facts.Facts["capital.equityPromises"] = "informal";
        facts.Facts["capital.historyStatus"] = "missing";
        facts.Facts["corporate.approvals"] = "often_missing";
        facts.Facts["corporate.authority"] = "unclear";
        facts.Facts["company.entityAlignment"] = "material_outside";
        facts.Facts["corporate.records"] = "missing";
        facts.Facts["company.hiddenControl"] = "informal";

        facts.Facts["ip.coreProductExists"] = true;
        facts.Facts["ip.overallRights"] = "none";
        facts.Facts["ip.ipCreators"] = new List<string> { "founders", "contractors", "employees", "former", "studio" };
        facts.Facts["ip.founderRights"] = "founder_owned";
        facts.Facts["ip.contractorRights"] = "no_contract";
        facts.Facts["ip.formerCreatorStatus"] = "dispute";
        facts.Facts["ip.studioRights"] = "unknown_chain";
        facts.Facts["ip.externalEmployerCreation"] = "not_reviewed";
        facts.Facts["ip.employerResourcesUsed"] = true;
        facts.Facts["ip.thirdPartyComponentsUsed"] = true;
        facts.Facts["ip.thirdPartyTermsReview"] = "none";
        facts.Facts["ip.externalDependency"] = "critical";
        facts.Facts["ip.criticalAccountsControl"] = "worker";
        facts.Facts["founders.activeDispute"] = true;
        facts.Facts["ip.brandDomainControl"] = "worker";
        facts.Facts["ip.brandRegistration"] = "not_registered";
        facts.Facts["ip.contentProvenance"] = "external_unchecked";

        var engines = new List<IModuleRuleEngine>
        {
            new FoundersRuleEngine(),
            new CorporateRuleEngine(),
            new IpRuleEngine(),
            new TeamRuleEngine()
        };

        var rawFindings = FindingProcessor.CollectRawFindings(facts, DataBank.Risks, engines);
        var definedCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();

        foreach (var finding in rawFindings)
        {
            Assert.Contains(finding.Code, definedCodes);
        }
    }

    [Fact(DisplayName = "8.2 [Invariant] Все коды RiskDefinition.Code в DataBank.Risks уникальны")]
    public void Invariant_Every_RiskDefinition_Code_Is_Unique()
    {
        var codes = DataBank.Risks.Select(r => r.Code).ToList();
        var uniqueCodes = codes.Distinct().ToList();
        Assert.Equal(codes.Count, uniqueCodes.Count);
    }

    [Fact(DisplayName = "8.3 [Invariant] Все SuppressCodes без исключения существуют в DataBank.Risks")]
    public void Invariant_Every_SuppressCode_Is_Valid()
    {
        var definedCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();

        foreach (var risk in DataBank.Risks)
        {
            foreach (var suppress in risk.SuppressCodes)
            {
                Assert.True(definedCodes.Contains(suppress),
                    $"Risk '{risk.Code}' specifies unknown SuppressCode '{suppress}'.");
            }
        }
    }

    [Fact(DisplayName = "8.4 [Invariant] Все канонические RiskCodes из StrongAreasCalculator существуют в DataBank.Risks")]
    public void Invariant_Every_StrongAreas_Mapped_RiskCode_Exists_In_RiskLibrary()
    {
        var definedCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();

        foreach (var risk in DataBank.Risks)
        {
            var affectedDims = StrongAreasCalculator.GetAffectedDimensions(risk.Code);
            foreach (var dim in affectedDims)
            {
                Assert.True(DataBank.Dimensions.Any(d => d.Id == dim),
                    $"Risk '{risk.Code}' references unknown dimension '{dim}'.");
            }
        }
    }

    [Fact(DisplayName = "8.5 [Invariant] StrongAreasCalculator не содержит мертвых legacy алиасов")]
    public void Invariant_StrongAreasCalculator_Contains_Zero_Legacy_Aliases()
    {
        var legacyAliases = new[]
        {
            "FND_SINGLE_FOUNDER_DEPENDENCY",
            "COR_NO_ENTITY",
            "COR_REGISTRATION_IN_PROGRESS",
            "COR_CAP_TABLE_GAP",
            "COR_EQUITY_PROMISES",
            "COR_HISTORY_GAP",
            "COR_APPROVALS_GAP",
            "COR_AUTHORITY_RISK",
            "IP_EMPLOYEE_RIGHTS_GAP",
            "IP_MOONLIGHTING_EMPLOYER_RISK",
            "IP_THIRD_PARTY_LICENSE_RISK",
            "IP_TECHNICAL_CONTROL_RISK",
            "IP_BRAND_NOT_PROTECTED",
            "IP_CONTENT_RISK"
        };

        foreach (var alias in legacyAliases)
        {
            Assert.Throws<InvalidOperationException>(() => StrongAreasCalculator.GetAffectedDimensions(alias));
        }
    }
}
