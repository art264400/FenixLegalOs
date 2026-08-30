using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Product;

public class ProductFactNormalizer : IFactNormalizer
{
    public string ModuleId => "product";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // PROD-01: User stage & live users
        if (answers.TryGetValue("PROD-01", out var prod01Raw) && prod01Raw != null)
        {
            var p01 = prod01Raw.ToString();
            switch (p01)
            {
                case "prelaunch":
                    f["product.userStage"] = "prelaunch";
                    f["product.liveUsers"] = false;
                    break;
                case "first":
                    f["product.userStage"] = "first_users";
                    f["product.liveUsers"] = true;
                    f["product.userScale"] = "small";
                    break;
                case "regular":
                    f["product.userStage"] = "regular";
                    f["product.liveUsers"] = true;
                    f["product.userScale"] = "medium";
                    break;
                case "large":
                    f["product.userStage"] = "large";
                    f["product.liveUsers"] = true;
                    f["product.userScale"] = "large";
                    break;
            }
        }

        // PROD-02: User categories (multiple)
        if (answers.TryGetValue("PROD-02", out var prod02Raw) && prod02Raw != null)
        {
            var userTypes = ExtractList(prod02Raw);
            if (userTypes.Count > 0)
            {
                f["product.userTypes"] = userTypes;
                if (userTypes.Contains("minors"))
                {
                    f["product.minorsPossible"] = true;
                }
            }
        }

        // PROD-03: Access modes (multiple)
        if (answers.TryGetValue("PROD-03", out var prod03Raw) && prod03Raw != null)
        {
            var accessModes = ExtractList(prod03Raw);
            if (accessModes.Count > 0)
            {
                f["product.accessModes"] = accessModes;
            }
        }

        // PROD-04: User rules status
        if (answers.TryGetValue("PROD-04", out var prod04Raw) && prod04Raw != null)
        {
            var p04 = prod04Raw.ToString();
            if (p04 is "current" or "old" or "template" or "preparing" or "none")
            {
                f["product.userRulesStatus"] = p04;
            }
            else if (p04 == "unknown")
            {
                f["product.userRulesStatus"] = "unknown";
                AddUnknown(f, "PROD-04");
            }
        }

        // PROD-05: Rules match actual product
        if (answers.TryGetValue("PROD-05", out var prod05Raw) && prod05Raw != null)
        {
            var p05 = prod05Raw.ToString();
            if (p05 is "yes" or "mostly" or "changed" or "template_unchecked")
            {
                f["product.rulesMatch"] = p05;
            }
            else if (p05 == "unknown")
            {
                f["product.rulesMatch"] = "unknown";
                AddUnknown(f, "PROD-05");
            }
        }

        // PROD-06: Offer clarity
        if (answers.TryGetValue("PROD-06", out var prod06Raw) && prod06Raw != null)
        {
            var p06 = prod06Raw.ToString();
            if (p06 is "clear" or "mostly" or "some_unclear" or "mismatch")
            {
                f["product.offerClarity"] = p06;
            }
            else if (p06 == "unknown")
            {
                f["product.offerClarity"] = "unknown";
                AddUnknown(f, "PROD-06");
            }
        }

        // PROD-07: Provider role
        if (answers.TryGetValue("PROD-07", out var prod07Raw) && prod07Raw != null)
        {
            var p07 = prod07Raw.ToString();
            if (p07 is "company" or "joint" or "marketplace" or "varies")
            {
                f["product.providerRole"] = p07;
            }
            else if (p07 == "unknown")
            {
                f["product.providerRole"] = "unknown";
                AddUnknown(f, "PROD-07");
            }
        }

        // PROD-07A: Role clarity
        if (answers.TryGetValue("PROD-07A", out var prod07ARaw) && prod07ARaw != null)
        {
            var p07A = prod07ARaw.ToString();
            if (p07A is "clear" or "mostly" or "partial" or "unclear")
            {
                f["product.roleClarity"] = p07A;
            }
            else if (p07A == "unknown")
            {
                f["product.roleClarity"] = "unknown";
                AddUnknown(f, "PROD-07A");
            }
        }

