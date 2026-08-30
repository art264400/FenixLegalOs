using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class TeamRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «КОМАНДА И СОТРУДНИКИ» (v1.1)
        // =====================================================================
        new() {
            Code = "TEAM_NO_WRITTEN_AGREEMENTS",
            RootCauseGroup = "TEAM_AGREEMENTS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Часть команды регулярно работает без достаточного письменного оформления",
            Finding = "Система видит, что с частью людей, регулярно работающих на компанию, письменные условия отсутствуют или охватывают только часть команды.",
            WhyItMatters = "Без ясных документов сложнее подтвердить обязанности, оплату, конфиденциальность, права на результаты и порядок прекращения сотрудничества.",
            Recommendation = "Определить людей без письменных условий и в первую очередь закрыть ключевые роли.",
            AffectedDimensions = new() { "written_agreements" },
            Recommendations = new() {
                "Определить людей без письменных условий.",
                "В первую очередь закрыть ключевые роли.",
                "Использовать единый набор базовых условий для новых участников команды."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_KEY_PERSON_UNDOCUMENTED",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Отношения с ключевым участником команды оформлены недостаточно",
            Finding = "Один из людей, от которого существенно зависит продукт или бизнес, работает без достаточно понятных письменных условий.",
            WhyItMatters = "Проблема особенно значима, если этот человек контролирует ключевые знания, доступы или создает важную часть продукта.",
            Recommendation = "Проверить договор с ключевым участником и урегулировать обязанности, конфиденциальность и IP.",
            AffectedDimensions = new() { "written_agreements", "key_person_dependency" },
            SuppressCodes = new() { "TEAM_NO_WRITTEN_AGREEMENTS", "TEAM_UNCLEAR_TERMS", "TEAM_CONFIDENTIALITY_GAP" },
            Recommendations = new() {
                "Проверить договор с ключевым участником.",
                "Урегулировать обязанности, конфиденциальность, результаты работы и прекращение отношений.",
                "Проверить корпоративный контроль над доступами и знаниями."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_WORK_FORMAT_MISMATCH",
            RootCauseGroup = "TEAM_WORK_FORMAT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Фактический формат работы части специалистов может отличаться от оформления",
            Finding = "Некоторые внешние специалисты по факту работают как постоянная часть команды: регулярно, под управлением компании и на длительной основе.",
            WhyItMatters = "Юридическая квалификация такой модели зависит от страны и фактических обстоятельств; ее стоит проверить отдельно, а не полагаться только на название договора.",
            Recommendation = "Выделить специалистов с постоянным форматом работы и сопоставить фактические условия с выбранной формой сотрудничества.",
            AffectedDimensions = new() { "work_format" },
            Recommendations = new() {
                "Выделить специалистов с постоянным форматом работы.",
                "Сопоставить фактические условия с выбранной формой сотрудничества.",
                "Проверить модель с учетом страны компании и человека."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_UNCLEAR_TERMS",
            RootCauseGroup = "TEAM_AGREEMENTS",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Договоры с частью команды мало отражают реальную работу",
            Finding = "Важные условия о задачах, оплате или прекращении сотрудничества остаются в переписке либо договоры сформулированы слишком общо.",
            WhyItMatters = "Это повышает риск разного понимания того, что человек должен сделать и когда отношения могут закончиться.",
            Recommendation = "Сопоставить договоры с реальными ролями и добавить ключевые обязанности и коммерческие условия.",
            AffectedDimensions = new() { "terms_clarity" },
            Recommendations = new() {
                "Сопоставить договоры с реальными ролями.",
                "Добавить ключевые обязанности и коммерческие условия.",
                "Убрать критичные договоренности из устной коммуникации."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_CONFIDENTIALITY_GAP",
            RootCauseGroup = "TEAM_CONFIDENTIALITY",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Не вся команда с доступом к внутренней информации связана правилами конфиденциальности",
            Finding = "Часть людей получает код, клиентскую, финансовую или другую внутреннюю информацию без достаточно понятных обязательств по ее защите.",
            WhyItMatters = "При прекращении отношений или передаче информации третьим лицам компании будет сложнее ссылаться на заранее согласованные ограничения.",
            Recommendation = "Определить категории конфиденциальной информации и включить правила NDA в договоры.",
            AffectedDimensions = new() { "confidentiality" },
            Recommendations = new() {
                "Определить категории конфиденциальной информации.",
                "Проверить, у каких участников команды отсутствуют соответствующие условия.",
                "Включить правила в договоры и процедуру ухода."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_RIGHTS_TO_WORK_GAP",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Права на результаты работы части команды определены не полностью",
            Finding = "Сотрудники или подрядчики создают важные результаты, но из их документов не всегда понятно, кому принадлежит созданное.",
            WhyItMatters = "Для технологической компании этот пробел может перейти из кадрового вопроса в проблему прав на продукт.",
            Recommendation = "Определить создателей ключевых результатов и проверить документы о переходе прав.",
            AffectedDimensions = new() { "work_rights" },
            Recommendations = new() {
                "Определить создателей ключевых результатов.",
                "Передать сигнал в модуль прав на продукт и проверить документы.",
                "Закрыть пробелы по ключевым участникам команды."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "TEAM_ACCESS_CONTROL_GAP",
            RootCauseGroup = "TEAM_ACCESS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Компания не имеет единой картины доступов команды",
            Finding = "Доступы к коду, серверам, клиентским данным или другим важным системам выдаются без единого учета либо их нельзя быстро восстановить по списку.",
            WhyItMatters = "При изменении состава команды это повышает риск сохранения лишних доступов и зависимости от отдельных людей.",
            Recommendation = "Составить список критических систем и владельцев доступов с минимально необходимыми уровнями.",
            AffectedDimensions = new() { "access_accounts" },
            Recommendations = new() {
                "Составить список критических систем и владельцев доступов.",
                "Определить минимально необходимые уровни доступа.",
                "Связать выдачу и отзыв доступов с приходом и уходом людей."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_PERSONAL_ACCOUNT_DEPENDENCY",
            RootCauseGroup = "TEAM_ACCESS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Важные рабочие сервисы зависят от личных аккаунтов участников команды",
            Finding = "Несколько значимых сервисов используются через личные учетные записи, а корпоративный контроль ограничен.",
            WhyItMatters = "Если человек уйдет или потеряет доступ, компания может столкнуться с остановкой работы или сложным восстановлением контроля.",
            Recommendation = "Перенести критические сервисы под корпоративные аккаунты и настроить резервных администраторов.",
            AffectedDimensions = new() { "access_accounts" },
            Recommendations = new() {
                "Перенести критические сервисы под корпоративные аккаунты.",
                "Настроить резервных администраторов.",
                "Проверять этот вопрос при каждом уходе."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_OFFBOARDING_GAP",
            RootCauseGroup = "TEAM_OFFBOARDING",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Порядок действий при уходе участника команды не стандартизирован",
            Finding = "Передача дел, файлов, оборудования и закрытие доступов выполняются по ситуации либо зависят от конкретного руководителя.",
            WhyItMatters = "Это увеличивает вероятность того, что после ухода останутся доступы, данные или незавершенная передача знаний.",
            Recommendation = "Сформировать обязательный чек-лист действий при уходе и назначить владельца процесса.",
            AffectedDimensions = new() { "offboarding" },
            Recommendations = new() {
                "Сформировать короткий обязательный список действий при уходе.",
                "Назначить владельца процесса.",
                "Связать его с доступами, оборудованием, информацией и результатами работы."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_FORMER_ACCESS_RISK",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Бывший участник команды может сохранять доступ к важным системам или информации",
            Finding = "По вашим ответам нельзя подтвердить, что после прекращения сотрудничества все критичные доступы были закрыты.",
            WhyItMatters = "Это текущий, а не теоретический риск: бывший участник может сохранять возможность доступа к продукту, данным или рабочей инфраструктуре.",
            Recommendation = "Немедленно проверить активные учетные записи и ключи доступа бывшего участника и закрыть доступы.",
            AffectedDimensions = new() { "former_people", "access_accounts" },
            Recommendations = new() {
                "Немедленно проверить его активные учетные записи и ключи доступа.",
                "Закрыть или заменить критические доступы.",
                "Проверить журналы доступа и обновить процедуру ухода."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_KEY_PERSON_DEPENDENCY",
            RootCauseGroup = "KEY_PERSON",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Важная часть бизнеса зависит от одного человека",
            Finding = "Ключевые знания, функции или доступы сосредоточены у одного участника команды, и быстрая передача его работы затруднена.",
            WhyItMatters = "Внезапный уход такого человека может остановить важную часть продукта или операций.",
            Recommendation = "Определить незаменимые знания и доступы, документировать процессы и подготовить план передачи функций.",
            AffectedDimensions = new() { "key_person_dependency" },
            Recommendations = new() {
                "Определить знания и доступы, которые нельзя быстро заменить.",
                "Документировать ключевые процессы.",
                "Создать резервный доступ и план передачи функций."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_FOREIGN_TEAM_REVIEW",
            RootCauseGroup = "TEAM_CROSS_BORDER",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Международные отношения с командой требуют отдельной проверки",
            Finding = "Часть сотрудников или постоянных подрядчиков работает из другой страны, а применимость местных правил отдельно не проверялась.",
            WhyItMatters = "Формат сотрудничества, налоги и трудовые последствия могут зависеть от конкретных стран и фактических условий работы.",
            Recommendation = "Определить страны и статус ключевых участников и проверить договорную модель по существенным юрисдикциям.",
            AffectedDimensions = new() { "foreign_team" },
            Recommendations = new() {
                "Определить страны и статус ключевых участников.",
                "Проверить договорную модель по существенным юрисдикциям.",
                "При необходимости адаптировать договоры и процесс оплаты."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "TEAM_LEGAL_REVIEW"
        },
        new() {
            Code = "TEAM_EQUITY_PROMISE",
            RootCauseGroup = "EQUITY_PROMISE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "team",
            Modules = new() { "team" },
            Title = "Участнику команды обещана доля, но условия закреплены не полностью",
            Finding = "Сотруднику, подрядчику или советнику обещано участие в капитале, однако размер, условия или окончательное оформление остаются неясными.",
            WhyItMatters = "Такое обещание может стать спором с человеком и должно учитываться при расчете будущей структуры капитала.",
            Recommendation = "Собрать все обещания долей команде, зафиксировать условия и отразить обязательства в единой таблице капитала.",
            AffectedDimensions = new() { "team_equity" },
            Recommendations = new() {
                "Собрать все обещания долей команде.",
                "Зафиксировать размер и условия получения.",
                "Передать обязательство в корпоративную таблицу капитала и инвестиционные расчеты."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CORPORATE_CLEANUP"
        }
    };
}
