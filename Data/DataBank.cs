using FenixLegalOs.Models;

namespace FenixLegalOs.Data;

public static class DataBank
{
    public const string QuestionBankVersion = "1.1.0";
    public const string ScoringEngineVersion = "1.1.0";
    public const string RiskLibraryVersion = "1.1.0";

    public static readonly List<DiagnosticSection> Sections = new()
    {
        new("founders",   1, "Сооснователи",                 "Founders",           15),
        new("corporate",  2, "Корпоративная структура",      "Corporate",          12),
        new("ip",         3, "Интеллектуальная собственность", "IP",               18),
        new("team",       4, "Команда и подрядчики",         "Team",               10),
        new("product",    5, "Продукт и пользователи",       "Product",            10),
        new("data",       6, "Данные, privacy и AI",         "Data & AI",          15),
        new("contracts",  7, "Коммерческие договоры",        "Contracts",          8),
        new("investment", 8, "Инвестиционная готовность",    "Investor Readiness", 12),
    };

    public static readonly List<DiagnosticQuestion> Questions = new()
    {
        // =====================================================================
        // 1. FOUNDERS (Блок 1 — Сооснователи)
        // =====================================================================
        new() {
            Id = "FND-C01", SectionId = "founders", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Сколько человек сейчас фактически участвуют в проекте как сооснователи?",
            Options = new() {
                new("solo", "Единственный основатель", 1),
                new("2", "2 сооснователя", 1),
                new("3", "3 сооснователя", 1),
                new("4plus", "4 и более", 1),
                new("inactive_exist", "Формально несколько, но не все участвуют", 1)
            }
        },
        new() {
            Id = "FND-C03", SectionId = "founders", Order = 2, Type = "single", ScoreMode = "trigger", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли человек, который получил или должен был получить долю, но уже перестал участвовать?",
            Options = new() {
                new("none", "Нет", 1),
                new("resolved", "Выход полностью урегулирован письменно", 1),
                new("unresolved", "Есть нерешённые вопросы по доле/выходу", 0, "HIGH", "R_FOUNDERS_NO_LEAVER"),
                new("dispute", "Есть активный спор или конфликт", 0, "CRITICAL", "R_FOUNDERS_EQUITY_UNFIXED"),
                new("unknown", "Не уверен(а)", 0.5)
            }
        },
        new() {
            Id = "FND-C04", SectionId = "founders", Order = 3, Type = "single", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли подписанный документ, регулирующий отношения между основателями?",
            Options = new() {
                new("signed", "Подписан единый Founder Agreement / SHA", 1),
                new("multiple_docs", "Правила в нескольких документах", 0.8),
                new("draft", "Подготовлен, но ещё не подписан", 0.5),
                new("informal", "Переписка, таблица или устная договорённость", 0.25),
                new("none", "Документа нет", 0),
                new("unknown", "Не уверен(а)", 0.25)
            }
        },
        new() {
            Id = "FND-01", SectionId = "founders", DimensionId = "existing_dispute", Order = 4, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли нерешённые разногласия по долям, ролям, деньгам или выходу из проекта?",
            Options = new() {
                new("none", "Нет", 1.0, ConfidenceClass: "known"),
                new("minor", "Отдельные некритические вопросы", 0.75, ConfidenceClass: "known"),
                new("significant", "Существенные нерешённые вопросы", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_AGREEMENT", ConfidenceClass: "partial"),
                new("active_conflict", "Активный конфликт между фаундерами", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("formal_dispute", "Формальный спор или судебные претензии", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known")
            }
        },
        new() {
            Id = "FND-02", SectionId = "founders", DimensionId = "roles", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько чётко закреплены роли и зоны ответственности каждого сооснователя?",
            Options = new() {
                new("written", "Закреплены письменно", 1.0, ConfidenceClass: "known"),
                new("verbal_clear", "Понятно всем, но только устно", 0.75, ConfidenceClass: "known"),
                new("overlap", "Есть дублирование и пересечения задач", 0.5, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "partial"),
                new("shared", "Многое общее без ответственных лиц", 0.25, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "partial"),
                new("dispute", "Споры о ролях и обязанностях", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "known")
            }
        },
        new() {
            Id = "FND-03", SectionId = "founders", DimensionId = "commitment", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Соответствует ли фактическая занятость каждого сооснователя договорённостям?",
            Options = new() {
                new("full", "Да, все работают в полном объёме", 1.0, ConfidenceClass: "known"),
                new("parttime_agreed", "Частичная занятость согласована всеми", 0.85, ConfidenceClass: "known"),
                new("accepted_diff", "Вклад различается, но устраивает всех", 0.65, ConfidenceClass: "known"),
                new("less_no_rules", "Меньше ожидаемого без фиксированных правил", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_VESTING", ConfidenceClass: "partial"),
                new("stopped", "Один из сооснователей практически перестал работать", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known")
            }
        },
        new() {
            Id = "FND-04", SectionId = "founders", DimensionId = "equity_clarity", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько определённо зафиксировано распределение долей между фаундерами?",
            Options = new() {
                new("registered", "Оформлено в уставных документах компании", 1.0, ConfidenceClass: "known"),
                new("written_agreed", "Согласовано письменно в договоре", 0.8, ConfidenceClass: "known"),
                new("preliminary", "Письменная предварительная договорённость", 0.6, ConfidenceClass: "partial"),
                new("verbal", "Только устная договорённость", 0.4, Severity: "HIGH", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("ambiguous", "Несколько неясных обещаний долей", 0.15, Severity: "HIGH", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "partial"),
                new("dispute", "Спор по долям", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "FND-05", SectionId = "founders", DimensionId = "early_exit_equity", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Что происходит с долей, если основатель прекращает работу над проектом раньше времени?",
            Options = new() {
                new("vesting", "Оформлен график постепенного закрепления (Vesting)", 1.0, ConfidenceClass: "known"),
                new("repurchase", "Оформлен обязательный выкуп/возврат доли", 0.9, ConfidenceClass: "known"),
                new("verbal_rule", "Договорились устно, не оформили", 0.55, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_VESTING", ConfidenceClass: "partial"),
                new("retains_all", "Сохраняет всю долю без условий работы", 0.1, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("not_discussed", "Вопрос не обсуждался", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "FND-05A", SectionId = "founders", DimensionId = "early_exit_equity", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Различаются ли условия выкупа доли при обычном уходе и уходе из-за нарушения (Good / Bad Leaver)?",
            Options = new() {
                new("yes", "Да, правила Good/Bad Leaver оформлены", 1.0, ConfidenceClass: "known"),
                new("partial", "Частично зафиксированы", 0.7, ConfidenceClass: "partial"),
                new("verbal", "Только устно", 0.4, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "partial"),
                new("no", "Нет различий", 0.15, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "FND-06", SectionId = "founders", DimensionId = "governance", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Зафиксировано ли, какие решения требуют согласия всех сооснователей?",
            Options = new() {
                new("written", "Письменно зафиксирован перечень единогласных решений", 1.0, ConfidenceClass: "known"),
                new("verbal", "Понимание есть, но только устно", 0.75, ConfidenceClass: "known"),
                new("partial", "Зафиксирована только часть правил", 0.5, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "partial"),
                new("all_together", "Все решения принимаем строго вместе", 0.25, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("none", "Правил принятия решений нет", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known")
            }
        },
        new() {
            Id = "FND-07", SectionId = "founders", DimensionId = "deadlock", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли зафиксированный механизм на случай тупикового разногласия (Deadlock), когда голоса равны?",
            Options = new() {
                new("mechanism", "Оформлен чёткий механизм (решающий голос / выкуп / медиация)", 1.0, ConfidenceClass: "known"),
                new("stages", "Несколько этапов переговоров", 0.85, ConfidenceClass: "known"),
                new("mediator", "Привлечение внешнего медиатора", 0.55, ConfidenceClass: "partial"),
                new("casting_vote", "Решающий голос конкретного сооснователя", 0.7, ConfidenceClass: "known"),
                new("only_agree", "Только договариваться устно", 0.15, Severity: "HIGH", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("none", "Механизма нет", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.1, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 2. CORPORATE (Блок 2 — Корпоративная структура)
        // =====================================================================
        new() {
            Id = "COR-C01", SectionId = "corporate", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Зарегистрировано ли юридическое лицо для проекта?",
            Options = new() {
                new("one", "Да, зарегистрирована одна компания", 1),
                new("several", "Да, зарегистрирована группа из нескольких компаний", 1),
                new("process", "В процессе регистрации", 0.75),
                new("none", "Нет, компания ещё не зарегистрирована", 0.5),
                new("unknown", "Не уверен(а)", 0.5)
            }
        },
        new() {
            Id = "COR-01", SectionId = "corporate", DimensionId = "ownership_accuracy", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = new List<string>{"one","several"} } },
            Question = "Соответствуют ли официально зарегистрированные доли фактическим договоренностям фаундеров?",
            Options = new() {
                new("full", "Да, полностью соответствуют", 1.0, ConfidenceClass: "known"),
                new("future_planned", "Запланированы изменения у нотариуса/в юрисдикции", 0.8, ConfidenceClass: "known"),
                new("undocumented_future", "Есть неоформленные обещания долей", 0.5, Severity: "HIGH", RiskCode: "R_CORP_VERBAL_PROMISES", ConfidenceClass: "partial"),
                new("mismatch", "Официальные доли существенно расходятся с фактическими", 0.2, Severity: "CRITICAL", RiskCode: "R_CORP_SHARES_MISMATCH", ConfidenceClass: "known"),
                new("dispute", "Есть спор о юридическом владении", 0.0, Severity: "CRITICAL", RiskCode: "R_CORP_SHARES_MISMATCH", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "COR-02", SectionId = "corporate", DimensionId = "cap_table", Order = 3, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = new List<string>{"one","several"} } },
            Question = "Есть ли у компании актуальная таблица капитализации (Cap Table)?",
            Options = new() {
                new("full_table", "Ведётся актуальная Cap Table со всеми конвертируемыми правами", 1.0, ConfidenceClass: "known"),
                new("future_separate", "Ведётся, но опционы/займы считаются отдельно", 0.8, ConfidenceClass: "known"),
                new("irregular", "Есть, но давно не обновлялась", 0.5, Severity: "MEDIUM", RiskCode: "R_CORP_CAPTABLE_STALE", ConfidenceClass: "partial"),
                new("scattered", "Данные рассыпаны по документам", 0.25, Severity: "HIGH", RiskCode: "R_CORP_NO_CAPTABLE", ConfidenceClass: "partial"),
                new("none", "Таблицы капитализации нет", 0.0, Severity: "HIGH", RiskCode: "R_CORP_NO_CAPTABLE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 3. IP (Блок 3 — Права на продукт)
        // =====================================================================
        new() {
            Id = "IP-01", SectionId = "ip", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Есть ли уже созданный продукт или его технический прототип?",
            Options = new() {
                new("idea", "Пока только идея / концепция", 1),
                new("prototype", "Есть прототип / MVP", 1),
                new("live_product", "Работающий готовый продукт", 1),
                new("multiple", "Несколько продуктов", 1)
            }
        },
        new() {
            Id = "IP-04", SectionId = "ip", DimensionId = "core_ownership", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 22, DimensionWeight = 22, WithinDimensionWeight = 100,
            Question = "Есть ли документальное подтверждение, что права на ключевой код и дизайн принадлежат компании?",
            Options = new() {
                new("full", "Да, по всему ключевому продукту есть акты и договоры", 1.0, ConfidenceClass: "known"),
                new("main", "По основной части продукта есть документы", 0.75, ConfidenceClass: "known"),
                new("part", "Только по незначительной части", 0.4, Severity: "HIGH", RiskCode: "R_IP_CONTRACTS_PARTIAL", ConfidenceClass: "partial"),
                new("verbal", "Договорились, но документов передачи прав нет", 0.2, Severity: "CRITICAL", RiskCode: "R_IP_NO_CONTRACTS", ConfidenceClass: "known"),
                new("none", "Документы отсутствуют", 0.0, Severity: "CRITICAL", RiskCode: "R_IP_NO_CONTRACTS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "IP-05", SectionId = "ip", DimensionId = "founders_rights", Order = 3, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            Question = "Передавали ли фаундеры свои ранние наработки и код на юридическое лицо компании?",
            Options = new() {
                new("yes", "Да, оформлен договор передачи IP (Assignment) в компанию", 1.0, ConfidenceClass: "known"),
                new("charter", "Предусмотрено в соглашении сооснователей", 0.9, ConfidenceClass: "known"),
                new("partial", "Часть наработок передана", 0.5, Severity: "MEDIUM", RiskCode: "R_IP_FOUNDER_ASSIGN", ConfidenceClass: "partial"),
                new("verbal", "Договорились устно", 0.35, Severity: "MEDIUM", RiskCode: "R_IP_FOUNDER_ASSIGN", ConfidenceClass: "known"),
                new("on_founders", "Права остаются на фаундерах как физлицах", 0.1, Severity: "HIGH", RiskCode: "R_IP_FOUNDER_ASSIGN", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 4. TEAM (Блок 4 — Команда)
        // =====================================================================
        new() {
            Id = "TEAM-C01", SectionId = "team", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Есть ли в команде привлекаемые разработчики, дизайнеры или сотрудники помимо фаундеров?",
            Options = new() {
                new("founders_only", "Нет, над продуктом работают только фаундеры", 1),
                new("contractors", "Есть фрилансеры / подрядчики", 1),
                new("employees", "Есть штатные сотрудники", 1),
                new("mixed", "Есть и сотрудники, и фрилансеры", 1)
            }
        },
        new() {
            Id = "TEAM-01", SectionId = "team", DimensionId = "documentation", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 25, DimensionWeight = 25, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "TEAM-C01", Op = "neq", Value = "founders_only" } },
            Question = "Все ли участники команды работают по оформленным письменным договорам?",
            Options = new() {
                new("all", "Да, со всеми заключены договоры", 1.0, ConfidenceClass: "known"),
                new("most", "С большинством", 0.75, ConfidenceClass: "known"),
                new("part", "С частью команды договоров нет", 0.25, Severity: "HIGH", RiskCode: "R_TEAM_CONTRACTS", ConfidenceClass: "partial"),
                new("none", "Письменные договоры не заключались", 0.0, Severity: "HIGH", RiskCode: "R_TEAM_CONTRACTS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 5. PRODUCT (Блок 5 — Продукт)
        // =====================================================================
        new() {
            Id = "PROD-01", SectionId = "product", DimensionId = "presence", Order = 1, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            Question = "Есть ли у вашего сервиса публичные Terms of Use / Пользовательское соглашение?",
            Options = new() {
                new("yes_custom", "Да, персонально разработанное соглашение", 1.0, ConfidenceClass: "known"),
                new("template", "Да, составлено по шаблону из интернета", 0.5, Severity: "MEDIUM", RiskCode: "R_PRODUCT_TERMS_TEMPLATE", ConfidenceClass: "known"),
                new("draft", "Готовится, но не опубликовано", 0.25, Severity: "HIGH", RiskCode: "R_PRODUCT_NO_TERMS", ConfidenceClass: "partial"),
                new("none", "Пользовательского соглашения нет", 0.0, Severity: "HIGH", RiskCode: "R_PRODUCT_NO_TERMS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 6. DATA & AI (Блок 6 — Данные и ИИ)
        // =====================================================================
        new() {
            Id = "DATA-01", SectionId = "data", DimensionId = "privacy_notice", Order = 1, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            Question = "Есть ли на сайте/в приложении опубликованная Политика конфиденциальности (Privacy Policy)?",
            Options = new() {
                new("yes_custom", "Да, актуальная Privacy Policy под реальные потоки данных", 1.0, ConfidenceClass: "known"),
                new("template", "Да, шаблонный документ", 0.5, Severity: "MEDIUM", RiskCode: "R_DATA_PP_TEMPLATE", ConfidenceClass: "known"),
                new("outdated", "Есть, но давно не обновлялась после смены функционала", 0.5, Severity: "HIGH", RiskCode: "R_DATA_PP_MISMATCH", ConfidenceClass: "known"),
                new("none", "Политика конфиденциальности отсутствует", 0.0, Severity: "HIGH", RiskCode: "R_DATA_NO_PP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },
        new() {
            Id = "AI-01", SectionId = "data", DimensionId = "ai_transfer", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            Question = "Передаются ли данные или файлы пользователей во внешние нейросети (OpenAI, Anthropic и др.)?",
            Options = new() {
                new("no", "Нет, ИИ не используется или данные не передаются", 1.0, ConfidenceClass: "known"),
                new("anonymized", "Передаются только анонимизированные данные", 0.85, ConfidenceClass: "known"),
                new("raw_data", "Передаются обычные персональные данные пользователей", 0.4, Severity: "HIGH", RiskCode: "R_DATA_AI_TRANSFER", ConfidenceClass: "known"),
                new("sensitive", "В нейросети могут попадать чувствительные/финансовые данные", 0.0, Severity: "CRITICAL", RiskCode: "R_DATA_AI_SENSITIVE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 7. CONTRACTS (Блок 7 — Договоры)
        // =====================================================================
        new() {
            Id = "CONTRACT-01", SectionId = "contracts", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Есть ли у компании существенные B2B-клиенты, крупные партнёры или поставщики?",
            Options = new() {
                new("clients", "Да, работаем с B2B-клиентами", 1),
                new("partners", "Да, есть крупные партнёры / поставщики", 1),
                new("none", "Нет, работаем только с физическими лицами (B2C)", 1)
            }
        },
        new() {
            Id = "CONTRACT-02", SectionId = "contracts", DimensionId = "written", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "CONTRACT-01", Op = "neq", Value = "none" } },
            Question = "Подписываются ли с B2B-контрагентами полноценные письменные договоры?",
            Options = new() {
                new("always", "Практически всегда подписываем договора", 1.0, ConfidenceClass: "known"),
                new("invoices", "Часть отношений держится только на счетах и переписке", 0.5, Severity: "MEDIUM", RiskCode: "R_CONTRACTS_ADHOC", ConfidenceClass: "partial"),
                new("none", "Работаем без подписания бумаг", 0.0, Severity: "HIGH", RiskCode: "R_CONTRACTS_NONE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // 8. INVESTMENT (Блок 8 — Инвестиции)
        // =====================================================================
        new() {
            Id = "INVEST-01", SectionId = "investment", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Планируете ли вы привлекать венчурные или частные инвестиции?",
            Options = new() {
                new("m3", "В ближайшие 3 месяца", 1),
                new("m3_6", "Через 3–6 месяцев", 1),
                new("m6_12", "Через 6–12 месяцев", 1),
                new("none", "Не планируем", 1)
            }
        },
        new() {
            Id = "INVEST-02", SectionId = "investment", DimensionId = "prior_investments", Order = 2, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            Question = "Получала ли компания ранее деньги в обмен на долю или обещание доли?",
            Options = new() {
                new("none", "Нет, инвестиций не было", 1.0, ConfidenceClass: "known"),
                new("all_formal", "Да, всё оформлено через устав или SAFE/Convertible Note", 1.0, ConfidenceClass: "known"),
                new("informal", "Да, деньги получены по устным или неформальным соглашениям", 0.0, Severity: "HIGH", RiskCode: "R_INV_INFORMAL", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        }
    };

    public static readonly List<RiskDefinition> Risks = new()
    {
        // FOUNDERS
        new() { Code = "R_FOUNDERS_EQUITY_UNFIXED", RootCauseGroup = "FOUNDER_CONTROL", Severity = "CRITICAL", Priority = "NOW", SectionId = "founders", Modules = new() { "founders", "corporate" }, Title = "Доли сооснователей не зафиксированы документально", Finding = "Доли фаундеров зафиксированы только устно или не распределены.", WhyItMatters = "Устная договорённость работает, пока всё хорошо. При первом разногласии юридически считается, что компании нет или доли не принадлежат никому.", Recommendation = "Закрепить доли документально в Корпоративном соглашении / SHA.", Recommendations = new() { "Закрепить доли документально в Корпоративном соглашении / SHA." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "FOUNDERS_REVIEW", Cta = "Разобрать структуру между основателями" },
        new() { Code = "R_FOUNDERS_ROLES", RootCauseGroup = "FOUNDER_CONTROL", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "founders", Modules = new() { "founders" }, Title = "Роли сооснователей не закреплены письменно", Finding = "Зоны ответственности сооснователей зафиксированы только устно.", WhyItMatters = "Приводит к дублированию задач или ситуациям, когда критические направления остаются без ответственного.", Recommendation = "Составить и подписать соглашение о распределении ролей и KPI сооснователей.", Recommendations = new() { "Составить и подписать соглашение о распределении ролей и KPI сооснователей." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "FOUNDERS_REVIEW" },
        new() { Code = "R_FOUNDERS_AGREEMENT_PARTIAL", RootCauseGroup = "FOUNDER_CONTROL", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "founders", Modules = new() { "founders" }, Title = "Соглашение сооснователей оформлено частично", Finding = "Правила между фаундерами зафиксированы не в полном объёме.", WhyItMatters = "Непокрытые вопросы (deadlock, выход фаундера) обычно вызывают самые острые конфликты.", Recommendation = "Доработать соглашение сооснователей до полноценного Founder Agreement.", Recommendations = new() { "Доработать соглашение сооснователей до полноценного Founder Agreement." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "FOUNDERS_REVIEW" },
        new() { Code = "R_FOUNDERS_NO_AGREEMENT", RootCauseGroup = "FOUNDER_CONTROL", Severity = "HIGH", Priority = "NOW", SectionId = "founders", Modules = new() { "founders" }, Title = "Отсутствует соглашение сооснователей", Finding = "Между фаундерами нет письменных правил распределения долей, ролей и выхода.", WhyItMatters = "При уходе сооснователя возникает 'мёртвый капитал' — человек забирает долю и не работает.", Recommendation = "Разработать Founder Agreement с правилами принятия решений и Vesting.", Recommendations = new() { "Разработать Founder Agreement с правилами принятия решений и Vesting." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "FOUNDERS_REVIEW", Cta = "Разработать соглашение сооснователей" },
        new() { Code = "R_FOUNDERS_NO_LEAVER", RootCauseGroup = "FOUNDER_EXIT", Severity = "HIGH", Priority = "NOW", SectionId = "founders", Modules = new() { "founders" }, Title = "Не определены правила выхода фаундера (Bad/Good Leaver)", Finding = "Отсутствует механизм выкупа или возврата доли при прекращении участия фаундера.", WhyItMatters = "Если один из фаундеров решит покинуть проект, за ним остаётся вся доля без обязательств по работе.", Recommendation = "Внедрить правила Bad/Good Leaver с фиксированными условиями выкупа долей.", Recommendations = new() { "Внедрить правила Bad/Good Leaver с фиксированными условиями выкупа долей." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "FOUNDERS_REVIEW" },
        new() { Code = "R_FOUNDERS_NO_VESTING", RootCauseGroup = "FOUNDER_EXIT", Severity = "HIGH", Priority = "30_DAYS", SectionId = "founders", Modules = new() { "founders" }, Title = "Vesting долей сооснователей не оформлен", Finding = "Доли сооснователей переданы сразу без графика вестинга.", WhyItMatters = "Инвесторы требуют наличие vesting (обычно 4 года с 1 годом cliff) до выделения инвестиций.", Recommendation = "Подписать соглашение о вестинге долей сооснователей.", Recommendations = new() { "Подписать соглашение о вестинге долей сооснователей." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "FOUNDERS_REVIEW" },
        new() { Code = "R_FOUNDERS_DECISIONS", RootCauseGroup = "FOUNDER_CONTROL", Severity = "HIGH", Priority = "NOW", SectionId = "founders", Modules = new() { "founders" }, Title = "Правила принятия ключевых решений не зафиксированы", Finding = "Отсутствует порядок голосования и механизм разблокировки тупиковых ситуаций (Deadlock).", WhyItMatters = "Один фаундер может заблокировать работу всей компании или принять критическое решение без согласия остальных.", Recommendation = "Определить матрицу решений и механизм тупика.", Recommendations = new() { "Определить матрицу решений и механизм тупика." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "FOUNDERS_REVIEW" },

        // CORPORATE
        new() { Code = "R_CORP_SHARES_MISMATCH", RootCauseGroup = "ENTITY_ALIGNMENT", Severity = "CRITICAL", Priority = "NOW", SectionId = "corporate", Modules = new() { "corporate" }, Title = "Официальные доли расходятся с фактическими", Finding = "Зарегистрированные доли в юрлице отличаются от устных договоренностей.", WhyItMatters = "При инвестиционном Due Diligence инвестор проверяет только официальный устав и реестр участников.", Recommendation = "Привести официальную структуру в соответствие с фактическими договоренностями.", Recommendations = new() { "Привести официальную структуру в соответствие с фактическими договоренностями." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "CORPORATE_CLEANUP", Cta = "Привести корпоративную структуру в порядок" },
        new() { Code = "R_CORP_VERBAL_PROMISES", RootCauseGroup = "EQUITY_PROMISE", Severity = "HIGH", Priority = "30_DAYS", SectionId = "corporate", Modules = new() { "corporate" }, Title = "Устные обещания долей или опционов", Finding = "Сотрудникам, эдвайзерам или партнерам обещаны доли только на словах.", WhyItMatters = "В будущем эти лица могут предъявить юридические претензии или заблокировать инвестиционный раунд.", Recommendation = "Оформить устные обещания в опционный план (ESOP) или письменный опционный договор.", Recommendations = new() { "Оформить устные обещания в опционный план (ESOP) или письменный опционный договор." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "CORPORATE_CLEANUP" },
        new() { Code = "R_CORP_CAPTABLE_STALE", RootCauseGroup = "ENTITY_ALIGNMENT", Severity = "MEDIUM", Priority = "BEFORE_ROUND", SectionId = "corporate", Modules = new() { "corporate" }, Title = "Таблица капитализации (Cap Table) устарела", Finding = "Таблица капитализации давно не обновлялась.", WhyItMatters = "Затрудняет оценку разводнения долей перед переговорами с инвесторами.", Recommendation = "Обновить Cap Table с учётом всех конвертируемых займов и опционов.", Recommendations = new() { "Обновить Cap Table с учётом всех конвертируемых займов и опционов." }, LawyerRequired = false, Resolution = "self" },
        new() { Code = "R_CORP_NO_CAPTABLE", RootCauseGroup = "ENTITY_ALIGNMENT", Severity = "HIGH", Priority = "BEFORE_ROUND", SectionId = "corporate", Modules = new() { "corporate" }, Title = "Отсутствует таблица капитализации (Cap Table)", Finding = "У компании нет структурированного Cap Table.", WhyItMatters = "Обязательное требование любого венчурного фонда перед подготовкой Term Sheet.", Recommendation = "Сформировать актуальную таблицу капитализации компании.", Recommendations = new() { "Сформировать актуальную таблицу капитализации компании." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "CORPORATE_CLEANUP" },

        // IP
        new() { Code = "R_IP_CONTRACTS_PARTIAL", RootCauseGroup = "KEY_DEVELOPER", Severity = "HIGH", Priority = "NOW", SectionId = "ip", Modules = new() { "ip" }, Title = "Часть разработчиков работает без договоров", Finding = "Не со всеми создателями кода и дизайна заключены письменные соглашения.", WhyItMatters = "Авторские права на фрагменты продукта принадлежат конкретным фрилансерам.", Recommendation = "Заключить договоры уступки прав (IP Assignment) со всеми авторами.", Recommendations = new() { "Заключить договоры уступки прав (IP Assignment) со всеми авторами." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "IP_RIGHTS_REVIEW", Cta = "Проверить права на продукт" },
        new() { Code = "R_IP_NO_CONTRACTS", RootCauseGroup = "KEY_DEVELOPER", Severity = "CRITICAL", Priority = "NOW", SectionId = "ip", Modules = new() { "ip" }, Title = "Отсутствуют договоры с разработчиками", Finding = "Продукт создавался внешними специалистами без письменных договоров.", WhyItMatters = "По закону авторские права принадлежат создателю. Код юридически принадлежит фрилансерам.", Recommendation = "Подписать договоры уступки прав (IP Assignment) прошлым числом.", Recommendations = new() { "Подписать договоры уступки прав (IP Assignment) прошлым числом." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "IP_RIGHTS_REVIEW", Cta = "Передать права на продукт компании" },
        new() { Code = "R_IP_FOUNDER_ASSIGN", RootCauseGroup = "KEY_DEVELOPER", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "ip", Modules = new() { "ip" }, Title = "Права фаундеров на ранний код не переданы компании", Finding = "Код и интеллектуальная собственность, созданные фаундерами до регистрации юрлица, не переданы компании.", WhyItMatters = "Права остаются за физическими лицами, а не за юридическим лицом стартапа.", Recommendation = "Подписать договор передачи IP (Founder IP Assignment) от фаундеров в компанию.", Recommendations = new() { "Подписать договор передачи IP (Founder IP Assignment) от фаундеров в компанию." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "IP_RIGHTS_REVIEW" },

        // TEAM
        new() { Code = "R_TEAM_CONTRACTS", RootCauseGroup = "KEY_DEVELOPER", Severity = "HIGH", Priority = "NOW", SectionId = "team", Modules = new() { "team" }, Title = "Сотрудники или подрядчики работают без договоров", Finding = "Часть команды выполняет задачи без оформленных соглашений.", WhyItMatters = "Риски штрафов от налоговой/трудовой инспекции и споры о правах на результаты работы.", Recommendation = "Заключить трудовые или ГПХ договоры со всеми участниками команды.", Recommendations = new() { "Заключить трудовые или ГПХ договоры со всеми участниками команды." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "TEAM_LEGAL_REVIEW", Cta = "Проверить юридическую конструкцию команды" },

        // PRODUCT
        new() { Code = "R_PRODUCT_TERMS_TEMPLATE", RootCauseGroup = "PRODUCT_DOCS", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "product", Modules = new() { "product" }, Title = "Terms of Use скопированы по шаблону", Finding = "Пользовательское соглашение не адаптировано под реальную бизнес-модель продукта.", WhyItMatters = "Шаблонные условия не защищают компанию от судебных исков пользователей и возвратов.", Recommendation = "Разработать персонализированные Terms of Use под особенности вашего сервиса.", Recommendations = new() { "Разработать персонализированные Terms of Use под особенности вашего сервиса." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "PRODUCT_LEGAL_REVIEW" },
        new() { Code = "R_PRODUCT_NO_TERMS", RootCauseGroup = "PRODUCT_DOCS", Severity = "HIGH", Priority = "NOW", SectionId = "product", Modules = new() { "product" }, Title = "Отсутствует пользовательское соглашение (Terms of Use)", Finding = "У сервиса нет публичной оферты или пользовательского соглашения.", WhyItMatters = "Компания не ограничила свою ответственность перед пользователями за сбои и убытки.", Recommendation = "Подготовить и опубликовать оферту / Terms of Use на сайте и в приложении.", Recommendations = new() { "Подготовить и опубликовать оферту / Terms of Use на сайте и в приложении." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "PRODUCT_LEGAL_REVIEW", Cta = "Проверить юридическую модель продукта" },

        // DATA & AI
        new() { Code = "R_DATA_PP_TEMPLATE", RootCauseGroup = "DATA_AI_TRANSPARENCY", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "data", Modules = new() { "data" }, Title = "Privacy Policy составлена по шаблону", Finding = "Политика конфиденциальности не отражает реальные каналы и цели сбора данных.", WhyItMatters = "Регуляторы штрафуют за неточное информирование пользователей о сборе данных.", Recommendation = "Обновить Privacy Policy в точном соответствии с используемыми метриками и сервисами.", Recommendations = new() { "Обновить Privacy Policy в точном соответствии с используемыми метриками и сервисами." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "DATA_AI_REVIEW" },
        new() { Code = "R_DATA_NO_PP", RootCauseGroup = "DATA_AI_TRANSPARENCY", Severity = "HIGH", Priority = "NOW", SectionId = "data", Modules = new() { "data" }, Title = "Отсутствует Privacy Policy", Finding = "Продукт собирает персональные данные, но не имеет Политики конфиденциальности.", WhyItMatters = "Штрафы регуляторов и блокировка приложении в App Store / Google Play.", Recommendation = "Подготовить Privacy Policy под реальные потоки данных.", Recommendations = new() { "Подготовить Privacy Policy под реальные потоки данных." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "DATA_AI_REVIEW", Cta = "Разобрать модель работы с данными и ИИ" },
        new() { Code = "R_DATA_PP_MISMATCH", RootCauseGroup = "DATA_AI_TRANSPARENCY", Severity = "HIGH", Priority = "30_DAYS", SectionId = "data", Modules = new() { "data" }, Title = "Privacy Policy не соответствует фактическому сбору данных", Finding = "Продукт собирает данные, которые не упоминаются в Политике конфиденциальности.", WhyItMatters = "Нарушение законодательства о персональных данных.", Recommendation = "Сверить Privacy Policy с продуктовыми логами и трекерами и внести изменения.", Recommendations = new() { "Сверить Privacy Policy с продуктовыми логами и трекерами и внести изменения." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "DATA_AI_REVIEW" },
        new() { Code = "R_DATA_AI_TRANSFER", RootCauseGroup = "DATA_AI_TRANSPARENCY", Severity = "HIGH", Priority = "NOW", SectionId = "data", Modules = new() { "data" }, Title = "Персональные данные передаются в сторонние AI-сервисы", Finding = "Данные пользователей отправляются во внешние нейросети (OpenAI, Anthropic и др.).", WhyItMatters = "Риски утечки данных и прямые нарушения GDPR / законов о персональных данных.", Recommendation = "Внедрить анонимизацию данных перед отправкой в AI-API и обновить Privacy Policy.", Recommendations = new() { "Внедрить анонимизацию данных перед отправкой в AI-API и обновить Privacy Policy." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "DATA_AI_REVIEW" },
        new() { Code = "R_DATA_AI_SENSITIVE", RootCauseGroup = "DATA_AI_TRANSPARENCY", Severity = "CRITICAL", Priority = "NOW", SectionId = "data", Modules = new() { "data" }, Title = "Через AI обрабатываются чувствительные данные", Finding = "В нейросети могут попадать финансовые, медицинские или детские данные.", WhyItMatters = "Высокие штрафы регуляторов за трансграничную передачу чувствительной информации.", Recommendation = "Исключить передачу чувствительных данных в коммерческие AI-API.", Recommendations = new() { "Исключить передачу чувствительных данных в коммерческие AI-API." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "DATA_AI_REVIEW" },

        // CONTRACTS
        new() { Code = "R_CONTRACTS_ADHOC", RootCauseGroup = "PRODUCT_DOCS", Severity = "MEDIUM", Priority = "30_DAYS", SectionId = "contracts", Modules = new() { "contracts" }, Title = "Договоры с клиентами заключаются бессистемно", Finding = "Каждая сделка оформляется по разным условиям без единой матрицы.", WhyItMatters = "Сложность контроля обязательств, сроков и условий интеллектуальной собственности.", Recommendation = "Внедрить типовую форму B2B-договора и стандартизировать процессы продажи.", Recommendations = new() { "Внедрить типовую форму B2B-договора и стандартизировать процессы продажи." }, LawyerRequired = false, Resolution = "check_with_lawyer", ServiceCode = "CONTRACTS_REVIEW" },
        new() { Code = "R_CONTRACTS_NONE", RootCauseGroup = "PRODUCT_DOCS", Severity = "HIGH", Priority = "NOW", SectionId = "contracts", Modules = new() { "contracts" }, Title = "B2B-клиенты обслуживаются без договоров", Finding = "Услуги или доступ к продукту предоставляются B2B-клиентам без подписания бумаг.", WhyItMatters = "Невозможно взыскать дебиторскую задолженность или доказать факт оказания услуг.", Recommendation = "Перевести всех клиентов на единую оферту или стандартный договор.", Recommendations = new() { "Перевести всех клиентов на единую оферту или стандартный договор." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "CONTRACTS_REVIEW", Cta = "Проверить ключевые договоры" },

        // INVESTMENT
        new() { Code = "R_INV_INFORMAL", RootCauseGroup = "ROUND_BLOCKER", Severity = "HIGH", Priority = "BEFORE_ROUND", SectionId = "investment", Modules = new() { "investment" }, Title = "Инвестиционные договоренности не оформлены", Finding = "Деньги инвесторов получены по неформальным соглашениям или устным обещаниям.", WhyItMatters = "Инвестор может передумать и потребовать сумму обратно как задолженность с процентами.", Recommendation = "Оформить инвестиционные средства через SAFE, Convertible Note или долю в компании.", Recommendations = new() { "Оформить инвестиционные средства через SAFE, Convertible Note или долю в компании." }, LawyerRequired = true, Resolution = "lawyer_required", ServiceCode = "INVESTOR_READINESS", Cta = "Подготовить компанию к проверке инвестором" }
    };
}
