using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Team;

public class TeamRuleEngine : IModuleRuleEngine
{
    public string ModuleId => "team";

    public IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> allRisks)
    {
        var list = new List<RiskFinding>();
        var f = facts.Facts;

        bool hasNonFounderTeam = GetBoolFact(f, "team.hasNonFounderTeam");
        if (!hasNonFounderTeam)
        {
            return list;
        }

        var writtenAgreementCoverage = (string?)f.GetValueOrDefault("team.writtenAgreementCoverage");
        bool keyPersonExists = GetBoolFact(f, "team.keyPersonExists");
        var keyPersonDependency = (string?)f.GetValueOrDefault("team.keyPersonDependency");
        var keyPersonContinuity = (string?)f.GetValueOrDefault("team.keyPersonContinuity");
        var workFormatMismatch = (string?)f.GetValueOrDefault("team.workFormatMismatch");
        var termsClarity = (string?)f.GetValueOrDefault("team.termsClarity");
        var confidentialityCoverage = (string?)f.GetValueOrDefault("team.confidentialityCoverage");
        var createsImportantWork = f.GetValueOrDefault("team.createsImportantWork");
        var workRightsClarity = (string?)f.GetValueOrDefault("team.workRightsClarity");
        var accessControl = (string?)f.GetValueOrDefault("team.accessControl");
        var personalAccountDependency = (string?)f.GetValueOrDefault("team.personalAccountDependency");
        var offboardingProcess = (string?)f.GetValueOrDefault("team.offboardingProcess");
        var formerAccessStatus = (string?)f.GetValueOrDefault("team.formerAccessStatus");
        var foreignWorkers = f.GetValueOrDefault("team.foreignWorkers");
        var foreignArrangementReview = (string?)f.GetValueOrDefault("team.foreignArrangementReview");
        var equityPromise = (string?)f.GetValueOrDefault("team.equityPromise");

        // 1. TEAM_KEY_PERSON_UNDOCUMENTED (§27.2)
        // team.keyPersonExists == true AND team.writtenAgreementCoverage in [many_missing, almost_none, half] -> HIGH
        if (keyPersonExists && writtenAgreementCoverage is "many_missing" or "almost_none" or "half")
        {
            AddFinding(list, allRisks, "TEAM_KEY_PERSON_UNDOCUMENTED", "TEAM-03", writtenAgreementCoverage, RiskSeverity.High);
        }

        // 2. TEAM_NO_WRITTEN_AGREEMENTS (§25)
        // team.writtenAgreementCoverage in [many_missing, almost_none] -> HIGH
        if (writtenAgreementCoverage is "many_missing" or "almost_none")
        {
            AddFinding(list, allRisks, "TEAM_NO_WRITTEN_AGREEMENTS", "TEAM-03", writtenAgreementCoverage, RiskSeverity.High);
        }

        // 3. TEAM_WORK_FORMAT_MISMATCH (§25)
        // team.workFormatMismatch in [several, many] (only when contractors exist) -> HIGH
        var workerTypes = f.GetValueOrDefault("team.workerTypes") as List<string> ?? new();
        bool hasContractors = workerTypes.Contains("freelancers") || workerTypes.Contains("external_devs");
        if (hasContractors && workFormatMismatch is "several" or "many")
        {
            AddFinding(list, allRisks, "TEAM_WORK_FORMAT_MISMATCH", "TEAM-05", workFormatMismatch, RiskSeverity.High);
        }

        // 4. TEAM_UNCLEAR_TERMS (§25)
        // team.termsClarity in [partly_informal, generic] -> MEDIUM
        if (termsClarity is "partly_informal" or "generic")
        {
            AddFinding(list, allRisks, "TEAM_UNCLEAR_TERMS", "TEAM-06", termsClarity, RiskSeverity.Medium);
        }

        // 5. TEAM_CONFIDENTIALITY_GAP (§25)
        // team.confidentialityCoverage in [some, none] -> MEDIUM
        if (confidentialityCoverage is "some" or "none")
        {
            AddFinding(list, allRisks, "TEAM_CONFIDENTIALITY_GAP", "TEAM-07", confidentialityCoverage, RiskSeverity.Medium);
        }

        // 6. TEAM_RIGHTS_TO_WORK_GAP (§25)
        // team.createsImportantWork != false AND team.workRightsClarity in [none, some] -> HIGH
        bool createsWork = createsImportantWork is true or "unknown";
        if (createsWork && workRightsClarity is "none" or "some")
        {
            AddFinding(list, allRisks, "TEAM_RIGHTS_TO_WORK_GAP", "TEAM-08A", workRightsClarity, RiskSeverity.High);
        }

        // 7. TEAM_ACCESS_CONTROL_GAP (§25)
        // team.accessControl in [ad_hoc, unknown_access] -> HIGH
        if (accessControl is "ad_hoc" or "unknown_access")
        {
            AddFinding(list, allRisks, "TEAM_ACCESS_CONTROL_GAP", "TEAM-09", accessControl, RiskSeverity.High);
        }

        // 8. TEAM_PERSONAL_ACCOUNT_DEPENDENCY (§25)
        // team.personalAccountDependency in [important, critical] -> HIGH
        if (personalAccountDependency is "important" or "critical")
        {
            AddFinding(list, allRisks, "TEAM_PERSONAL_ACCOUNT_DEPENDENCY", "TEAM-10", personalAccountDependency, RiskSeverity.High);
        }

        // 9. TEAM_OFFBOARDING_GAP (§25)
        // team.offboardingProcess in [case_by_case, none] -> MEDIUM
        if (offboardingProcess is "case_by_case" or "none")
        {
            AddFinding(list, allRisks, "TEAM_OFFBOARDING_GAP", "TEAM-11", offboardingProcess, RiskSeverity.Medium);
        }

        // 10. TEAM_FORMER_ACCESS_RISK (§27.2)
        // team.formerAccessStatus == retained -> CRITICAL
        if (formerAccessStatus == "retained")
        {
            AddFinding(list, allRisks, "TEAM_FORMER_ACCESS_RISK", "TEAM-12", "retained", RiskSeverity.Critical);
        }

        // 11. TEAM_KEY_PERSON_DEPENDENCY (§27.2)
        // team.keyPersonDependency == critical OR team.keyPersonContinuity in [weak, critical] -> HIGH
        if (keyPersonDependency == "critical" || keyPersonContinuity is "weak" or "critical")
        {
            string basisAns = keyPersonDependency == "critical" ? keyPersonDependency : (keyPersonContinuity ?? "critical");
            string basisQ = keyPersonDependency == "critical" ? "TEAM-04" : "TEAM-13";
            AddFinding(list, allRisks, "TEAM_KEY_PERSON_DEPENDENCY", basisQ, basisAns, RiskSeverity.High);
        }

        // 12. TEAM_FOREIGN_TEAM_REVIEW (§25)
        // team.foreignWorkers == true AND team.foreignArrangementReview in [ordinary_unchecked, no_contract] -> MEDIUM
        bool hasForeign = foreignWorkers is true;
        if (hasForeign && foreignArrangementReview is "ordinary_unchecked" or "no_contract")
        {
            AddFinding(list, allRisks, "TEAM_FOREIGN_TEAM_REVIEW", "TEAM-14A", foreignArrangementReview, RiskSeverity.Medium);
        }

        // 13. TEAM_EQUITY_PROMISE (§25 / §27.2)
        // team.equityPromise in [oral, undefined] -> HIGH
        if (equityPromise is "oral" or "undefined")
        {
            AddFinding(list, allRisks, "TEAM_EQUITY_PROMISE", "TEAM-15", equityPromise, RiskSeverity.High);
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
