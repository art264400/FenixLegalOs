using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Scoring.Interfaces;

namespace FenixLegalOs.Scoring.Modules.Investment;

public class InvestmentFactNormalizer : IFactNormalizer
{
    public string ModuleId => "investment";

    public void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore store)
    {
        var f = store.Facts;

        // ─── INVEST-01 (Контекст: горизонт привлечения инвестиций) ───────────
        if (answers.TryGetValue("INVEST-01", out var a01) && a01 != null)
        {
            var str01 = a01.ToString();
            switch (str01)
            {
                case "none":
                    f["investment.timing"] = "none";
                    f["investment.activeFundraise"] = false;
                    break;
                case "possible_year":
                    f["investment.timing"] = "within_12m";
                    f["investment.activeFundraise"] = false;
                    break;
                case "6_12":
                    f["investment.timing"] = "6_12m";
                    f["investment.activeFundraise"] = false;
                    break;
                case "3_6":
                    f["investment.timing"] = "3_6m";
                    f["investment.activeFundraise"] = false;
                    break;
                case "searching":
                    f["investment.timing"] = "active_search";
                    f["investment.activeFundraise"] = true;
                    break;
                case "specific":
                    f["investment.timing"] = "specific_investor";
                    f["investment.activeFundraise"] = true;
                    break;
                case "terms":
                    f["investment.timing"] = "terms_received";
                    f["investment.activeFundraise"] = true;
                    f["investment.termSheetOrTerms"] = true;
                    break;
            }
        }

        // ─── INVEST-02 (Диагностика: наличие прошлых инвестиций) ─────────────
        if (answers.TryGetValue("INVEST-02", out var a02) && a02 != null)
        {
            var str02 = a02.ToString();
            switch (str02)
            {
                case "no":
                    f["investment.priorInvestment"] = false;
                    break;
                case "formal":
                    f["investment.priorInvestment"] = true;
                    f["investment.priorInvestmentStatus"] = "formal";
                    break;
                case "partial":
                    f["investment.priorInvestment"] = true;
                    f["investment.priorInvestmentStatus"] = "partial";
                    break;
                case "informal":
                    f["investment.priorInvestment"] = true;
                    f["investment.priorInvestmentStatus"] = "informal";
                    break;
                case "unknown":
                    f["investment.priorInvestment"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-02");
                    break;
            }
        }

        // ─── INVEST-02A (Диагностика: четкость прав прошлых инвесторов) ──────
        if (answers.TryGetValue("INVEST-02A", out var a02A) && a02A != null)
        {
            var str02A = a02A.ToString();
            switch (str02A)
            {
                case "yes":
                    f["investment.priorRightsClarity"] = "clear";
                    break;
                case "main":
                    f["investment.priorRightsClarity"] = "main";
                    break;
                case "unclear":
                    f["investment.priorRightsClarity"] = "unclear";
                    break;
                case "no":
                    f["investment.priorRightsClarity"] = "none";
                    break;
                case "unknown":
                    f["investment.priorRightsClarity"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-02A");
                    break;
            }
        }

        // ─── INVEST-03 (Диагностика: будущая структура долей) ────────────────
        if (answers.TryGetValue("INVEST-03", out var a03) && a03 != null)
        {
            var str03 = a03.ToString();
            switch (str03)
            {
                case "exact":
                    f["investment.futureOwnershipClarity"] = "exact";
                    break;
                case "mostly_promises":
                    f["investment.futureOwnershipClarity"] = "mostly_promises";
                    break;
                case "current_only":
                    f["investment.futureOwnershipClarity"] = "current_only";
                    break;
                case "none":
                    f["investment.futureOwnershipClarity"] = "none";
                    break;
                case "unknown":
                    f["investment.futureOwnershipClarity"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-03");
                    break;
            }
        }

        // ─── INVEST-04 (Диагностика: размытие долей основателей) ─────────────
        if (answers.TryGetValue("INVEST-04", out var a04) && a04 != null)
        {
            var str04 = a04.ToString();
            switch (str04)
            {
                case "yes":
                    f["investment.dilutionModel"] = "yes";
                    break;
                case "one_scenario":
                    f["investment.dilutionModel"] = "one_scenario";
                    break;
                case "rough":
                    f["investment.dilutionModel"] = "rough";
                    break;
                case "no":
                    f["investment.dilutionModel"] = "none";
                    break;
                case "unknown":
                    f["investment.dilutionModel"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-04");
                    break;
            }
        }

        // ─── INVEST-05 (Диагностика: размер раунда и использование средств) ──
        if (answers.TryGetValue("INVEST-05", out var a05) && a05 != null)
        {
            var str05 = a05.ToString();
            switch (str05)
            {
                case "clear":
                    f["investment.roundDefinition"] = "clear";
                    break;
                case "amount_rough":
                    f["investment.roundDefinition"] = "amount_rough";
                    break;
                case "use_clear_amount_pending":
                    f["investment.roundDefinition"] = "use_clear_amount_pending";
                    break;
                case "max_possible":
                    f["investment.roundDefinition"] = "max_possible";
                    break;
                case "none":
                    f["investment.roundDefinition"] = "none";
                    break;
                case "unknown":
                    f["investment.roundDefinition"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-05");
                    break;
            }
        }

        // ─── INVEST-06 (Диагностика: знание финансового запаса) ──────────────
        if (answers.TryGetValue("INVEST-06", out var a06) && a06 != null)
        {
            var str06 = a06.ToString();
            switch (str06)
            {
                case "regular":
                    f["investment.runwayKnown"] = "regular";
                    break;
                case "rough":
                    f["investment.runwayKnown"] = "rough";
                    break;
                case "old":
                    f["investment.runwayKnown"] = "old";
                    break;
                case "no":
                    f["investment.runwayKnown"] = "none";
                    break;
                case "unknown":
                    f["investment.runwayKnown"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-06");
                    break;
            }
        }

        // ─── INVEST-06A (Диагностика: фактический диапазон runway) ───────────
        if (answers.TryGetValue("INVEST-06A", out var a06A) && a06A != null)
        {
            var str06A = a06A.ToString();
            switch (str06A)
            {
                case "lt3":
                    f["investment.runwayMonthsBucket"] = "lt3";
                    break;
                case "3_6":
                    f["investment.runwayMonthsBucket"] = "3_6";
                    break;
                case "6_12":
                    f["investment.runwayMonthsBucket"] = "6_12";
                    break;
                case "gt12":
                    f["investment.runwayMonthsBucket"] = "gt12";
                    break;
                case "unknown":
                    f["investment.runwayMonthsBucket"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-06A");
                    break;
            }
        }

        // ─── INVEST-07 (Диагностика: финансовая модель) ──────────────────────
        if (answers.TryGetValue("INVEST-07", out var a07) && a07 != null)
        {
            var str07 = a07.ToString();
            switch (str07)
            {
                case "current":
                    f["investment.financialModel"] = "current";
                    break;
                case "simple":
                    f["investment.financialModel"] = "simple";
                    break;
                case "old":
                    f["investment.financialModel"] = "old";
                    break;
                case "fragments":
                    f["investment.financialModel"] = "fragments";
                    break;
                case "none":
                    f["investment.financialModel"] = "none";
                    break;
                case "unknown":
                    f["investment.financialModel"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-07");
                    break;
            }
        }

        // ─── INVEST-08 (Диагностика: подтверждаемость показателей) ───────────
        if (answers.TryGetValue("INVEST-08", out var a08) && a08 != null)
        {
            var str08 = a08.ToString();
            switch (str08)
            {
                case "yes":
                    f["investment.metricsEvidence"] = "yes";
                    break;
                case "most":
                    f["investment.metricsEvidence"] = "most";
                    break;
                case "approx":
                    f["investment.metricsEvidence"] = "approx";
                    break;
                case "hard":
                    f["investment.metricsEvidence"] = "hard";
                    break;
                case "unknown":
                    f["investment.metricsEvidence"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-08");
                    break;
            }
        }

        // ─── INVEST-09 (Диагностика: папка документов для DD) ────────────────
        if (answers.TryGetValue("INVEST-09", out var a09) && a09 != null)
        {
            var str09 = a09.ToString();
            switch (str09)
            {
                case "organized":
                    f["investment.documentFolder"] = "organized";
                    break;
                case "mostly":
                    f["investment.documentFolder"] = "mostly";
                    break;
                case "scattered":
                    f["investment.documentFolder"] = "scattered";
                    break;
                case "reconstruct":
                    f["investment.documentFolder"] = "reconstruct";
                    break;
                case "missing":
                    f["investment.documentFolder"] = "missing";
                    break;
                case "unknown":
                    f["investment.documentFolder"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-09");
                    break;
            }
        }

        // ─── INVEST-10 (Триггер / самооценка: известные проблемы) ────────────
        if (answers.TryGetValue("INVEST-10", out var a10) && a10 != null)
        {
            var str10 = a10.ToString();
            switch (str10)
            {
                case "none":
                    f["investment.selfReportedIssues"] = "none";
                    break;
                case "small":
                    f["investment.selfReportedIssues"] = "small";
                    break;
                case "material_plan":
                    f["investment.selfReportedIssues"] = "material_plan";
                    break;
                case "material_unresolved":
                    f["investment.selfReportedIssues"] = "material_unresolved";
                    break;
                case "unknown":
                    f["investment.selfReportedIssues"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-10");
                    break;
            }
        }

        // ─── INVEST-11 (Диагностика: презентация pitch deck) ─────────────────
        if (answers.TryGetValue("INVEST-11", out var a11) && a11 != null)
        {
            var str11 = a11.ToString();
            switch (str11)
            {
                case "current":
                    f["investment.pitchDeck"] = "current";
                    break;
                case "old":
                    f["investment.pitchDeck"] = "old";
                    break;
                case "preparing":
                    f["investment.pitchDeck"] = "preparing";
                    break;
                case "none":
                    f["investment.pitchDeck"] = "none";
                    break;
            }
        }

        // ─── INVEST-12 (Диагностика: понимание условий сделки) ───────────────
        if (answers.TryGetValue("INVEST-12", out var a12) && a12 != null)
        {
            var str12 = a12.ToString();
            switch (str12)
            {
                case "yes":
                    f["investment.dealTermsUnderstanding"] = "yes";
                    break;
                case "mostly":
                    f["investment.dealTermsUnderstanding"] = "mostly";
                    break;
                case "price_only":
                    f["investment.dealTermsUnderstanding"] = "price_only";
                    break;
                case "unclear":
                    f["investment.dealTermsUnderstanding"] = "unclear";
                    break;
                case "not_reviewed":
                    f["investment.dealTermsUnderstanding"] = "not_reviewed";
                    break;
            }
        }

        // ─── INVEST-13 (Диагностика: контроль и право вето инвестора) ────────
        if (answers.TryGetValue("INVEST-13", out var a13) && a13 != null)
        {
            var str13 = a13.ToString();
            switch (str13)
            {
                case "reserved_only":
                    f["investment.investorControl"] = "reserved_only";
                    break;
                case "extra_known":
                    f["investment.investorControl"] = "extra_known";
                    break;
                case "material":
                    f["investment.investorControl"] = "material";
                    break;
                case "broad_veto":
                    f["investment.investorControl"] = "broad_veto";
                    break;
                case "unknown":
                    f["investment.investorControl"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-13");
                    break;
            }
        }

        // ─── INVEST-14 (Диагностика: экономика выхода и ликвидационная прив.)
        if (answers.TryGetValue("INVEST-14", out var a14) && a14 != null)
        {
            var str14 = a14.ToString();
            switch (str14)
            {
                case "yes":
                    f["investment.exitEconomicsUnderstanding"] = "yes";
                    break;
                case "check_math":
                    f["investment.exitEconomicsUnderstanding"] = "check_math";
                    break;
                case "seen_unclear":
                    f["investment.exitEconomicsUnderstanding"] = "seen_unclear";
                    break;
                case "not_discussed":
                    f["investment.exitEconomicsUnderstanding"] = "not_applicable";
                    break;
                case "unknown":
                    f["investment.exitEconomicsUnderstanding"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-14");
                    break;
            }
        }

        // ─── INVEST-15 (Диагностика: правовая проверка условий сделки) ───────
        if (answers.TryGetValue("INVEST-15", out var a15) && a15 != null)
        {
            var str15 = a15.ToString();
            switch (str15)
            {
                case "specialist":
                    f["investment.dealReview"] = "specialist";
                    break;
                case "lawyer_unclear":
                    f["investment.dealReview"] = "lawyer_unclear";
                    break;
                case "self":
                    f["investment.dealReview"] = "self";
                    break;
                case "none":
                    f["investment.dealReview"] = "none";
                    break;
                case "unknown":
                    f["investment.dealReview"] = "unknown";
                    AddUnknownQuestion(f, "INVEST-15");
                    break;
            }
        }
    }

    private static void AddUnknownQuestion(Dictionary<string, object?> facts, string questionId)
    {
        if (!facts.TryGetValue("diagnostic.unknownQuestionIds", out var obj) || obj is not List<string> list)
        {
            list = new List<string>();
            facts["diagnostic.unknownQuestionIds"] = list;
        }
        if (!list.Contains(questionId))
        {
            list.Add(questionId);
        }
    }
}
