using System.Collections.Generic;
using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.DataAi;

public class DataAiFactNormalizer : IFactNormalizer
{
    public string ModuleId => "data";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts)
    {
        var f = facts.Facts;

        // ─── DATA-01: Declaration of personal data processing ────────────────
        if (answers.TryGetValue("DATA-01", out var d01Raw) && d01Raw != null)
        {
            var str = d01Raw.ToString();
            switch (str)
            {
                case "yes":
                    f["data.userInfoDeclared"] = true;
                    f["data.personalDataProcessed"] = true;
                    break;
                case "no":
                    f["data.userInfoDeclared"] = false;
                    f["data.personalDataProcessed"] = false;
                    break;
                case "unknown":
                    f["data.userInfoDeclared"] = "unknown";
                    AddUnknown(f, "DATA-01");
                    break;
            }
        }

        // ─── DATA-02: Factual personal data categories (Precedence over DATA-01) ─
        if (answers.TryGetValue("DATA-02", out var d02Raw) && d02Raw != null)
        {
            var types = ExtractList(d02Raw);
            if (types.Count > 0)
            {
                var dataTypes = new List<string>();
                var sensitiveSignals = new List<string>();
                bool hasPersonalData = false;

                foreach (var t in types)
                {
                    if (t == "unknown")
                    {
                        dataTypes.Add("unknown");
                        AddUnknown(f, "DATA-02");
                        continue;
                    }
                    if (t == "none")
                    {
                        continue;
                    }

                    dataTypes.Add(t);

                    switch (t)
                    {
                        case "contact":
                        case "account":
                        case "media":
                        case "messages":
                        case "behavior":
                        case "device":
                        case "work_edu":
                            hasPersonalData = true;
                            break;
                        case "location":
                            hasPersonalData = true;
                            sensitiveSignals.Add("location");
                            break;
                        case "payments":
                            hasPersonalData = true;
                            sensitiveSignals.Add("financial");
                            break;
                        case "health":
                            hasPersonalData = true;
                            sensitiveSignals.Add("health");
                            break;
                        case "identity":
                            hasPersonalData = true;
                            sensitiveSignals.Add("identity");
                            break;
                        case "biometric":
                            hasPersonalData = true;
                            sensitiveSignals.Add("biometric");
                            break;
                        case "other":
                            // Canonical rule: do NOT automatically set personalDataProcessed=true for 'other'
                            break;
                    }
                }

                f["data.types"] = dataTypes;
                if (sensitiveSignals.Count > 0)
                {
                    f["data.sensitiveSignals"] = sensitiveSignals;
                }

                if (types.Contains("none") && !hasPersonalData)
                {
                    f["data.personalDataProcessed"] = false;
                }
                else if (hasPersonalData)
                {
                    // Factual DATA-02 category presence strictly overrides DATA-01 declaration
                    f["data.personalDataProcessed"] = true;
                }
            }
        }

        // ─── DATA-03: Sensitive data ─────────────────────────────────────────
        if (answers.TryGetValue("DATA-03", out var d03Raw) && d03Raw != null)
        {
            var str = d03Raw.ToString();
            switch (str)
            {
                case "no":
                    f["data.sensitiveData"] = false;
                    break;
                case "sometimes":
                    f["data.sensitiveData"] = true;
                    f["data.sensitiveDataMateriality"] = "sometimes";
                    break;
                case "core":
                    f["data.sensitiveData"] = true;
                    f["data.sensitiveDataMateriality"] = "core";
                    break;
                case "unknown":
                    f["data.sensitiveData"] = "unknown";
                    AddUnknown(f, "DATA-03");
                    break;
            }
        }

        // ─── DATA-04: Data sources ───────────────────────────────────────────
        if (answers.TryGetValue("DATA-04", out var d04Raw) && d04Raw != null)
        {
            var sources = ExtractList(d04Raw);
            if (sources.Count > 0)
            {
                f["data.sources"] = sources;
                if (sources.Contains("unknown"))
                {
                    AddUnknown(f, "DATA-04");
                }
            }
        }

        // ─── DATA-05: Data Map status ────────────────────────────────────────
        if (answers.TryGetValue("DATA-05", out var d05Raw) && d05Raw != null)
        {
            var str = d05Raw.ToString();
            f["data.mapStatus"] = str;
            if (str == "unknown") AddUnknown(f, "DATA-05");
        }

        // ─── DATA-06: Privacy notice presence ────────────────────────────────
        if (answers.TryGetValue("DATA-06", out var d06Raw) && d06Raw != null)
        {
            var str = d06Raw.ToString();
            var val = str switch
            {
                "yes" => "current_or_exists",
                "old" => "old",
                "template" => "template",
                "preparing" => "preparing",
                "none" => "none",
                _ => "unknown"
            };
            f["data.privacyNotice"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-06");
        }

        // ─── DATA-07: Privacy notice accuracy/match ──────────────────────────
        if (answers.TryGetValue("DATA-07", out var d07Raw) && d07Raw != null)
        {
            var str = d07Raw.ToString();
            f["data.privacyNoticeMatch"] = str;
            if (str == "unknown") AddUnknown(f, "DATA-07");
        }

        // ─── DATA-08: Data processing purposes & Secondary use ───────────────
        if (answers.TryGetValue("DATA-08", out var d08Raw) && d08Raw != null)
        {
            var purposes = ExtractList(d08Raw);
            if (purposes.Count > 0)
            {
                f["data.purposes"] = purposes;
                if (purposes.Contains("unknown"))
                {
                    AddUnknown(f, "DATA-08");
                }
                foreach (var p in purposes)
                {
                    if (p is "analytics" or "marketing" or "ads" or "recommendations" or "ai_training" or "partners" or "other")
                    {
                        f["data.secondaryUse"] = true;
                    }
                    if (p == "ai_training")
                    {
                        f["ai.trainingUse"] = true;
                    }
                }
            }
        }

        // ─── DATA-09: Secondary use disclosure ───────────────────────────────
        if (answers.TryGetValue("DATA-09", out var d09Raw) && d09Raw != null)
        {
            var str = d09Raw.ToString();
            var val = str switch
            {
                "clear" => "clear",
                "document_only" => "document_only",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["data.secondaryUseDisclosure"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-09");
        }

        // ─── DATA-10: External services used ─────────────────────────────────
        if (answers.TryGetValue("DATA-10", out var d10Raw) && d10Raw != null)
        {
            var str = d10Raw.ToString();
            switch (str)
            {
                case "no":
                    f["data.externalServicesUsed"] = false;
                    break;
                case "yes":
                    f["data.externalServicesUsed"] = true;
                    break;
                case "unknown":
                    f["data.externalServicesUsed"] = "unknown";
                    AddUnknown(f, "DATA-10");
                    break;
            }
        }

        // ─── DATA-10A: External service map / Sub-processors ─────────────────
        if (answers.TryGetValue("DATA-10A", out var d10aRaw) && d10aRaw != null)
        {
            var str = d10aRaw.ToString();
            var val = str switch
            {
                "yes" => "clear",
                "main" => "main",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["data.externalServiceMap"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-10A");
        }

        // ─── DATA-11: Vendor terms / DPA review ──────────────────────────────
        if (answers.TryGetValue("DATA-11", out var d11Raw) && d11Raw != null)
        {
            var str = d11Raw.ToString();
            var val = str switch
            {
                "main" => "main",
                "some" => "some",
                "known_not_reviewed" => "known_not_reviewed",
                "no" => "none",
                _ => "unknown"
            };
            f["data.vendorTermsReview"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-11");
        }

        // ─── DATA-12: User geography ─────────────────────────────────────────
        if (answers.TryGetValue("DATA-12", out var d12Raw) && d12Raw != null)
        {
            var str = d12Raw.ToString();
            var val = str switch
            {
                "one" => "one_country",
                "multiple" => "multiple",
                "global" => "global",
                "not_tracked" => "not_tracked",
                _ => "unknown"
            };
            f["data.userGeography"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-12");
        }

        // ─── DATA-13: Storage countries known ────────────────────────────────
        if (answers.TryGetValue("DATA-13", out var d13Raw) && d13Raw != null)
        {
            var str = d13Raw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "main" => "main",
                "foreign_unreviewed" => "foreign_unreviewed",
                "no" => "no",
                _ => "unknown"
            };
            f["data.storageCountriesKnown"] = val;
            if (str == "foreign_unreviewed")
            {
                f["data.dataStoredAbroad"] = true;
            }
            if (str == "unknown") AddUnknown(f, "DATA-13");
        }

        // ─── DATA-14: Cross-border & localization review ─────────────────────
        if (answers.TryGetValue("DATA-14", out var d14Raw) && d14Raw != null)
        {
            var str = d14Raw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["data.crossBorderReview"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-14");
        }

        // ─── DATA-15: Data retention rules ───────────────────────────────────
        if (answers.TryGetValue("DATA-15", out var d15Raw) && d15Raw != null)
        {
            var str = d15Raw.ToString();
            var val = str switch
            {
                "defined" => "defined",
                "general" => "general",
                "keep_useful" => "keep_useful",
                "none" => "none",
                _ => "unknown"
            };
            f["data.retentionRules"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-15");
        }

        // ─── DATA-16: Deletion capability / Right to be forgotten ────────────
        if (answers.TryGetValue("DATA-16", out var d16Raw) && d16Raw != null)
        {
            var str = d16Raw.ToString();
            var val = str switch
            {
                "process" => "process",
                "manual" => "manual",
                "possible_no_process" => "possible_no_process",
                "not_all_systems" => "not_all_systems",
                "no" => "none",
                _ => "unknown"
            };
            f["data.deletionCapability"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-16");
        }

        // ─── DATA-17: User data request handling process ─────────────────────
        if (answers.TryGetValue("DATA-17", out var d17Raw) && d17Raw != null)
        {
            var str = d17Raw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "rare_but_known" => "rare_but_known",
                "manual_each" => "manual_each",
                "none" => "none",
                _ => "unknown"
            };
            f["data.userRequestProcess"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-17");
        }

        // ─── DATA-18: Team access to personal data ───────────────────────────
        if (answers.TryGetValue("DATA-18", out var d18Raw) && d18Raw != null)
        {
            var str = d18Raw.ToString();
            var val = str switch
            {
                "need_to_know" => "need_to_know",
                "mostly" => "mostly",
                "broad" => "broad",
                "uncontrolled" => "uncontrolled",
                _ => "unknown"
            };
            f["data.teamAccess"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-18");
        }

        // ─── DATA-19: Offboarding access revocation ──────────────────────────
        if (answers.TryGetValue("DATA-19", out var d19Raw) && d19Raw != null)
        {
            var str = d19Raw.ToString();
            var val = str switch
            {
                "systematic" => "systematic",
                "usually" => "usually",
                "case" => "case",
                "no" => "no",
                _ => "unknown"
            };
            f["data.offboardingAccess"] = val;
            if (str == "unknown") AddUnknown(f, "DATA-19");
        }

        // =====================================================================
        // AI FACTS (AI-01 .. AI-08)
        // =====================================================================

        // ─── AI-01: AI/ML usage mode ─────────────────────────────────────────
        if (answers.TryGetValue("AI-01", out var ai01Raw) && ai01Raw != null)
        {
            var str = ai01Raw.ToString();
            switch (str)
            {
                case "no":
                    f["ai.used"] = false;
                    f["ai.external"] = false;
                    f["ai.ownModel"] = false;
                    break;
                case "external":
                    f["ai.used"] = true;
                    f["ai.external"] = true;
                    f["ai.ownModel"] = false;
                    break;
                case "own":
                    f["ai.used"] = true;
                    f["ai.external"] = false;
                    f["ai.ownModel"] = true;
                    break;
                case "both":
                    f["ai.used"] = true;
                    f["ai.external"] = true;
                    f["ai.ownModel"] = true;
                    break;
                case "unknown":
                    f["ai.used"] = "unknown";
                    AddUnknown(f, "AI-01");
                    break;
            }
        }

        // ─── AI-02: User data sent to external AI ────────────────────────────
        if (answers.TryGetValue("AI-02", out var ai02Raw) && ai02Raw != null)
        {
            var str = ai02Raw.ToString();
            var val = str switch
            {
                "none" => "none",
                "deidentified" => "deidentified",
                "ordinary" => "ordinary",
                "content" => "content",
                "sensitive" => "sensitive",
                _ => "unknown"
            };
            f["ai.userDataSent"] = val;
            if (str == "sensitive")
            {
                f["ai.sensitiveDataSent"] = true;
            }
            if (str == "unknown") AddUnknown(f, "AI-02");
        }

        // ─── AI-03: User disclosure of external AI ───────────────────────────
        if (answers.TryGetValue("AI-03", out var ai03Raw) && ai03Raw != null)
        {
            var str = ai03Raw.ToString();
            var val = str switch
            {
                "clear" => "clear",
                "document" => "document",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["ai.userDisclosure"] = val;
            if (str == "unknown") AddUnknown(f, "AI-03");
        }

        // ─── AI-04: External AI provider terms review ────────────────────────
        if (answers.TryGetValue("AI-04", out var ai04Raw) && ai04Raw != null)
        {
            var str = ai04Raw.ToString();
            var val = str switch
            {
                "full" => "full",
                "main" => "main",
                "not_specific" => "not_specific",
                "no" => "none",
                _ => "unknown"
            };
            f["ai.providerTermsReview"] = val;
            if (str == "unknown") AddUnknown(f, "AI-04");
        }

        // ─── AI-05: Sensitive data transfer to AI ────────────────────────────
        if (answers.TryGetValue("AI-05", out var ai05Raw) && ai05Raw != null)
        {
            var str = ai05Raw.ToString();
            switch (str)
            {
                case "no":
                    f["ai.sensitiveDataSent"] = false;
                    break;
                case "deidentified":
                    f["ai.sensitiveDataSent"] = "deidentified";
                    break;
                case "sometimes":
                    f["ai.sensitiveDataSent"] = true;
                    f["ai.sensitiveDataMateriality"] = "sometimes";
                    break;
                case "core":
                    f["ai.sensitiveDataSent"] = true;
                    f["ai.sensitiveDataMateriality"] = "core";
                    break;
                case "unknown":
                    f["ai.sensitiveDataSent"] = "unknown";
                    AddUnknown(f, "AI-05");
                    break;
            }
        }

        // ─── AI-06: Own model training use ───────────────────────────────────
        if (answers.TryGetValue("AI-06", out var ai06Raw) && ai06Raw != null)
        {
            var str = ai06Raw.ToString();
            switch (str)
            {
                case "no":
                    f["ai.trainingUse"] = false;
                    break;
                case "deidentified":
                    f["ai.trainingUse"] = "deidentified";
                    break;
                case "user_data":
                    f["ai.trainingUse"] = true;
                    break;
                case "possible_undefined":
                    f["ai.trainingUse"] = "possible_undefined";
                    break;
                case "unknown":
                    f["ai.trainingUse"] = "unknown";
                    AddUnknown(f, "AI-06");
                    break;
            }
        }

        // ─── AI-06A: Training disclosure & Opt-Out ───────────────────────────
        if (answers.TryGetValue("AI-06A", out var ai06aRaw) && ai06aRaw != null)
        {
            var str = ai06aRaw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["ai.trainingDisclosure"] = val;
            if (str == "unknown") AddUnknown(f, "AI-06A");
        }

        // ─── AI-07: AI automated material decisions ──────────────────────────
        if (answers.TryGetValue("AI-07", out var ai07Raw) && ai07Raw != null)
        {
            var str = ai07Raw.ToString();
            var val = str switch
            {
                "no" => "none",
                "assist" => "assist",
                "ai_human_check" => "human_check",
                "automatic" => "automatic",
                _ => "unknown"
            };
            f["ai.materialDecisionUse"] = val;
            if (str == "unknown") AddUnknown(f, "AI-07");
        }

        // ─── AI-07A: Decision transparency & explainability ──────────────────
        if (answers.TryGetValue("AI-07A", out var ai07aRaw) && ai07aRaw != null)
        {
            var str = ai07aRaw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "partial" => "partial",
                "no" => "none",
                _ => "unknown"
            };
            f["ai.decisionTransparencyReview"] = val;
            if (str == "unknown") AddUnknown(f, "AI-07A");
        }

        // ─── AI-08: Human review / Human-in-the-loop ─────────────────────────
        if (answers.TryGetValue("AI-08", out var ai08Raw) && ai08Raw != null)
        {
            var str = ai08Raw.ToString();
            var val = str switch
            {
                "yes" => "yes",
                "sometimes" => "sometimes",
                "no" => "none",
                _ => "unknown"
            };
            f["ai.humanReview"] = val;
            if (str == "unknown") AddUnknown(f, "AI-08");
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
