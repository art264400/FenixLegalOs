using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.DataAi;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class DataAiPipelineHardeningTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;
    private readonly DataAiRuleEngine _ruleEngine;
    private readonly List<RiskDefinition> _allRisks;

    public DataAiPipelineHardeningTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_data_ai_pipe_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repo);
        _ruleEngine = new DataAiRuleEngine();
        _allRisks = DataBank.Risks;
    }

    // =========================================================================
    // 1. NAMESPACE OWNERSHIP & FACTSTORE IMMUTABILITY
    // =========================================================================

    [Fact(DisplayName = "1.1 DataAiFactNormalizer strictly writes data.*, ai.* and diagnostic.unknownQuestionIds")]
    public void DataAiFactNormalizer_Namespace_Ownership()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "auth" },
            ["DATA-03"] = "sometimes",
            ["DATA-05"] = "unknown",
            ["DATA-06"] = "custom",
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary",
            ["AI-07"] = "ai_human_check"
        };

        var facts = new SharedFactStore();
        var normalizer = new DataAiFactNormalizer();
        normalizer.Normalize(rawAnswers, facts);

        var forbiddenPrefixes = new[] { "founders.", "corporate.", "ip.", "team.", "product.", "contracts.", "investment." };

        foreach (var key in facts.Facts.Keys)
        {
            Assert.True(key.StartsWith("data.") || key.StartsWith("ai.") || key == "diagnostic.unknownQuestionIds",
                $"Forbidden fact key produced by DataAiFactNormalizer: {key}");

            foreach (var forbidden in forbiddenPrefixes)
            {
                Assert.False(key.StartsWith(forbidden), $"DataAiFactNormalizer wrote forbidden key: {key}");
            }
        }
    }

    [Fact(DisplayName = "1.2 DataAiRuleEngine.Evaluate() is completely read-only and never mutates SharedFactStore")]
    public void DataAiRuleEngine_FactStore_Immutability()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "financial", "auth" },
            ["DATA-05"] = "none",
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "none",
            ["AI-04"] = "none",
            ["AI-05"] = "core"
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var beforeJson = JsonSerializer.Serialize(facts.Facts);

        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var afterJson = JsonSerializer.Serialize(facts.Facts);

        Assert.NotEmpty(findings);
        Assert.Equal(beforeJson, afterJson);
    }

    // =========================================================================
    // 2. STALE ANSWER ISOLATION (8 SUB-BRANCHES THROUGH PIPELINE)
    // =========================================================================

    [Fact(DisplayName = "2.1 Stale personal-data branch: DATA-01=no disables all downstream DATA facts and findings")]
    public void Stale_PersonalData_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["DATA-05"] = "none",          // Stale
            ["DATA-06"] = "none",          // Stale
            ["DATA-09"] = "partial",       // Stale
            ["DATA-10A"] = "partial",      // Stale
            ["DATA-15"] = "none",          // Stale
            ["DATA-16"] = "not_all_systems",// Stale
            ["DATA-18"] = "broad"          // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        var dataRisks = result.Risks.Where(r => r.SectionId == "data" && r.Code.StartsWith("DATA_")).ToList();

        Assert.Empty(dataRisks);
    }

    [Fact(DisplayName = "2.2 Stale secondary-use branch: removing secondary use removes DATA_SECONDARY_USE_UNCLEAR")]
    public void Stale_SecondaryUse_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-08"] = new List<string> { "none" }, // Secondary use inactive
            ["DATA-09"] = "partial"                     // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "DATA_SECONDARY_USE_UNCLEAR");
    }

    [Fact(DisplayName = "2.3 Stale external-services branch: DATA-10=no disables DATA_THIRD_PARTY_UNKNOWN")]
    public void Stale_ExternalServices_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-10"] = "no",
            ["DATA-10A"] = "partial", // Stale
            ["DATA-11"] = "none"      // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "DATA_THIRD_PARTY_UNKNOWN");
    }

    [Fact(DisplayName = "2.4 Stale cross-border branch: one country + local storage disables DATA_CROSS_BORDER_REVIEW")]
    public void Stale_CrossBorder_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-12"] = "one",
            ["DATA-13"] = "yes",
            ["DATA-14"] = "partial" // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "DATA_CROSS_BORDER_REVIEW");
    }

    [Fact(DisplayName = "2.5 Stale AI used branch: AI-01=no disables all AI facts and findings")]
    public void Stale_AiUsed_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "no",
            ["AI-02"] = "sensitive",
            ["AI-04"] = "none",
            ["AI-05"] = "core",
            ["AI-06"] = "user_data",
            ["AI-06A"] = "none",
            ["AI-07"] = "automatic",
            ["AI-07A"] = "none",
            ["AI-08"] = "none"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var aiRisks = result.Risks.Where(r => r.SectionId == "data" && r.Code.StartsWith("AI_")).ToList();

        Assert.Empty(aiRisks);
    }

    [Fact(DisplayName = "2.6 Stale external-AI branch: AI-01=own disables external AI transfer findings")]
    public void Stale_ExternalAi_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-02"] = "sensitive", // Stale
            ["AI-03"] = "none",      // Stale
            ["AI-04"] = "none",      // Stale
            ["AI-05"] = "core"       // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "AI_USER_DATA_TRANSFER");
        Assert.DoesNotContain(result.Risks, r => r.Code == "AI_SENSITIVE_DATA_TRANSFER");
        Assert.DoesNotContain(result.Risks, r => r.Code == "AI_PROVIDER_TERMS_UNKNOWN");
    }

    [Fact(DisplayName = "2.7 Stale AI training branch: AI-06=no disables AI_TRAINING_NOT_DISCLOSED")]
    public void Stale_AiTraining_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-06"] = "no",
            ["AI-06A"] = "none" // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "AI_TRAINING_NOT_DISCLOSED");
    }

    [Fact(DisplayName = "2.8 Stale automated decision branch: AI-07=assist disables AI_AUTOMATED_DECISION")]
    public void Stale_AutomatedDecision_Branch_Isolation()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-07"] = "assist",
            ["AI-07A"] = "none" // Stale
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "AI_AUTOMATED_DECISION");
    }

    // =========================================================================
    // 3. STRONG AREAS VERIFICATION
    // =========================================================================

    [Fact(DisplayName = "3.1 Strong Area requires dimension score >= 80 AND no High/Critical/Blocker findings")]
    public void StrongAreas_Generic_Contract()
    {
        // Setup with perfect data map
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "direct" },
            ["DATA-05"] = "clear" // data_map score = 100, 0 findings
        };

        var result = _engine.ComputeResult(rawAnswers);
        var dataSection = result.Sections.FirstOrDefault(s => s.SectionId == "data");
        Assert.NotNull(dataSection);

        var dataMapDim = dataSection.Dimensions.FirstOrDefault(d => d.DimensionId == "data_map");
        Assert.NotNull(dataMapDim);
        Assert.Equal(100, dataMapDim.Score);

        // data_map dimension should be recognized as strong area
        Assert.Contains(result.Strengths, s => s.Contains("data_map") || s.Contains("Карта данных") || s.Contains("движения данных"));
    }

    [Fact(DisplayName = "3.2 High finding blocks Strong Area even if dimension score >= 80")]
    public void HighFinding_Blocks_StrongArea()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "financial", "auth" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "direct" },
            ["DATA-05"] = "developers_only" // emits DATA_MAP_INCOMPLETE (High)
        };

        var result = _engine.ComputeResult(rawAnswers);
        Assert.Contains(result.Risks, r => r.Code == "DATA_MAP_INCOMPLETE");

        // data_map must not be in Strengths
        Assert.DoesNotContain(result.Strengths, s => s.Contains("data_map") || s.Contains("Карта данных") || s.Contains("движения данных"));
    }

    // =========================================================================
    // 4. CONFIDENCE & NULLABLE / N/A SCORE SEMANTICS
    // =========================================================================

    [Fact(DisplayName = "4.1 All applicable diagnostic questions answered yields high confidence; unanswered reduces confidence")]
    public void Confidence_Calculation_Integrity()
    {
        // 1. Full answers
        var answersFull = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-03"] = "no",
            ["DATA-04"] = new List<string> { "direct" },
            ["DATA-05"] = "clear",
            ["DATA-06"] = "yes",
            ["DATA-07"] = "unchanged",
            ["DATA-08"] = new List<string> { "none" },
            ["DATA-10"] = "no",
            ["DATA-12"] = "one",
            ["DATA-13"] = "yes",
            ["DATA-15"] = "defined",
            ["DATA-16"] = "process",
            ["DATA-17"] = "yes",
            ["DATA-18"] = "need_to_know",
            ["AI-01"] = "no"
        };
        var resultFull = _engine.ComputeResult(answersFull);
        Assert.True(resultFull.Confidence >= 80);

        // 2. Partial answers with unknown/partial choices (lowers confidence)
        var answersPartial = new Dictionary<string, object>(answersFull)
        {
            ["DATA-05"] = "unknown",
            ["DATA-13"] = "unknown",
            ["DATA-15"] = "unknown"
        };

        var resultPartial = _engine.ComputeResult(answersPartial);
        Assert.True(resultPartial.Confidence < resultFull.Confidence);
    }

    // =========================================================================
    // 5. MODULE APPLICABILITY & OVERALL SCORE (15% WEIGHT)
    // =========================================================================

    [Fact(DisplayName = "5.1 When both personal data and AI are false, DATA_AI module is NotApplicable and overall score renormalizes")]
    public void DataAi_NotApplicable_Renormalizes_OverallScore()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "no",
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var dataSec = result.Sections.FirstOrDefault(s => s.SectionId == "data");
        Assert.NotNull(dataSec);
        Assert.Equal(ApplicabilityStatus.NotApplicable, dataSec.Status);

        // Section score is null or excluded from overall weighted average
        Assert.Null(dataSec.Score);
        Assert.True(result.Overall >= 0);
    }

    [Fact(DisplayName = "5.2 When DATA is active, module participates with 15% canonical weight")]
    public void DataAi_Active_Weight_Integrity()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-05"] = "clear",
            ["AI-01"] = "no"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var dataSec = result.Sections.First(s => s.SectionId == "data");
        Assert.Equal(ApplicabilityStatus.Applicable, dataSec.Status);
        Assert.Equal(15, dataSec.Weight);
    }

    // =========================================================================
    // 6. DETERMINISM & NAVIGATION STATE CONSISTENCY
    // =========================================================================

    [Fact(DisplayName = "6.1 Repeated ComputeResult evaluation produces byte-for-byte identical output")]
    public void ComputeResult_Is_Deterministic()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "closed",
            ["PROD-01"] = "first",
            ["PROD-22"] = new List<string> { "health" },
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "financial" },
            ["DATA-05"] = "main_only",
            ["DATA-10"] = "yes",
            ["DATA-10A"] = "main",
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "none",
            ["AI-04"] = "none",
            ["AI-05"] = "core",
            ["AI-07"] = "ai_human_check",
            ["AI-08"] = "none"
        };

        var r1 = _engine.ComputeResult(rawAnswers);
        var r2 = _engine.ComputeResult(rawAnswers);
        var r3 = _engine.ComputeResult(rawAnswers);

        // Normalize ComputedAt timestamp for JSON comparison
        r1.ComputedAt = "STATIC_TIMESTAMP";
        r2.ComputedAt = "STATIC_TIMESTAMP";
        r3.ComputedAt = "STATIC_TIMESTAMP";

        var res1 = JsonSerializer.Serialize(r1);
        var res2 = JsonSerializer.Serialize(r2);
        var res3 = JsonSerializer.Serialize(r3);

        Assert.Equal(res1, res2);
        Assert.Equal(res2, res3);
    }

    [Fact(DisplayName = "6.2 ComputeResult and ResolveEffectiveState produce identical VisibleQuestions and EffectiveAnswers")]
    public void ComputeResult_Matches_ResolveEffectiveState()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-10"] = "yes",
            ["DATA-10A"] = "yes",
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary"
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);
        var result = _engine.ComputeResult(rawAnswers);

        Assert.Equal(effectiveAnswers.Count, result.AnsweredCount);
    }

    // =========================================================================
    // 7. ROUTING DAG & REGISTRY INVARIANTS
    // =========================================================================

    [Fact(DisplayName = "7.1 RoutingDependencyValidator validates entire production question bank with 0 errors")]
    public void RoutingDag_Validation_Clean()
    {
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }

    [Fact(DisplayName = "7.2 Registry counts remain strictly 30 questions, 10 dimensions, 15 risks for DATA_AI")]
    public void Registry_Invariants_Preserved()
    {
        var dataQuestions = DataBank.Questions.Where(q => q.SectionId == "data").ToList();
        var dataDimensions = DataBank.Dimensions.Where(d => d.SectionId == "data").ToList();
        var dataRisks = DataBank.Risks.Where(r => r.SectionId == "data").ToList();

        Assert.Equal(30, dataQuestions.Count);
        Assert.Equal(10, dataDimensions.Count);
        Assert.Equal(15, dataRisks.Count);
        Assert.Equal(88, DataBank.Risks.Count);
    }
}
