using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class FoundersRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
                // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «СООСНОВАТЕЛИ» (CANONICAL §25 — 18 FINDINGS)
        // =====================================================================
        new() {
            Code = "FND_ACTIVE_DISPUTE",
            SuppressCodes = new() { "FND_ROLE_AMBIGUITY", "FND_DOCUMENTATION_GAP" },
            RootCauseGroup = "FOUNDER_CONFLICT",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Между основателями уже существует существенный конфликт",
            Finding = "По вашим ответам между основателями есть нерешенные разногласия, которые уже влияют или могут влиять на доли, управление, деньги, права на продукт или выход из компании.",
            WhyItMatters = "В такой ситуации стандартная профилактическая документация может быть недостаточной: сначала нужно определить фактические позиции сторон и существующие права.",
            Recommendation = "Зафиксировать предмет разногласий и позиции сторон до принятия новых существенных решений.",
            AffectedDimensions = new() { "existing_dispute" },
            Recommendations = new() {
                "Зафиксировать предмет разногласий и позиции сторон.",
                "Проверить действующие корпоративные и договорные документы.",
                "Определить юридический сценарий урегулирования до новых существенных решений."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Урегулировать разногласия с Fenix Law"
        },
        new() {
            Code = "FND_EQUITY_DISPUTE",
            SuppressCodes = new() { "FND_EQUITY_NOT_FORMALIZED", "FND_EQUITY_AMBIGUITY" },
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Принадлежность долей между основателями оспаривается или определена неоднозначно",
            Finding = "Система видит спор или существенную неопределенность относительно того, кому должна принадлежать часть компании.",
            WhyItMatters = "Неопределенность по долям напрямую влияет на контроль, экономические права и возможность безопасно менять структуру компании или привлекать инвестиции.",
            Recommendation = "Собрать все договоренности и документы о долях и сопоставить их с зарегистрированным владением.",
            AffectedDimensions = new() { "equity_clarity" },
            Recommendations = new() {
                "Собрать все договоренности и документы о долях.",
                "Сопоставить их с официально зарегистрированным владением.",
                "До новых сделок определить и оформить согласованную структуру."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Зафиксировать структуру долей"
        },
        new() {
            Code = "FND_DEAD_EQUITY",
            SuppressCodes = new() { "FND_NO_VESTING", "FND_COMMITMENT_MISMATCH", "FND_EXIT_RULES_MISSING" },
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Существенная доля может остаться у человека, который больше не участвует в компании",
            Finding = "По вашим ответам полная доля одного из основателей не связана с продолжением его участия, при этом его вклад уже ниже ожидаемого, он неактивен или покинул проект.",
            WhyItMatters = "Это может повлиять на управление компанией, мотивацию действующей команды и будущую инвестиционную проверку.",
            Recommendation = "Определить, какая часть доли должна зависеть от продолжения участия, и согласовать механизм выкупа.",
            AffectedDimensions = new() { "early_exit_equity", "commitment" },
            Recommendations = new() {
                "Определить, какая часть доли должна зависеть от продолжения участия.",
                "Согласовать последствия обычного и проблемного ухода.",
                "Оформить согласованный механизм в документах."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DEADLOCK",
            SuppressCodes = new() { "FND_GOVERNANCE_AMBIGUITY", "FND_NO_DEADLOCK_PROTECTION" },
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Компания может оказаться неспособной принять ключевое решение",
            Finding = "У основателей сопоставимый контроль, существенные решения требуют совместного согласия, а понятный механизм выхода из тупиковой ситуации не определен.",
            WhyItMatters = "При серьезном разногласии риск состоит не только в конфликте, но и в фактической неспособности компании принять решение о финансировании, стратегии или другой критичной операции.",
            Recommendation = "Определить перечень совместных решений и зафиксировать правила разрешения тупика.",
            AffectedDimensions = new() { "deadlock" },
            Recommendations = new() {
                "Определить перечень решений, где действительно необходимо совместное согласие.",
                "Согласовать этапы разрешения тупика и конечный механизм.",
                "Закрепить правила в документах между основателями и корпоративных документах."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DEPARTED_UNRESOLVED",
            SuppressCodes = new() { "FND_EXIT_RULES_MISSING" },
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Уход одного из основателей юридически не завершен",
            Finding = "Человек уже перестал активно участвовать в компании, но его доля, полномочия, обязательства или иные последствия выхода остаются неурегулированными.",
            WhyItMatters = "Нерешенный выход может блокировать решения, создавать спор о долях и стать отдельным вопросом при инвестиционной проверке.",
            Recommendation = "Определить права ушедшего основателя и юридически закрыть передачу дел и доли.",
            AffectedDimensions = new() { "early_exit_equity", "exit_continuity" },
            Recommendations = new() {
                "Определить текущие права и полномочия ушедшего основателя.",
                "Урегулировать судьбу доли и передачу дел.",
                "Синхронизировать договоренности с корпоративными документами и доступами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_CONFLICT_OF_INTEREST",
            RootCauseGroup = "FOUNDER_CONFLICT_OF_INTEREST",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Сторонняя деятельность основателя может пересекаться с интересами компании",
            Finding = "Один из основателей ведет или может вести деятельность, которая пересекается с бизнесом компании, а правила такого пересечения определены не полностью.",
            WhyItMatters = "Это может создавать спор о приоритетах, клиентах, технологиях или результатах работы и дополнительно влиять на права на продукт.",
            Recommendation = "Определить допустимые и недопустимые пересечения и зафиксировать правила конфликтов интересов.",
            AffectedDimensions = new() { "conflict_of_interest" },
            Recommendations = new() {
                "Определить допустимые и недопустимые пересечения.",
                "Проверить обязательства перед внешним работодателем или другим бизнесом.",
                "Зафиксировать правила конфликтов интересов и использования результатов работы."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_ROLE_AMBIGUITY",
            RootCauseGroup = "FOUNDER_GOVERNANCE",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Ответственность за часть ключевых функций распределена не полностью",
            Finding = "По вашим ответам роли основателей понятны лишь частично либо значительная часть функций фактически остается общей.",
            WhyItMatters = "На ранней стадии это может работать неформально, но при росте повышает вероятность споров о полномочиях и ответственности.",
            Recommendation = "Определить владельца каждой ключевой функции и зафиксировать согласованную модель.",
            AffectedDimensions = new() { "roles" },
            Recommendations = new() {
                "Определить владельца каждой ключевой функции.",
                "Разделить операционные и совместные решения.",
                "Зафиксировать согласованную модель в документах."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_COMMITMENT_MISMATCH",
            RootCauseGroup = "FOUNDER_COMMITMENT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Фактический вклад одного из основателей ниже ожидаемого",
            Finding = "Участие одного или нескольких основателей заметно отличается от согласованного объема, а специальные правила на такой случай не определены.",
            WhyItMatters = "Если вклад и доля расходятся длительное время, это может привести к конфликту и проблеме неактивной доли.",
            Recommendation = "Сверить ожидаемую и фактическую занятость и проверить связь с долей.",
            AffectedDimensions = new() { "commitment" },
            Recommendations = new() {
                "Сверить ожидаемую и фактическую занятость.",
                "Согласовать срок и условия восстановления участия либо иной сценарий.",
                "Проверить, как эта ситуация связана с долей и правилами ухода."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EQUITY_NOT_FORMALIZED",
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Договоренность о долях не полностью оформлена",
            Finding = "Основатели в целом понимают распределение долей, но существующая договоренность подтверждается только частично или не доведена до юридического оформления.",
            WhyItMatters = "При изменении отношений или появлении инвестора устная либо предварительная договоренность может оказаться недостаточной для подтверждения структуры.",
            Recommendation = "Собрать текущую договоренность и оформить итоговую структуру в применимых документах.",
            AffectedDimensions = new() { "equity_clarity" },
            Recommendations = new() {
                "Собрать текущую договоренность в одном месте.",
                "Сопоставить ее с зарегистрированными правами.",
                "Оформить итоговую структуру в применимых документах."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EQUITY_AMBIGUITY",
            SuppressCodes = new() { "FND_EQUITY_NOT_FORMALIZED" },
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "По долям существуют несколько несовпадающих договоренностей",
            Finding = "По вашим ответам есть разные обещания или неясность относительно распределения долей между основателями.",
            WhyItMatters = "Это может привести к спору о собственности и усложнить корпоративные изменения или инвестиционный раунд.",
            Recommendation = "Собрать все обещания, определить единую структуру и синхронизировать с корпоративными документами.",
            AffectedDimensions = new() { "equity_clarity" },
            Recommendations = new() {
                "Собрать все обещания и версии договоренностей.",
                "Определить единую согласованную структуру.",
                "Синхронизировать ее с корпоративными документами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_NO_VESTING",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Полная доля не связана с продолжением участия основателя",
            Finding = "Сейчас основатель сохраняет всю долю независимо от того, как долго он продолжает работать над компанией.",
            WhyItMatters = "Пока все активно участвуют, это может не создавать непосредственной проблемы, но при раннем уходе в структуре капитала может остаться крупная доля неактивного участника.",
            Recommendation = "Обсудить механизм связи доли с продолжением участия и оформить согласованную модель.",
            AffectedDimensions = new() { "early_exit_equity" },
            Recommendations = new() {
                "Обсудить механизм связи доли с продолжением участия.",
                "Определить последствия раннего ухода.",
                "Оформить согласованную модель."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_INCOMPLETE_LEAVER_RULES",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Последствия разных сценариев ухода основателя определены не полностью",
            Finding = "Компания не полностью различает обычный добровольный уход и уход вследствие серьезного нарушения обязательств.",
            WhyItMatters = "Без заранее согласованных правил один и тот же механизм может применяться к существенно разным ситуациям и стать источником спора.",
            Recommendation = "Определить основные сценарии ухода и зафиксировать правила в документах.",
            AffectedDimensions = new() { "early_exit_equity" },
            Recommendations = new() {
                "Определить основные сценарии ухода.",
                "Согласовать последствия для доли, полномочий и передачи дел.",
                "Закрепить правила в документах."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_GOVERNANCE_AMBIGUITY",
            RootCauseGroup = "FOUNDER_GOVERNANCE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила принятия решений между основателями определены не полностью",
            Finding = "Не по всем существенным вопросам понятно, кто может решать самостоятельно и где требуется совместное согласие.",
            WhyItMatters = "При росте числа решений и обязательств это повышает риск споров о полномочиях и замедляет управление.",
            Recommendation = "Разделить операционные и ключевые совместные решения и определить пороги согласования.",
            AffectedDimensions = new() { "governance" },
            Recommendations = new() {
                "Разделить операционные и ключевые совместные решения.",
                "Определить пороги согласования.",
                "Синхронизировать договоренности с корпоративными полномочиями."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_NO_DEADLOCK_PROTECTION",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Не определен порядок действий при серьезном тупике между основателями",
            Finding = "По вашим ответам специального механизма на случай, если основатели не смогут договориться, нет либо он не доводит ситуацию до окончательного решения.",
            WhyItMatters = "При реальном конфликте переговоров может оказаться недостаточно для продолжения работы компании.",
            Recommendation = "Определить этапы эскалации и закрепить механизм разрешения тупика письменно.",
            AffectedDimensions = new() { "deadlock" },
            Recommendations = new() {
                "Определить этапы эскалации.",
                "Согласовать финальный способ выхода из тупика.",
                "Закрепить механизм письменно."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EXIT_RULES_MISSING",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила выхода основателя определены не полностью",
            Finding = "Заранее не определены все ключевые действия при уходе основателя: уведомление, передача дел, полномочия и судьба доли.",
            WhyItMatters = "Уход в таком случае приходится урегулировать уже после возникновения интересов сторон, что повышает вероятность конфликта.",
            Recommendation = "Определить процедуру выхода, связать ее с долей и предусмотреть передачу дел.",
            AffectedDimensions = new() { "exit_continuity" },
            Recommendations = new() {
                "Определить процедуру выхода.",
                "Связать ее с долей и полномочиями.",
                "Предусмотреть передачу дел и доступов."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_CONTRIBUTION_AMBIGUITY",
            RootCauseGroup = "FOUNDER_FINANCING",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Личные вложения основателей учитываются неоднозначно",
            Finding = "В компанию вложены личные средства, но их статус как займа, вклада или расходов определен не полностью.",
            WhyItMatters = "В дальнейшем это может создать разные ожидания о возврате денег и правах основателей.",
            Recommendation = "Собрать историю вложений и оформить подтверждающие решения или договоры займа/вклада.",
            AffectedDimensions = new() { "founder_contributions" },
            Recommendations = new() {
                "Собрать историю личных вложений.",
                "Определить юридический статус каждой существенной суммы.",
                "Оформить подтверждающие решения или договоры."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_STRATEGIC_MISALIGNMENT",
            RootCauseGroup = "FOUNDER_STRATEGY",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "У основателей различаются ожидания относительно будущего компании",
            Finding = "По вашим ответам есть существенные различия во взглядах на инвестиции, темп роста или возможную продажу компании.",
            WhyItMatters = "Такие различия способны перейти из стратегической дискуссии в спор о финансировании и управлении.",
            Recommendation = "Обсудить ключевые сценарии роста и зафиксировать договоренности, влияющие на управление.",
            AffectedDimensions = new() { "strategic_alignment" },
            Recommendations = new() {
                "Обсудить ключевые сценарии роста и финансирования.",
                "Определить решения, требующие общего согласия.",
                "Зафиксировать договоренности, влияющие на управление и выход."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DOCUMENTATION_GAP",
            RootCauseGroup = "FOUNDER_DOCUMENTATION",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила между основателями существуют, но закреплены не полностью",
            Finding = "Основные договоренности могут быть понятны участникам, однако система не видит подтверждения, что они собраны в подписанных документах.",
            WhyItMatters = "При изменении отношений доказать содержание устной договоренности или переписки сложнее, чем заранее оформленные правила.",
            Recommendation = "Собрать действующие договоренности и оформить единый согласованный набор правил.",
            AffectedDimensions = new() { "governance", "early_exit_equity" },
            Recommendations = new() {
                "Собрать действующие договоренности.",
                "Устранить противоречия между документами и перепиской.",
                "Оформить единый согласованный набор правил."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "FOUNDERS_REVIEW"
        },
    };
}
