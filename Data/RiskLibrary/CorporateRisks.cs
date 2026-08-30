using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class CorporateRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «КОРПОРАТИВНАЯ СТРУКТУРА» (v1.1)
        // =====================================================================
        new() {
            Code = "COR_OWNERSHIP_DISPUTE",
            RootCauseGroup = "OWNERSHIP",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существует спор о юридическом владении компанией",
            Finding = "По вашим ответам стороны по-разному понимают, кому должна принадлежать часть компании.",
            WhyItMatters = "Спор о владении влияет на контроль, экономические права и практически неизбежно станет центральным вопросом при сделке или инвестиционной проверке.",
            Recommendation = "Собрать официальные документы и урегулировать расхождения до новых выпусков долей.",
            AffectedDimensions = new() { "ownership_accuracy" },
            Recommendations = new() {
                "Собрать официальные документы и договоренности о долях.",
                "Определить фактическую и зарегистрированную структуру владения.",
                "До новых выпусков или сделок юридически урегулировать расхождение."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_OWNERSHIP_MISMATCH",
            RootCauseGroup = "OWNERSHIP",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Юридическое владение компанией не полностью соответствует договоренностям",
            Finding = "Система видит существенное расхождение между тем, как участники понимают распределение долей, и тем, что оформлено в реестре сейчас.",
            WhyItMatters = "Такое расхождение может повлиять на голосование, выплаты дивидендов и привести к отказу инвестора при Due Diligence.",
            Recommendation = "Сопоставить зарегистрированные доли со всеми действующими договоренностями и внести изменения в реестр.",
            AffectedDimensions = new() { "ownership_accuracy" },
            Recommendations = new() {
                "Сопоставить зарегистрированные доли со всеми действующими договоренностями.",
                "Определить, какие изменения должны быть оформлены.",
                "Провести необходимые корпоративные действия и обновить реестр/таблицу долей."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_UNDOCUMENTED_EQUITY",
            RootCauseGroup = "EQUITY_PROMISE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существуют права на будущий капитал, которые не отражены полностью",
            Finding = "Одному или нескольким лицам обещано участие в капитале, но обязательства не полностью документированы или не учтены в структуре.",
            WhyItMatters = "Неучтенные обещания могут неожиданно изменить будущие доли и вызвать юридический конфликт с командой или инвестором.",
            Recommendation = "Собрать все обещания долей, опционов и зафиксировать их в официальной опционной программе (ESOP) или соглашении.",
            AffectedDimensions = new() { "equity_commitments" },
            Recommendations = new() {
                "Собрать все обещания долей и их условия.",
                "Отразить документированные обязательства в единой таблице капитала (Cap table).",
                "Оформить неформальные обещания либо закрыть их до следующей сделки."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_CAP_TABLE_UNRELIABLE",
            RootCauseGroup = "OWNERSHIP",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Невозможно быстро подтвердить полную структуру капитала (Cap table)",
            Finding = "Информация о текущих и будущих правах на доли находится в разных местах либо единой достоверной картины нет.",
            WhyItMatters = "Без надежной картины капитала сложно безопасно выпускать новые доли, планировать раунд и объяснять структуру инвестору.",
            Recommendation = "Сформировать единую актуальную таблицу капитала (Cap Table) и ввести порядок ее обязательного обновления.",
            AffectedDimensions = new() { "cap_table" },
            Recommendations = new() {
                "Собрать зарегистрированное владение, опционы, обещания и инвестиционные обязательства.",
                "Сформировать единую актуальную таблицу капитала.",
                "Ввести порядок обновления после каждого изменения."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_CORPORATE_HISTORY_GAP",
            RootCauseGroup = "CORPORATE_HISTORY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "История изменений капитала подтверждается не полностью",
            Finding = "Часть прошлых изменений участников или долей оформлялась неполно либо последовательность документов нельзя восстановить полностью.",
            WhyItMatters = "Инвестору или покупателю важно понимать не только текущие доли, но и законную последовательность их возникновения.",
            Recommendation = "Собрать документы по каждому изменению капитала и восстановить недостающие решения.",
            AffectedDimensions = new() { "corporate_history" },
            Recommendations = new() {
                "Собрать документы по каждому изменению капитала.",
                "Восстановить недостающие решения и регистрационные подтверждения, где это возможно.",
                "Сопоставить историю с текущей таблицей долей."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_APPROVAL_GAP",
            RootCauseGroup = "CORPORATE_GOVERNANCE",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существенные корпоративные решения оформляются непоследовательно",
            Finding = "Часть значимых действий компании принималась без системного документального оформления корпоративных решений.",
            WhyItMatters = "Это может усложнить подтверждение полномочий и истории существенных сделок при проверке компании.",
            Recommendation = "Определить перечень действий, требующих обязательного корпоративного решения, и ввести регламент.",
            AffectedDimensions = new() { "corporate_approvals" },
            Recommendations = new() {
                "Определить перечень действий, требующих корпоративного решения.",
                "Проверить исторические существенные события.",
                "Ввести единый порядок оформления решений."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_AUTHORITY_GAP",
            RootCauseGroup = "CORPORATE_GOVERNANCE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Полномочия на подписание договоров и принятие обязательств не определены четко",
            Finding = "Обязательства принимаются лицами без понятных или формализованных полномочий.",
            WhyItMatters = "Сделки, подписанные без должных полномочий, могут быть оспорены контрагентами или участниками, создавая прямые финансовые убытки.",
            Recommendation = "Четко зафиксировать полномочия и финансовые лимиты единоличного исполнительного органа и выдать доверенности.",
            AffectedDimensions = new() { "authority" },
            Recommendations = new() {
                "Четко зафиксировать полномочия и финансовые лимиты генерального директора.",
                "Выдать доверенности с однозначным объемом полномочий.",
                "Ввести внутренний регламент согласования договоров."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_ENTITY_MISMATCH",
            RootCauseGroup = "ENTITY_ALIGNMENT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Ключевые активы или деятельность оформлены на неожиданных лиц / внешние структуры",
            Finding = "Существенная часть деятельности, прав на продукт или активов оформлена вне операционной компании.",
            WhyItMatters = "Инвестор вкладывает деньги в компанию, ожидая, что вся ценность находится внутри неё. Размытие активов блокирует инвестиционный раунд.",
            Recommendation = "Провести аудит нахождения прав и ключевых договоров и перевести их на операционную компанию проекта.",
            AffectedDimensions = new() { "entity_alignment" },
            Recommendations = new() {
                "Провести аудит нахождения прав и ключевых договоров.",
                "Перевести активы и договоры на операционную компанию проекта.",
                "Разграничить функции компаний группы соглашениями."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_RECORDS_GAP",
            RootCauseGroup = "CORPORATE_RECORDS",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Корпоративные документы разрознены или требуют систематизации",
            Finding = "Основные документы компании находятся в разных местах или частично утеряны.",
            WhyItMatters = "Затягивает подготовку к Due Diligence и увеличивает операционные риски при любых сделках.",
            Recommendation = "Собрать все оригиналы и скан-копии уставов, свидетельств, решений и организовать защищенный Data Room.",
            AffectedDimensions = new() { "records" },
            Recommendations = new() {
                "Собрать все оригиналы и скан-копии корпоративных документов.",
                "Организовать защищенный цифровой архив (Data Room) компании."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_HIDDEN_CONTROL",
            RootCauseGroup = "HIDDEN_CONTROL",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Фактический контроль или экономический интерес не отражен формально",
            Finding = "Существует неформальная договоренность о контроле или доле лица, не указанного в официальных документах.",
            WhyItMatters = "Скрытый бенефициар — один из главных стоп-факторов для институциональных инвесторов и комплаенса банков.",
            Recommendation = "Провести индивидуальную консультацию с венчурным юристом для безопасной формализации структуры.",
            AffectedDimensions = new() { "authority" },
            Recommendations = new() {
                "Провести консультацию с венчурным юристом.",
                "Определить безопасный вариант формализации отношений (опцион, конвертируемый заем, холдинг).",
                "Устранить неформальные риски до привлечения инвестиций."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_NO_ENTITY_FOR_ACTIVITY",
            RootCauseGroup = "ENTITY_ALIGNMENT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Бизнес уже работает, но отдельная юридическая оболочка еще не сформирована",
            Finding = "Проект уже ведет значимую деятельность, однако отдельная компания отсутствует или еще не завершила регистрацию.",
            WhyItMatters = "В такой ситуации договоры, деньги, права на продукт и обязательства могут возникать непосредственно у основателей, что усложняет последующее структурирование.",
            Recommendation = "Определить подходящую юридическую структуру для текущей модели, зафиксировать возникшие активы и перенести ключевые отношения на компанию.",
            AffectedDimensions = new() { "entity_alignment", "ownership_accuracy" },
            Recommendations = new() {
                "Определить подходящую юридическую структуру для текущей модели.",
                "Зафиксировать, какие активы и обязательства уже возникли у founders.",
                "После регистрации перенести ключевые отношения на компанию."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        },
    };
}