        // PROD-08: Terms acceptance
        if (answers.TryGetValue("PROD-08", out var prod08Raw) && prod08Raw != null)
        {
            var p08 = prod08Raw.ToString();
            if (p08 is "explicit" or "link_only" or "published_only" or "no_rules")
            {
                f["product.termsAcceptance"] = p08;
            }
            else if (p08 == "unknown")
            {
                f["product.termsAcceptance"] = "unknown";
                AddUnknown(f, "PROD-08");
            }
        }

        // PROD-09: Acceptance evidence
        if (answers.TryGetValue("PROD-09", out var prod09Raw) && prod09Raw != null)
        {
            var p09 = prod09Raw.ToString();
            if (p09 is "versioned" or "fact_only" or "none")
            {
                f["product.acceptanceEvidence"] = p09;
            }
            else if (p09 == "unknown")
            {
                f["product.acceptanceEvidence"] = "unknown";
                AddUnknown(f, "PROD-09");
            }
        }

        // PROD-10: Payment model
        if (answers.TryGetValue("PROD-10", out var prod10Raw) && prod10Raw != null)
        {
            var p10 = prod10Raw.ToString();
            switch (p10)
            {
                case "free":
                    f["product.paymentModel"] = "free";
                    f["product.paid"] = false;
                    f["product.subscription"] = false;
                    break;
                case "one_off":
                    f["product.paymentModel"] = "one_off";
                    f["product.paid"] = true;
                    f["product.subscription"] = false;
                    break;
                case "subscription":
                    f["product.paymentModel"] = "subscription";
                    f["product.paid"] = true;
                    f["product.subscription"] = true;
                    break;
                case "mixed":
                    f["product.paymentModel"] = "mixed";
                    f["product.paid"] = true;
                    f["product.subscription"] = true;
                    break;
                case "commission":
                    f["product.paymentModel"] = "commission";
                    f["product.paid"] = true;
                    break;
                case "other":
                    f["product.paymentModel"] = "other";
                    break;
            }
        }

        // PROD-11: Price transparency
        if (answers.TryGetValue("PROD-11", out var prod11Raw) && prod11Raw != null)
        {
            var p11 = prod11Raw.ToString();
            if (p11 is "clear" or "mostly" or "late_fees")
            {
                f["product.priceTransparency"] = p11;
            }
            else if (p11 == "unknown")
            {
                f["product.priceTransparency"] = "unknown";
                AddUnknown(f, "PROD-11");
            }
        }

        // PROD-12: Refund rules
        if (answers.TryGetValue("PROD-12", out var prod12Raw) && prod12Raw != null)
        {
            var p12 = prod12Raw.ToString();
            if (p12 is "published" or "case_policy" or "unclear" or "no_refunds")
            {
                f["product.refundRules"] = p12;
            }
            else if (p12 == "unknown")
            {
                f["product.refundRules"] = "unknown";
                AddUnknown(f, "PROD-12");
            }
        }

        // PROD-13: Auto-renew
        if (answers.TryGetValue("PROD-13", out var prod13Raw) && prod13Raw != null)
        {
            var p13 = prod13Raw.ToString();
            if (p13 == "yes") f["product.autoRenew"] = true;
            else if (p13 == "no") f["product.autoRenew"] = false;
            else if (p13 == "depends") f["product.autoRenew"] = "depends";
            else if (p13 == "unknown")
            {
                f["product.autoRenew"] = "unknown";
                AddUnknown(f, "PROD-13");
            }
        }

        // PROD-13A: Auto-renew disclosure
        if (answers.TryGetValue("PROD-13A", out var prod13ARaw) && prod13ARaw != null)
        {
            var p13A = prod13ARaw.ToString();
            if (p13A == "clear") f["product.autoRenewDisclosure"] = "clear";
            else if (p13A == "terms_only") f["product.autoRenewDisclosure"] = "terms_only";
            else if (p13A == "no") f["product.autoRenewDisclosure"] = "none";
            else if (p13A == "unknown")
            {
                f["product.autoRenewDisclosure"] = "unknown";
                AddUnknown(f, "PROD-13A");
            }
        }

