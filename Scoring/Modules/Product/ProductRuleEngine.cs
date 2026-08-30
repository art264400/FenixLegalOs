using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Product;

public class ProductRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "product";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        bool liveUsers = GetBoolFact(f, "product.liveUsers");
        var userRulesStatus = (string?)f.GetValueOrDefault("product.userRulesStatus");
        var rulesMatch = (string?)f.GetValueOrDefault("product.rulesMatch");
        var offerClarity = (string?)f.GetValueOrDefault("product.offerClarity");
        var providerRole = (string?)f.GetValueOrDefault("product.providerRole");
        var roleClarity = (string?)f.GetValueOrDefault("product.roleClarity");
        var termsAcceptance = (string?)f.GetValueOrDefault("product.termsAcceptance");
        var acceptanceEvidence = (string?)f.GetValueOrDefault("product.acceptanceEvidence");
        bool paid = GetBoolFact(f, "product.paid");
        bool subscription = GetBoolFact(f, "product.subscription");
        var priceTransparency = (string?)f.GetValueOrDefault("product.priceTransparency");
        var refundRules = (string?)f.GetValueOrDefault("product.refundRules");
        var autoRenew = f.GetValueOrDefault("product.autoRenew");
        var autoRenewDisclosure = (string?)f.GetValueOrDefault("product.autoRenewDisclosure");
        var subscriptionCancellation = (string?)f.GetValueOrDefault("product.subscriptionCancellation");
        var trialDisclosure = (string?)f.GetValueOrDefault("product.trialDisclosure");
        var suspensionRules = (string?)f.GetValueOrDefault("product.suspensionRules");
        var suspensionPaymentRules = (string?)f.GetValueOrDefault("product.suspensionPaymentRules");
        var ugc = f.GetValueOrDefault("product.ugc");
        var ugcRestrictions = (string?)f.GetValueOrDefault("product.ugcRestrictions");
        var ugcUseRules = (string?)f.GetValueOrDefault("product.ugcUseRules");
        var ugcComplaint = (string?)f.GetValueOrDefault("product.ugcComplaint");
        var minorsAllowed = f.GetValueOrDefault("product.minorsAllowed");
        var minorsReview = (string?)f.GetValueOrDefault("product.minorsReview");
        var userGeography = (string?)f.GetValueOrDefault("product.userGeography");
        var multiCountryReview = (string?)f.GetValueOrDefault("product.multiCountryReview");
        var regulatedFunctions = f.GetValueOrDefault("product.regulatedFunctions") as List<string> ?? new();

        // 1. PROD_RULES_MISSING (§27.2)
        // product.liveUsers == true AND product.userRulesStatus == none -> HIGH
        if (liveUsers && userRulesStatus == "none")
        {
            AddFinding(list, allRisks, "PROD_RULES_MISSING", "PROD-04", userRulesStatus, RiskSeverity.High);
        }

        // 2. PROD_RULES_MISMATCH (§27.2)
        // product.rulesMatch in [changed, template_unchecked] -> HIGH
        if (rulesMatch is "changed" or "template_unchecked")
        {
            AddFinding(list, allRisks, "PROD_RULES_MISMATCH", "PROD-05", rulesMatch, RiskSeverity.High);
        }

        // 3. PROD_OFFER_UNCLEAR (§25)
        // product.offerClarity in [some_unclear, mismatch, unknown] -> HIGH
        if (offerClarity is "some_unclear" or "mismatch" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_OFFER_UNCLEAR", "PROD-06", offerClarity, RiskSeverity.High);
        }

        // 4. PROD_ROLE_UNCLEAR (§27.2)
        // product.providerRole in [joint, marketplace, varies] AND product.roleClarity in [partial, unclear, unknown] -> HIGH
        if (providerRole is "joint" or "marketplace" or "varies" &&
            roleClarity is "partial" or "unclear" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_ROLE_UNCLEAR", "PROD-07A", roleClarity, RiskSeverity.High);
        }

        // 5. PROD_ACCEPTANCE_WEAK (§25)
        // product.liveUsers == true AND (termsAcceptance in [link_only, published_only, no_rules, unknown] OR (termsAcceptance == explicit AND acceptanceEvidence in [none, unknown]))
        if (liveUsers)
        {
            if (termsAcceptance is "link_only" or "published_only" or "no_rules" or "unknown")
            {
                AddFinding(list, allRisks, "PROD_ACCEPTANCE_WEAK", "PROD-08", termsAcceptance, RiskSeverity.High);
            }
            else if (termsAcceptance == "explicit" && acceptanceEvidence is "none" or "unknown")
            {
                AddFinding(list, allRisks, "PROD_ACCEPTANCE_WEAK", "PROD-09", acceptanceEvidence, RiskSeverity.High);
            }
        }

        // 6. PROD_PAYMENT_TRANSPARENCY (§25)
        // product.paid == true AND product.priceTransparency in [late_fees, unknown] -> MEDIUM
        if (paid && priceTransparency is "late_fees" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_PAYMENT_TRANSPARENCY", "PROD-11", priceTransparency, RiskSeverity.Medium);
        }

        // 7. PROD_REFUND_RULES (§25)
        // product.paid == true AND product.refundRules in [unclear, case_policy, unknown] -> MEDIUM
        // (no_refunds and published do NOT trigger this finding)
        if (paid && refundRules is "unclear" or "case_policy" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_REFUND_RULES", "PROD-12", refundRules, RiskSeverity.Medium);
        }

        // 8. PROD_SUBSCRIPTION_RULES (§27.2)
        // product.subscription == true AND ((autoRenew == true AND autoRenewDisclosure in [terms_only, none, unknown]) OR subscriptionCancellation in [complex, undefined] OR trialDisclosure == none) -> HIGH
        if (subscription)
        {
            bool autoRenewIssue = (autoRenew is true || autoRenew is "true") && autoRenewDisclosure is "terms_only" or "none" or "unknown";
            bool cancellationIssue = subscriptionCancellation is "complex" or "undefined";
            bool trialIssue = trialDisclosure == "none";

            if (autoRenewIssue || cancellationIssue || trialIssue)
            {
                string qId = autoRenewIssue ? "PROD-13A" : cancellationIssue ? "PROD-14" : "PROD-15";
                string ansVal = autoRenewIssue ? (autoRenewDisclosure ?? "") : cancellationIssue ? (subscriptionCancellation ?? "") : (trialDisclosure ?? "");
                AddFinding(list, allRisks, "PROD_SUBSCRIPTION_RULES", qId, ansVal, RiskSeverity.High);
            }
        }

        // 9. PROD_ACCOUNT_RESTRICTIONS (§25)
        // product.liveUsers == true AND (suspensionRules in [case_by_case, none, unknown] OR (paid == true AND suspensionPaymentRules in [individual, undefined, unknown])) -> MEDIUM
        if (liveUsers)
        {
            bool rulesWeak = suspensionRules is "case_by_case" or "none" or "unknown";
            bool paymentWeak = paid && suspensionPaymentRules is "individual" or "undefined" or "unknown";
            if (rulesWeak || paymentWeak)
            {
                string qId = rulesWeak ? "PROD-16" : "PROD-17";
                string ansVal = rulesWeak ? (suspensionRules ?? "") : (suspensionPaymentRules ?? "");
                AddFinding(list, allRisks, "PROD_ACCOUNT_RESTRICTIONS", qId, ansVal, RiskSeverity.Medium);
            }
        }

        // 10. PROD_USER_CONTENT_RULES (§25)
        // (product.ugc == true OR unknown) AND (ugcRestrictions in [none, general, unknown] OR ugcUseRules in [none, partial, unknown] OR ugcComplaint in [no, partial, unknown]) -> HIGH
        bool hasUgc = ugc is true || ugc is "unknown";
        if (hasUgc)
        {
            bool restrictionsWeak = ugcRestrictions is "none" or "general" or "unknown";
            bool useRulesWeak = ugcUseRules is "none" or "partial" or "unknown";
            bool complaintWeak = ugcComplaint is "no" or "partial" or "unknown";

            if (restrictionsWeak || useRulesWeak || complaintWeak)
            {
                string qId = restrictionsWeak ? "PROD-18A" : useRulesWeak ? "PROD-18B" : "PROD-19";
                string ansVal = restrictionsWeak ? (ugcRestrictions ?? "") : useRulesWeak ? (ugcUseRules ?? "") : (ugcComplaint ?? "");
                AddFinding(list, allRisks, "PROD_USER_CONTENT_RULES", qId, ansVal, RiskSeverity.High);
            }
        }

        // 11. PROD_MINORS_REVIEW (§27.2)
        // product.minorsAllowed in [true, possible] AND product.minorsReview in [partial, no, unknown] -> HIGH
        bool allowedOrPossible = minorsAllowed is true || minorsAllowed is "possible";
        if (allowedOrPossible && minorsReview is "partial" or "no" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_MINORS_REVIEW", "PROD-20A", minorsReview, RiskSeverity.High);
        }

        // 12. PROD_MULTI_COUNTRY_REVIEW (§25)
        // product.userGeography in [multiple, global, not_tracked, unknown] AND product.multiCountryReview in [initial_only, no, unknown] -> MEDIUM
        if (userGeography is "multiple" or "global" or "not_tracked" or "unknown"
            && multiCountryReview is "initial_only" or "no" or "unknown")
        {
            AddFinding(list, allRisks, "PROD_MULTI_COUNTRY_REVIEW", "PROD-21A", multiCountryReview, RiskSeverity.Medium);
        }

        // 13. PROD_REGULATORY_REVIEW (§27.2)
        // product.regulatedFunctions contains any value except none -> HIGH
        bool hasRegulated = regulatedFunctions.Any(rf => rf != "none");
        if (hasRegulated)
        {
            AddFinding(list, allRisks, "PROD_REGULATORY_REVIEW", "PROD-22", string.Join(",", regulatedFunctions), RiskSeverity.High);
        }

        return list;
    }

    private static bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        if (f.TryGetValue(key, out var val) && val is bool b) return b;
        return false;
    }

    private static void AddFinding(
        List<RiskFinding> list,
        IReadOnlyList<RiskDefinition> allRisks,
        string riskCode,
        string questionId,
        string answerId,
        RiskSeverity severity)
    {
        var def = allRisks.FirstOrDefault(r => r.Code == riskCode);
        if (def == null) return;

        list.Add(new RiskFinding
        {
            Code = def.Code,
            SectionId = def.SectionId,
            Severity = severity,
            Priority = def.Priority,
            RootCauseGroup = def.RootCauseGroup,
            ServiceCode = def.ServiceCode,
            Title = def.Title,
            Finding = def.Finding,
            WhyItMatters = def.WhyItMatters,
            Recommendation = def.Recommendation,
            Recommendations = def.Recommendations,
            AffectedDimensions = def.AffectedDimensions,
            Basis = new List<RiskFindingBasis>
            {
                new() { QuestionId = questionId, AnswerId = answerId }
            }
        });
    }
}
