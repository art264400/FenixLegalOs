using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Founders;

public class FoundersRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "founders";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        var activeCountObj = f.GetValueOrDefault("founders.activeCount");
        int? activeCount = activeCountObj is int ac ? ac : (int?)null;
        var founderAgree = (string?)f.GetValueOrDefault("founders.founderAgreementStatus");
        var equityClarity = (string?)f.GetValueOrDefault("founders.equityClarity");
        bool activeDispute = GetBoolFact(f, "founders.activeDispute");
        var disputeLevel = (string?)f.GetValueOrDefault("founders.disputeLevel");
        bool isEqual5050 = GetBoolFact(f, "founders.isEqual5050");
        bool nearEqualControl = GetBoolFact(f, "founders.nearEqualControl") || isEqual5050;
        var keyDecisionMode = (string?)f.GetValueOrDefault("founders.keyDecisionMode");
        var deadlockMech = (string?)f.GetValueOrDefault("founders.deadlockMechanism");
        var vestingStatus = (string?)f.GetValueOrDefault("founders.vestingStatus");
        var leaverRules = (string?)f.GetValueOrDefault("founders.leaverRules");
        bool inactiveExists = GetBoolFact(f, "founders.inactiveExists");
        var departedStatus = (string?)f.GetValueOrDefault("founders.departedFounderStatus");
        var roleClarity = (string?)f.GetValueOrDefault("founders.roleClarity");
        var commitmentStatus = (string?)f.GetValueOrDefault("founders.commitmentStatus");
        var extActivity = (string?)f.GetValueOrDefault("founders.externalActivity");
        var govClarity = (string?)f.GetValueOrDefault("founders.governanceClarity");
        var exitRules = (string?)f.GetValueOrDefault("founders.exitRules");
        var personalContribs = (string?)f.GetValueOrDefault("founders.personalContributions");
        var stratAlign = (string?)f.GetValueOrDefault("founders.strategicAlignment");

        // FND_DEPARTED_UNRESOLVED (CRITICAL) — §27.2: departedFounderStatus in [unresolved, dispute] OR exitRules == "unresolved_departure"
        if (departedStatus is "unresolved" or "dispute" || exitRules == "unresolved_departure" || (inactiveExists && departedStatus is "stopped" or "formal_only"))
        {
            AddFinding(list, allRisks, "FND_DEPARTED_UNRESOLVED", "FND-C03", departedStatus ?? exitRules ?? "unresolved", "CRITICAL");
        }

        if (activeCount.HasValue && activeCount.Value >= 2)
        {
            // FND_ACTIVE_DISPUTE (CRITICAL) — §24: disputeLevel in [active, formal]
            if (disputeLevel is "active" or "formal")
            {
                AddFinding(list, allRisks, "FND_ACTIVE_DISPUTE", "FND-01", disputeLevel, "CRITICAL");
            }

            // FND_EQUITY_DISPUTE (CRITICAL)
            if (equityClarity == "dispute" || departedStatus == "dispute")
            {
                AddFinding(list, allRisks, "FND_EQUITY_DISPUTE", "FND-04", "dispute", "CRITICAL");
            }
            // FND_EQUITY_AMBIGUITY (HIGH)
            else if (equityClarity == "ambiguous")
            {
                AddFinding(list, allRisks, "FND_EQUITY_AMBIGUITY", "FND-04", "ambiguous", "HIGH");
            }
            // FND_EQUITY_NOT_FORMALIZED (MEDIUM)
            else if (equityClarity is "verbal" or "preliminary" || founderAgree is "oral" or "none" or "in_progress" or "draft" or "informal")
            {
                AddFinding(list, allRisks, "FND_EQUITY_NOT_FORMALIZED", "FND-04", equityClarity ?? founderAgree ?? "unformalized", "MEDIUM");
            }

            // FND_DEADLOCK (CRITICAL) — Strict §27.2: activeCount == 2 AND nearEqualControl AND keyDecisionMode in [material_unanimity, broad_unanimity] AND score(FND-07) <= 0.15
            if (activeCount.Value == 2 && nearEqualControl && keyDecisionMode is "material_unanimity" or "broad_unanimity" && deadlockMech is "none" or "only_agree" or "unknown")
            {
                AddFinding(list, allRisks, "FND_DEADLOCK", "FND-07", deadlockMech ?? "none", "CRITICAL");
            }
            // FND_NO_DEADLOCK_PROTECTION (HIGH)
            else if (deadlockMech is "none" or "only_agree" or "unknown")
            {
                AddFinding(list, allRisks, "FND_NO_DEADLOCK_PROTECTION", "FND-07", deadlockMech ?? "only_agree", "HIGH");
            }

            // FND_DEAD_EQUITY (CRITICAL)
            if ((vestingStatus is "none" or "informal" or "verbal_rule") && (departedStatus is "unresolved" or "stopped" || commitmentStatus == "stopped" || inactiveExists))
            {
                AddFinding(list, allRisks, "FND_DEAD_EQUITY", "FND-03", commitmentStatus ?? departedStatus ?? "stopped", "CRITICAL");
            }

            // FND_NO_VESTING (HIGH)
            if (vestingStatus is "none" or "informal" or "not_discussed" or "verbal_rule")
            {
                AddFinding(list, allRisks, "FND_NO_VESTING", "FND-05", vestingStatus ?? "none", "HIGH");
            }

            // FND_INCOMPLETE_LEAVER_RULES (MEDIUM)
            if (leaverRules is "oral" or "none" or "partial")
            {
                AddFinding(list, allRisks, "FND_INCOMPLETE_LEAVER_RULES", "FND-05A", leaverRules, "MEDIUM");
            }

            // FND_ROLE_AMBIGUITY (MEDIUM / HIGH)
            if (roleClarity is "overlap" or "disputed")
            {
                string sev = roleClarity == "disputed" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_ROLE_AMBIGUITY", "FND-02", roleClarity, sev);
            }

            // FND_COMMITMENT_MISMATCH (HIGH)
            if (commitmentStatus is "below_expected")
            {
                AddFinding(list, allRisks, "FND_COMMITMENT_MISMATCH", "FND-03", commitmentStatus, "HIGH");
            }

            // FND_CONFLICT_OF_INTEREST (HIGH / CRITICAL)
            if (extActivity is "potential_competitor" or "employer_same_field" or "active_competition")
            {
                string sev = extActivity == "active_competition" ? "CRITICAL" : "HIGH";
                AddFinding(list, allRisks, "FND_CONFLICT_OF_INTEREST", "FND-10", extActivity, sev);
            }

            // FND_GOVERNANCE_AMBIGUITY (MEDIUM / HIGH)
            if (govClarity is "none" or "all_together" or "partial" || keyDecisionMode is "broad_unanimity" or "undefined")
            {
                string sev = govClarity == "none" || keyDecisionMode == "undefined" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_GOVERNANCE_AMBIGUITY", "FND-06", govClarity ?? keyDecisionMode ?? "none", sev);
            }

            // FND_EXIT_RULES_MISSING (MEDIUM)
            if (exitRules is "none" or "oral")
            {
                AddFinding(list, allRisks, "FND_EXIT_RULES_MISSING", "FND-08", exitRules, "MEDIUM");
            }

            // FND_CONTRIBUTION_AMBIGUITY (MEDIUM / HIGH)
            if (personalContribs is "material_unclear" or "dispute")
            {
                string sev = personalContribs == "dispute" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_CONTRIBUTION_AMBIGUITY", "FND-09", personalContribs, sev);
            }

            // FND_STRATEGIC_MISALIGNMENT (MEDIUM / HIGH)
            if (stratAlign is "material_difference" or "conflict")
            {
                string sev = stratAlign == "conflict" ? "HIGH" : "MEDIUM";
                AddFinding(list, allRisks, "FND_STRATEGIC_MISALIGNMENT", "FND-11", stratAlign, sev);
            }

            // FND_DOCUMENTATION_GAP (MEDIUM)
            if (founderAgree is "draft" or "none" or "oral" or "informal" || disputeLevel == "material")
            {
                AddFinding(list, allRisks, "FND_DOCUMENTATION_GAP", "FND-C04", founderAgree ?? "informal", "MEDIUM");
            }
        }

        return list;
    }

    private static bool GetBoolFact(Dictionary<string, object?> f, string key)
    {
        return f.TryGetValue(key, out var val) && val is bool b && b;
    }

    private static void AddFinding(List<RiskFinding> list, IReadOnlyList<RiskDefinition> allRisks, string code, string qId, string ansId, string severity)
    {
        var def = allRisks.FirstOrDefault(r => r.Code == code);
        if (def == null) return;

        var existing = list.FirstOrDefault(f => f.Code == code);
        if (existing != null)
        {
            existing.Severity = severity;
            if (!existing.Basis.Any(b => b.QuestionId == qId))
            {
                existing.Basis.Add(new RiskFindingBasis { QuestionId = qId, AnswerId = ansId });
            }
            return;
        }

        list.Add(new RiskFinding
        {
            Code = def.Code,
            RootCauseGroup = def.RootCauseGroup,
            Severity = severity,
            Priority = def.Priority,
            SectionId = def.SectionId,
            Title = def.Title,
            Finding = def.Finding,
            WhyItMatters = def.WhyItMatters,
            Recommendation = def.Recommendation.Length > 0 ? def.Recommendation : (def.Recommendations.FirstOrDefault() ?? ""),
            Recommendations = def.Recommendations.Count > 0 ? def.Recommendations : new List<string> { def.Recommendation },
            Basis = new List<RiskFindingBasis> { new() { QuestionId = qId, AnswerId = ansId } },
            LawyerRequired = def.LawyerRequired,
            Resolution = def.Resolution,
            ServiceCode = def.ServiceCode,
            Cta = def.Cta
        });
    }
}
