using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Data;
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

public class DataAiRuleEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly DataAiRuleEngine _ruleEngine;
    private readonly List<RiskDefinition> _allRisks;

    public DataAiRuleEngineTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_data_ai_rules_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
        _ruleEngine = new DataAiRuleEngine();
        _allRisks = DataBank.Risks;
    }

    // =========================================================================
    // A. REGISTRY INVARIANTS
    // =========================================================================

    [Fact(DisplayName = "A1. Exactly 15 canonical DATA/AI risks registered")]
    public void Exactly_15_Canonical_DataAi_Risks()
    {
        Assert.Equal(15, DataAiRisks.All.Count);
    }

    [Fact(DisplayName = "A2. Every risk code is unique across the entire Risk Library")]
    public void Risk_Codes_Globally_Unique()
    {
        var allCodes = DataBank.Risks.Select(r => r.Code).ToList();
        var uniqueCodes = allCodes.Distinct().ToList();
        Assert.Equal(allCodes.Count, uniqueCodes.Count);
    }

    [Fact(DisplayName = "A3. Every DATA_AI risk definition contains non-empty title, finding, recommendation, serviceCode")]
    public void Risk_Definitions_Complete_Metadata()
    {
        foreach (var risk in DataAiRisks.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(risk.Code));
            Assert.False(string.IsNullOrWhiteSpace(risk.Title));
            Assert.False(string.IsNullOrWhiteSpace(risk.Finding));
            Assert.False(string.IsNullOrWhiteSpace(risk.Recommendation));
            Assert.Equal("DATA_AI_REVIEW", risk.ServiceCode);
            Assert.Equal("data", risk.SectionId);
        }
    }

    [Fact(DisplayName = "A4. Every AffectedDimension in DATA_AI resolves to one of the 10 canonical dimensions")]
    public void Affected_Dimensions_Resolve_Canonically()
    {
        var canonicalDimIds = DataBank.Dimensions
            .Where(d => d.SectionId == "data")
            .Select(d => d.Id)
            .ToHashSet();

        foreach (var risk in DataAiRisks.All)
        {
            Assert.NotEmpty(risk.AffectedDimensions);
            foreach (var dim in risk.AffectedDimensions)
            {
                Assert.Contains(dim, canonicalDimIds);
            }
        }
    }

    [Fact(DisplayName = "A5. Suppression targets resolve to valid risk codes without self-suppression or duplicates")]
    public void Suppression_Targets_Valid_And_Clean()
    {
        var allCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();

        foreach (var risk in DataAiRisks.All)
        {
            if (risk.SuppressCodes != null && risk.SuppressCodes.Count > 0)
            {
                Assert.DoesNotContain(risk.Code, risk.SuppressCodes);
                Assert.Equal(risk.SuppressCodes.Count, risk.SuppressCodes.Distinct().Count());
                foreach (var target in risk.SuppressCodes)
                {
                    Assert.Contains(target, allCodes);
                }
            }
        }
    }

    // =========================================================================
    // B. DATA RULES
    // =========================================================================

    [Fact(DisplayName = "B8. DATA_MAP_INCOMPLETE triggers when map is incomplete and external services or >2 data types exist")]
    public void DataMapIncomplete_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "financial", "auth" },
            ["DATA-05"] = "developers_only"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_MAP_INCOMPLETE");
    }

    [Fact(DisplayName = "B9. DATA_MAP_INCOMPLETE does NOT trigger when map is full")]
    public void DataMapIncomplete_DoesNotTrigger_When_Full()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "financial", "auth" },
            ["DATA-05"] = "full"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_MAP_INCOMPLETE");
    }

    [Fact(DisplayName = "B10. DATA_PRIVACY_NOTICE_MISSING triggers when privacyNotice is none")]
    public void PrivacyNoticeMissing_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-06"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_PRIVACY_NOTICE_MISSING");
    }

    [Fact(DisplayName = "B11. DATA_PRIVACY_NOTICE_OUTDATED triggers on changed/template or old notice with AI")]
    public void PrivacyNoticeOutdated_Triggers()
    {
        var answers1 = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-06"] = "custom",
            ["DATA-07"] = "changed"
        };
        var facts1 = FactNormalizer.NormalizeFacts(answers1);
        var findings1 = _ruleEngine.Evaluate(facts1, _allRisks);
        Assert.Contains(findings1, f => f.Code == "DATA_PRIVACY_NOTICE_OUTDATED");

        var answers2 = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-06"] = "old",
            ["AI-01"] = "external"
        };
        var facts2 = FactNormalizer.NormalizeFacts(answers2);
        var findings2 = _ruleEngine.Evaluate(facts2, _allRisks);
        Assert.Contains(findings2, f => f.Code == "DATA_PRIVACY_NOTICE_OUTDATED");
    }

    [Fact(DisplayName = "B12. DATA_SECONDARY_USE_UNCLEAR triggers when secondary use is active and disclosure is partial/none/unknown")]
    public void SecondaryUseUnclear_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-08"] = new List<string> { "marketing", "analytics" },
            ["DATA-09"] = "partial"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_SECONDARY_USE_UNCLEAR");
    }

    [Fact(DisplayName = "B13. Secondary use alone does NOT trigger finding when fully disclosed")]
    public void SecondaryUse_DoesNotTrigger_When_Fully_Disclosed()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-08"] = new List<string> { "marketing", "analytics" },
            ["DATA-09"] = "clear"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_SECONDARY_USE_UNCLEAR");
    }

    [Fact(DisplayName = "B14. DATA_THIRD_PARTY_UNKNOWN triggers on incomplete service map or unreviewed terms")]
    public void ThirdPartyUnknown_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-10"] = "yes",
            ["DATA-10A"] = "partial",
            ["DATA-11"] = "some"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_THIRD_PARTY_UNKNOWN");
    }

    [Fact(DisplayName = "B15. External services alone do NOT trigger finding when map and terms are reviewed")]
    public void ThirdParty_DoesNotTrigger_When_Fully_Mapped_And_Reviewed()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-10"] = "yes",
            ["DATA-10A"] = "yes",
            ["DATA-11"] = "main"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_THIRD_PARTY_UNKNOWN");
    }

    [Fact(DisplayName = "B16. DATA_CROSS_BORDER_REVIEW triggers when data is stored abroad and review is partial")]
    public void CrossBorderReview_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-13"] = "foreign_unreviewed",
            ["DATA-14"] = "partial"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_CROSS_BORDER_REVIEW");
    }

    [Fact(DisplayName = "B17. Foreign storage alone does NOT trigger finding when cross-border review is full")]
    public void ForeignStorage_DoesNotTrigger_When_Reviewed()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-13"] = "foreign_unreviewed",
            ["DATA-14"] = "yes"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "DATA_CROSS_BORDER_REVIEW");
    }

    [Fact(DisplayName = "B18. DATA_RETENTION_UNDEFINED and DATA_DELETION_GAP trigger appropriately")]
    public void RetentionAndDeletion_Trigger()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-15"] = "none",
            ["DATA-16"] = "not_all_systems"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_RETENTION_UNDEFINED");
        Assert.Contains(findings, f => f.Code == "DATA_DELETION_GAP");
    }

    [Fact(DisplayName = "B19. DATA_ACCESS_TOO_BROAD triggers when team access is broad")]
    public void TeamAccessTooBroad_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-18"] = "broad"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_ACCESS_TOO_BROAD");
    }

    // =========================================================================
    // C. AI RULES
    // =========================================================================

    [Fact(DisplayName = "C22. AI_USER_DATA_TRANSFER triggers when user data is sent to external AI")]
    public void AiUserDataTransfer_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary",
            ["AI-03"] = "clear",
            ["AI-04"] = "full"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "AI_USER_DATA_TRANSFER");
        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    [Fact(DisplayName = "C23. External AI alone does NOT trigger AI_USER_DATA_TRANSFER if no user data is sent")]
    public void ExternalAi_DoesNotTrigger_When_No_UserData()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_USER_DATA_TRANSFER");
    }

    [Fact(DisplayName = "C24. AI_SENSITIVE_DATA_TRANSFER triggers CRITICAL and suppresses lower AI transfer findings")]
    public void AiSensitiveDataTransfer_Triggers_Critical_And_Suppresses()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "partial",
            ["AI-04"] = "not_specific",
            ["AI-05"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var rawFindings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(rawFindings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER" && f.Severity == RiskSeverity.Critical);

        // Process through FindingProcessor to verify suppression
        var finalFindings = FindingProcessor.MergeAndSuppressFindings(rawFindings.ToList(), facts);
        Assert.Contains(finalFindings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
        Assert.DoesNotContain(finalFindings, f => f.Code == "AI_USER_DATA_TRANSFER");
        Assert.DoesNotContain(finalFindings, f => f.Code == "AI_PROVIDER_TERMS_UNKNOWN");
    }

    [Fact(DisplayName = "C25. Sensitive data existence alone does NOT trigger CRITICAL without deficiency")]
    public void SensitiveData_Alone_DoesNotTrigger_Critical()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["AI-02"] = "sensitive",
            ["AI-03"] = "clear", // fully disclosed
            ["AI-04"] = "full",   // fully reviewed terms
            ["AI-05"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "AI_SENSITIVE_DATA_TRANSFER");
    }

    [Fact(DisplayName = "C26. AI_TRAINING_NOT_DISCLOSED triggers on trainingUse with partial/none/unknown disclosure")]
    public void AiTrainingNotDisclosed_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "own",
            ["AI-06"] = "user_data",
            ["AI-06A"] = "partial"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");
    }

    [Fact(DisplayName = "C27. DATA-08 ai_training and AI-06 user_data yield equivalent downstream rule behavior")]
    public void AiTraining_Equivalence_Between_Data08_And_Ai06()
    {
        var answers1 = new Dictionary<string, object>
        {
            ["AI-06"] = "user_data",
            ["AI-06A"] = "no"
        };
        var facts1 = FactNormalizer.NormalizeFacts(answers1);
        var findings1 = _ruleEngine.Evaluate(facts1, _allRisks);

        var answers2 = new Dictionary<string, object>
        {
            ["DATA-08"] = new List<string> { "ai_training" },
            ["AI-06A"] = "no"
        };
        var facts2 = FactNormalizer.NormalizeFacts(answers2);
        var findings2 = _ruleEngine.Evaluate(facts2, _allRisks);

        Assert.Contains(findings1, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");
        Assert.Contains(findings2, f => f.Code == "AI_TRAINING_NOT_DISCLOSED");
    }

    [Fact(DisplayName = "C28. AI_AUTOMATED_DECISION triggers when material decision is automatic without full transparency")]
    public void AiAutomatedDecision_Triggers()
    {
        var answers = new Dictionary<string, object>
        {
            ["AI-07"] = "automatic",
            ["AI-07A"] = "partial"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "AI_AUTOMATED_DECISION");
    }

    [Fact(DisplayName = "C29. AI_HUMAN_REVIEW_GAP triggers in regulated product context when human review is spot/none/unknown")]
    public void AiHumanReviewGap_Triggers_In_Regulated_Context()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { "health", "payments" },
            ["AI-01"] = "external",
            ["AI-07"] = "assist",
            ["AI-08"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "AI_HUMAN_REVIEW_GAP");
    }

    // =========================================================================
    // D. UNKNOWN / N/A ISOLATION
    // =========================================================================

    [Fact(DisplayName = "D33. No-data and no-AI scenario yields ZERO DATA_AI findings")]
    public void NoData_NoAi_Yields_Zero_Findings()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Empty(findings);
    }

    [Fact(DisplayName = "D34. Data-only scenario yields NO AI-only findings")]
    public void DataOnly_Scenario_Yields_No_Ai_Findings()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "auth", "financial" },
            ["DATA-05"] = "none",
            ["AI-01"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Contains(findings, f => f.Code == "DATA_MAP_INCOMPLETE");
        Assert.DoesNotContain(findings, f => f.Code.StartsWith("AI_"));
    }

    [Fact(DisplayName = "D35. Stale hidden answers have zero effect under EffectiveAnswers resolution")]
    public void Stale_Hidden_Answers_Zero_Effect()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["DATA-05"] = "none", // Stale answer to hidden question
            ["AI-01"] = "no",
            ["AI-02"] = "sensitive" // Stale answer to hidden question
        };

        var allQs = DataBank.Questions;
        var (visibleQs, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        var findings = _ruleEngine.Evaluate(factStore, _allRisks);
        Assert.Empty(findings);
    }

    // =========================================================================
    // E. PIPELINE INTEGRATION
    // =========================================================================

    [Fact(DisplayName = "E37. Full ScoringEngine integrates DataAiRuleEngine seamlessly")]
    public void ScoringEngine_Full_Pipeline_Integration()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "no",
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contacts", "auth", "financial" },
            ["DATA-05"] = "none",
            ["AI-01"] = "external",
            ["AI-02"] = "ordinary",
            ["AI-04"] = "none"
        };

        var result = _engine.ComputeResult(answers);
        Assert.NotNull(result);
        Assert.Contains(result.Risks, f => f.Code == "DATA_MAP_INCOMPLETE");
        Assert.Contains(result.Risks, f => f.Code == "AI_USER_DATA_TRANSFER");
        Assert.Contains(result.Risks, f => f.Code == "AI_PROVIDER_TERMS_UNKNOWN");
    }
}
