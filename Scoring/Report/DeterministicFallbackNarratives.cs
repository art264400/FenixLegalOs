using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class DeterministicFallbackNarratives
{
    public static ReportNarrativesDto GenerateFallbackNarratives(ReportContext ctx)
    {
        var narratives = new ReportNarrativesDto();

        // 1. Project Profile Narrative
        narratives.ProjectProfileNarrative = ctx.Profile.ConfigurationNarrative;

        // 2. Executive Conclusion
        var driverTopics = new System.Collections.Generic.List<string>();
        foreach (var f in ctx.TopFindings.Take(2))
        {
            var topic = f.FindingCode switch
            {
                "FND_DEADLOCK" or "FND_DEADLOCK_RISK" => "неурегулированность порядка принятия решений и риск тупика между основателями",
                "COR_NO_ENTITY_FOR_ACTIVITY" => "ведение фактической деятельности до регистрации юридического лица",
                "IP_PRODUCT_RIGHTS_UNCONFIRMED" or "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" => "неполная консолидация прав на созданный продукт",
                "PROD_RULES_DISCREPANCY" => "неактуальность пользовательских условий сервиса",
                "TEAM_NO_WRITTEN_CONTRACTS" => "отсутствие письменных договоров с частью команды",
                "DATA_PRIVACY_NOTICE_MISSING" => "отсутствие опубликованной политики конфиденциальности",
                _ => f.DetailSectionId.ToLowerInvariant() switch
                {
                    "founders" => "вопросы распределения долей и контроля между основателями",
                    "corporate" => "особенности текущей корпоративной формы",
                    "ip" => "оформление прав на интеллектуальную собственность",
                    "team" => "оформление отношений с привлеченными специалистами",
                    "product" => "пользовательский контур и правила сервиса",
                    "data" => "процессы обработки пользовательских данных",
                    "contracts" => "договорная база с контрагентами",
                    _ => "выявленные правовые факторы"
                }
            };
            if (!driverTopics.Contains(topic)) driverTopics.Add(topic);
        }

        var topDriversPhrase = driverTopics.Count > 0
            ? string.Join(" и ", driverTopics)
            : "выявленные организационно-правовые факторы";

        var scoreDesc = ctx.Overall.Score >= 80 ? "высокий уровень юридической готовности"
            : ctx.Overall.Score >= 60 ? "умеренное состояние с отдельными точками внимания"
            : ctx.Overall.Score >= 40 ? "существенные пробелы в защите прав и оформлении структуры"
            : "критические уязвимости в базовой юридической конструкции";

        if (ctx.Overall.Score >= 80 && ctx.TopFindings.Count == 0)
        {
            narratives.ExecutiveConclusion =
                $"Текущая оценка юридической готовности проекта ({ctx.Overall.Score} / 100) отражает {scoreDesc}. " +
                $"Базовая юридическая конструкция и ключевые договоренности оформлены на высоком уровне, критических уязвимостей и блокеров не выявлено. " +
                $"Текущее состояние обеспечивает устойчивость для операционной деятельности и планового масштабирования.";
        }
        else if (ctx.Overall.Score >= 80 && ctx.TopFindings.Count > 0)
        {
            narratives.ExecutiveConclusion =
                $"Текущая оценка юридической готовности проекта ({ctx.Overall.Score} / 100) отражает {scoreDesc}. " +
                $"Базовые элементы структуры выстроены, при этом точечного внимания требуют {topDriversPhrase}. " +
                $"Плановое устранение этих вопросов позволит закрепить высокую правовую защищенность бизнеса.";
        }
        else
        {
            narratives.ExecutiveConclusion =
                $"Текущая оценка юридической готовности проекта ({ctx.Overall.Score} / 100) отражает {scoreDesc}. " +
                $"Ключевыми факторами снижения оценки являются {topDriversPhrase}. " +
                $"При текущей конфигурации сохраняются правовые уязвимости, требующие первоочередного структурирования до масштабирования или привлечения внешнего финансирования.";
        }

        // 3. Root Cause Summaries (Canonical Schema)
        foreach (var top in ctx.TopFindings)
        {
            var key = !string.IsNullOrWhiteSpace(top.RootCauseCode) ? top.RootCauseCode : top.FindingCode;
            narratives.RootCauseSummaries[key] = top.ShortSummary;
        }

        // 4. Module Narratives (Score-aware)
        foreach (var focus in ctx.FocusModules)
        {
            string summary;
            if (focus.Score >= 80)
            {
                if (focus.Findings.Count > 0)
                {
                    summary = $"Направление «{focus.Title}» демонстрирует высокий уровень готовности ({focus.Score} / 100). Базовая структура выстроена корректно, при этом для полной правовой защиты требуется точечно урегулировать вопрос: {focus.Findings[0].Title.ToLowerInvariant()}.";
                }
                else
                {
                    summary = $"Направление «{focus.Title}» выстроено на высоком уровне ({focus.Score} / 100), существенных правовых рисков не выявлено.";
                }
            }
            else if (focus.Score >= 60)
            {
                summary = $"Направление «{focus.Title}» получило оценку {focus.Score} / 100 ({focus.ScoreBand}). Базовые элементы структуры присутствуют, однако требуется доработка отдельных условий и документального оформления.";
            }
            else if (focus.Score >= 40)
            {
                summary = $"Направление «{focus.Title}» получило оценку {focus.Score} / 100 ({focus.ScoreBand}). В данном блоке выявлены существенные правовые пробелы, создающие повышенную уязвимость для бизнеса.";
            }
            else
            {
                summary = $"Направление «{focus.Title}» получило оценку {focus.Score} / 100 ({focus.ScoreBand}). Выявлены критические уязвимости и риски, требующие первоочередного правового структурирования.";
            }

            var moduleNarrative = new ModuleNarrativeDto
            {
                Summary = summary,
                PracticalMeaning = $"Вопросы в сфере «{focus.Title}» напрямую проверяются инвесторами и контрагентами. Своевременное оформление защищает интересы проекта и обеспечивает предсказуемость отношений."
            };

            foreach (var finding in focus.Findings)
            {
                moduleNarrative.FindingNarratives[finding.FindingCode] = new FindingNarrativeDto
                {
                    WhyFound = finding.WhyFound,
                    WhyItMatters = finding.WhyItMatters,
                    Recommendation = finding.Recommendation
                };
            }

            narratives.ModuleNarratives[focus.SectionId] = moduleNarrative;
        }

        // 5. Action Narratives (Keyed ONLY by ActionId)
        foreach (var action in ctx.ActionPlan)
        {
            narratives.ActionNarratives[action.ActionId] = new ActionNarrativeItemDto
            {
                WhyNow = action.WhyNow,
                ExpectedResult = action.ExpectedResult
            };
        }

        // 6. Fenix Law Recommendation
        narratives.FenixLawRecommendation = ctx.FenixLaw.SummaryText;

        return narratives;
    }
}
