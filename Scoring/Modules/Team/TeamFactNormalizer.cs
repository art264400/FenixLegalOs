using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Team;

public class TeamFactNormalizer : IFactNormalizer
{
    public string ModuleId => "team";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // TEAM-01: Multiple select (worker types & non-founder team existence)
        if (answers.TryGetValue("TEAM-01", out var team01Raw) && team01Raw != null)
        {
            var workerTypes = new List<string>();
            bool hasNone = false;

            if (team01Raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in je.EnumerateArray())
                {
                    var val = el.GetString();
                    if (val == "none") hasNone = true;
                    else if (!string.IsNullOrEmpty(val)) workerTypes.Add(val);
                }
            }
            else if (team01Raw is IEnumerable<string> strList)
            {
                foreach (var val in strList)
                {
                    if (val == "none") hasNone = true;
                    else if (!string.IsNullOrEmpty(val)) workerTypes.Add(val);
                }
            }
            else
            {
                var val = team01Raw.ToString();
                if (val == "none") hasNone = true;
                else if (!string.IsNullOrEmpty(val)) workerTypes.Add(val);
            }

            f["team.workerTypes"] = workerTypes;
            f["team.hasNonFounderTeam"] = !hasNone && workerTypes.Count > 0;
        }

        // TEAM-02: Size bucket
        if (answers.TryGetValue("TEAM-02", out var team02Raw) && team02Raw != null)
        {
            var team02 = team02Raw.ToString() ?? "";
            if (team02 is "1_2" or "3_5" or "6_10" or "11_30" or "30_plus")
            {
                f["team.sizeBucket"] = team02;
            }
        }

        // TEAM-03: Written agreement coverage
        if (answers.TryGetValue("TEAM-03", out var team03Raw) && team03Raw != null)
        {
            var team03 = team03Raw.ToString() ?? "";
            if (team03 is "all" or "key_only" or "half" or "many_missing" or "almost_none" or "unknown")
            {
                f["team.writtenAgreementCoverage"] = team03;
            }
        }

        // TEAM-04: Key person dependency
        if (answers.TryGetValue("TEAM-04", out var team04Raw) && team04Raw != null)
        {
            var team04 = team04Raw.ToString() ?? "";
            switch (team04)
            {
                case "none":
                    f["team.keyPersonExists"] = false;
                    f["team.keyPersonDependency"] = "none";
                    break;
                case "mitigated":
                    f["team.keyPersonExists"] = true;
                    f["team.keyPersonDependency"] = "mitigated";
                    break;
                case "some":
                    f["team.keyPersonExists"] = true;
                    f["team.keyPersonDependency"] = "some";
                    break;
                case "critical":
                    f["team.keyPersonExists"] = true;
                    f["team.keyPersonDependency"] = "critical";
                    break;
                case "unknown":
                    f["team.keyPersonDependency"] = "unknown";
                    // DO NOT set team.keyPersonExists
                    break;
            }
        }

        // TEAM-05: Work format mismatch (misclassification risk)
        if (answers.TryGetValue("TEAM-05", out var team05Raw) && team05Raw != null)
        {
            var team05 = team05Raw.ToString() ?? "";
            var mismatch = team05 switch
            {
                "no" => "none",
                "few" => "few",
                "several" => "several",
                "many" => "many",
                "unknown" => "unknown",
                _ => null
            };
            if (mismatch != null)
            {
                f["team.workFormatMismatch"] = mismatch;
            }
        }

        // TEAM-06: Terms clarity
        if (answers.TryGetValue("TEAM-06", out var team06Raw) && team06Raw != null)
        {
            var team06 = team06Raw.ToString() ?? "";
            if (team06 is "clear" or "mostly" or "partly_informal" or "generic" or "unknown")
            {
                f["team.termsClarity"] = team06;
            }
        }

        // TEAM-07: Confidentiality coverage (NDA)
        if (answers.TryGetValue("TEAM-07", out var team07Raw) && team07Raw != null)
        {
            var team07 = team07Raw.ToString() ?? "";
            if (team07 is "all" or "key" or "some" or "none" or "unknown")
            {
                f["team.confidentialityCoverage"] = team07;
            }
        }

        // TEAM-08: Creates important work
        if (answers.TryGetValue("TEAM-08", out var team08Raw) && team08Raw != null)
        {
            var team08 = team08Raw.ToString() ?? "";
            switch (team08)
            {
                case "no":
                    f["team.createsImportantWork"] = false;
                    break;
                case "yes":
                    f["team.createsImportantWork"] = true;
                    break;
                case "unknown":
                    f["team.createsImportantWork"] = "unknown";
                    break;
            }
        }

        // TEAM-08A: Work rights clarity (IP assignment)
        if (answers.TryGetValue("TEAM-08A", out var team08aRaw) && team08aRaw != null)
        {
            var team08a = team08aRaw.ToString() ?? "";
            var rights = team08a switch
            {
                "all" => "all",
                "most" => "most",
                "some" => "some",
                "no" => "none",
                "unknown" => "unknown",
                _ => null
            };
            if (rights != null)
            {
                f["team.workRightsClarity"] = rights;
            }
        }

        // TEAM-09: Access control
        if (answers.TryGetValue("TEAM-09", out var team09Raw) && team09Raw != null)
        {
            var team09 = team09Raw.ToString() ?? "";
            if (team09 is "controlled" or "mostly" or "ad_hoc" or "unknown_access" or "unknown")
            {
                f["team.accessControl"] = team09;
            }
        }

        // TEAM-10: Personal account dependency
        if (answers.TryGetValue("TEAM-10", out var team10Raw) && team10Raw != null)
        {
            var team10 = team10Raw.ToString() ?? "";
            var acc = team10 switch
            {
                "company" => "none",
                "minor" => "minor",
                "important" => "important",
                "critical" => "critical",
                "unknown" => "unknown",
                _ => null
            };
            if (acc != null)
            {
                f["team.personalAccountDependency"] = acc;
            }
        }

        // TEAM-11: Offboarding process
        if (answers.TryGetValue("TEAM-11", out var team11Raw) && team11Raw != null)
        {
            var team11 = team11Raw.ToString() ?? "";
            if (team11 is "systematic" or "informal" or "case_by_case" or "none" or "unknown")
            {
                f["team.offboardingProcess"] = team11;
            }
        }

        // TEAM-12: Former people
        if (answers.TryGetValue("TEAM-12", out var team12Raw) && team12Raw != null)
        {
            var team12 = team12Raw.ToString() ?? "";
            switch (team12)
            {
                case "none":
                    f["team.formerPeopleExist"] = false;
                    break;
                case "closed":
                    f["team.formerPeopleExist"] = true;
                    f["team.formerAccessStatus"] = "closed";
                    break;
                case "not_sure":
                    f["team.formerPeopleExist"] = true;
                    f["team.formerAccessStatus"] = "not_sure";
                    break;
                case "retained":
                    f["team.formerPeopleExist"] = true;
                    f["team.formerAccessStatus"] = "retained";
                    break;
                case "conflict":
                    f["team.formerPeopleExist"] = true;
                    f["team.formerPersonConflict"] = true;
                    // DO NOT set formerAccessStatus
                    break;
                case "unknown":
                    f["team.formerAccessStatus"] = "unknown";
                    // DO NOT set formerPeopleExist
                    break;
            }
        }

        // TEAM-13: Key person continuity
        if (answers.TryGetValue("TEAM-13", out var team13Raw) && team13Raw != null)
        {
            var team13 = team13Raw.ToString() ?? "";
            var continuity = team13 switch
            {
                "continuity" => "good",
                "time_needed" => "time_needed",
                "knowledge_only" => "weak",
                "stop" => "critical",
                "unknown" => "unknown",
                _ => null
            };
            if (continuity != null)
            {
                f["team.keyPersonContinuity"] = continuity;
            }
        }

        // TEAM-14: Foreign workers
        if (answers.TryGetValue("TEAM-14", out var team14Raw) && team14Raw != null)
        {
            var team14 = team14Raw.ToString() ?? "";
            switch (team14)
            {
                case "no":
                    f["team.foreignWorkers"] = false;
                    break;
                case "yes":
                    f["team.foreignWorkers"] = true;
                    break;
                case "unknown":
                    f["team.foreignWorkers"] = "unknown";
                    break;
            }
        }

        // TEAM-14A: Foreign arrangement review
        if (answers.TryGetValue("TEAM-14A", out var team14aRaw) && team14aRaw != null)
        {
            var team14a = team14aRaw.ToString() ?? "";
            if (team14a is "yes" or "mostly" or "ordinary_unchecked" or "no_contract" or "unknown")
            {
                f["team.foreignArrangementReview"] = team14a;
            }
        }

        // TEAM-15: Equity promise / ESOP
        if (answers.TryGetValue("TEAM-15", out var team15Raw) && team15Raw != null)
        {
            var team15 = team15Raw.ToString() ?? "";
            if (team15 is "none" or "formal" or "written_pending" or "oral" or "undefined" or "unknown")
            {
                f["team.equityPromise"] = team15;
            }
        }
    }
}