        // PROD-14: Subscription cancellation
        if (answers.TryGetValue("PROD-14", out var prod14Raw) && prod14Raw != null)
        {
            var p14 = prod14Raw.ToString();
            if (p14 is "self_service" or "support" or "complex" or "undefined")
            {
                f["product.subscriptionCancellation"] = p14;
            }
            else if (p14 == "unknown")
            {
                f["product.subscriptionCancellation"] = "unknown";
                AddUnknown(f, "PROD-14");
            }
        }

        // PROD-15: Trial disclosure & autocharge
        if (answers.TryGetValue("PROD-15", out var prod15Raw) && prod15Raw != null)
        {
            var p15 = prod15Raw.ToString();
            switch (p15)
            {
                case "no_trial":
                    f["product.trialExists"] = false;
                    break;
                case "no_autocharge":
                    f["product.trialExists"] = true;
                    f["product.trialAutoCharge"] = false;
                    break;
                case "clear":
                    f["product.trialExists"] = true;
                    f["product.trialAutoCharge"] = true;
                    f["product.trialDisclosure"] = "clear";
                    break;
                case "terms_only":
                    f["product.trialExists"] = true;
                    f["product.trialAutoCharge"] = true;
                    f["product.trialDisclosure"] = "terms_only";
                    break;
                case "not_explained":
                    f["product.trialExists"] = true;
                    f["product.trialAutoCharge"] = true;
                    f["product.trialDisclosure"] = "none";
                    break;
                case "unknown":
                    f["product.trialExists"] = "unknown";
                    AddUnknown(f, "PROD-15");
                    break;
            }
        }

        // PROD-16: Suspension rules
        if (answers.TryGetValue("PROD-16", out var prod16Raw) && prod16Raw != null)
        {
            var p16 = prod16Raw.ToString();
            if (p16 is "clear" or "partial" or "case_by_case" or "none")
            {
                f["product.suspensionRules"] = p16;
            }
            else if (p16 == "unknown")
            {
                f["product.suspensionRules"] = "unknown";
                AddUnknown(f, "PROD-16");
            }
        }

        // PROD-17: Suspension payment rules
        if (answers.TryGetValue("PROD-17", out var prod17Raw) && prod17Raw != null)
        {
            var p17 = prod17Raw.ToString();
            if (p17 is "clear" or "cause_based" or "individual" or "undefined")
            {
                f["product.suspensionPaymentRules"] = p17;
            }
            else if (p17 == "unknown")
            {
                f["product.suspensionPaymentRules"] = "unknown";
                AddUnknown(f, "PROD-17");
            }
        }

        // PROD-18: User-generated content
        if (answers.TryGetValue("PROD-18", out var prod18Raw) && prod18Raw != null)
        {
            var p18 = prod18Raw.ToString();
            if (p18 == "no") f["product.ugc"] = false;
            else if (p18 == "yes") f["product.ugc"] = true;
            else if (p18 == "unknown")
            {
                f["product.ugc"] = "unknown";
                AddUnknown(f, "PROD-18");
            }
        }

        // PROD-18A: UGC restrictions
        if (answers.TryGetValue("PROD-18A", out var prod18ARaw) && prod18ARaw != null)
        {
            var p18A = prod18ARaw.ToString();
            if (p18A == "yes") f["product.ugcRestrictions"] = "clear";
            else if (p18A == "general") f["product.ugcRestrictions"] = "general";
            else if (p18A == "no") f["product.ugcRestrictions"] = "none";
            else if (p18A == "unknown")
            {
                f["product.ugcRestrictions"] = "unknown";
                AddUnknown(f, "PROD-18A");
            }
        }

        // PROD-18B: UGC use rules
        if (answers.TryGetValue("PROD-18B", out var prod18BRaw) && prod18BRaw != null)
        {
            var p18B = prod18BRaw.ToString();
            if (p18B == "yes") f["product.ugcUseRules"] = "clear";
            else if (p18B == "partial") f["product.ugcUseRules"] = "partial";
            else if (p18B == "no") f["product.ugcUseRules"] = "none";
            else if (p18B == "unknown")
            {
                f["product.ugcUseRules"] = "unknown";
                AddUnknown(f, "PROD-18B");
            }
        }

