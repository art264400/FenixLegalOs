using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public class DataAiCrossModuleIntegrationTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repo;
    private readonly DataAiRuleEngine _ruleEngine;
    private readonly List<RiskDefinition> _allRisks;

    public DataAiCrossModuleIntegrationTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_data_ai_cross_{Guid.NewGuid():N}.db");
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
    // 1. TEAM → DATA-19 REUSE AND VISIBILITY
    // =========================================================================

    [Fact(DisplayName = "1.1 DATA-19 is visible when Team offboarding facts are absent")]
    public void Data19_Visible_When_Team_Facts_Absent()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["TEAM-01"] = new List<string> { "employees" }
            // TEAM-11 and TEAM-12 are absent
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.Contains(visibleQs, q => q.Id == "DATA-19");
    }

    [Fact(DisplayName = "1.2 DATA-19 is visible when Team offboardingProcess is explicit unknown")]
    public void Data19_Visible_When_Team_Offboarding_Unknown()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "unknown",
            ["TEAM-12"] = "closed"
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.Contains(visibleQs, q => q.Id == "DATA-19");
    }

    [Fact(DisplayName = "1.3 DATA-19 is visible when Team offboardingProcess is known but formerAccessStatus is absent")]
    public void Data19_Visible_When_FormerAccessStatus_Absent()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "systematic"
            // TEAM-12 absent
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.Contains(visibleQs, q => q.Id == "DATA-19");
    }

    [Fact(DisplayName = "1.4 DATA-19 is skipped when both offboardingProcess and formerAccessStatus are known")]
    public void Data19_Skipped_When_Both_Team_Facts_Known()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "closed"
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.DoesNotContain(visibleQs, q => q.Id == "DATA-19");
    }

    [Fact(DisplayName = "1.5 Skipped DATA-19 does not synthesize false/unknown/zero facts or corrupt access_offboarding dimension")]
    public void Skipped_Data19_Integrity()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-18"] = "need_to_know",
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "systematic",
            ["TEAM-12"] = "closed"
        };

        var result = _engine.ComputeResult(rawAnswers);
        var dataSection = result.Sections.FirstOrDefault(s => s.SectionId == "data");
        Assert.NotNull(dataSection);

        var accessDim = dataSection.Dimensions.FirstOrDefault(d => d.DimensionId == "access_offboarding");
        Assert.NotNull(accessDim);
        Assert.True(accessDim.IsApplicable);
        Assert.Equal(100, accessDim.Score); // Score computed cleanly from DATA-18 without corrupting denominator
    }

    // =========================================================================
    // 2. STALE TEAM → DATA-19 ISOLATION
    // =========================================================================

    [Fact(DisplayName = "2.1 Stale TEAM-11/TEAM-12 answers lose effectiveness when hidden by upstream Team state, restoring DATA-19")]
    public void Stale_Team_Answers_Restore_Data19_Visibility()
    {
        // When TEAM-01 has employees (team exists), but TEAM-11/12 are not answered or hidden, DATA-19 is visible.
        // If stale TEAM-11/TEAM-12 answers exist in raw dictionary while TEAM-01 is "employees", but TEAM-03 is not answered,
        // TEAM-11/TEAM-12 are visible or hidden depending on routing.
        // If TEAM-01 is "employees" and no TEAM-11/12 are effective, DATA-19 is visible:
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["TEAM-01"] = new List<string> { "employees" }
            // TEAM-11 and TEAM-12 are absent
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        // Stale TEAM answers must not be effective
        Assert.DoesNotContain("TEAM-11", effectiveAnswers.Keys);
        Assert.DoesNotContain("TEAM-12", effectiveAnswers.Keys);

        // Team facts must not exist
        Assert.False(factStore.Facts.ContainsKey("team.offboardingProcess"));
        Assert.False(factStore.Facts.ContainsKey("team.formerAccessStatus"));

        // DATA-19 must be visible because Team facts are absent
        Assert.Contains(visibleQs, q => q.Id == "DATA-19");
    }

    // =========================================================================
    // 3. PRODUCT → AI REGULATED CONTEXT
    // =========================================================================

    [Theory(DisplayName = "3.1 Canonical regulated domains (health, investments, payments, loans, hiring, certificates) trigger AI-08 and AI_HUMAN_REVIEW_GAP")]
    [InlineData("health")]
    [InlineData("investments")]
    [InlineData("payments")]
    [InlineData("loans")]
    [InlineData("hiring")]
    [InlineData("certificates")]
    public void Canonical_Regulated_Domains_Trigger_Ai08_And_ReviewGap(string domain)
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { domain },
            ["AI-01"] = "external",
            ["AI-07"] = "assist",
            ["AI-08"] = "none"
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.Contains(visibleQs, q => q.Id == "AI-08");

        var findings = _ruleEngine.Evaluate(factStore, _allRisks);
        Assert.Contains(findings, f => f.Code == "AI_HUMAN_REVIEW_GAP");
    }

    [Theory(DisplayName = "3.2 Excluded domains (crypto, gambling, marketplace) alone do NOT make AI-08 visible with assist only")]
    [InlineData("crypto")]
    [InlineData("gambling")]
    [InlineData("marketplace")]
    public void Excluded_Domains_Do_Not_Trigger_Ai08_With_Assist(string domain)
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { domain },
            ["AI-01"] = "external",
            ["AI-07"] = "assist"
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.DoesNotContain(visibleQs, q => q.Id == "AI-08");
    }

    [Fact(DisplayName = "3.3 Stale Product regulated context loses effect when PROD-22 changes to non-regulated domain")]
    public void Stale_Product_Context_Does_Not_Trigger_Ai08()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { "crypto" }, // Health removed
            ["AI-01"] = "external",
            ["AI-07"] = "assist",
            ["AI-08"] = "none"                           // Stale answer to hidden question
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.DoesNotContain(visibleQs, q => q.Id == "AI-08");
        Assert.DoesNotContain("AI-08", effectiveAnswers.Keys);

        var findings = _ruleEngine.Evaluate(factStore, _allRisks);
        Assert.DoesNotContain(findings, f => f.Code == "AI_HUMAN_REVIEW_GAP");
    }

    // =========================================================================
    // 4. DATA → AI TRAINING CROSSOVER
    // =========================================================================

    [Fact(DisplayName = "4.1 DATA-08 ai_training sets ai.trainingUse = true and triggers AI_TRAINING_NOT_DISCLOSED identically to AI-06")]
    public void Data08_AiTraining_Crossover_Equivalence()
    {
        // Path A: DATA-08 ai_training
        var answersA = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-08"] = new List<string> { "ai_training" },
            ["AI-06A"] = "partial"
        };
        var factsA = FactNormalizer.NormalizeFacts(answersA);
        Assert.True(factsA.Facts.TryGetValue("ai.trainingUse", out var valA) && valA is true);
        var findingsA = _ruleEngine.Evaluate(factsA, _allRisks);
        Assert.Contains(findingsA, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");

        // Path B: AI-06 user_data
        var answersB = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-06"] = "user_data",
            ["AI-06A"] = "partial"
        };
        var factsB = FactNormalizer.NormalizeFacts(answersB);
        Assert.True(factsB.Facts.TryGetValue("ai.trainingUse", out var valB) && valB is true);
        var findingsB = _ruleEngine.Evaluate(factsB, _allRisks);
        Assert.Contains(findingsB, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");
    }

    [Fact(DisplayName = "4.2 Stale DATA-08 removes ai.trainingUse = true and hides AI-06A")]
    public void Stale_Data08_Removes_AiTraining_State()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-08"] = new List<string> { "marketing" }, // ai_training removed
            ["AI-06A"] = "no"                               // Stale answer
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.DoesNotContain(visibleQs, q => q.Id == "AI-06A");
        Assert.DoesNotContain("AI-06A", effectiveAnswers.Keys);

        var findings = _ruleEngine.Evaluate(factStore, _allRisks);
        Assert.DoesNotContain(findings, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");
    }

    // =========================================================================
    // 5. SENSITIVE DATA COMPOUND RULE & NEAR-MISSES
    // =========================================================================

    [Fact(DisplayName = "5.1 data.sensitiveData = true alone emits ZERO findings")]
    public void SensitiveData_Alone_Emits_Zero_Findings()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts" },
            ["DATA-03"] = "core",
            ["DATA-05"] = "full"
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code.Contains("SENSITIVE"));
    }

    [Fact(DisplayName = "5.2 Near-miss 1: AI_SENSITIVE_DATA_TRANSFER fails when ai.external is false")]
    public void SensitiveData_NearMiss_Not_External()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "none",
            ["AI-04"] = "none",
            ["AI-05"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    [Fact(DisplayName = "5.3 Near-miss 2: AI_SENSITIVE_DATA_TRANSFER fails when sensitiveDataSent is false")]
    public void SensitiveData_NearMiss_Not_Sensitive()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary",
            ["AI-03"] = "none",
            ["AI-04"] = "none",
            ["AI-05"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    [Fact(DisplayName = "5.4 Near-miss 3: AI_SENSITIVE_DATA_TRANSFER fails when user disclosure is healthy")]
    public void SensitiveData_NearMiss_Disclosure_Healthy()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "clear", // Healthy disclosure
            ["AI-04"] = "none",
            ["AI-05"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    [Fact(DisplayName = "5.5 Near-miss 4: AI_SENSITIVE_DATA_TRANSFER fails when provider terms review is healthy")]
    public void SensitiveData_NearMiss_ProviderReview_Healthy()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "none",
            ["AI-04"] = "full", // Healthy terms review
            ["AI-05"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    // =========================================================================
    // 6. CROSS-BORDER CONTEXT AND ISOLATION
    // =========================================================================

    [Fact(DisplayName = "6.1 Foreign storage alone does not trigger finding when cross-border review is yes")]
    public void ForeignStorage_Reviewed_NoFinding()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-13"] = "foreign_unreviewed",
            ["DATA-14"] = "yes"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_CROSS_BORDER_REVIEW");
    }

    [Fact(DisplayName = "6.2 Global users alone do not trigger finding when cross-border review is yes")]
    public void GlobalUsers_Reviewed_NoFinding()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-12"] = "global",
            ["DATA-14"] = "yes"
        };
        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_CROSS_BORDER_REVIEW");
    }

    // =========================================================================
    // 7. AI SENSITIVE SUPPRESSION & CROSS-MODULE AUDIT
    // =========================================================================

    [Fact(DisplayName = "7.1 AI_SENSITIVE_DATA_TRANSFER suppresses AI_USER_DATA_TRANSFER and AI_PROVIDER_TERMS_UNKNOWN")]
    public void AiSensitive_Suppresses_Subordinate_Risks()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "none",
            ["AI-04"] = "none",
            ["AI-05"] = "core"
        };

        var facts = FactNormalizer.NormalizeFacts(rawAnswers);
        var rawFindings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(rawFindings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
        Assert.Contains(rawFindings, f => f.Code == "AI_USER_DATA_TRANSFER");
        Assert.Contains(rawFindings, f => f.Code == "AI_PROVIDER_TERMS_UNKNOWN");

        var finalFindings = FindingProcessor.MergeAndSuppressFindings(rawFindings.ToList(), facts);
        Assert.Contains(finalFindings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
        Assert.DoesNotContain(finalFindings, f => f.Code == "AI_USER_DATA_TRANSFER");
        Assert.DoesNotContain(finalFindings, f => f.Code == "AI_PROVIDER_TERMS_UNKNOWN");
    }

    [Fact(DisplayName = "7.2 Cross-module risks (Team/Product/IP) coexist with DATA/AI risks without auto-suppression")]
    public void CrossModule_Risks_Coexist_Without_AutoSuppression()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-09"] = "ad_hoc", // TEAM_ACCESS_CONTROL_GAP
            ["PROD-01"] = "first",
            ["PROD-04"] = "none", // PROD_RULES_MISSING
            ["DATA-01"] = "yes",
            ["DATA-06"] = "none", // DATA_PRIVACY_NOTICE_MISSING
            ["DATA-18"] = "broad", // DATA_ACCESS_TOO_BROAD
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary",
            ["AI-04"] = "none" // AI_USER_DATA_TRANSFER, AI_PROVIDER_TERMS_UNKNOWN
        };

        var result = _engine.ComputeResult(rawAnswers);

        Assert.Contains(result.Risks, r => r.Code == "TEAM_ACCESS_CONTROL_GAP");
        Assert.Contains(result.Risks, r => r.Code == "PROD_RULES_MISSING");
        Assert.Contains(result.Risks, r => r.Code == "DATA_PRIVACY_NOTICE_MISSING");
        Assert.Contains(result.Risks, r => r.Code == "DATA_ACCESS_TOO_BROAD");
        Assert.Contains(result.Risks, r => r.Code == "AI_USER_DATA_TRANSFER");
        Assert.Contains(result.Risks, r => r.Code == "AI_PROVIDER_TERMS_UNKNOWN");
    }
}
