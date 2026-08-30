using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class InvestmentRisks
{
    public static readonly List<RiskDefinition> All = new()
    {
        // 1. INVEST_PRIOR_INVESTMENT_UNCLEAR (§25 / §27.2)
        new()
        {
            Code = "INVEST_PRIOR_INVESTMENT_UNCLEAR",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            RootCauseGroup = "INVESTMENT_HISTORY",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "prior_investments" },
            Title = "Обязательства перед прошлым инвестором определены не полностью",
            Finding = "Компания ранее получала инвестиционные деньги, но часть условий оформлена неполно или сама команда не может точно объяснить будущие права инвестора.",
            WhyItMatters = "Новый инвестор будет учитывать уже существующие права, поэтому неопределенность может задержать расчет структуры и документы раунда.",
            Recommendations = new()
            {
                "Собрать все документы и переводы по прошлым инвестициям.",
                "Определить права, которые уже возникли или могут возникнуть.",
                "Отразить их в единой структуре капитала до нового раунда."
            },
            Recommendation = "Собрать все документы и переводы по прошлым инвестициям, определить права инвестора и зафиксировать их в единой структуре капитала."
        },

        // 2. INVEST_FUTURE_CAP_TABLE_UNCLEAR (§25)
        new()
        {
            Code = "INVEST_FUTURE_CAP_TABLE_UNCLEAR",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            RootCauseGroup = "EQUITY_PROMISE",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "future_ownership" },
            Title = "Будущая структура долей после учета всех обещаний не определена",
            Finding = "Компания понимает текущие доли, но не может точно учесть прошлые инвестиции, обещания команде или другие будущие права.",
            WhyItMatters = "Без этого невозможно корректно посчитать влияние нового раунда и объяснить инвестору структуру владения после сделки.",
            Recommendations = new()
            {
                "Собрать все текущие и будущие права на капитал.",
                "Сформировать расчет структуры до и после раунда.",
                "Синхронизировать его с корпоративными документами."
            },
            Recommendation = "Собрать все права на капитал и сформировать единый расчет структуры долей до и после раунда."
        },

        // 3. INVEST_DILUTION_NOT_MODELED (§25)
        new()
        {
            Code = "INVEST_DILUTION_NOT_MODELED",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            RootCauseGroup = "INVESTMENT_ECONOMICS",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "dilution" },
            Title = "Основатели не посчитали, как новый раунд изменит их доли",
            Finding = "Последствия нового выпуска долей понимаются приблизительно либо вообще не моделировались.",
            WhyItMatters = "Без предварительного расчета founders могут согласовать экономику сделки, не понимая итоговую структуру после инвестиций и командных опционов.",
            Recommendations = new()
            {
                "Сделать базовый расчет до/после раунда.",
                "Добавить уже обещанные права команде и инвесторам.",
                "Проверить несколько сценариев суммы и оценки."
            },
            Recommendation = "Сделать базовый расчет изменения долей до и после раунда для разных сценариев суммы и оценки."
        },

        // 4. INVEST_ROUND_NOT_DEFINED (§25)
        new()
        {
            Code = "INVEST_ROUND_NOT_DEFINED",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            RootCauseGroup = "INVESTMENT_PREPARATION",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "round_definition" },
            Title = "Размер и цель инвестиционного раунда определены не полностью",
            Finding = "Компания хочет привлечь инвестиции, но сумма, период финансирования или конкретные направления использования денег еще не связаны в единую модель.",
            WhyItMatters = "Это ослабляет аргументацию раунда и затрудняет оценку того, достаточно ли денег для достижения следующего этапа.",
            Recommendations = new()
            {
                "Определить ключевые цели следующего этапа.",
                "Посчитать расходы и необходимый запас времени.",
                "Связать сумму раунда с конкретными результатами бизнеса."
            },
            Recommendation = "Определить ключевые цели следующего этапа и связать необходимую сумму раунда с планом расходов."
        },

        // 5. INVEST_RUNWAY_WARNING (§25)
        new()
        {
            Code = "INVEST_RUNWAY_WARNING",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            RootCauseGroup = "INVESTMENT_PREPARATION",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "runway" },
            Title = "Компания начинает привлечение с ограниченным запасом времени",
            Finding = "По вашим ответам текущих денег может хватить менее чем на несколько месяцев либо компания не имеет надежного расчета финансового запаса.",
            WhyItMatters = "Ограниченный запас времени снижает переговорную позицию и оставляет меньше времени на исправление вопросов, найденных инвестором.",
            Recommendations = new()
            {
                "Обновить расчет ежемесячных расходов и доступных денег.",
                "Определить реалистичный срок закрытия раунда.",
                "Параллельно подготовить резервный сценарий финансирования или сокращения расходов."
            },
            Recommendation = "Обновить расчет ежемесячных расходов и подготовить резервный сценарий финансирования до закрытия раунда."
        },

        // 6. INVEST_FIN_MODEL_WEAK (§25)
        new()
        {
            Code = "INVEST_FIN_MODEL_WEAK",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            RootCauseGroup = "INVESTMENT_PREPARATION",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "financial_model" },
            Title = "Финансовая модель недостаточно готова к обсуждению раунда",
            Finding = "Финансовый план отсутствует, устарел или состоит из отдельных расчетов без единой картины доходов, расходов и потребности в капитале.",
            WhyItMatters = "Инвестору важно понимать, как компания планирует использовать деньги и какие предположения стоят за ростом.",
            Recommendations = new()
            {
                "Собрать базовую модель доходов и расходов.",
                "Зафиксировать ключевые предположения и найм.",
                "Связать модель с размером раунда и финансовым запасом."
            },
            Recommendation = "Собрать базовую модель доходов и расходов с ключевыми предположениями по росту и найму."
        },

        // 7. INVEST_METRICS_UNVERIFIABLE (§25)
        new()
        {
            Code = "INVEST_METRICS_UNVERIFIABLE",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            RootCauseGroup = "INVESTMENT_EVIDENCE",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "metrics_evidence" },
            Title = "Часть ключевых показателей будет сложно подтвердить инвестору",
            Finding = "Некоторые цифры о выручке, клиентах, росте или расходах рассчитываются приблизительно либо не имеют доступного подтверждения.",
            WhyItMatters = "Во время проверки инвестор сопоставляет презентацию с первичными данными и документами; существенное расхождение снижает доверие к остальной информации.",
            Recommendations = new()
            {
                "Определить ключевые показатели, которые используются в презентации.",
                "Для каждого указать источник данных и способ расчета.",
                "Исправить показатели, которые нельзя воспроизвести или подтвердить."
            },
            Recommendation = "Определить ключевые показатели презентации, проверить их источники данных и формулы расчета."
        },

        // 8. INVEST_DD_DOCS_NOT_READY (§25 / §27.2)
        new()
        {
            Code = "INVEST_DD_DOCS_NOT_READY",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            RootCauseGroup = "INVESTMENT_DOCUMENTS",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "dd_documents" },
            Title = "Документы компании не готовы к быстрой проверке инвестором",
            Finding = "Основные документы находятся в разных местах, часть придется искать или восстанавливать уже после запроса инвестора.",
            WhyItMatters = "Это замедляет проверку и часто выводит на поверхность старые корпоративные, IP или договорные пробелы в самый неудобный момент.",
            Recommendations = new()
            {
                "Собрать корпоративные, финансовые, IP, командные и ключевые коммерческие документы.",
                "Устранить явные пробелы до передачи папки инвестору.",
                "Поддерживать единую актуальную структуру документов."
            },
            Recommendation = "Собрать ключевые корпоративные, финансовые, IP и договорные документы в единую структурированную Data Room."
        },

        // 9. INVEST_TERMS_NOT_UNDERSTOOD (§25 / §27.2)
        new()
        {
            Code = "INVEST_TERMS_NOT_UNDERSTOOD",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            RootCauseGroup = "INVESTMENT_DEAL",
            ServiceCode = "DEAL_SUPPORT",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "deal_terms" },
            Title = "Последствия условий инвестора понятны не полностью",
            Finding = "Компания уже обсуждает конкретную сделку, но основное внимание было сосредоточено на сумме и проценте, а права контроля и экономические последствия других условий остаются неясными.",
            WhyItMatters = "В инвестиционной сделке отдельные условия могут влиять на управление компанией и распределение денег не меньше, чем заявленная оценка.",
            Recommendations = new()
            {
                "Разобрать все существенные условия сделки до согласования.",
                "Посчитать экономические последствия ключевых сценариев.",
                "Сопоставить права инвестора с управлением компанией и следующими раундами."
            },
            Recommendation = "Разобрать все юридические и экономические условия Term Sheet до подписания с опытным юристом."
        },

        // 10. INVEST_DEAL_UNREVIEWED (§25)
        new()
        {
            Code = "INVEST_DEAL_UNREVIEWED",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            RootCauseGroup = "INVESTMENT_DEAL",
            ServiceCode = "DEAL_SUPPORT",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "deal_review" },
            Title = "Документы инвестиционной сделки не прошли профильную проверку со стороны компании",
            Finding = "Конкретные инвестиционные условия уже обсуждаются или получены, но документы анализируются самостоятельно либо без понятного опыта в инвестиционных сделках.",
            WhyItMatters = "На этой стадии ошибки в экономике, контроле или обязательствах могут закрепиться в подписанных условиях и повлиять на последующие документы.",
            Recommendations = new()
            {
                "Проверить term sheet или полученные условия до подписания.",
                "Выделить экономические и контрольные последствия.",
                "Сопроводить согласование окончательных документов и закрытие сделки."
            },
            Recommendation = "Привлечь профильного юриста для проверки Term Sheet и основных инвестиционных соглашений."
        },

        // 11. INVEST_SELF_AWARENESS_GAP (§25 / §27.2 - Cross-module / Pipeline deferred)
        new()
        {
            Code = "INVEST_SELF_AWARENESS_GAP",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            RootCauseGroup = "INVESTMENT_READINESS",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            AffectedDimensions = new() { "round_definition" },
            Title = "Компания недооценивает вопросы, которые вероятно появятся при проверке инвестора",
            Finding = "Вы не отметили известных существенных проблем, однако предыдущие блоки диагностики выявили один или несколько вопросов уровня High/Critical.",
            WhyItMatters = "Если такие вопросы впервые появятся уже у инвестора, у компании останется меньше времени и контроля над способом их исправления.",
            Recommendations = new()
            {
                "Сопоставить результаты диагностики с будущей папкой документов.",
                "Закрыть наиболее существенные вопросы до активной проверки.",
                "Подготовить краткое объяснение по рискам, которые нельзя устранить сразу."
            },
            Recommendation = "Сопоставить выявленные юридические вопросы с будущей инвестиционной проверкой и закрыть их заранее."
        },

        // 12. INVEST_ROUND_BLOCKER (§25 / §27.2 - Cross-module / Overlay deferred)
        new()
        {
            Code = "INVEST_ROUND_BLOCKER",
            SectionId = "investment",
            Modules = new() { "investment" },
            Severity = RiskSeverity.Blocker,
            Priority = RiskPriority.Now,
            RootCauseGroup = "ROUND_BLOCKER",
            ServiceCode = "INVESTOR_READINESS",
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            AffectedDimensions = new() { "round_definition" },
            Title = "Есть вопрос, который может существенно осложнить или задержать текущий раунд",
            Finding = "Один из выявленных юридических вопросов относится к тем областям, которые инвестор с высокой вероятностью будет проверять до закрытия сделки, а раунд уже близок или идет сейчас.",
            WhyItMatters = "Даже при высокой общей готовности один существенный вопрос по долям, правам на продукт, прошлым инвестициям или уходу founder может стать отдельным условием закрытия.",
            Recommendations = new()
            {
                "Сначала определить точный объем проблемы и документы.",
                "Составить план исправления до или в рамках сделки.",
                "Согласовать порядок раскрытия и закрытия вопроса с инвестиционными документами."
            },
            Recommendation = "Составить план устранения критичного вопроса до выхода на активную стадию сделки с инвестором."
        }
    };
}
