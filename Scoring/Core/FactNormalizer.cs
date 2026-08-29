using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;
using FenixLegalOs.Scoring.Modules.Corporate;
using FenixLegalOs.Scoring.Modules.Founders;
using FenixLegalOs.Scoring.Modules.IP;

namespace FenixLegalOs.Scoring.Core;

/// <summary>
/// Composite fact normalizer orchestrating all module-specific normalizers.
/// </summary>
public class FactNormalizer
{
    private static readonly List<IFactNormalizer> ModuleNormalizers = new()
    {
        new FoundersFactNormalizer(),
        new CorporateFactNormalizer(),
        new IpFactNormalizer()
    };

    public static SharedFactStore NormalizeFacts(Dictionary<string, object> answers)
    {
        var store = new SharedFactStore();

        // 1. Run all registered module fact normalizers
        foreach (var normalizer in ModuleNormalizers)
        {
            normalizer.Normalize(answers, store);
        }

        // =========================================================================
        // TEMPORARY MIGRATION DEBT (§24 Baseline signals)
        // To be extracted into Team, Revenue, DataAi, Contracts, Investment modules
        // in subsequent module extraction passes.
        // =========================================================================
        var f = store.Facts;

        var teamC01 = GetAnswerStr(answers, "TEAM-C01");
        f["team.hasNonFounderTeam"] = teamC01 != "founders_only" && teamC01 != "solo_only" && !string.IsNullOrEmpty(teamC01) && teamC01 != "none";

        var rev01 = GetAnswerStr(answers, "REV-01");
        var revC01 = GetAnswerStr(answers, "REV-C01");
        bool hasRev = (rev01 != "none" && !string.IsNullOrEmpty(rev01)) || (revC01 != "none" && !string.IsNullOrEmpty(revC01));
        f["company.hasRevenue"] = hasRev;
        f["revenue.exists"] = hasRev;

        var data01 = GetAnswerStr(answers, "DATA-01");
        var data02 = GetAnswerStr(answers, "DATA-02");
        f["data.personalDataProcessed"] = data01 == "yes" || (!string.IsNullOrEmpty(data02) && data02 != "none");

        var ai01 = GetAnswerStr(answers, "AI-01");
        f["ai.used"] = ai01 is "external" or "own" or "both";

        var ai02 = GetAnswerStr(answers, "AI-02");
        f["ai.sensitiveDataSent"] = ai02 == "sensitive";

        var contract01 = GetAnswerStr(answers, "CONTRACT-01");
        f["contracts.b2bRelevant"] = contract01 != "none" && !string.IsNullOrEmpty(contract01);

        var invest01 = GetAnswerStr(answers, "INVEST-01");
        f["investment.timing"] = invest01 switch
        {
            "m3" or "m3_6" => "near_term",
            "m6_12" => "mid_term",
            "looking" or "discussing" or "terms" => "active",
            _ => "none"
        };

        var invest02 = GetAnswerStr(answers, "INVEST-02");
        var invC01 = GetAnswerStr(answers, "INV-C01");
        f["investment.priorInvestment"] = (invest02 != "none" && !string.IsNullOrEmpty(invest02)) || invC01 == "yes";

        return store;
    }

    private static string GetAnswerStr(IReadOnlyDictionary<string, object> answers, string key)
    {
        if (!answers.TryGetValue(key, out var val) || val == null) return "";
        return val.ToString() ?? "";
    }
}
