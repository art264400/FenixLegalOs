using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class DeterministicFallbackNarratives
{
    public static ReportNarrativesDto GenerateFallbackNarratives(ReportContext ctx)
    {
        var narratives = new ReportNarrativesDto
        {
            ContextFingerprint = ReportQualityGate.ComputeContextFingerprint(ctx),
            SchemaVersion = "2.0",
            ProjectProfileNarrative = ctx.Profile.ConfigurationNarrative,
            FenixLawRecommendation = ctx.FenixLaw.SummaryText
        };

        // 1. Executive Synthesis Fallback
        var execSynthesis = GenerateExecutiveFallback(ctx);
        narratives.ExecutiveConclusion = execSynthesis.ExecutiveConclusion;
        narratives.RootCauseSummaries = execSynthesis.RootCauseSummaries;

        // 2. Module Narratives Fallback
        foreach (var focus in ctx.FocusModules)
        {
            narratives.ModuleNarratives[focus.SectionId] = GenerateModuleFallback(ctx, focus.SectionId);
        }

        // 3. Action Narratives Fallback
        var actionIds = ctx.ActionPlan.Select(a => a.ActionId).ToList();
        narratives.ActionNarratives = GenerateActionBatchFallback(ctx, actionIds);

        return narratives;
    }

    public static ModuleNarrativeDto GenerateModuleFallback(ReportContext ctx, string sectionId)
    {
        var focus = ctx.FocusModules.FirstOrDefault(m => string.Equals(m.SectionId, sectionId, StringComparison.OrdinalIgnoreCase));
        if (focus == null)
        {
            // If module is not in FocusModules, create minimal neutral fallback
            return new ModuleNarrativeDto
            {
                Summary = $"Направление «{sectionId}» рассмотрено в рамках общего правового скрининга.",
                PracticalMeaning = "Правовое структурирование данного направления обеспечивает предсказуемость для бизнеса и инвесторов.",
                FindingNarratives = new Dictionary<string, FindingNarrativeDto>()
            };
        }

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
            PracticalMeaning = !string.IsNullOrWhiteSpace(focus.PracticalMeaning)
                ? focus.PracticalMeaning
                : $"Пока правовая основа направления «{focus.Title}» не формализована, компания накапливает скрытые риски. При проверке инвесторами или контрагентами неурегулированные вопросы затянут заключение сделок и потребуют восстановления истории документов."
        };

        foreach (var finding in focus.Findings)
        {
            moduleNarrative.FindingNarratives[finding.FindingCode] = new FindingNarrativeDto
            {
                WhyFound = finding.WhyFound,
                WhyItMatters = finding.WhyItMatters,
                Recommendation = finding.Recommendation,
                Recommendations = finding.Recommendations
            };
        }

        return moduleNarrative;
    }

    public static Dictionary<string, ActionNarrativeItemDto> GenerateActionBatchFallback(ReportContext ctx, IEnumerable<string> actionIds)
    {
        var result = new Dictionary<string, ActionNarrativeItemDto>(StringComparer.OrdinalIgnoreCase);
        var targetSet = new HashSet<string>(actionIds, StringComparer.OrdinalIgnoreCase);

        foreach (var action in ctx.ActionPlan.Where(a => targetSet.Contains(a.ActionId)))
        {
            result[action.ActionId] = new ActionNarrativeItemDto
            {
                WhyNow = action.WhyNow,
                ExpectedResult = action.ExpectedResult
            };
        }

        return result;
    }

    public static ExecutiveSynthesisResponseDto GenerateExecutiveFallback(
        ReportContext ctx,
        Dictionary<string, ModuleNarrativeDto>? moduleNarratives = null)
    {
        var driverTopics = new List<string>();
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

        var criticalCount = ctx.AllFindings.Count(f => f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical);
        var highCount = ctx.AllFindings.Count(f => f.Severity == RiskSeverity.High);
        var focusTitles = string.Join(", ", ctx.FocusModules.Take(3).Select(m => $"«{m.Title}»"));

        var weakModulesSummary = string.Join(", ", ctx.FocusModules
            .Where(m => m.Score < 60)
            .Select(m => $"«{m.Title}» ({m.Score}/100)"));

        var positiveStrengths = ctx.PositiveFactors.Count > 0
            ? $" Подтвержденные сильные стороны проекта: {string.Join(", ", ctx.PositiveFactors.Take(2).Select(p => $"«{p.Title}»"))}."
            : "";

        string executiveConclusion;

        if (ctx.Overall.Score >= 80 && criticalCount == 0 && highCount == 0)
        {
            executiveConclusion =
                $"Комплексный правовой скрининг показал высокий уровень юридической готовности проекта ({ctx.Overall.Score} / 100, категория «{ctx.Overall.LevelTitle}»). " +
                $"Базовая корпоративная архитектура, ключевые права на продукт и договорные практики с участниками оформлены надлежащим образом.{positiveStrengths} " +
                $"Критических правовых блокеров и существенных уязвимостей, препятствующих операционной деятельности или привлечению венчурного финансирования, не выявлено. " +
                $"Текущие задачи носят плановый характер и направлены на поддержание актуальности документации по мере масштабирования бизнеса.";
        }
        else if (ctx.Overall.Score >= 60 && criticalCount == 0)
        {
            executiveConclusion =
                $"По результатам диагностики общий уровень правовой готовности компании оценивается в {ctx.Overall.Score} / 100 (категория «{ctx.Overall.LevelTitle}»). " +
                $"Проект сформировал базовый юридический фундамент, однако содержит {highCount} правовых факторов высокого приоритета, требующих плановой доработки. " +
                $"Основное внимание необходимо сосредоточить на направлениях {focusTitles}, где выявлены {topDriversPhrase}.{positiveStrengths} " +
                $"Своевременная реализация первоочередных шагов дорожной карты позволит устранить риски при Due Diligence и укрепить правовую позицию компании перед инвесторами и контрагентами.";
        }
        else
        {
            var weakPart = !string.IsNullOrWhiteSpace(weakModulesSummary)
                ? $"Наиболее уязвимыми зонами по результатам скоринга являются {weakModulesSummary}."
                : (!string.IsNullOrWhiteSpace(focusTitles)
                    ? $"Основное снижение оценки обусловлено направлениями {focusTitles}."
                    : "Основное снижение оценки обусловлено выявленными факторами риска.");

            // Grounded low-score synthesis strictly derived from existing findings (Fix for Issue 16)
            var groundedRiskBlocks = new List<string>();

            var founderFindings = ctx.AllFindings.Where(f => f.SectionId.Equals("founders", StringComparison.OrdinalIgnoreCase)).ToList();
            if (founderFindings.Any(f => f.Code.Contains("DEADLOCK") || f.Code.Contains("CONTROL")))
            {
                groundedRiskBlocks.Add("в блоке управления — нерегламентированный порядок преодоления тупиковых ситуаций");
            }
            if (founderFindings.Any(f => f.Code.Contains("VESTING")))
            {
                groundedRiskBlocks.Add("отсутствие вестинга долей основателей");
            }

            var ipTeamFindings = ctx.AllFindings.Where(f => f.SectionId.Equals("ip", StringComparison.OrdinalIgnoreCase) || f.SectionId.Equals("team", StringComparison.OrdinalIgnoreCase)).ToList();
            if (ipTeamFindings.Any(f => f.Code.Contains("RIGHTS") || f.Code.Contains("ASSIGNMENT") || f.Code.Contains("TRANSFER")))
            {
                groundedRiskBlocks.Add("в блоке интеллектуальной собственности и команды — неполная передача исключительных прав от участников разработки");
            }
            if (ipTeamFindings.Any(f => f.Code.Contains("CONTRACT") || f.Code.Contains("WRITTEN")))
            {
                groundedRiskBlocks.Add("отсутствие надлежащей письменной договорной базы с привлеченными специалистами");
            }

            var dataProdFindings = ctx.AllFindings.Where(f => f.SectionId.Equals("data", StringComparison.OrdinalIgnoreCase) || f.SectionId.Equals("product", StringComparison.OrdinalIgnoreCase)).ToList();
            if (dataProdFindings.Any(f => f.Code.Contains("PRIVACY") || f.Code.Contains("POLICY")))
            {
                groundedRiskBlocks.Add("в контуре данных и продукта — работа без актуализированной Политики конфиденциальности");
            }
            if (dataProdFindings.Any(f => f.Code.Contains("AI") || f.Code.Contains("EXTERNAL_AI")))
            {
                groundedRiskBlocks.Add("непроверенные условия передачи данных во внешние сервисы ИИ");
            }
            if (dataProdFindings.Any(f => f.Code.Contains("DELETION") || f.Code.Contains("USER_RIGHTS")))
            {
                groundedRiskBlocks.Add("отсутствие прозрачных механизмов удаления информации по запросу пользователей");
            }

            var specificRisksPhrase = groundedRiskBlocks.Count > 0
                ? $"Ключевыми факторами риска выступают: {string.Join("; ", groundedRiskBlocks)}. "
                : $"Ключевыми факторами риска выступают выявленные уязвимости в базовой правовой архитектуре. ";

            var topActionsSummary = ctx.ActionPlan.Take(3).Select(a => a.Title.ToLowerInvariant()).ToList();
            var planPhrase = topActionsSummary.Count > 0
                ? $"Для комплексного устранения рисков рекомендуется реализовать пошаговый план: в первую очередь {string.Join(", затем ", topActionsSummary)}."
                : "Для комплексного устранения рисков рекомендуется реализовать утвержденный план первоочередных правовых мероприятий.";

            executiveConclusion =
                $"Диагностика выявила существенные пробелы в правовой конструкции проекта, что выражается в итоговой оценке {ctx.Overall.Score} / 100 (категория «{ctx.Overall.LevelTitle}»). " +
                $"Ключевыми факторами снижения оценки являются {topDriversPhrase}. " +
                $"В структуре компании зафиксировано {criticalCount} критических и {highCount} высоких правовых рисков. {weakPart} " +
                $"{specificRisksPhrase}" +
                $"Эти правовые факторы требуют первоочередного внимания, поскольку напрямую влияют на чистоту актива и устойчивость бизнеса при проверке Due Diligence.{positiveStrengths} " +
                $"{planPhrase}";
        }

        var rootCauses = new Dictionary<string, string>();
        foreach (var top in ctx.TopFindings)
        {
            var key = !string.IsNullOrWhiteSpace(top.RootCauseCode) ? top.RootCauseCode : top.FindingCode;
            rootCauses[key] = top.ShortSummary;
        }

        return new ExecutiveSynthesisResponseDto
        {
            ProjectProfileNarrative = ctx.Profile.ConfigurationNarrative,
            ExecutiveConclusion = executiveConclusion,
            RootCauseSummaries = rootCauses,
            FenixLawRecommendation = ctx.FenixLaw.SummaryText
        };
    }
}
