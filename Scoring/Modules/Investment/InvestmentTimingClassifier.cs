using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Modules.Investment;

public enum FundraisingTimingBucket
{
    None,        // No fundraising / long horizon
    MidTerm,     // 3–12 months
    ActiveRound  // Active search / specific investor / terms received
}

public static class InvestmentTimingClassifier
{
    public static FundraisingTimingBucket GetTimingBucket(SharedFactStore facts)
    {
        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        return ClassifyTiming(timing);
    }

    public static FundraisingTimingBucket ClassifyTiming(string? timing)
    {
        return timing switch
        {
            "terms_received" or "specific_investor" or "active_search" => FundraisingTimingBucket.ActiveRound,
            "3_6m" or "6_12m" => FundraisingTimingBucket.MidTerm,
            _ => FundraisingTimingBucket.None // "within_12m", "none", null -> fail-closed (no matrix escalation)
        };
    }

    /// <summary>
    /// Exact §27.2 predicate for close / active fundraising round:
    /// investment.timing in ["3_6m", "active_search", "specific_investor", "terms_received"]
    /// </summary>
    public static bool IsCloseOrActiveRound(SharedFactStore facts)
    {
        var timing = (string?)facts.Facts.GetValueOrDefault("investment.timing");
        return timing is "3_6m" or "active_search" or "specific_investor" or "terms_received";
    }
}
