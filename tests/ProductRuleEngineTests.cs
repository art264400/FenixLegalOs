using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.Product;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ProductRuleEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly ProductRuleEngine _ruleEngine;
    private readonly List<RiskDefinition> _allRisks;

    public ProductRuleEngineTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_fenix_prod_rules_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();
        var dbInit = new DbInitializer(config);
        dbInit.Initialize();
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
        _ruleEngine = new ProductRuleEngine();
        _allRisks = DataBank.Risks;
    }

    // ─── 1. liveUsers=true + userRulesStatus=none -> PROD_RULES_MISSING HIGH ─────
    [Fact(DisplayName = "1. liveUsers=true + userRulesStatus=none -> PROD_RULES_MISSING HIGH")]
    public void LiveUsers_NoRules_Emits_ProdRulesMissing()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_RULES_MISSING");
        Assert.Equal(RiskSeverity.High, r.Severity);
        Assert.Equal("PRODUCT_RULES", r.RootCauseGroup);
        Assert.Equal("PRODUCT_LEGAL_REVIEW", r.ServiceCode);
    }

    // ─── 2. prelaunch + userRulesStatus=none -> NO PROD_RULES_MISSING ────────────
    [Fact(DisplayName = "2. prelaunch + userRulesStatus=none -> NO PROD_RULES_MISSING")]
    public void Prelaunch_NoRules_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "prelaunch",
            ["PROD-04"] = "none"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_RULES_MISSING");
    }

    // ─── 3. rulesMatch=changed -> PROD_RULES_MISMATCH ────────────────────────────
    [Fact(DisplayName = "3. rulesMatch=changed -> PROD_RULES_MISMATCH")]
    public void RulesMatch_Changed_Emits_ProdRulesMismatch()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-04"] = "current",
            ["PROD-05"] = "changed"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_RULES_MISMATCH");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 4. rulesMatch=template_unchecked -> PROD_RULES_MISMATCH ─────────────────
    [Fact(DisplayName = "4. rulesMatch=template_unchecked -> PROD_RULES_MISMATCH")]
    public void RulesMatch_TemplateUnchecked_Emits_ProdRulesMismatch()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-04"] = "template",
            ["PROD-05"] = "template_unchecked"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_RULES_MISMATCH");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 5. rulesMatch=mostly -> no mismatch finding ─────────────────────────────
    [Fact(DisplayName = "5. rulesMatch=mostly -> no mismatch finding")]
    public void RulesMatch_Mostly_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-04"] = "current",
            ["PROD-05"] = "mostly"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_RULES_MISMATCH");
    }

    // ─── 6. providerRole=marketplace + roleClarity=unclear -> PROD_ROLE_UNCLEAR ─
    [Fact(DisplayName = "6. providerRole=marketplace + roleClarity=unclear -> PROD_ROLE_UNCLEAR")]
    public void ProviderRole_Marketplace_RoleClarity_Unclear_Emits_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-07"] = "marketplace",
            ["PROD-07A"] = "unclear"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_ROLE_UNCLEAR");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 7. providerRole=company + roleClarity=unclear -> no PROD_ROLE_UNCLEAR ──
    [Fact(DisplayName = "7. providerRole=company + roleClarity=unclear -> no PROD_ROLE_UNCLEAR")]
    public void ProviderRole_Company_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-07"] = "company",
            ["PROD-07A"] = "unclear" // Stale / not applicable for company
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_ROLE_UNCLEAR");
    }

    // ─── 8. subscription=true + autoRenew=true + autoRenewDisclosure=terms_only ─
    [Fact(DisplayName = "8. subscription=true + autoRenew=true + autoRenewDisclosure=terms_only -> one PROD_SUBSCRIPTION_RULES")]
    public void Subscription_AutoRenew_TermsOnly_Emits_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "terms_only",
            ["PROD-14"] = "self_service"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_SUBSCRIPTION_RULES");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 9. subscription=true + subscriptionCancellation=complex ─────────────────
    [Fact(DisplayName = "9. subscription=true + subscriptionCancellation=complex -> same one finding")]
    public void Subscription_Cancellation_Complex_Emits_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "no",
            ["PROD-14"] = "complex"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_SUBSCRIPTION_RULES");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 10. subscription=true + trialDisclosure=none ────────────────────────────
    [Fact(DisplayName = "10. subscription=true + trialDisclosure=none -> same one finding")]
    public void Subscription_Trial_NotExplained_Emits_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "no",
            ["PROD-14"] = "self_service",
            ["PROD-15"] = "not_explained"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_SUBSCRIPTION_RULES");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 11. Multiple subscription failures -> exactly one PROD_SUBSCRIPTION_RULES
    [Fact(DisplayName = "11. Multiple subscription failures -> exactly one PROD_SUBSCRIPTION_RULES")]
    public void Multiple_Subscription_Failures_Merge_To_One()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "no", // none
            ["PROD-14"] = "complex",
            ["PROD-15"] = "not_explained"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var subFindings = findings.Where(f => f.Code == "PROD_SUBSCRIPTION_RULES").ToList();
        Assert.Single(subFindings);
    }

    // ─── 12. trialExists=false / no_trial -> does not trigger trial failure ──────
    [Fact(DisplayName = "12. trialExists=false / no_trial -> does not trigger trial failure")]
    public void NoTrial_Does_Not_Trigger_Trial_Failure()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "no",
            ["PROD-14"] = "self_service",
            ["PROD-15"] = "no_trial"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_SUBSCRIPTION_RULES");
    }

    // ─── 13. minorsAllowed=true + minorsReview=no -> PROD_MINORS_REVIEW ──────────
    [Fact(DisplayName = "13. minorsAllowed=true + minorsReview=no -> PROD_MINORS_REVIEW")]
    public void MinorsAllowed_NoReview_Emits_ProdMinorsReview()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["PROD-20"] = "yes",
            ["PROD-20A"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_MINORS_REVIEW");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 14. minorsAllowed=possible + minorsReview=unknown -> PROD_MINORS_REVIEW ─
    [Fact(DisplayName = "14. minorsAllowed=possible + minorsReview=unknown -> PROD_MINORS_REVIEW")]
    public void MinorsPossible_UnknownReview_Emits_ProdMinorsReview()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["PROD-20"] = "possible",
            ["PROD-20A"] = "unknown"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_MINORS_REVIEW");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 15. minorsAllowed=false -> no PROD_MINORS_REVIEW ────────────────────────
    [Fact(DisplayName = "15. minorsAllowed=false -> no PROD_MINORS_REVIEW")]
    public void MinorsNotAllowed_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-02"] = new List<string> { "consumers" },
            ["PROD-20"] = "no",
            ["PROD-20A"] = "no" // Stale
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_MINORS_REVIEW");
    }

    // ─── 16. regulatedFunctions=["crypto"] -> PROD_REGULATORY_REVIEW HIGH ────────
    [Fact(DisplayName = "16. regulatedFunctions=['crypto'] -> PROD_REGULATORY_REVIEW HIGH")]
    public void RegulatedFunctions_Crypto_Emits_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { "crypto" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var r = Assert.Single(findings, f => f.Code == "PROD_REGULATORY_REVIEW");
        Assert.Equal(RiskSeverity.High, r.Severity);
    }

    // ─── 17. regulatedFunctions=["none"] -> no PROD_REGULATORY_REVIEW ────────────
    [Fact(DisplayName = "17. regulatedFunctions=['none'] -> no PROD_REGULATORY_REVIEW")]
    public void RegulatedFunctions_None_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-22"] = new List<string> { "none" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_REGULATORY_REVIEW");
    }

    // ─── 18. regulatedFunctions never auto-Critical solely because of function ───
    [Fact(DisplayName = "18. regulatedFunctions never auto-Critical solely because of that function")]
    public void RegulatedFunctions_Severity_Is_High_Not_Critical()
    {
        foreach (var func in new[] { "payments", "investments", "loans", "crypto", "health", "hiring", "certificates", "gambling", "marketplace" })
        {
            var answers = new Dictionary<string, object>
            {
                ["PROD-22"] = new List<string> { func }
            };
            var facts = FactNormalizer.NormalizeFacts(answers);
            var findings = _ruleEngine.Evaluate(facts, _allRisks);

            var r = Assert.Single(findings, f => f.Code == "PROD_REGULATORY_REVIEW");
            Assert.Equal(RiskSeverity.High, r.Severity);
        }
    }

    // ─── 19. refundRules=no_refunds -> does NOT automatically emit finding ───────
    [Fact(DisplayName = "19. refundRules=no_refunds -> does NOT automatically emit PROD_REFUND_RULES")]
    public void RefundRules_NoRefunds_No_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "one_off",
            ["PROD-12"] = "no_refunds"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_REFUND_RULES");
    }

    // ─── 20. Free product -> no payment / refund finding from stale answers ─────
    [Fact(DisplayName = "20. free product -> no payment transparency / refund finding caused by hidden stale answers")]
    public void FreeProduct_StaleAnswers_No_Payment_Findings()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-10"] = "free",
            ["PROD-11"] = "late_fees",
            ["PROD-12"] = "unclear"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_PAYMENT_TRANSPARENCY");
        Assert.DoesNotContain(findings, f => f.Code == "PROD_REFUND_RULES");
    }

    // ─── 21. UGC false -> no UGC finding from stale PROD-18A/B/19 answers ────────
    [Fact(DisplayName = "21. UGC false -> no UGC finding from stale PROD-18A/B/19 answers")]
    public void Ugc_False_StaleAnswers_No_Ugc_Finding()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-18"] = "no",
            ["PROD-18A"] = "no",
            ["PROD-18B"] = "no",
            ["PROD-19"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.DoesNotContain(findings, f => f.Code == "PROD_USER_CONTENT_RULES");
    }

    // ─── 22. Hidden stale Product answers produce zero findings in full engine ───
    [Fact(DisplayName = "22. hidden stale Product answers produce zero findings in full engine")]
    public void Hidden_Stale_Product_Answers_Zero_Findings_In_Full_Engine()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "prelaunch",
            ["PROD-04"] = "none",
            ["PROD-05"] = "changed", // Stale: PROD-05 hidden when PROD-04 is none
            ["PROD-08"] = "no_rules" // Stale: PROD-08 hidden for prelaunch
        };
        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_RULES_MISSING");
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_RULES_MISMATCH");
        Assert.DoesNotContain(result.Risks, r => r.Code == "PROD_ACCEPTANCE_WEAK");
    }

    // ─── 23. Unknown vs absent semantics are preserved ───────────────────────────
    [Fact(DisplayName = "23. unknown vs absent semantics are preserved")]
    public void Unknown_Vs_Absent_Semantics()
    {
        // Absent PROD-06 -> no fact -> no finding
        var fAbsent = FactNormalizer.NormalizeFacts(new() { ["PROD-01"] = "first" });
        var findingsAbsent = _ruleEngine.Evaluate(fAbsent, _allRisks);
        Assert.DoesNotContain(findingsAbsent, f => f.Code == "PROD_OFFER_UNCLEAR");

        // Explicit unknown PROD-06 -> fact = "unknown" -> finding emitted
        var fUnknown = FactNormalizer.NormalizeFacts(new() { ["PROD-01"] = "first", ["PROD-06"] = "unknown" });
        var findingsUnknown = _ruleEngine.Evaluate(fUnknown, _allRisks);
        Assert.Contains(findingsUnknown, f => f.Code == "PROD_OFFER_UNCLEAR");
    }

    // ─── 24. AffectedDimensions resolve for all 13 Product risks ─────────────────
    [Fact(DisplayName = "24. AffectedDimensions resolve for all 13 Product risks")]
    public void AffectedDimensions_Resolve_For_All_13_Product_Risks()
    {
        var prodDims = ProductDimensions.All.Select(d => d.Id).ToHashSet();

        Assert.Equal(13, ProductRisks.All.Count);
        foreach (var risk in ProductRisks.All)
        {
            Assert.NotEmpty(risk.AffectedDimensions);
            foreach (var dim in risk.AffectedDimensions)
            {
                Assert.Contains(dim, prodDims);
            }
        }
    }

    // ─── 25. All emitted Product risk codes exist in ProductRisks ────────────────
    [Fact(DisplayName = "25. all emitted Product risk codes exist in ProductRisks")]
    public void All_Emitted_Codes_Exist_In_ProductRisks()
    {
        var canonicalCodes = ProductRisks.All.Select(r => r.Code).ToHashSet();

        // Feed answers that trigger everything
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-05"] = "changed",
            ["PROD-06"] = "mismatch",
            ["PROD-07"] = "marketplace",
            ["PROD-07A"] = "unclear",
            ["PROD-08"] = "link_only",
            ["PROD-10"] = "subscription",
            ["PROD-11"] = "late_fees",
            ["PROD-12"] = "unclear",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "terms_only",
            ["PROD-14"] = "complex",
            ["PROD-15"] = "not_explained",
            ["PROD-16"] = "none",
            ["PROD-17"] = "undefined",
            ["PROD-18"] = "yes",
            ["PROD-18A"] = "no",
            ["PROD-18B"] = "no",
            ["PROD-19"] = "no",
            ["PROD-20"] = "yes",
            ["PROD-20A"] = "no",
            ["PROD-21"] = "global",
            ["PROD-21A"] = "no",
            ["PROD-22"] = new List<string> { "crypto" }
        };

        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        Assert.Equal(13, findings.Count);
        foreach (var finding in findings)
        {
            Assert.Contains(finding.Code, canonicalCodes);
        }
    }

    // ─── 26. No orphan Product risks ─────────────────────────────────────────────
    [Fact(DisplayName = "26. no orphan Product risks - all 13 are triggered by corresponding facts")]
    public void No_Orphan_Product_Risks()
    {
        var canonicalCodes = ProductRisks.All.Select(r => r.Code).ToList();
        Assert.Equal(13, canonicalCodes.Count);

        // Every risk should be testable and emittable
        var allTriggerAnswers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-04"] = "none",
            ["PROD-05"] = "changed",
            ["PROD-06"] = "mismatch",
            ["PROD-07"] = "marketplace",
            ["PROD-07A"] = "unclear",
            ["PROD-08"] = "link_only",
            ["PROD-10"] = "subscription",
            ["PROD-11"] = "late_fees",
            ["PROD-12"] = "unclear",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "terms_only",
            ["PROD-16"] = "none",
            ["PROD-18"] = "yes",
            ["PROD-18A"] = "no",
            ["PROD-20"] = "yes",
            ["PROD-20A"] = "no",
            ["PROD-21"] = "global",
            ["PROD-21A"] = "no",
            ["PROD-22"] = new List<string> { "crypto" }
        };

        var facts = FactNormalizer.NormalizeFacts(allTriggerAnswers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);
        var emittedCodes = findings.Select(f => f.Code).ToHashSet();

        foreach (var code in canonicalCodes)
        {
            Assert.Contains(code, emittedCodes);
        }
    }

    // ─── 27. No duplicate Product finding codes in output ────────────────────────
    [Fact(DisplayName = "27. no duplicate Product finding codes in output")]
    public void No_Duplicate_Finding_Codes()
    {
        var answers = new Dictionary<string, object>
        {
            ["PROD-01"] = "first",
            ["PROD-10"] = "subscription",
            ["PROD-13"] = "yes",
            ["PROD-13A"] = "none",
            ["PROD-14"] = "undefined",
            ["PROD-15"] = "not_explained",
            ["PROD-18"] = "yes",
            ["PROD-18A"] = "none",
            ["PROD-18B"] = "none",
            ["PROD-19"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var findings = _ruleEngine.Evaluate(facts, _allRisks);

        var codes = findings.Select(f => f.Code).ToList();
        Assert.Equal(codes.Distinct().Count(), codes.Count);
    }

    // ─── 28. Existing suppression graph remains acyclic ──────────────────────────
    [Fact(DisplayName = "28. existing suppression graph remains acyclic")]
    public void Suppression_Graph_Is_Acyclic()
    {
        var allRisks = DataBank.Risks;
        var map = allRisks.ToDictionary(r => r.Code, r => r.SuppressCodes ?? new());

        foreach (var (code, suppList) in map)
        {
            // DFS cycle check
            var visited = new HashSet<string>();
            var stack = new Stack<string>();
            stack.Push(code);

            while (stack.Count > 0)
            {
                var current = stack.Pop();
                if (map.TryGetValue(current, out var nextList))
                {
                    foreach (var next in nextList)
                    {
                        Assert.NotEqual(code, next); // No direct cycle
                        if (visited.Add(next))
                        {
                            stack.Push(next);
                        }
                    }
                }
            }
        }
    }
}
