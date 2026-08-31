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
            SupportedFindingCodes = new() { "FND_DEADLOCK_RISK", "FND_DEADLOCK", "FND_GOVERNANCE_GAP" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_AGREEMENT_SHA",
            Title = "Разработать и подписать корпоративный договор сооснователей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "founders",
            BusinessReason = "Устные договоренности или фрагментарные переписки в мессенджерах не имеют обязательной юридической силы и создают прямую угрозу пересмотра долей при первом успехе компании.",
            RequiredOutcome = "Подписан юридически обязывающий документ, комплексно фиксирующий доли, порядок голосования, ограничения на продажу долей третьим лицам и ключевые обязательства сторон.",
            WhatToDo = "Подготовить проект соглашения между основателями, согласовать существенные условия и зафиксировать подписями всех участников.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "FND_DOCUMENTATION_GAP", "FND_EQUITY_NOT_FORMALIZED", "FND_EQUITY_AMBIGUITY", "FND_NO_AGREEMENT" }
        },
        new()
        {
            ActionId = "ACT_FOUNDER_VESTING_LEAVER",
            Title = "Внедрить механизм поэтапного закрепления долей (вестинг) и порядок выхода сооснователей",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "founders",
            BusinessReason = "Если основатель прекратит участие на раннем этапе, сохраняя полную долю в компании (dead equity), проект станет токсичным и практически непривлекательным для венчурных инвесторов.",
            RequiredOutcome = "В корпоративном договоре закреплен график поэтапного перехода прав на доли в зависимости от срока и вклада основателя, а также правила выкупа долей при добровольном или вынужденном выходе из проекта.",
            WhatToDo = "Включить положения о вестинге и правах выкупа долей при уходе основателя в корпоративный договор.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA" },
            SupportedFindingCodes = new() { "FND_NO_VESTING", "FND_EXIT_UNREGULATED", "FND_LEAVER_UNPROTECTED", "FND_DEAD_EQUITY" }
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
            SupportedFindingCodes = new() { "FND_PERSONAL_INVESTMENT_UNRECORDED", "FND_LOAN_NOT_DOCUMENTED" }
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
            RequiredOutcome = "Официальный реестр участников и учредительные документы приведены в полное соответствие с фактическим согласованным распределением долей.",
            WhatToDo = "Сопоставить реестр участников со всеми соглашениями, провести необходимые корпоративные действия и внести изменения в государственный реестр.",
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
            SupportedFindingCodes = new() { "COR_GOVERNANCE_GAP", "COR_SIGNATORY_UNCLEAR", "COR_DIRECTOR_POWER_UNCLEAR", "COR_DECISIONS_UNSYSTEMATIC" }
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
            SupportedFindingCodes = new() { "IP_CONTRACTOR_RIGHTS_GAP", "IP_CONTRACTOR_RIGHTS_MISSING", "IP_STUDIO_RIGHTS_GAP" }
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
            SupportedFindingCodes = new() { "IP_OPEN_SOURCE_RISK", "IP_THIRD_PARTY_CONTENT_RISK", "IP_LICENSE_COMPLIANCE_GAP" }
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
            SupportedFindingCodes = new() { "IP_TRADEMARK_NOT_FILED", "IP_BRAND_UNPROTECTED" }
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
            BusinessReason = "Работа команды и привлеченных специалистов без письменного оформления создает риски споров по оплате и юридической утраты прав на созданный ими программный код.",
            RequiredOutcome = "Со всеми постоянными членами команды заключены официальные трудовые договоры или договоры оказания услуг с разделами о служебных произведениях.",
            WhatToDo = "Внедрить типовой пакет договоров найма и привлечения подрядчиков с обязательным положением об отчуждении IP.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_NO_WRITTEN_CONTRACTS", "TEAM_WRITTEN_AGREEMENTS_MISSING", "TEAM_ORAL_ONLY" }
        },
        new()
        {
            ActionId = "ACT_TEAM_IP_TRANSFER_ACTS",
            Title = "Внедрить процедуру регулярного подписания актов передачи прав на служебные произведения",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Одного трудового договора недостаточно: создание конкретных модулей должно подтверждаться служебными заданиями и актами приема-передачи результатов.",
            RequiredOutcome = "В компании внедрен регулярный регламент оформления служебных заданий и ежемесячных актов сдачи-приемки созданного кода и дизайна.",
            WhatToDo = "Настроить шаблон служебного задания и внедрить ежемесячное подписание актов с разработчиками.",
            Dependencies = new() { "ACT_TEAM_CONTRACTS_FORMALIZATION" },
            SupportedFindingCodes = new() { "TEAM_IP_TRANSFER_GAP", "TEAM_IP_CREATION_UNRECORDED" }
        },
        new()
        {
            ActionId = "ACT_TEAM_RECLASSIFICATION_RISK",
            Title = "Минимизировать риски переквалификации отношений с подрядчиками в трудовые",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Признание отношений с самозанятыми/ИП трудовыми влечет доначисление налогов, социальных платежей и крупные административные штрафы.",
            RequiredOutcome = "Из договоров с контрагентами исключены признаки трудового распорядка (фиксированные часы работы, подчинение графику, постоянное рабочее место).",
            WhatToDo = "Провести аудит договоров с внешними специалистами и скорректировать формулировки на предмет отсутствия трудовых признаков.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_EMPLOYMENT_RECLASSIFICATION", "TEAM_LABOR_RECLASSIFICATION_RISK" }
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
            SupportedFindingCodes = new() { "TEAM_ORAL_OPTION_PROMISES", "TEAM_OPTION_AMBIGUITY", "TEAM_ESOP_UNSTRUCTURED" }
        },
        new()
        {
            ActionId = "ACT_TEAM_NDA_ACCESS_CONTROL",
            Title = "Внедрить соглашения о конфиденциальности (NDA) и регламент разграничения доступов",
            ActionType = "PROCESS_SETUP",
            ResolutionMode = ResolutionMode.InternalAction,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "team",
            BusinessReason = "Отсутствие NDA и неконтролируемые доступы к репозиториям и базам данных создают угрозу утечки коммерческой тайны при увольнении сотрудников.",
            RequiredOutcome = "Со всеми специалистами подписаны соглашения о неразглашении конфиденциальной информации и настроен ролевой доступ к критическим сервисам.",
            WhatToDo = "Подписать NDA со всеми участниками и провести ревизию доступов в Git, серверах и базах данных.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "TEAM_NO_NDA", "TEAM_ACCESS_CONTROL_GAP" }
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
            SupportedFindingCodes = new() { "PROD_RULES_DISCREPANCY", "PROD_NO_TERMS_OF_SERVICE", "PROD_TERMS_MISMATCH", "PROD_LIABILITY_UNLIMITED" }
        },
        new()
        {
            ActionId = "ACT_PROD_PAYMENT_REFUND_FLOW",
            Title = "Привести модель платежей, подписок и возвратов в соответствие с законодательством",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "product",
            BusinessReason = "Автоматические списания без предварительного уведомления и отсутствие прозрачных правил возврата ведут к чарджбэкам, блокировкам платежных шлюзов и штрафам.",
            RequiredOutcome = "В оферте и интерфейсе внедрены прозрачные условия отмены подписок, автопродления и регламент возврата денежных средств.",
            WhatToDo = "Настроить информирование пользователей перед регулярными списаниями и опубликовать правила возврата.",
            Dependencies = new() { "ACT_PROD_TERMS_OF_SERVICE" },
            SupportedFindingCodes = new() { "PROD_SUBSCRIPTION_AUTO_RENEWAL", "PROD_REFUND_POLICY_GAP" }
        },
        new()
        {
            ActionId = "ACT_PROD_REGULATORY_COMPLIANCE",
            Title = "Проверить соблюдение регуляторных требований на целевых рынках присутствия",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "product",
            BusinessReason = "Трансграничные продажи B2C-продуктов могут подпадать под специальные требования о защите прав потребителей, маркировке и возрастных ограничениях.",
            RequiredOutcome = "Подтверждено соответствие продукта регуляторным требованиям ключевых стран пользователей (age verification, disclosures, consumer protections).",
            WhatToDo = "Провести юридический анализ регуляторных ограничений на целевых рынках и внести необходимые изменения в продукт.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "PROD_REGULATORY_RISK", "PROD_AGE_GATE_MISSING", "PROD_CROSS_BORDER_CONSUMER_RISK" }
        },

        // =====================================================================
        // 6. ДАННЫЕ И ИИ (DATA & AI)
        // =====================================================================
        new()
        {
            ActionId = "ACT_DATA_PRIVACY_POLICY_CREATE",
            Title = "Разработать профессиональную Политику конфиденциальности (Privacy Policy)",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.Now,
            SectionId = "data",
            BusinessReason = "Использование шаблонной или неполной политики конфиденциальности является нарушением законодательства о персональных данных (GDPR / местный закон о ПД) и влечет крупные штрафы.",
            RequiredOutcome = "Разработана точная Политика конфиденциальности, описывающая реальные потоки данных, цели обработки, сроки хранения и перечень третьих лиц, получающих данные.",
            WhatToDo = "Провести картирование потоков данных (Data Mapping) и составить индивидуальную Политику конфиденциальности с юристом.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "DATA_PRIVACY_NOTICE_MISSING", "DATA_NO_PRIVACY_POLICY", "DATA_PRIVACY_POLICY_INADEQUATE" }
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
            BusinessReason = "Передача пользовательских данных сторонним нейросетям (OpenAI, Anthropic и др.) без проверки условий может привести к использованию коммерческих данных для обучения публичных моделей.",
            RequiredOutcome = "Проверены условия выбранного API-режима провайдера (Enterprise / Zero Data Retention), подписано соглашение об обработке данных (DPA) и включены уведомления пользователей.",
            WhatToDo = "Провести аудит API-настроек провайдера, активировать режим запрета обучения на данных пользователей и отразить использование ИИ в политике конфиденциальности.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "AI_SENSITIVE_DATA_TRANSFER", "AI_PROVIDER_TERMS_UNCHECKED", "AI_DATA_LEAKAGE_RISK", "AI_TRAINING_OPT_OUT_GAP" }
        },
        new()
        {
            ActionId = "ACT_DATA_RETENTION_DELETION",
            Title = "Внедрить регламент и функционал удаления персональных данных по запросу пользователей",
            ActionType = "PRODUCT_INTEGRATION",
            ResolutionMode = ResolutionMode.LegalAndProduct,
            DefaultPriority = RiskPriority.ThirtyDays,
            SectionId = "data",
            BusinessReason = "Невозможность исполнить запрос пользователя на удаление его данных (Right to Erasure) является прямым нарушением регуляторных требований.",
            RequiredOutcome = "Реализован рабочий сценарий полного удаления или деперсонализации данных пользователя из базы и внешних интеграций по первому требованию.",
            WhatToDo = "Разработать технический скрипт удаления данных и утвердить внутренний регламент реагирования на запросы субъектов данных.",
            Dependencies = new() { "ACT_DATA_PRIVACY_POLICY_CREATE" },
            SupportedFindingCodes = new() { "DATA_RETENTION_UNDEFINED", "DATA_DELETION_FLOW_MISSING", "DATA_SUBJECT_RIGHTS_UNSUPPORTED" }
        },
        new()
        {
            ActionId = "ACT_DATA_LOCALIZATION_SECURITY",
            Title = "Подтвердить соблюдение требований к локализации баз данных и защите информации",
            ActionType = "LEGAL_REVIEW",
            ResolutionMode = ResolutionMode.LegalReview,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "data",
            BusinessReason = "Нарушение законов о локализации персональных данных грозит блокировкой доменного имени сервиса уполномоченным государственным органом.",
            RequiredOutcome = "Серверная инфраструктура и базы данных размещены в соответствии с нормами локализации целевых стран с применением шифрования данных при передаче и хранении.",
            WhatToDo = "Проверить физическое расположение серверов хранения персональных данных и внедрить политику безопасности информации.",
            Dependencies = new(),
            SupportedFindingCodes = new() { "DATA_LOCALIZATION_RISK", "DATA_CROSS_BORDER_TRANSFER_GAP", "DATA_SECURITY_MEASURES_WEAK" }
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
            SupportedFindingCodes = new() { "CONTRACTS_NOT_FORMALIZED", "CONTRACT_NO_WRITTEN_FORMS", "CONTRACT_MODEL_MISMATCH" }
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
            SupportedFindingCodes = new() { "CONTRACT_RISK_ALLOCATION_WEAK", "CONTRACT_UNLIMITED_LIABILITY", "CONTRACT_TERMINATION_RISK" }
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
            SupportedFindingCodes = new() { "INVEST_CAP_TABLE_UNCLEAR", "INVEST_VALUATION_PROMISES_DISPUTED" }
        },
        new()
        {
            ActionId = "ACT_INVEST_DATA_ROOM_DD_PACK",
            Title = "Сформировать инвестиционную Data Room и устранить сквозные юридические блокеры",
            ActionType = "LEGAL_DRAFTING",
            ResolutionMode = ResolutionMode.LegalWork,
            DefaultPriority = RiskPriority.BeforeRound,
            SectionId = "investment",
            BusinessReason = "Инвестор приостановит сделку или откажется от раунда, если при юридической проверке (Due Diligence) обнаружит отсутствие прав на продукт или корпоративный тупик.",
            RequiredOutcome = "Создана структурированная виртуальная комната данных (Data Room), содержащая закрывающие документы по корпоративной структуре, IP, команде и договорам.",
            WhatToDo = "Собрать и структурировать полный юридический архив компании по стандартному инвестиционному чек-листу.",
            Dependencies = new() { "ACT_FOUNDER_AGREEMENT_SHA", "ACT_IP_FOUNDER_ASSIGNMENT", "ACT_TEAM_CONTRACTS_FORMALIZATION" },
            SupportedFindingCodes = new() { "INVEST_ROUND_BLOCKER", "INVEST_DATA_ROOM_MISSING", "INVEST_TIMING_IMMEDIATE_UNPREPARED" }
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
            SupportedFindingCodes = new() { "INV_SELF_AWARENESS_GAP", "INVEST_AWARENESS_GAP" }
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

        // 3. Deterministic root-cause / section fallback with strictly specific outcomes
        return ResolveFallbackAction(finding);
    }

    private static ActionDefinition ResolveFallbackAction(RiskFinding f)
    {
        var code = f.Code?.ToUpperInvariant() ?? "";
        var sec = f.SectionId?.ToLowerInvariant() ?? "";

        if (code.Contains("DEADLOCK")) return GetById("ACT_FOUNDER_DEADLOCK_RESOLVE")!;
        if (code.Contains("VESTING") || code.Contains("LEAVER")) return GetById("ACT_FOUNDER_VESTING_LEAVER")!;
        if (code.Contains("DISPUTE")) return GetById("ACT_FOUNDER_DISPUTE_SETTLE")!;
        if (sec == "founders" || code.StartsWith("FND")) return GetById("ACT_FOUNDER_AGREEMENT_SHA")!;

        if (code.Contains("ENTITY")) return GetById("ACT_CORP_INCORPORATION")!;
        if (code.Contains("CAP_TABLE") || code.Contains("OWNERSHIP")) return GetById("ACT_CORP_CAP_TABLE_CLEANUP")!;
        if (sec == "corporate" || code.StartsWith("COR")) return GetById("ACT_CORP_GOVERNANCE_SYSTEMATIZE")!;

        if (code.Contains("FOUNDER")) return GetById("ACT_IP_FOUNDER_ASSIGNMENT")!;
        if (code.Contains("CONTRACTOR") || code.Contains("STUDIO")) return GetById("ACT_IP_CONTRACTOR_ASSIGNMENT")!;
        if (code.Contains("OPEN_SOURCE") || code.Contains("THIRD_PARTY")) return GetById("ACT_IP_OPEN_SOURCE_COMPLIANCE")!;
        if (code.Contains("TRADEMARK") || code.Contains("BRAND")) return GetById("ACT_IP_TRADEMARK_PROTECTION")!;
        if (sec == "ip" || code.StartsWith("IP")) return GetById("ACT_IP_CONSOLIDATION_AUDIT")!;

        if (code.Contains("NDA") || code.Contains("ACCESS")) return GetById("ACT_TEAM_NDA_ACCESS_CONTROL")!;
        if (code.Contains("OPTION") || code.Contains("ESOP")) return GetById("ACT_TEAM_OPTION_POOL_FORMALIZATION")!;
        if (code.Contains("RECLASSIFICATION") || code.Contains("LABOR")) return GetById("ACT_TEAM_RECLASSIFICATION_RISK")!;
        if (sec == "team" || code.StartsWith("TEAM")) return GetById("ACT_TEAM_CONTRACTS_FORMALIZATION")!;

        if (code.Contains("TERMS") || code.Contains("RULES") || code.Contains("OFFER")) return GetById("ACT_PROD_TERMS_OF_SERVICE")!;
        if (code.Contains("SUBSCRIPTION") || code.Contains("REFUND")) return GetById("ACT_PROD_PAYMENT_REFUND_FLOW")!;
        if (sec == "product" || code.StartsWith("PROD")) return GetById("ACT_PROD_TERMS_OF_SERVICE")!;

        if (code.Contains("AI")) return GetById("ACT_DATA_AI_PROVIDER_REVIEW")!;
        if (code.Contains("CONSENT")) return GetById("ACT_DATA_CONSENT_FLOW_SETUP")!;
        if (code.Contains("DELETION") || code.Contains("RETENTION")) return GetById("ACT_DATA_RETENTION_DELETION")!;
        if (code.Contains("LOCALIZATION")) return GetById("ACT_DATA_LOCALIZATION_SECURITY")!;
        if (sec == "data" || code.StartsWith("DATA")) return GetById("ACT_DATA_PRIVACY_POLICY_CREATE")!;

        if (code.Contains("RISK_ALLOCATION") || code.Contains("LIABILITY")) return GetById("ACT_CONTRACT_RISK_ALLOCATION_REVIEW")!;
        if (code.Contains("DEPENDENCY") || code.Contains("VENDOR")) return GetById("ACT_CONTRACT_DEPENDENCY_HEDGING")!;
        if (sec == "contracts" || code.StartsWith("CONTRACT") || code.StartsWith("CTR")) return GetById("ACT_CONTRACT_TEMPLATES_DEVELOPMENT")!;

        if (code.Contains("ROUND") || code.Contains("BLOCKER") || code.Contains("DATA_ROOM")) return GetById("ACT_INVEST_DATA_ROOM_DD_PACK")!;
        if (code.Contains("CAP_TABLE")) return GetById("ACT_INVEST_CAP_TABLE_PREPARATION")!;
        if (sec == "investment" || code.StartsWith("INVEST") || code.StartsWith("INV")) return GetById("ACT_INVEST_DATA_ROOM_DD_PACK")!;

        // Fallback default
        return GetById("ACT_CORP_INCORPORATION")!;
    }
}
