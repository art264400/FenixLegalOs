using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.ActionLibrary;

/// <summary>
/// Канонический каталог детерминированных корректирующих юридических действий (ActionLibrary vNext).
/// Каждое действие имеет уникальный ActionId, специфичный BusinessReason, RequiredOutcome, ResolutionMode и Dependencies.
/// </summary>
public static class ActionLibrary
{
    public static readonly IReadOnlyList<ActionDefinition> All = new List<ActionDefinition>
    {
        // =====================================================================
        // 1. СООСНОВАТЕЛИ (FOUNDERS)
        // =====================================================================
        new()
        {
            ActionId = "ACT_FOUNDER_DEADLOCK_RESOLVE",
            Title = "Утвердить регламент разрешения корпоративных разногласий и тупиков",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "founders",
            BusinessReason = "При равенстве долей 50/50 или совместном голосовании любое неразрешимое разногласие ведет к угрозе взаимной блокировки управления, способно полностью парализовать операционную деятельность и сорвать инвестиционный раунд.",
            RequiredOutcome = "В корпоративный договор внедрен четкий регламент разрешения тупиковых ситуаций (процедура эскалации, привлечение нейтрального медиатора и правила выкупа доли при недостижении согласия).",
            WhatToDo = "Разработать и подписать положение о порядке преодоления тупиковых ситуаций в соглашении основателей.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_DEADLOCK_RISK", "FND_DEADLOCK", "FND_GOVERNANCE_GAP", "FND_NO_DEADLOCK_PROTECTION", "FND_GOVERNANCE_AMBIGUITY" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_AGREEMENT_SHA",
            Title = "Разработать и подписать корпоративный договор сооснователей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "founders",
            BusinessReason = "Устные договоренности или несистематизированные переписки сложно однозначно доказать в спорной ситуации, что создает риск разногласий по структуре владения при росте оценки компании.",
            RequiredOutcome = "Подписан юридически обязывающий документ, комплексно фиксирующий доли, порядок голосования, ограничения на продажу долей третьим лицам и ключевые обязательства сторон.",
            WhatToDo = "Подготовить проект соглашения между основателями, согласовать существенные условия и зафиксировать подписями всех участников.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_DOCUMENTATION_GAP", "FND_EQUITY_NOT_FORMALIZED", "FND_EQUITY_AMBIGUITY", "FND_NO_AGREEMENT", "FND_STRATEGIC_MISALIGNMENT" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_VESTING_LEAVER",
            Title = "Внедрить механизм поэтапного закрепления долей (вестинг) и порядок выхода сооснователей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            BusinessReason = "Если основатель прекратит участие на раннем этапе, сохраняя полную долю в компании (dead equity), это заблокирует пул долей для новых партнеров и существенно осложнит переговоры с венчурными инвесторами.",
            RequiredOutcome = "В корпоративном договоре закреплен график поэтапного перехода прав на доли в зависимости от срока и вклада основателя, а также правила выкупа долей при добровольном или вынужденном выходе из проекта.",
            WhatToDo = "Включить положения о вестинге и правах выкупа долей при уходе основателя в корпоративный договор.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA" },
            SupportedFindingCodes = new() { "FND_NO_VESTING", "FND_EXIT_UNREGULATED", "FND_LEAVER_UNPROTECTED", "FND_DEAD_EQUITY", "FND_INCOMPLETE_LEAVER_RULES", "FND_EXIT_RULES_MISSING" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_DISPUTE_SETTLE",
            Title = "Юридически зафиксировать и урегулировать открытые разногласия между основателями",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "founders",
            BusinessReason = "Наличие неурегулированного конфликта по долям или деньгам блокирует любые корпоративные действия, привлечение финансирования и регистрацию прав на компанию.",
            RequiredOutcome = "Подписано соглашение об урегулировании разногласий или оформлен официальный выход участника с полным прекращением взаимных финансовых и имущественных претензий.",
            WhatToDo = "Зафиксировать позиции сторон с привлечением юриста и подписать соглашение о разделе долей либо мировое соглашение о выходе.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_ACTIVE_DISPUTE", "FND_EQUITY_DISPUTE", "FND_DEPARTED_UNRESOLVED" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_ROLES_COMMITMENT",
            Title = "Формализовать зоны ответственности и минимальный объем вовлеченности основателей",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            BusinessReason = "Размытые ожидания по занятости (full-time vs part-time) ведут к скрытому недовольству и разрушению команды на этапе интенсивного масштабирования.",
            RequiredOutcome = "Зафиксирован документ с матрицей ключевых обязанностей (RACI), графиком вовлеченности и процедурой пересмотра условий при изменении статуса основателя.",
            WhatToDo = "Согласовать и подписать внутренний регламент распределения ролей и подтверждения ключевых обязательств.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_ROLE_AMBIGUITY", "FND_COMMITMENT_MISMATCH" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_PERSONAL_INVESTMENTS",
            Title = "Оформить личные инвестиции и займы основателей надлежащими договорами",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            BusinessReason = "Вложенные личные средства без подтверждающих документов создают неясность структуры баланса и риски налоговых претензий при возврате средств.",
            RequiredOutcome = "Все личные займы и вклады основателей подтверждены договорами процентного/беспроцентного займа либо оформлены как вклад в добавочный капитал.",
            WhatToDo = "Собрать выписки и платежные поручения и подписать договоры займа между основателями и компанией.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_PERSONAL_INVESTMENT_UNRECORDED", "FND_LOAN_NOT_DOCUMENTED", "FND_CONTRIBUTION_AMBIGUITY" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_CONFLICT_OF_INTEREST",
            Title = "Утвердить правила разрешения конфликта интересов и внешней занятости основателей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "founders",
            BusinessReason = "Сторонняя деятельность основателя может пересекаться с бизнесом компании и создавать споры о приоритетах, клиентах, технологиях и правах на продукт.",
            RequiredOutcome = "В соглашении основателей закреплены четкие правила допустимых и недопустимых внешних проектов, порядок раскрытия конфликта интересов и принадлежность создаваемых разработок.",
            WhatToDo = "Определить допустимые пересечения, проверить обязательства перед внешними работодателями и зафиксировать правила конфликта интересов.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA" },
            SupportedFindingCodes = new() { "FND_CONFLICT_OF_INTEREST" }
        },

