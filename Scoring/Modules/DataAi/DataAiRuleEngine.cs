using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.DataAi;

public class DataAiRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "data";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        // ─── Data Normalized Facts ───────────────────────────────────────────
        bool personalDataProcessed = GetBoolFact(f, "data.personalDataProcessed");
        var dataTypes = f.GetValueOrDefault("data.types") as List<string> ?? new();
        var mapStatus = (string?)f.GetValueOrDefault("data.mapStatus");
        var privacyNotice = (string?)f.GetValueOrDefault("data.privacyNotice");
        var privacyNoticeMatch = (string?)f.GetValueOrDefault("data.privacyNoticeMatch");
        bool secondaryUse = GetBoolFact(f, "data.secondaryUse");
        var secondaryUseDisclosure = (string?)f.GetValueOrDefault("data.secondaryUseDisclosure");
        bool externalServicesUsed = GetBoolFact(f, "data.externalServicesUsed");
        var externalServiceMap = (string?)f.GetValueOrDefault("data.externalServiceMap");
        var vendorTermsReview = (string?)f.GetValueOrDefault("data.vendorTermsReview");
        var userGeography = (string?)f.GetValueOrDefault("data.userGeography");
        var storageCountriesKnown = (string?)f.GetValueOrDefault("data.storageCountriesKnown");
        bool dataStoredAbroad = GetBoolFact(f, "data.dataStoredAbroad");
        var crossBorderReview = (string?)f.GetValueOrDefault("data.crossBorderReview");
        var retentionRules = (string?)f.GetValueOrDefault("data.retentionRules");
        var deletionCapability = (string?)f.GetValueOrDefault("data.deletionCapability");
        var teamAccess = (string?)f.GetValueOrDefault("data.teamAccess");

        // ─── AI Normalized Facts ─────────────────────────────────────────────
        bool aiUsed = GetBoolFact(f, "ai.used");
        bool aiExternal = GetBoolFact(f, "ai.external");
        var userDataSent = (string?)f.GetValueOrDefault("ai.userDataSent");
        var userDisclosure = (string?)f.GetValueOrDefault("ai.userDisclosure");
        var providerTermsReview = (string?)f.GetValueOrDefault("ai.providerTermsReview");
        var sensitiveDataSent = f.GetValueOrDefault("ai.sensitiveDataSent");
        var trainingUse = f.GetValueOrDefault("ai.trainingUse");
        var trainingDisclosure = (string?)f.GetValueOrDefault("ai.trainingDisclosure");
        var materialDecisionUse = (string?)f.GetValueOrDefault("ai.materialDecisionUse");
        var decisionTransparencyReview = (string?)f.GetValueOrDefault("ai.decisionTransparencyReview");
        var humanReview = (string?)f.GetValueOrDefault("ai.humanReview");
        bool aiRegulatedProductContext = GetBoolFact(f, "ai.regulatedProductContext");
        var regulatedFunctions = f.GetValueOrDefault("product.regulatedFunctions") as List<string> ?? new();

        // ─── 1. DATA_MAP_INCOMPLETE (§27.2) ──────────────────────────────────
        // data.mapStatus in [developers_only, main_only, none, unknown] AND (data.externalServicesUsed == true OR count(data.types) > 2) -> HIGH
        if (mapStatus is "developers_only" or "main_only" or "none" or "unknown" &&
            (externalServicesUsed || dataTypes.Count > 2))
        {
            AddFinding(list, allRisks, "DATA_MAP_INCOMPLETE", "DATA-05", mapStatus ?? "unknown", RiskSeverity.High);
        }

        // ─── 2. DATA_PRIVACY_NOTICE_MISSING (§25, §24) ───────────────────────
        // data.personalDataProcessed == true AND data.privacyNotice == none -> HIGH
        if (personalDataProcessed && privacyNotice == "none")
        {
            AddFinding(list, allRisks, "DATA_PRIVACY_NOTICE_MISSING", "DATA-06", privacyNotice, RiskSeverity.High);
        }

        // ─── 3. DATA_PRIVACY_NOTICE_OUTDATED (§27.2) ──────────────────────────
        // data.privacyNoticeMatch in [changed, template_unchecked] OR (data.privacyNotice in [old, template] AND ai.used == true) -> HIGH
        if (privacyNoticeMatch is "changed" or "template_unchecked" ||
            (privacyNotice is "old" or "template" && aiUsed))
        {
            var qId = privacyNoticeMatch is "changed" or "template_unchecked" ? "DATA-07" : "DATA-06";
            var ansId = privacyNoticeMatch ?? privacyNotice ?? "unknown";
            AddFinding(list, allRisks, "DATA_PRIVACY_NOTICE_OUTDATED", qId, ansId, RiskSeverity.High);
        }

        // ─── 4. DATA_SECONDARY_USE_UNCLEAR (§25, §24) ─────────────────────────
        // data.secondaryUse == true AND data.secondaryUseDisclosure in [partial, none, unknown] -> HIGH
        if (secondaryUse && secondaryUseDisclosure is "partial" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "DATA_SECONDARY_USE_UNCLEAR", "DATA-09", secondaryUseDisclosure ?? "unknown", RiskSeverity.High);
        }

        // ─── 5. DATA_THIRD_PARTY_UNKNOWN (§27.2) ─────────────────────────────
        // data.externalServicesUsed == true AND (data.externalServiceMap in [partial, none, unknown] OR data.vendorTermsReview in [known_not_reviewed, none, unknown]) -> HIGH
        if (externalServicesUsed &&
            (externalServiceMap is "partial" or "none" or "unknown" ||
             vendorTermsReview is "known_not_reviewed" or "none" or "unknown"))
        {
            var qId = externalServiceMap is "partial" or "none" or "unknown" ? "DATA-10A" : "DATA-11";
            var ansId = externalServiceMap ?? vendorTermsReview ?? "unknown";
            AddFinding(list, allRisks, "DATA_THIRD_PARTY_UNKNOWN", qId, ansId, RiskSeverity.High);
        }

        // ─── 6. DATA_CROSS_BORDER_REVIEW (§25, §24) ──────────────────────────
        // (data.dataStoredAbroad == true OR storageCountriesKnown in [foreign_unreviewed, no, unknown] OR userGeography in [multiple, global, not_tracked, unknown]) AND crossBorderReview in [partial, none, unknown] -> HIGH
        if ((dataStoredAbroad ||
             storageCountriesKnown is "foreign_unreviewed" or "no" or "unknown" ||
             userGeography is "multiple" or "global" or "not_tracked" or "unknown") &&
            crossBorderReview is "partial" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "DATA_CROSS_BORDER_REVIEW", "DATA-14", crossBorderReview ?? "unknown", RiskSeverity.High);
        }

        // ─── 7. DATA_RETENTION_UNDEFINED (§25, §24) ───────────────────────────
        // data.personalDataProcessed == true AND data.retentionRules in [unlimited, keep_useful, none, unknown] -> MEDIUM
        if (personalDataProcessed && retentionRules is "unlimited" or "keep_useful" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "DATA_RETENTION_UNDEFINED", "DATA-15", retentionRules ?? "unknown", RiskSeverity.Medium);
        }

        // ─── 8. DATA_DELETION_GAP (§25, §24, §27) ────────────────────────────
        // data.personalDataProcessed == true AND data.deletionCapability in [possible_no_process, not_all_systems, none, unknown] -> HIGH
        if (personalDataProcessed && deletionCapability is "possible_no_process" or "not_all_systems" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "DATA_DELETION_GAP", "DATA-16", deletionCapability ?? "unknown", RiskSeverity.High);
        }

        // ─── 9. DATA_ACCESS_TOO_BROAD (§25, §24) ──────────────────────────────
        // data.personalDataProcessed == true AND data.teamAccess in [broad, uncontrolled, unknown] -> HIGH
        if (personalDataProcessed && teamAccess is "broad" or "uncontrolled" or "unknown")
        {
            AddFinding(list, allRisks, "DATA_ACCESS_TOO_BROAD", "DATA-18", teamAccess ?? "unknown", RiskSeverity.High);
        }

        // ─── 10. AI_USER_DATA_TRANSFER (§27.2) ────────────────────────────────
        // ai.external == true AND ai.userDataSent in [ordinary, content, sensitive, unknown] -> HIGH
        if (aiExternal && userDataSent is "ordinary" or "content" or "sensitive" or "unknown")
        {
            AddFinding(list, allRisks, "AI_USER_DATA_TRANSFER", "AI-02", userDataSent ?? "unknown", RiskSeverity.High);
        }

        // ─── 11. AI_SENSITIVE_DATA_TRANSFER (§27.2) ───────────────────────────
        // ai.external == true AND ai.sensitiveDataSent in [true, unknown] AND ai.userDisclosure in [partial, none, unknown] AND ai.providerTermsReview in [not_specific, none, unknown] -> CRITICAL
        bool sensitiveSent = sensitiveDataSent is true || (sensitiveDataSent is string sensStr && sensStr == "unknown");
        if (aiExternal && sensitiveSent &&
            userDisclosure is "partial" or "none" or "unknown" &&
            providerTermsReview is "not_specific" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "AI_SENSITIVE_DATA_TRANSFER", "AI-05", "sensitive_transfer", RiskSeverity.Critical);
        }

        // ─── 12. AI_PROVIDER_TERMS_UNKNOWN (§25, §27.2) ───────────────────────
        // ai.external == true AND ai.userDataSent in [ordinary, content, sensitive, unknown] AND ai.providerTermsReview in [not_specific, none, unknown] -> HIGH
        if (aiExternal && userDataSent is "ordinary" or "content" or "sensitive" or "unknown" &&
            providerTermsReview is "not_specific" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "AI_PROVIDER_TERMS_UNKNOWN", "AI-04", providerTermsReview ?? "unknown", RiskSeverity.High);
        }

        // ─── 13. AI_TRAINING_NOT_DISCLOSED (§27.2) ────────────────────────────
        // ai.trainingUse in [true, possible_undefined, deidentified, unknown] AND ai.trainingDisclosure in [partial, none, unknown] -> HIGH
        bool isTrainingActive = trainingUse is true || (trainingUse is string trStr && trStr is "possible_undefined" or "deidentified" or "unknown");
        if (isTrainingActive && trainingDisclosure is "partial" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "AI_TRAINING_NOT_DISCLOSED", "AI-06A", trainingDisclosure ?? "unknown", RiskSeverity.High);
        }

        // ─── 14. AI_AUTOMATED_DECISION (§27.2) ────────────────────────────────
        // ai.materialDecisionUse == automatic AND ai.decisionTransparencyReview in [partial, none, unknown] -> HIGH
        if (materialDecisionUse == "automatic" && decisionTransparencyReview is "partial" or "none" or "unknown")
        {
            AddFinding(list, allRisks, "AI_AUTOMATED_DECISION", "AI-07A", decisionTransparencyReview ?? "unknown", RiskSeverity.High);
        }

        // ─── 15. AI_HUMAN_REVIEW_GAP (§25, §24) ───────────────────────────────
        // ai.materialDecisionUse in [assist, human_check, automatic, unknown] AND ai.humanReview in [none, sometimes, unknown] AND (ai.regulatedProductContext == true OR product.regulatedFunctions non-empty) -> HIGH
        bool hasRegulatedContext = aiRegulatedProductContext || (regulatedFunctions.Count > 0 && !regulatedFunctions.Contains("none"));
        if (materialDecisionUse is "assist" or "human_check" or "automatic" or "unknown" &&
            humanReview is "none" or "sometimes" or "unknown" &&
            hasRegulatedContext)
        {
            AddFinding(list, allRisks, "AI_HUMAN_REVIEW_GAP", "AI-08", humanReview ?? "unknown", RiskSeverity.High);
        }

        return list;
    }

    private static bool GetBoolFact(Dictionary<string, object?> facts, string key)
    {
        if (!facts.TryGetValue(key, out var val) || val == null) return false;
        if (val is bool b) return b;
        if (val is string s && bool.TryParse(s, out var parsed)) return parsed;
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
