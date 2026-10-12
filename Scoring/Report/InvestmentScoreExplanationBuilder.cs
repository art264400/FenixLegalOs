using FenixLegalOs.Models;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

/// <summary>
/// Explains the saved investment scores without recalculating them or asking the narrative model
/// to infer how a fact affected the result. A blocker cap is a ceiling, not an additive penalty.
/// </summary>
public static class InvestmentScoreExplanationBuilder
{
    public static void Populate(InvestmentReadinessReportDto investment, SectionScore? section, SharedFactStore facts)
    {
        investment.HasFactorBreakdown = false;
        investment.MainScoreReasons = new();
        investment.AdditionalScoreReasons = string.Empty;
        investment.PreparedItems = new();
        investment.BlockerEffectExplanation = DescribeBlockerEffect(investment);

        if (!investment.IsApplicable)
        {
            investment.ScoreExplanation = "Инвестиционный блок неприменим к текущей диагностике и исключен из расчета общей юридической оценки.";
            return;
        }

        var priorUncertainty = (investment.HasPriorInvestment &&
                                string.Equals(investment.PriorInvestmentStatus, "unknown", StringComparison.OrdinalIgnoreCase)) ||
                               Fact(facts, "investment.priorInvestment") == "unknown"
            ? " Наличие или условия прошлых инвестиций не определены; их следует уточнить."
            : string.Empty;

        var dimensions = section?.Dimensions
            .Where(d => d.IsApplicable && d.Score is >= 0 and <= 100 && double.IsFinite(d.Weight) && d.Weight > 0)
            .ToList() ?? new List<DimensionScore>();
        var totalWeight = dimensions.Sum(d => d.Weight);
        if (dimensions.Count == 0 || !double.IsFinite(totalWeight) || totalWeight <= 0)
        {
            investment.ScoreExplanation = $"Сохраненная базовая оценка: {investment.BaseScore}/100. Подробная разбивка факторов этого расчета недоступна; причины снижения по общему баллу не восстанавливаются." + priorUncertainty;
            return;
        }

        investment.HasFactorBreakdown = true;
        var reasons = dimensions.Where(d => d.Score < 100)
            .Select(d => new InvestmentScoreReasonDto
            {
                DimensionId = d.DimensionId,
                Title = GetTitle(d.DimensionId),
                Explanation = DescribeFactor(d.DimensionId, facts),
                Score = d.Score,
                Weight = d.Weight,
                Impact = (100 - d.Score) * d.Weight / totalWeight
            })
            .OrderByDescending(r => r.Impact)
            .ThenBy(r => r.Score)
            .ThenBy(r => r.DimensionId, StringComparer.Ordinal)
            .ToList();
        investment.MainScoreReasons = reasons.Take(3).ToList();
        var additional = reasons.Skip(3).Select(r => r.Title.ToLowerInvariant()).ToList();
        if (additional.Count > 0)
            investment.AdditionalScoreReasons = $"Также на базовую оценку повлияли: {string.Join(", ", additional)}.";
        investment.ScoreExplanation = reasons.Count > 0
            ? $"Базовая подготовленность: {investment.BaseScore}/100. Оценка отражает полноту подготовки по применимым ответам, а не количество существенных блокеров. Основные причины снижения приведены ниже."
            : $"Базовая подготовленность: {investment.BaseScore}/100. По доступной разбивке все применимые факторы получили полную оценку; причин снижения по этим факторам не выявлено.";
        investment.ScoreExplanation += priorUncertainty;
        investment.PreparedItems = DescribePreparedItems(dimensions, facts).Take(3).ToList();
    }

    private static string GetTitle(string dimension) => dimension switch
    {
        "prior_investments" => "Оформление прошлых инвестиций",
        "future_ownership" => "Расчет будущей структуры долей",
        "dilution" => "Расчет размытия долей",
        "round_definition" => "Сумма и цели финансирования",
        "runway" => "Расчет финансового запаса",
        "financial_model" => "Финансовый план",
        "metrics_evidence" => "Подтверждение бизнес-показателей",
        "dd_documents" => "Подготовка документов и презентации",
        "deal_terms" => "Понимание условий инвестиционной сделки",
        "deal_review" => "Юридическая проверка условий сделки",
        _ => "Другие сведения об инвестиционной подготовке"
    };

    private static string Fact(SharedFactStore facts, string key) =>
        facts.Facts.GetValueOrDefault(key)?.ToString()?.ToLowerInvariant() ?? string.Empty;