        // PROD-19: UGC complaint procedure
        if (answers.TryGetValue("PROD-19", out var prod19Raw) && prod19Raw != null)
        {
            var p19 = prod19Raw.ToString();
            if (p19 == "yes") f["product.ugcComplaint"] = "yes";
            else if (p19 == "partial") f["product.ugcComplaint"] = "partial";
            else if (p19 == "no") f["product.ugcComplaint"] = "no";
            else if (p19 == "not_needed") f["product.ugcComplaint"] = "not_applicable";
            else if (p19 == "unknown")
            {
                f["product.ugcComplaint"] = "unknown";
                AddUnknown(f, "PROD-19");
            }
        }

        // PROD-20: Minors allowed
        if (answers.TryGetValue("PROD-20", out var prod20Raw) && prod20Raw != null)
        {
            var p20 = prod20Raw.ToString();
            if (p20 == "no") f["product.minorsAllowed"] = false;
            else if (p20 == "yes") f["product.minorsAllowed"] = true;
            else if (p20 == "possible") f["product.minorsAllowed"] = "possible";
            else if (p20 == "unknown")
            {
                f["product.minorsAllowed"] = "unknown";
                AddUnknown(f, "PROD-20");
            }
        }

        // PROD-20A: Minors review
        if (answers.TryGetValue("PROD-20A", out var prod20ARaw) && prod20ARaw != null)
        {
            var p20A = prod20ARaw.ToString();
            if (p20A == "yes") f["product.minorsReview"] = "yes";
            else if (p20A == "partial") f["product.minorsReview"] = "partial";
            else if (p20A == "no") f["product.minorsReview"] = "no";
            else if (p20A == "unknown")
            {
                f["product.minorsReview"] = "unknown";
                AddUnknown(f, "PROD-20A");
            }
        }

        // PROD-21: User geography
        if (answers.TryGetValue("PROD-21", out var prod21Raw) && prod21Raw != null)
        {
            var p21 = prod21Raw.ToString();
            if (p21 == "one") f["product.userGeography"] = "one_country";
            else if (p21 is "multiple" or "global" or "not_tracked") f["product.userGeography"] = p21;
            else if (p21 == "unknown")
            {
                f["product.userGeography"] = "unknown";
                AddUnknown(f, "PROD-21");
            }
        }

        // PROD-21A: Multi-country legal review
        if (answers.TryGetValue("PROD-21A", out var prod21ARaw) && prod21ARaw != null)
        {
            var p21A = prod21ARaw.ToString();
            if (p21A is "main_markets" or "initial_only" or "no")
            {
                f["product.multiCountryReview"] = p21A;
            }
            else if (p21A == "unknown")
            {
                f["product.multiCountryReview"] = "unknown";
                AddUnknown(f, "PROD-21A");
            }
        }

        // PROD-22: Regulated functions (multiple)
        if (answers.TryGetValue("PROD-22", out var prod22Raw) && prod22Raw != null)
        {
            var regFuncs = ExtractList(prod22Raw);
            if (regFuncs.Count > 0)
            {
                f["product.regulatedFunctions"] = regFuncs;
            }
        }
    }

    private static List<string> ExtractList(object raw)
    {
        var list = new List<string>();
        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in je.EnumerateArray())
            {
                var str = el.GetString();
                if (!string.IsNullOrEmpty(str)) list.Add(str);
            }
        }
        else if (raw is IEnumerable<string> strEnum)
        {
            foreach (var s in strEnum)
            {
                if (!string.IsNullOrEmpty(s)) list.Add(s);
            }
        }
        else if (raw != null)
        {
            var str = raw.ToString();
            if (!string.IsNullOrEmpty(str)) list.Add(str);
        }
        return list;
    }

    private static void AddUnknown(Dictionary<string, object?> f, string questionId)
    {
        if (!f.TryGetValue("diagnostic.unknownQuestionIds", out var obj) || obj is not List<string> list)
        {
            list = new List<string>();
            f["diagnostic.unknownQuestionIds"] = list;
        }
        if (!list.Contains(questionId))
        {
            list.Add(questionId);
        }
    }
}