        // =====================================================================
        // 2. КОРПОРАТИВНАЯ СТРУКТУРА (CORPORATE)
        // =====================================================================
        new()
        {
            ActionId = "ACT_CORP_INCORPORATION",
            Title = "Зарегистрировать юридическое лицо под операционную деятельность проекта",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "corporate",
            BusinessReason = "Ведение коммерческой деятельности, прием платежей и привлечение подрядчиков без юридического лица возлагает полную личную и неограниченную ответственность на физических лиц.",
            RequiredOutcome = "Зарегистрирована компания (ТОО / ООО / C-Corp / Ltd) в целевой юрисдикции, открыты расчетные счета и разграничена личная ответственность основателей.",
            WhatToDo = "Выбрать целевую юрисдикцию, подготовить учредительные документы и зарегистрировать компанию.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA" },
            SupportedFindingCodes = new() { "COR_NO_ENTITY_FOR_ACTIVITY", "COR_NO_ENTITY" }
        },
        new()
        {
            ActionId = "ACT_CORP_CAP_TABLE_CLEANUP",
            Title = "Актуализировать и юридически выверить структуру владения долями компании",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "corporate",
            BusinessReason = "Расхождение между зарегистрированным составом участников и фактическими договоренностями является прямым стоп-фактором при юридической проверке инвестором (Due Diligence).",
            RequiredOutcome = "Сформирована единая актуальная таблица капитала (Cap Table), и корпоративные документы согласованы с фактическим распределением долей и опционных обязательств.",
            WhatToDo = "Свести все соглашения, опционы и инвестиционные обязательства в единую таблицу капитала (Cap Table) и при необходимости оформить соответствующие корпоративные решения.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "COR_OWNERSHIP_DISPUTE", "COR_OWNERSHIP_MISMATCH", "COR_UNDOCUMENTED_EQUITY", "COR_CAP_TABLE_UNCLEAR", "COR_CAP_TABLE_UNRELIABLE" }
        },
        new()
        {
            ActionId = "ACT_CORP_GOVERNANCE_SYSTEMATIZE",
            Title = "Систематизировать корпоративные решения и зафиксировать полномочия руководителя",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "corporate",
            BusinessReason = "Отсутствие протоколов ключевых решений и неопределенность лимитов полномочий директора создают риски признания совершенных сделок недействительными.",
            RequiredOutcome = "Сформирован полный архив решений общих собраний участников, в уставе закреплены четкие лимиты на совершение крупных сделок и одобрение ключевых договоров.",
            WhatToDo = "Провести инвентаризацию решений участников, оформить недостающие протоколы и утвердить регламент полномочий директора.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "COR_GOVERNANCE_GAP", "COR_SIGNATORY_UNCLEAR", "COR_DIRECTOR_POWER_UNCLEAR", "COR_DECISIONS_UNSYSTEMATIC", "COR_APPROVAL_GAP", "COR_AUTHORITY_GAP" }
        },
        new()
        {
            ActionId = "ACT_CORP_HOLDING_STRUCTURING",
            Title = "Разработать холдинговую архитектуру для разделения IP-активов и операционных рисков",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "corporate",
            BusinessReason = "Совмещение рискованной операционной деятельности и владения ключевыми нематериальными активами в одном юрлице создает угрозу потери прав при судебных претензиях клиентов.",
            RequiredOutcome = "Построена 2-уровневая структура (HoldCo для владения IP и привлечения инвестиций + OpCo для местных продаж и найма) с лицензионными соглашениями.",
            WhatToDo = "Разработать модель корпоративного владения с учетом налогового законодательства и целевых юрисдикций инвесторов.",
            Dependencies = new() { "ACT_CORP_INCORPORATION" },
            SupportedFindingCodes = new() { "COR_HOLDING_GAP", "COR_JURISDICTION_MISMATCH" }
        },
        new()
        {
            ActionId = "ACT_CORP_ASSET_BENEFICIARY_ALIGNMENT",
            Title = "Консолидировать активы на операционной компании и формализовать скрытый контроль",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "corporate",
            BusinessReason = "Оформление активов вне операционной компании или наличие неформализованного контроля блокирует институциональные инвестиции и банковский комплаенс.",
            RequiredOutcome = "Все ключевые права, активы и коммерческие договоры переведены на операционную компанию, а фактический контроль и доли участников юридически оформлены.",
            WhatToDo = "Провести аудит нахождения прав и ключевых договоров, перевести их на компанию проекта и формализовать реальную структуру владения.",
            Dependencies = new() { "ACT_CORP_INCORPORATION" },
            SupportedFindingCodes = new() { "COR_ENTITY_MISMATCH", "COR_HIDDEN_CONTROL" }
        },
        new()
        {
            ActionId = "ACT_CORP_DOCUMENT_ARCHIVE_SYSTEMATIZE",
            Title = "Восстановить историю капитала и систематизировать корпоративный архив документов",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "corporate",
            BusinessReason = "Разрозненность корпоративных документов и неполная история перехода долей затягивают проверку инвесторами и повышают риск юридических дефектов.",
            RequiredOutcome = "Собраны оригиналы и скан-копии учредительных документов, решений и подтверждений изменений капитала, организован структурированный корпоративный архив.",
            WhatToDo = "Собрать документы по каждому изменению капитала, восстановить недостающие решения и сформировать структурированный архив компании.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "COR_CORPORATE_HISTORY_GAP", "COR_RECORDS_GAP" }
        },

        // =====================================================================
        // 3. ИНТЕЛЛЕКТУАЛЬНАЯ СОБСТВЕННОСТЬ (IP)
        // =====================================================================
        new()
        {
            ActionId = "ACT_IP_FOUNDER_ASSIGNMENT",
            Title = "Оформить передачу исключительных прав на исходный код и дизайн от основателей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "ip",
            BusinessReason = "По закону авторские права первоначально возникают у физических лиц — создателей. Без письменного договора отчуждения прав компания не владеет собственным продуктом.",
            RequiredOutcome = "Подписаны договоры об отчуждении исключительных прав (IP Assignment) с каждым основателем с актами приема-передачи исходного кода, документации и дизайна.",
            WhatToDo = "Подготовить договоры отчуждения исключительных прав с подробным описанием переданных разработок и подписать акты.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", "IP_FOUNDER_RIGHTS_MISSING", "IP_FOUNDERS_NOT_ASSIGNED" }
        },
        new()
        {
            ActionId = "ACT_IP_CONTRACTOR_ASSIGNMENT",
            Title = "Оформить договоры авторского заказа и акты передачи прав с внешними подрядчиками",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "ip",
            BusinessReason = "Привлечение фрилансеров и студий без условия о передаче исключительных прав оставляет за ними право заблокировать использование кода или потребовать повторной оплаты.",
            RequiredOutcome = "Со всеми внешними разработчиками заключены договоры авторского заказа с полной передачей исключительных прав и подписаны закрывающие акты по выполненным этапам.",
            WhatToDo = "Собрать список всех привлеченных специалистов, подписать соглашения о передаче прав и акты приема-передачи исходных материалов.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_CONTRACTOR_RIGHTS_GAP", "IP_CONTRACTOR_RIGHTS_MISSING", "IP_STUDIO_RIGHTS_GAP", "IP_FORMER_DEVELOPER_GAP" }
        },
        new()
        {
            ActionId = "ACT_IP_CONSOLIDATION_AUDIT",
            Title = "Провести инвентаризацию и подтвердить цепочку прав на все компоненты продукта",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "ip",
            BusinessReason = "Любой разрыв в цепочке передачи прав делает актив уязвимым и выявляется инвесторами на первом же этапе технической и юридической проверки.",
            RequiredOutcome = "Сформирован актуальный реестр нематериальных активов компании с подтверждением перехода прав на ядро продукта, базы данных, архитектуру и дизайн.",
            WhatToDo = "Сопоставить состав программного стека с имеющимися договорами и устранить выявленные белые пятна.",
            Dependencies = new() { "ACT_IP_FOUNDER_ASSIGNMENT", "ACT_IP_CONTRACTOR_ASSIGNMENT" },
            SupportedFindingCodes = new() { "IP_PRODUCT_RIGHTS_UNCONFIRMED", "IP_RIGHTS_NOT_TRANSFERRED", "IP_CHAIN_OF_TITLE_BROKEN" }
        },
        new()
        {
            ActionId = "ACT_IP_OPEN_SOURCE_COMPLIANCE",
            Title = "Провести аудит лицензионной чистоты Open Source компонентов и стороннего контента",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "ip",
            BusinessReason = "Использование библиотек с вирусными лицензиями (GPL, AGPL) может обязать компанию раскрыть весь проприетарный коммерческий исходный код продукта.",
            RequiredOutcome = "Составлен перечень используемых сторонних библиотек (SBoM) с подтверждением их совместимости с закрытой коммерческой моделью монетизации продукта.",
            WhatToDo = "Запустить сканирование зависимостей проекта и проверить юридические условия лицензий сторонних модулей.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_OPEN_SOURCE_RISK", "IP_THIRD_PARTY_CONTENT_RISK", "IP_LICENSE_COMPLIANCE_GAP", "IP_THIRD_PARTY_COMPONENTS" }
        },
        new()
        {
            ActionId = "ACT_IP_EMPLOYER_CLEARANCE",
            Title = "Исключить риски прав работодателя на созданный продукт (Moonlighting / Release letter)",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "ip",
            BusinessReason = "Создание продукта параллельно с работой по найму создает угрозу иска бывшего работодателя о признании разработки служебным произведением.",
            RequiredOutcome = "Проведен аудит трудовых обязательств основателей, разграничены ресурсы и при необходимости получено письменное подтверждение работодателя об отсутствии претензий (Release letter).",
            WhatToDo = "Проверить трудовой договор и NDA основателя по основному месту работы и оформить подтверждение отсутствия пересечений и претензий.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_EMPLOYER_RISK" }
        },
        new()
        {
            ActionId = "ACT_IP_EXTERNAL_TECH_DEPENDENCY",
            Title = "Оценить риски зависимости ключевых функций продукта от внешних технологий и API",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.Now,
            SectionId = "ip",
            BusinessReason = "Зависимость ядра продукта от стороннего сервиса без гарантий доступности и запасного плана создает угрозу внезапной остановки бизнеса.",
            RequiredOutcome = "Определены критические внешние зависимости, проверены условия соглашений и разработан технический и договорный план резервирования.",
            WhatToDo = "Проверить условия использования и прекращения доступа к внешним сервисам и подготовить резервные сценарии замещения.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_EXTERNAL_DEPENDENCY" }
        },
        new()
        {
            ActionId = "ACT_IP_DOMAIN_BRAND_TRANSFER",
            Title = "Перенести домен и права на бренд на операционную компанию",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "ip",
            BusinessReason = "Нахождение домена или бренда на физическом лице создает зависимость от конкретного человека и блокирует оформление интеллектуальной собственности.",
            RequiredOutcome = "Доменные имена переведены под прямое управление корпоративного аккаунта компании с разграничением административного доступа.",
            WhatToDo = "Проверить текущих владельцев домена, оформить передачу домена и настроить двухфакторный корпоративный контроль.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_DOMAIN_BRAND_CONTROL" }
        },
        new()
        {
            ActionId = "ACT_IP_CONTENT_LICENSING_AUDIT",
            Title = "Провести аудит прав на контент, медиаматериалы и внешние базы данных",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.Now,
            SectionId = "ip",
            BusinessReason = "Использование чужого контента, изображений или датасетов без лицензии может привести к блокировке продукта и судебным искам правообладателей.",
            RequiredOutcome = "Проверены лицензии на все внешние датасеты и медиаматериалы, исключены сомнительные источники и оформлены лицензионные соглашения.",
            WhatToDo = "Определить источники ключевых материалов, проверить лицензии и заменить или оформить права на внешние данные.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_CONTENT_RIGHTS" }
        },
        new()
        {
            ActionId = "ACT_IP_TRADEMARK_PROTECTION",
            Title = "Подать заявки на регистрацию товарного знака в целевых юрисдикциях",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "ip",
            BusinessReason = "Без регистрации товарного знака конкуренты могут перехватить название продукта и потребовать ребрендинга или компенсации за нарушение прав на знак.",
            RequiredOutcome = "Поданы заявки на регистрацию словесного и комбинированного товарного знака в патентные ведомства ключевых стран присутствия.",
            WhatToDo = "Провести предварительный поиск на тождество и сходство и направить заявку на регистрацию товарного знака.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "IP_TRADEMARK_NOT_FILED", "IP_BRAND_UNPROTECTED", "IP_BRAND_REGISTRATION_INFO" }
        },

        // =====================================================================
        // 4. КОМАНДА И СОТРУДНИКИ (TEAM)
        // =====================================================================
        new()
        {
            ActionId = "ACT_TEAM_CONTRACTS_FORMALIZATION",
            Title = "Оформить письменные договоры с ключевыми специалистами и разработчиками",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "team",
            BusinessReason = "Работа команды и привлеченных специалистов без письменного оформления создает риски споров по оплате и правовой неопределенности в отношении созданного программного кода.",
            RequiredOutcome = "Со всеми постоянными членами команды заключены официальные трудовые договоры или договоры оказания услуг с разделами о служебных произведениях.",
            WhatToDo = "Внедрить типовой пакет договоров найма и привлечения подрядчиков с обязательным положением об отчуждении IP.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_NO_WRITTEN_CONTRACTS", "TEAM_NO_WRITTEN_AGREEMENTS", "TEAM_WRITTEN_AGREEMENTS_MISSING", "TEAM_ORAL_ONLY", "TEAM_KEY_PERSON_UNDOCUMENTED", "TEAM_UNCLEAR_TERMS" }
        },
        new()
        {
            ActionId = "ACT_TEAM_IP_TRANSFER_ACTS",
            Title = "Оформить переход прав на служебные произведения и разработки команды",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "team",
            BusinessReason = "Для подтверждения перехода исключительных прав на результаты интеллектуальной деятельности необходимо оформление актов приема-передачи или служебных заданий.",
            RequiredOutcome = "В компании внедрен регулярный регламент оформления служебных заданий и ежемесячных актов сдачи-приемки созданного кода и дизайна.",
            WhatToDo = "Настроить шаблон служебного задания и подписать закрывающие акты с разработчиками.",
            Dependencies = new() { "ACT_TEAM_CONTRACTS_FORMALIZATION" },
            SupportedFindingCodes = new() { "TEAM_RIGHTS_TO_WORK_GAP", "TEAM_IP_TRANSFER_GAP", "TEAM_IP_CREATION_UNRECORDED" }
        },
        new()
        {
            ActionId = "ACT_TEAM_RECLASSIFICATION_RISK",
            Title = "Минимизировать риски переквалификации отношений с подрядчиками в трудовые",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Признание отношений с самозанятыми/ИП трудовыми влечет доначисление налогов, социальных платежей и административные штрафы.",
            RequiredOutcome = "Из договоров с контрагентами исключены признаки трудового распорядка (фиксированные часы работы, подчинение графику, постоянное рабочее место).",
            WhatToDo = "Провести аудит договоров с внешними специалистами и скорректировать формулировки на предмет отсутствия трудовых признаков.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_WORK_FORMAT_MISMATCH", "TEAM_EMPLOYMENT_RECLASSIFICATION", "TEAM_LABOR_RECLASSIFICATION_RISK" }
        },
        new()
        {
            ActionId = "ACT_TEAM_OPTION_POOL_FORMALIZATION",
            Title = "Юридически структурировать программу опционного поощрения сотрудников (ESOP)",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "team",
            BusinessReason = "Устные обещания долей создают скрытые обязательства, искажают реальную структуру капитала и вызывают конфликты при оценке компании.",
            RequiredOutcome = "Утверждено официальное положение об опционной программе (Option Pool / Phantom Shares) с прозрачными правилами вестинга, клиффа и условий исполнения.",
            WhatToDo = "Разработать опционную документацию и согласовать размер пула с текущими участниками компании.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA" },
            SupportedFindingCodes = new() { "TEAM_ORAL_OPTION_PROMISES", "TEAM_EQUITY_PROMISE", "TEAM_OPTION_AMBIGUITY", "TEAM_ESOP_UNSTRUCTURED" }
        },
        new()
        {
            ActionId = "ACT_TEAM_ACCESS_LIST_AUDIT",
            Title = "Составить перечень критических информационных систем и администраторов доступов",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Отсутствие единого учета доступов к коду, серверам и базам данных повышает риск сохранения несанкционированных доступов и зависимости от отдельных лиц.",
            RequiredOutcome = "Сформирован и утвержден актуальный перечень критических систем с минимально необходимыми уровнями доступа и ответственными администраторами.",
            WhatToDo = "Провести ревизию всех используемых систем (Git, Cloud, DB, CRM), составить реестр доступов и ограничить права по принципу минимальной достаточности.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_ACCESS_CONTROL_GAP", "TEAM_ACCESS_TOO_BROAD", "DATA_ACCESS_TOO_BROAD" }
        },
        new()
        {
            ActionId = "ACT_TEAM_OFFBOARDING_CHECKLIST",
            Title = "Сформировать регламент и чек-лист действий при уходе участников команды",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Отсутствие стандартизированного процесса ухода сотрудников и подрядчиков приводит к сохранению рабочих доступов, утере документации и риску утечки данных.",
            RequiredOutcome = "Утвержден обязательный чек-лист ухода (отзыв доступов, передача репозиториев и оборудования, подписание закрывающих документов).",
            WhatToDo = "Разработать внутренний регламент офбординга и назначить ответственного за отзыв доступов при прекращении сотрудничества.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_OFFBOARDING_GAP", "TEAM_FORMER_ACCESS_RISK" }
        },
        new()
        {
            ActionId = "ACT_TEAM_PERSONAL_ACCOUNT_MIGRATION",
            Title = "Перенести сервисы с личных учетных записей на корпоративные аккаунты",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Использование личных аккаунтов для рабочих сервисов создает риск потери доступа к инфраструктуре при смене или уходе участников команды.",
            RequiredOutcome = "Все ключевые рабочие сервисы, домены и репозитории переведены на корпоративные учетные записи с резервным администрированием.",
            WhatToDo = "Провести инвентаризацию сервисов и перевести критические учетные записи под централизованный корпоративный контроль.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_PERSONAL_ACCOUNT_DEPENDENCY", "IP_ACCESS_CONTROL", "TEAM_KEY_PERSON_DEPENDENCY" }
        },
        new()
        {
            ActionId = "ACT_TEAM_NDA_ACCESS_CONTROL",
            Title = "Внедрить соглашения о конфиденциальности (NDA) и регламент разграничения доступов",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Отсутствие NDA и неконтролируемые доступы к репозиториям и базам данных создают угрозу утечки конфиденциальной информации при увольнении сотрудников.",
            RequiredOutcome = "Со всеми специалистами подписаны соглашения о неразглашении конфиденциальной информации и настроен ролевой доступ к критическим сервисам.",
            WhatToDo = "Подписать типовые NDA со всеми участниками команды и провести ревизию доступов к внутренним ресурсам компании.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_NO_NDA", "TEAM_CONFIDENTIALITY_GAP" }
        },
        new()
        {
            ActionId = "ACT_TEAM_FOREIGN_ARRANGEMENT_REVIEW",
            Title = "Проверить трансграничную модель привлечения иностранных специалистов",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "team",
            BusinessReason = "Международный найм без учета локального законодательства создает налоговые риски и риски признания постоянного представительства.",
            RequiredOutcome = "Договорная модель с иностранными специалистами проверена с учетом налоговых и трудовых норм целевых юрисдикций.",
            WhatToDo = "Сверить договоры с иностранными участниками команды с юристом по международному праву.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_FOREIGN_TEAM_REVIEW" }
        },

        // =====================================================================
        // 5. ПРОДУКТ И ПОЛЬЗОВАТЕЛИ (PRODUCT)
        // =====================================================================
        new()
        {
            ActionId = "ACT_PROD_TERMS_OF_SERVICE",
            Title = "Разработать Пользовательское соглашение и правила сервиса (публичную оферту)",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.Now,
            SectionId = "product",
            BusinessReason = "Работа сервиса без публичной оферты оставляет компанию незащищенной от неограниченных исков пользователей и потребительских требований о компенсации убытков.",
            RequiredOutcome = "Утверждены актуальные условия сервиса с ограничением ответственности, и в интерфейсе продукта реализован обязательный явный акцепт (клик-согласие).",
            WhatToDo = "Составить Пользовательское соглашение и интегрировать обязательный чекбокс согласия при регистрации и оформлении заказов.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "PROD_RULES_MISSING", "PROD_RULES_DISCREPANCY", "PROD_NO_TERMS_OF_SERVICE", "PROD_TERMS_MISMATCH", "PROD_LIABILITY_UNLIMITED", "PROD_OFFER_UNCLEAR", "PROD_ROLE_UNCLEAR", "PROD_ACCEPTANCE_WEAK", "PROD_RULES_MISMATCH" }
        },
        new()
        {
            ActionId = "ACT_PROD_PAYMENT_REFUND_FLOW",
            Title = "Привести модель платежей, подписок и возвратов в соответствие с законодательством",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "product",
            BusinessReason = "Автоматические списания без предварительного уведомления и отсутствие прозрачных правил возврата ведут к чарджбэкам, блокировкам платежных шлюзов и регуляторным претензиям.",
            RequiredOutcome = "В оферте и интерфейсе внедрены прозрачные условия отмены подписок, автопродления и регламент возврата денежных средств.",
            WhatToDo = "Настроить информирование пользователей перед регулярными списаниями и опубликовать правила возврата.",
            Dependencies = new() { "ACT_PROD_TERMS_OF_SERVICE" },
            SupportedFindingCodes = new() { "PROD_SUBSCRIPTION_RULES", "PROD_SUBSCRIPTION_AUTO_RENEWAL", "PROD_REFUND_RULES", "PROD_REFUND_POLICY_GAP", "PROD_PAYMENT_TRANSPARENCY", "PROD_ACCOUNT_RESTRICTIONS" }
        },
        new()
        {
            ActionId = "ACT_PROD_UGC_RULES",
            Title = "Внедрить правила модерации пользовательского контента (UGC) и механизм подачи жалоб",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "product",
            BusinessReason = "Размещение пользователями собственных материалов без прозрачных правил модерации создает риски ответственности сервиса за незаконный контент и нарушения прав третьих лиц.",
            RequiredOutcome = "В пользовательские правила включены ограничения на контент (UGC Policy), в интерфейсе реализован механизм жалоб (Notice and Takedown) и регламентированы права компании на материалы.",
            WhatToDo = "Разработать раздел правил сервиса о пользовательском контенте и настроить в продукте кнопку отправки жалоб на нарушения.",
            Dependencies = new() { "ACT_PROD_TERMS_OF_SERVICE" },
            SupportedFindingCodes = new() { "PROD_USER_CONTENT_RULES", "PROD_UGC_UNREGULATED" }
        },
        new()
        {
            ActionId = "ACT_PROD_MINORS_COMPLIANCE",
            Title = "Внедрить проверку возраста (Age Gate) и специальные условия для несовершеннолетних пользователей",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.Now,
            SectionId = "product",
            BusinessReason = "Работа сервиса с несовершеннолетними пользователями требует соблюдения специальных регуляторных норм о согласии родителей и защите детских данных.",
            RequiredOutcome = "В продукте внедрена механика проверки возраста при регистрации, и пользовательские документы адаптированы под требования законодательства о защите прав несовершеннолетних.",
            WhatToDo = "Интегрировать проверку даты рождения / возраста на этапе онбординга и предусмотреть форму согласия законных представителей.",
            Dependencies = new() { "ACT_PROD_TERMS_OF_SERVICE" },
            SupportedFindingCodes = new() { "PROD_MINORS_REVIEW", "PROD_AGE_GATE_MISSING" }
        },
        new()
        {
            ActionId = "ACT_PROD_REGULATORY_COMPLIANCE",
            Title = "Проверить соблюдение регуляторных требований на целевых рынках присутствия",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "product",
            BusinessReason = "Специализированные модели сервисов (платежи, финтех, образование, телемедицина) могут требовать лицензий или соблюдения специальных отраслевых стандартов.",
            RequiredOutcome = "Проведен анализ применимого отраслевого и потребительского законодательства целевых стран и подтверждено соответствие продукта регуляторным нормам.",
            WhatToDo = "Провести юридическую оценку отраслевых требований на ключевых рынках запуска и составить матрицу необходимых комплаенс-мер.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "PROD_REGULATORY_REVIEW", "PROD_MULTI_COUNTRY_REVIEW", "PROD_REGULATORY_RISK", "PROD_CROSS_BORDER_CONSUMER_RISK" }
        },

        // =====================================================================
        // 6. ДАННЫЕ И ИИ (DATA & AI)
        // =====================================================================
        new()
        {
            ActionId = "ACT_DATA_MAPPING_INTERNAL",
            Title = "Составить карту типов данных и внешних сервисов (Data Mapping)",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Без актуальной карты движения данных невозможно обеспечить соблюдение законодательства о персональных данных, контролировать внешние интеграции и корректно удалять информацию.",
            RequiredOutcome = "Сформирован детальный реестр потоков данных (Data Map), фиксирующий категории собираемых данных, цели их обработки, сроки хранения и перечень получателей.",
            WhatToDo = "Провести инвентаризацию всех точек сбора пользовательских данных, используемых внешних сервисов и составить карту потоков данных компании.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "DATA_MAP_INCOMPLETE", "DATA_THIRD_PARTY_UNKNOWN", "DATA_SECONDARY_USE_UNCLEAR" }
        },
        new()
        {
            ActionId = "ACT_DATA_PRIVACY_POLICY_CREATE",
            Title = "Разработать профессиональную Политику конфиденциальности (Privacy Policy)",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Несоответствие публичной политики конфиденциальности реальным процессам обработки персональных данных создает регуляторные риски и претензии со стороны пользователей и надзорных органов.",
            RequiredOutcome = "Разработана точная Политика конфиденциальности, описывающая реальные потоки данных, цели обработки, сроки хранения и перечень третьих лиц, получающих данные.",
            WhatToDo = "Составить индивидуальную Политику конфиденциальности с юристом на основе карты движения данных и опубликовать документ на сайте и в приложении.",
            Dependencies = new() { "ACT_DATA_MAPPING_INTERNAL" },
            SupportedFindingCodes = new() { "DATA_PRIVACY_NOTICE_MISSING", "DATA_PRIVACY_NOTICE_OUTDATED", "DATA_NO_PRIVACY_POLICY", "DATA_PRIVACY_POLICY_INADEQUATE", "DATA_PRIVACY_MISSING" }
        },
        new()
        {
            ActionId = "ACT_DATA_CONSENT_FLOW_SETUP",
            Title = "Реализовать механизм информированного сбора согласий пользователей на обработку данных",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Сбор персональных данных без доказанного согласия делает обработку неправомерной и лишает компанию возможности использовать накопленные базы пользователей.",
            RequiredOutcome = "В продукте внедрен явный сбор согласий (отдельный неотмеченный чекбокс) с логированием факта, даты и версии принятого согласия.",
            WhatToDo = "Интегрировать в веб-формы и мобильное приложение окно сбора согласий и сохранение логов согласия в базе данных.",
            Dependencies = new() { "ACT_DATA_PRIVACY_POLICY_CREATE" },
            SupportedFindingCodes = new() { "DATA_CONSENT_MISSING", "DATA_CONSENT_INVALID", "DATA_UNLAWFUL_PROCESSING" }
        },
        new()
        {
            ActionId = "ACT_DATA_AI_PROVIDER_REVIEW",
            Title = "Проверить юридические условия внешнего ИИ-провайдера и режимы передачи данных",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Передача пользовательских данных сторонним сервисам и моделям ИИ без проверки правовых условий создает риски несанкционированного раскрытия информации.",
            RequiredOutcome = "Проанализированы правовые условия и настройки API используемых ИИ-сервисов, подтвержден режим конфиденциальности данных пользователей и актуализированы документы.",
            WhatToDo = "Провести ревизию условий использования внешних ИИ-провайдеров и при необходимости активировать режим запрета обучения на данных пользователей.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "AI_USER_DATA_TRANSFER", "AI_SENSITIVE_DATA_TRANSFER", "AI_PROVIDER_TERMS_UNKNOWN", "AI_PROVIDER_TERMS_UNCHECKED", "AI_DATA_LEAKAGE_RISK", "AI_TRAINING_OPT_OUT_GAP", "AI_TRAINING_NOT_DISCLOSED", "DATA_AI_TRAINING_UNCHECKED" }
        },
        new()
        {
            ActionId = "ACT_AI_AUTOMATED_DECISION_OVERSIGHT",
            Title = "Внедрить контроль и участие человека в автоматизированных решениях ИИ (Human-in-the-loop)",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Принятие ИИ существенных решений о пользователях без участия человека или возможности пересмотра нарушает нормы законодательства и создает риски прямых убытков от ошибок модели.",
            RequiredOutcome = "Зафиксирован регламент участия человека (Human Review) в критических решениях модели, определена процедура эскалации и правила раскрытия информации пользователям.",
            WhatToDo = "Определить сферы влияния решений ИИ на людей, зафиксировать категории обязательной ручной проверки и синхронизировать правила с офертой.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "AI_AUTOMATED_DECISION", "AI_HUMAN_REVIEW_GAP" }
        },
        new()
        {
            ActionId = "ACT_DATA_RETENTION_DELETION",
            Title = "Внедрить регламент и функционал удаления персональных данных по запросу пользователей",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "data",
            BusinessReason = "Невозможность исполнить запрос пользователя на удаление его данных (Right to Erasure) создает риски регуляторных претензий и жалоб пользователей в юрисдикциях с развитым законодательством о защите данных.",
            RequiredOutcome = "Реализован рабочий сценарий полного удаления или деперсонализации данных пользователя из базы и внешних интеграций по первому требованию.",
            WhatToDo = "Разработать технический скрипт удаления данных и утвердить внутренний регламент реагирования на запросы субъектов данных.",
            Dependencies = new() { "ACT_DATA_PRIVACY_POLICY_CREATE" },
            SupportedFindingCodes = new() { "DATA_RETENTION_UNDEFINED", "DATA_DELETION_GAP", "DATA_DELETION_FLOW_MISSING", "DATA_SUBJECT_RIGHTS_UNSUPPORTED" }
        },
        new()
        {
            ActionId = "ACT_DATA_LOCALIZATION_SECURITY",
            Title = "Проверить требования к локализации и трансграничной передаче данных",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "data",
            BusinessReason = "Требования к локализации и трансграничной передаче зависят от стран, типов данных, ролей сторон и фактической архитектуры обработки; без такой проверки нельзя обоснованно утверждать соответствие продукта применимым нормам.",
            RequiredOutcome = "Составлена матрица стран, мест хранения и трансграничных передач данных; определены применимые требования, зафиксированы выявленные расхождения и подготовлен план их устранения.",
            WhatToDo = "Зафиксировать страны пользователей и места хранения данных, сопоставить основные трансграничные потоки с применимыми требованиями и документировать выводы проверки.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "DATA_CROSS_BORDER_REVIEW", "DATA_LOCALIZATION_RISK", "DATA_CROSS_BORDER_TRANSFER_GAP", "DATA_SECURITY_MEASURES_WEAK" }
        },

        // =====================================================================
        // 7. ДОГОВОРЫ С КЛИЕНТАМИ И ПАРТНЕРАМИ (CONTRACTS)
        // =====================================================================
        new()
        {
            ActionId = "ACT_CONTRACT_TEMPLATES_DEVELOPMENT",
            Title = "Разработать типовые формы коммерческих договоров с клиентами и контрагентами",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "contracts",
            BusinessReason = "Использование чужих или неадаптированных договоров с клиентами и контрагентами приводит к затягиванию циклов сделок и принятию невыгодных для компании условий.",
            RequiredOutcome = "Создан стандартный пакет типовых договоров оказания услуг и лицензионных соглашений с прозрачными условиями оплаты и приемки результатов.",
            WhatToDo = "Подготовить типовые договоры, инструкции для менеджеров по продажам и матрицу допустимых правовых уступок при согласовании.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "CONTRACTS_NOT_FORMALIZED", "CONTRACT_SCOPE_UNCLEAR", "CONTRACT_NO_WRITTEN_FORMS", "CONTRACT_MODEL_MISMATCH" }
        },
        new()
        {
            ActionId = "ACT_CONTRACT_RISK_ALLOCATION_REVIEW",
            Title = "Ограничить ответственность компании и пересмотреть условия договоров",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "contracts",
            BusinessReason = "Неограниченная ответственность за косвенные убытки и упущенную выгоду может поставить под угрозу существование бизнеса при единичном сбое сервиса.",
            RequiredOutcome = "В типовых и действующих договорах закреплен разумный баланс ответственности, установлены соразмерные пределы возмещения убытков и защитные условия для компании.",
            WhatToDo = "Провести аудит действующих контрактов и подписать дополнительные соглашения об ограничении ответственности.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "CONTRACT_RISK_ALLOCATION_WEAK", "CONTRACT_UNLIMITED_LIABILITY", "CONTRACT_TERMINATION_RISK", "CONTRACT_LARGE_DEAL_REVIEW" }
        },
        new()
        {
            ActionId = "ACT_CONTRACT_DEPENDENCY_HEDGING",
            Title = "Снизить правовую и финансовую зависимость от ключевых поставщиков и провайдеров",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "contracts",
            BusinessReason = "Зависимость от единственного контрагента без гарантированного срока расторжения создает риск внезапной остановки ключевых функций продукта.",
            RequiredOutcome = "В договорах с ключевыми поставщиками зафиксирован обязательный срок предупреждения о расторжении и проработаны альтернативные интеграции.",
            WhatToDo = "Пересмотреть критические соглашения с поставщиками и подготовить резервные договоры с альтернативными провайдерами.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "CONTRACT_COUNTERPARTY_DEPENDENCY", "CONTRACT_VENDOR_LOCK_IN" }
        },

        // =====================================================================
        // 8. ГОТОВНОСТЬ К ИНВЕСТИЦИЯМ (INVESTMENT READINESS)
        // =====================================================================
        new()
        {
            ActionId = "ACT_INVEST_CAP_TABLE_PREPARATION",
            Title = "Подготовить таблицу капитализации и структурировать конвертируемые займы",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "investment",
            BusinessReason = "Сложная или непрозрачная структура долей и неучтенные обещания инвесторам затягивают раунд и вызывают жесткий дисконт к оценке компании.",
            RequiredOutcome = "Сформирована юридически выверенная таблица капитализации и долей с расчетом размытия при конвертации инвестиционных инструментов (SAFE / Convertible Notes).",
            WhatToDo = "Собрать все предварительные договоренности с инвесторами и оформить единую таблицу капитализации.",
            Dependencies = new() { "ACT_CORP_CAP_TABLE_CLEANUP" },
            SupportedFindingCodes = new() { "INVEST_PRIOR_INVESTMENT_UNCLEAR", "INVEST_FUTURE_CAP_TABLE_UNCLEAR", "INVEST_CAP_TABLE_UNCLEAR", "INVEST_VALUATION_PROMISES_DISPUTED", "INVEST_DILUTION_NOT_MODELED" }
        },
        new()
        {
            ActionId = "ACT_INVEST_DATA_ROOM_DD_PACK",
            Title = "Сформировать инвестиционную Data Room и устранить сквозные юридические блокеры",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "investment",
            BusinessReason = "Наличие неурегулированных споров по структуре компании или правам на продукт может существенно затянуть инвестиционную проверку (Due Diligence) и осложнить согласование условий сделки.",
            RequiredOutcome = "Создана структурированная виртуальная комната данных (Data Room), содержащая закрывающие документы по корпоративной структуре, IP, команде и договорам.",
            WhatToDo = "Собрать и структурировать полный юридический архив компании по стандартному инвестиционному чек-листу.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA", "ACT_IP_FOUNDER_ASSIGNMENT", "ACT_TEAM_CONTRACTS_FORMALIZATION" },
            SupportedFindingCodes = new() { "INVEST_ROUND_BLOCKER", "INVEST_DATA_ROOM_MISSING", "INVEST_TIMING_IMMEDIATE_UNPREPARED", "INVEST_ROUND_NOT_DEFINED", "INVEST_RUNWAY_WARNING", "INVEST_FIN_MODEL_WEAK", "INVEST_DD_DOCS_NOT_READY" }
        },
        new()
        {
            ActionId = "ACT_INVEST_METRICS_EVIDENCE_PACK",
            Title = "Подтвердить расчет ключевых бизнес-показателей и метрик для инвестора",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "investment",
            BusinessReason = "Существенные расхождения между цифрами в презентации и первичными данными снижают доверие инвестора и ставят сделку под угрозу.",
            RequiredOutcome = "Для всех ключевых метрик презентации (выручка, пользователи, Churn, LTV, расходы) определены подтвержденные источники данных и воспроизводимые формулы расчета.",
            WhatToDo = "Определить ключевые показатели презентации, проверить их источники данных и формулы расчета и устранить неподтвержденные цифры.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "INVEST_METRICS_UNVERIFIABLE" }
        },
        new()
        {
            ActionId = "ACT_INVEST_DEAL_TERMS_LEGAL_REVIEW",
            Title = "Провести правовую экспертизу условий инвестиционной сделки (Term Sheet и договоры)",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "investment",
            BusinessReason = "Непонимание юридических последствий условий инвестора (ликвидационные привилегии, вето, drag-along) может привести к потере контроля над компанией основателями.",
            RequiredOutcome = "Проведена юридическая экспертиза Term Sheet и сделочных документов, просчитаны сценарии контроля и распределения выплат, защищены интересы основателей.",
            WhatToDo = "Разобрать все существенные условия сделки до подписания с юристом и сопроводить согласование окончательных документов.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "INVEST_TERMS_NOT_UNDERSTOOD", "INVEST_DEAL_UNREVIEWED" }
        },
        new()
        {
            ActionId = "ACT_INVEST_SELF_AWARENESS_GAP",
            Title = "Устранить расхождения между оценкой готовности команды и фактическим юридическим статусом",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.Now,
            SectionId = "investment",
            BusinessReason = "Выход на переговоры с инвесторами в уверенности полной готовности при наличии скрытых критических блокеров приводит к репутационным потерям и отказу в финансировании.",
            RequiredOutcome = "Команда имеет объективную карту уязвимостей и пошаговый план их устранения до начала активного фандрайзинга.",
            WhatToDo = "Использовать диагностику SLS для первоочередного устранения блокеров в структуре компании и правах на продукт.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "INV_SELF_AWARENESS_GAP", "INVEST_AWARENESS_GAP", "INVEST_SELF_AWARENESS_GAP" }
        }
    };

    private static readonly Dictionary<string, ActionDefinition> _byId =
        All.ToDictionary(a => a.ActionId, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, ActionDefinition> _byFindingCode =
        new(StringComparer.OrdinalIgnoreCase);

    static ActionLibrary()
    {
        foreach (var action in All)
        {
            foreach (var code in action.SupportedFindingCodes)
            {
                if (!_byFindingCode.ContainsKey(code))
                {
                    _byFindingCode[code] = action;
                }
            }
        }
    }

    public static ActionDefinition? GetById(string actionId)
    {
        if (string.IsNullOrWhiteSpace(actionId)) return null;
        return _byId.TryGetValue(actionId, out var action) ? action : null;
    }

    public static ActionDefinition? GetByFindingCode(string findingCode)
    {
        if (string.IsNullOrWhiteSpace(findingCode)) return null;
        return _byFindingCode.TryGetValue(findingCode, out var action) ? action : null;
    }

    public static ActionDefinition ResolveActionForFinding(RiskFinding finding)
    {
        // 1. Try explicit RecommendedActionId
        if (!string.IsNullOrWhiteSpace(finding.RecommendedActionId))
        {
            var act = GetById(finding.RecommendedActionId);
            if (act != null) return act;
        }

        // 2. Try mapped Finding Code
        var byCode = GetByFindingCode(finding.Code);
        if (byCode != null) return byCode;

        // 3. Strict explicit mapping: no word heuristic search or universal fallback
        throw new InvalidOperationException($"[ActionLibrary Gap] Risk finding '{finding.Code}' does not have an explicit ActionDefinition mapping in ActionLibrary.");
    }
}