    private static string DescribeFactor(string dimension, SharedFactStore facts) => dimension switch
    {
        "prior_investments" => DescribePriorInvestments(facts),
        "future_ownership" => Fact(facts, "investment.futureOwnershipClarity") switch
        {
            "current_only" => "Текущие доли известны, но будущие изменения с учетом обещаний и действующих обязательств не рассчитаны.",
            "mostly_promises" => "Будущая структура долей в целом понятна, но есть несколько неоформленных обещаний.",
            "none" => "По ответу нет полной картины действующих и обещанных прав на капитал.",
            "unknown" => "Ответ о будущей структуре долей неопределенный; возможность точно ее рассчитать не подтверждена.",
            _ => IncompleteFactor()
        },
        "dilution" => Fact(facts, "investment.dilutionModel") switch
        {
            "rough" => "Изменение долей основателей после привлечения денег понимается приблизительно, подробный расчет не выполнен.",
            "one_scenario" => "Изменение долей рассчитано только для основного сценария финансирования.",
            "none" => "По ответу изменение долей основателей после нового выпуска долей не рассчитывалось.",
            "unknown" => "Ответ о расчете изменения долей неопределенный; подготовка такого расчета не подтверждена.",
            _ => IncompleteFactor()
        },
        "round_definition" => Fact(facts, "investment.roundDefinition") switch
        {
            "amount_rough" => "Необходимая сумма финансирования определена примерно.",
            "use_clear_amount_pending" => "Направления использования денег понятны, но необходимая сумма еще рассчитывается.",
            "max_possible" => "Заявлена цель привлечь максимально возможную сумму, без привязки к конкретной потребности бизнеса.",
            "none" => "По ответу сумма и основные цели финансирования пока не определены.",
            "unknown" => "Ответ о сумме и целях финансирования неопределенный; их готовность не подтверждена.",
            _ => IncompleteFactor()
        },
        "runway" => DescribeRunway(facts),
        "financial_model" => Fact(facts, "investment.financialModel") switch
        {
            "simple" => "Финансовый план есть, но он достаточно простой.",
            "old" => "Финансовый план есть, но давно не обновлялся.",
            "fragments" => "Вместо единого финансового плана подготовлены только отдельные расчеты.",
            "none" => "По ответу финансового плана доходов, расходов и потребности в деньгах нет.",
            "unknown" => "Ответ о финансовом плане неопределенный; наличие актуального плана не подтверждено.",
            _ => IncompleteFactor()
        },
        "metrics_evidence" => Fact(facts, "investment.metricsEvidence") switch
        {
            "most" => "Документами можно подтвердить большинство основных показателей, но не все.",
            "approx" => "Часть основных показателей рассчитывается вручную или приблизительно.",
            "hard" => "Есть важные показатели компании, которые будет сложно подтвердить.",
            "unknown" => "Ответ о подтверждении показателей неопределенный; наличие подтверждений не установлено.",
            _ => IncompleteFactor()
        },
        "dd_documents" => DescribeDocuments(facts),
        "deal_terms" => DescribeDealTerms(facts),
        "deal_review" => Fact(facts, "investment.dealReview") switch
        {
            "lawyer_unclear" => "Документы проверял юрист, но его опыт инвестиционных сделок не подтвержден.",
            "self" => "Команда разбирает документы сделки самостоятельно.",
            "none" => "По ответу документы сделки пока никто не проверял со стороны компании.",
            "unknown" => "Ответ о проверке документов неопределенный; проведение независимой юридической проверки не подтверждено.",
            _ => IncompleteFactor()
        },
        _ => IncompleteFactor()
    };

    private static string IncompleteFactor() =>
        "В сохраненном расчете этот фактор получил неполную оценку. Доступных сведений недостаточно для более точного объяснения выбранного ответа.";

    private static string DescribePriorInvestments(SharedFactStore facts)
    {
        var status = Fact(facts, "investment.priorInvestmentStatus");
        var rights = Fact(facts, "investment.priorRightsClarity");
        var pieces = new List<string>();
        if (Fact(facts, "investment.priorInvestment") == "unknown")
            pieces.Add("Наличие прошлых инвестиций указано как неопределенное; это не означает подтвержденное получение денег.");
        else if (status == "partial") pieces.Add("Часть договоренностей по прошлым инвестициям оформлена не полностью.");
        else if (status == "informal") pieces.Add("По прошлым инвестициям были только переводы денег или устные договоренности.");
        if (rights == "main") pieces.Add("Права понятны по основным прошлым инвесторам, но не по всем.");
        else if (rights == "unclear") pieces.Add("Есть условия о правах прошлых инвесторов, которые команде не до конца понятны.");
        else if (rights == "none") pieces.Add("По ответу команда не может точно объяснить права каждого прошлого инвестора и сроки их возникновения.");
        else if (rights == "unknown") pieces.Add("Ответ о правах прошлых инвесторов неопределенный; содержание этих прав следует уточнить.");
        return pieces.Count > 0 ? string.Join(" ", pieces) : IncompleteFactor();
    }

    private static string DescribeRunway(SharedFactStore facts)
    {
        var pieces = new List<string>();
        switch (Fact(facts, "investment.runwayKnown"))
        {
            case "rough": pieces.Add("Финансовый запас оценивается приблизительно."); break;
            case "old": pieces.Add("Финансовый запас рассчитывался давно."); break;
            case "none": pieces.Add("По ответу финансовый запас не рассчитывался."); break;
            case "unknown": pieces.Add("Ответ о расчете финансового запаса неопределенный."); break;
        }
        switch (Fact(facts, "investment.runwayMonthsBucket"))
        {
            case "lt3": pieces.Add("По указанному диапазону текущих средств хватит менее чем на три месяца."); break;
            case "3_6": pieces.Add("По указанному диапазону текущих средств хватит на 3-6 месяцев."); break;
            case "6_12": pieces.Add("По указанному диапазону текущих средств хватит на 6-12 месяцев."); break;
            case "unknown": pieces.Add("Срок, на который хватит текущих средств, не определен."); break;
        }
        return pieces.Count > 0 ? string.Join(" ", pieces) : IncompleteFactor();
    }

    private static string DescribeDocuments(SharedFactStore facts)
    {
        var pieces = new List<string>();
        switch (Fact(facts, "investment.documentFolder"))
        {
            case "mostly": pieces.Add("Основные документы собраны, но для подготовки полного комплекта потребуется время."); break;
            case "scattered": pieces.Add("Документы есть, но находятся в разных местах."); break;
            case "reconstruct": pieces.Add("Часть документов придется искать или восстанавливать."); break;
            case "missing": pieces.Add("По ответу значительной части документов пока нет."); break;
            case "unknown": pieces.Add("Ответ о комплекте документов неопределенный; его готовность не подтверждена."); break;
        }
        switch (Fact(facts, "investment.pitchDeck"))
        {
            case "old": pieces.Add("Презентация есть, но требует обновления."); break;
            case "preparing": pieces.Add("Презентация сейчас готовится."); break;
            case "none": pieces.Add("По ответу презентации нет."); break;
        }
        return pieces.Count > 0 ? string.Join(" ", pieces) : IncompleteFactor();
    }

    private static string DescribeDealTerms(SharedFactStore facts)
    {
        var pieces = new List<string>();
        switch (Fact(facts, "investment.dealTermsUnderstanding"))
        {
            case "mostly": pieces.Add("В условиях сделки есть несколько непонятных пунктов."); break;
            case "price_only": pieces.Add("Анализировались в основном сумма инвестиций и процент участия."); break;
            case "unclear": pieces.Add("Большая часть условий сделки непонятна."); break;
            case "not_reviewed": pieces.Add("Документы об условиях сделки еще не анализировались."); break;
        }
        switch (Fact(facts, "investment.investorControl"))
        {
            case "extra_known": pieces.Add("У инвестора предусмотрены дополнительные ограничения, понятные команде."); break;
            case "material": pieces.Add("Инвестор получает значительное влияние на решения компании."); break;
            case "broad_veto": pieces.Add("Многие обычные решения требуют согласия инвестора."); break;
            case "unknown": pieces.Add("Ответ о влиянии инвестора на управление неопределенный."); break;
        }
        switch (Fact(facts, "investment.exitEconomicsUnderstanding"))
        {
            case "check_math": pieces.Add("Расчет распределения выплат при продаже компании требует проверки."); break;
            case "seen_unclear": pieces.Add("Условие очередности выплат известно, но его последствия непонятны."); break;
            case "unknown": pieces.Add("Ответ о распределении выплат при продаже компании неопределенный."); break;
        }
        return pieces.Count > 0 ? string.Join(" ", pieces) : IncompleteFactor();
    }

    private static IEnumerable<string> DescribePreparedItems(List<DimensionScore> dimensions, SharedFactStore facts)
    {
        bool HasDimension(string id) => dimensions.Any(d => d.DimensionId == id);
        if (HasDimension("metrics_evidence") && Fact(facts, "investment.metricsEvidence") == "yes")
            yield return "Основные показатели подтверждаются отчетами и документами.";
        if (HasDimension("dd_documents") && Fact(facts, "investment.pitchDeck") == "current")
            yield return "Презентация соответствует текущему состоянию бизнеса.";
        if (HasDimension("dd_documents") && Fact(facts, "investment.documentFolder") == "organized")
            yield return "Основные документы собраны и систематизированы.";
        if (HasDimension("future_ownership") && Fact(facts, "investment.futureOwnershipClarity") == "exact")
            yield return "Структура долей с учетом обещаний и действующих обязательств точно рассчитана.";
        if (HasDimension("prior_investments") && Fact(facts, "investment.priorInvestmentStatus") == "formal")
            yield return "Прошлые инвестиции оформлены документами.";
        if (HasDimension("financial_model") && Fact(facts, "investment.financialModel") == "current")
            yield return "Финансовый план актуален и регулярно обновляется.";
        if (HasDimension("runway") && Fact(facts, "investment.runwayKnown") == "regular")
            yield return "Финансовый запас рассчитывается регулярно.";
        if (HasDimension("deal_review") && Fact(facts, "investment.dealReview") == "specialist")
            yield return "Документы сделки проверял юрист с опытом инвестиционных сделок.";
    }

    private static string DescribeBlockerEffect(InvestmentReadinessReportDto investment)
    {
        if (!investment.IsApplicable)
            return "Ограничение инвестиционной готовности по блокерам раунда не применяется к этой диагностике.";
        var blockers = Math.Max(0, investment.UnresolvedBlockersCount);
        var currentRuleResult = investment.RoundIsCloseOrActive && blockers > 0
            ? Math.Min(investment.BaseScore, blockers == 1 ? 59 : 39)
            : investment.BaseScore;
        if (investment.ReadinessScore != currentRuleResult)
        {
            var blockerInfo = blockers > 0 ? $" В сохраненных сведениях указано существенных проблем: {blockers}." : string.Empty;
            return $"В сохраненном расчете базовая оценка: {investment.BaseScore}/100, итоговая готовность: {investment.ReadinessScore}/100.{blockerInfo} Доступные сведения о блокерах не объясняют это различие по действующим правилам. Значения сохранены без пересчета.";
        }
        if (!investment.RoundIsCloseOrActive)
        {
            if (investment.NewRoundPlanned)
                return "Блокеры близкого или активного раунда не оценивались: такой раунд не заявлен. Дополнительное ограничение базовой оценки не применялось.";
            if (investment.NewRoundPossible)
                return "Инвестиции рассматриваются как возможность. Блокеры близкого или активного раунда не оценивались; дополнительное ограничение базовой оценки не применялось.";
            if (investment.HasPriorInvestment)
                return "Новый раунд не заявлен. Оценка отражает применимые сведения о прошлых инвестициях; ограничение по блокерам нового раунда не применялось.";
            return "Планы нового раунда не указаны. Блокеры близкого или активного раунда не оценивались; дополнительное ограничение базовой оценки не применялось.";
        }
        if (investment.UnresolvedBlockersCount <= 0)
            return $"Существенных блокеров заявленного раунда по правилам диагностики не выявлено. Дополнительное ограничение не применялось: итоговая готовность остается {investment.ReadinessScore}/100.";
        var cap = investment.UnresolvedBlockersCount == 1 ? 59 : 39;
        var countText = investment.UnresolvedBlockersCount == 1 ? "Выявлена одна существенная проблема" : "Выявлены две или более существенные проблемы";
        return investment.BaseScore > cap
            ? $"{countText}: итоговая готовность ограничена уровнем {cap}/100 при базовой оценке {investment.BaseScore}/100. Это верхний предел готовности, а не дополнительный штраф за каждый риск."
            : $"{countText}: действует предел готовности {cap}/100. Базовая оценка {investment.BaseScore}/100 уже не превышает этот предел, поэтому дополнительного снижения нет; итоговая готовность: {investment.ReadinessScore}/100.";
    }
}