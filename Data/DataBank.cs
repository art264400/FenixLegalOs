using FenixLegalOs.Models;

namespace FenixLegalOs.Data;

public static class DataBank
{
    public const string QuestionBankVersion = "1.1.0-founders-focus";
    public const string ScoringEngineVersion = "1.1.0";
    public const string RiskLibraryVersion = "1.1.0";

    public static readonly List<DiagnosticSection> Sections = new()
    {
        new("founders", 1, "Сооснователи", "Founders", 15),
        new("corporate", 2, "Корпоративная структура", "Corporate", 12),
        new("ip", 3, "Интеллектуальная собственность", "IP", 18)
    };

    public static readonly List<DiagnosticQuestion> Questions = new()
    {
        // =====================================================================
        // БЛОК 1. СООСНОВАТЕЛИ (FOUNDERS) — ПОЛНЫЙ КАНОНИЧЕСКИЙ НАБОР v1.1
        // =====================================================================

        // 1. FND-C01 (Контекст: количество фаундеров)
        new() {
            Id = "FND-C01", SectionId = "founders", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Сколько человек сейчас фактически участвуют в проекте как сооснователи?",
            Explanation = "Помогает системе адаптировать опрос: для единственного основателя вопросы о распределении долей и конфликтах будут скрыты.",
            Options = new() {
                new("solo", "Я единственный основатель", 1),
                new("2", "2 сооснователя", 1),
                new("3", "3 сооснователя", 1),
                new("4plus", "4 и более сооснователей", 1),
                new("inactive_exist", "Формально несколько, но не все фактически работают", 1)
            }
        },

        // 2. FND-C02 (Контекст: распределение долей и контроль)
        new() {
            Id = "FND-C02", SectionId = "founders", Order = 2, Type = "equity_inputs", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Если доли уже согласованы, как они распределены между сооснователями?",
            Explanation = "От соотношения долей зависит наличие контроля и вероятность корпоративного тупика (Deadlock).",
            Options = new()
        },

        // 3. FND-C03 (Триггер: ушедшие фаундеры с долями)
        new() {
            Id = "FND-C03", SectionId = "founders", Order = 3, Type = "single", ScoreMode = "trigger", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли человек, который получил или должен был получить долю, но уже перестал участвовать?",
            Explanation = "Мёртвый капитал (dead equity) — частый блокер инвестиционных раундов.",
            Options = new() {
                new("none", "Нет, все заявленные основатели активно работают", 1),
                new("resolved", "Да, но его выход и судьба доли полностью урегулированы письменно", 1),
                new("unresolved", "Да, есть нерешённые вопросы по доле или компенсации", 0, "HIGH", "FND_DEPARTED_UNRESOLVED"),
                new("dispute", "Да, возник открытый конфликт / претензии", 0, "CRITICAL", "FND_EQUITY_DISPUTE"),
                new("unknown", "Не уверен(а)", 0.5)
            }
        },

        // 4. FND-C04 (Контекст: форма фиксации соглашений)
        new() {
            Id = "FND-C04", SectionId = "founders", Order = 4, Type = "single", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "В какой форме зафиксированы договоренности между сооснователями?",
            Options = new() {
                new("signed", "Подписан единый документ (Founder Agreement / Корпоративный договор / SHA)", 1),
                new("multiple_docs", "Правила зафиксированы в уставе и нескольких отдельных соглашениях", 0.8),
                new("draft", "Проект документа подготовлен, но пока не подписан", 0.5),
                new("informal", "Только в переписке, Telegram, таблицах или на словах", 0.25),
                new("none", "Письменных документов нет вообще", 0),
                new("unknown", "Не уверен(а)", 0.25)
            }
        },

        // 5. FND-01 (Диагностика: разногласия и конфликты)
        new() {
            Id = "FND-01", SectionId = "founders", DimensionId = "existing_dispute", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли сейчас нерешённые разногласия по долям, ролям, деньгам или выходу?",
            Options = new() {
                new("none", "Нет, все ключевые вопросы согласованы", 1.0, ConfidenceClass: "known"),
                new("minor", "Есть отдельные рабочие дискуссии, но без риска конфликта", 0.75, ConfidenceClass: "known"),
                new("material", "Есть существенные нерешённые вопросы, вызывающие напряжение", 0.25, Severity: "HIGH", RiskCode: "FND_DOCUMENTATION_GAP", ConfidenceClass: "partial"),
                new("active_conflict", "Активный конфликт между сооснователями", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: "known"),
                new("formal_dispute", "Формальный спор / претензии / угроза суда", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: "known")
            }
        },

        // 6. FND-02 (Диагностика: разделение ролей)
        new() {
            Id = "FND-02", SectionId = "founders", DimensionId = "roles", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько чётко закреплены роли и зоны ответственности каждого сооснователя?",
            Options = new() {
                new("written", "Закреплены письменно в соглашении с понятными KPI и обязанностями", 1.0, ConfidenceClass: "known"),
                new("clear_oral", "Понятны всем основателям, но зафиксированы только на словах", 0.75, ConfidenceClass: "known"),
                new("overlap", "Есть пересечения и споры, кто за что отвечает", 0.25, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: "partial"),
                new("disputed", "Постоянные разногласия по поводу вклада и обязанностей", 0.0, Severity: "HIGH", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: "known")
            }
        },

        // 7. FND-03 (Диагностика: занятость и вовлеченность)
        new() {
            Id = "FND-03", SectionId = "founders", DimensionId = "commitment", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Соответствует ли фактическая занятость каждого сооснователя договорённостям?",
            Options = new() {
                new("aligned", "Да, все основатели работают над проектом full-time", 1.0, ConfidenceClass: "known"),
                new("temporary_part_time", "Часть временно совмещает, но это согласовано всеми и отражено в долях", 0.85, ConfidenceClass: "known"),
                new("different_accepted", "Вклад по времени различается, но пока всех устраивает", 0.65, ConfidenceClass: "known"),
                new("below_expected", "Кто-то уделяет проекту гораздо меньше времени без ясных правил", 0.25, Severity: "HIGH", RiskCode: "FND_COMMITMENT_MISMATCH", ConfidenceClass: "partial"),
                new("stopped", "Один из сооснователей фактически прекратил работу, сохраняя долю", 0.0, Severity: "CRITICAL", RiskCode: "FND_DEAD_EQUITY", ConfidenceClass: "known")
            }
        },

        // 8. FND-04 (Диагностика: определенность долей)
        new() {
            Id = "FND-04", SectionId = "founders", DimensionId = "equity_clarity", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько определённо зафиксировано распределение долей между сооснователями?",
            Options = new() {
                new("registered", "Оформлено в уставе компании / реестре участников", 1.0, ConfidenceClass: "known"),
                new("written_agreed", "Зафиксировано в подписанном корпоративном договоре", 0.8, ConfidenceClass: "known"),
                new("preliminary", "Есть подписанный Term Sheet / предварительный меморандум", 0.6, ConfidenceClass: "partial"),
                new("verbal", "Только устная договоренность, в документах не отражено", 0.4, Severity: "MEDIUM", RiskCode: "FND_EQUITY_NOT_FORMALIZED", ConfidenceClass: "known"),
                new("ambiguous", "Есть несколько противоречивых обещаний долей", 0.15, Severity: "HIGH", RiskCode: "FND_EQUITY_AMBIGUITY", ConfidenceClass: "partial"),
                new("dispute", "Есть открытый спор о распределении долей", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 9. FND-05 (Диагностика: Vesting и ранний уход)
        new() {
            Id = "FND-05", SectionId = "founders", DimensionId = "early_exit_equity", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Что происходит с долей основателя, если он прекращает работу раньше оговоренного срока?",
            Options = new() {
                new("vesting", "Оформлен график постепенного закрепления долей (Vesting с периодом Cliff)", 1.0, ConfidenceClass: "known"),
                new("repurchase", "Оформлено обязательство обратного выкупа доли компанией / другими фаундерами", 0.9, ConfidenceClass: "known"),
                new("verbal_rule", "Договорились устно, но юридически не закрепили", 0.55, Severity: "MEDIUM", RiskCode: "FND_NO_VESTING", ConfidenceClass: "partial"),
                new("retains_all", "Сохраняет всю свою долю независимо от продолжения работы", 0.1, Severity: "HIGH", RiskCode: "FND_EXIT_RULES_MISSING", ConfidenceClass: "known"),
                new("not_discussed", "Этот вопрос вообще не обсуждался", 0.0, Severity: "HIGH", RiskCode: "FND_EXIT_RULES_MISSING", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 10. FND-05A (Диагностика: Good/Bad Leaver)
        new() {
            Id = "FND-05A", SectionId = "founders", DimensionId = "early_exit_equity", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Предусмотрены ли разные условия выкупа доли при обычном уходе и уходе из-за нарушения (Good / Bad Leaver)?",
            Options = new() {
                new("defined", "Да, правила Good/Bad Leaver прописаны документально", 1.0, ConfidenceClass: "known"),
                new("partial", "Частично зафиксированы", 0.7, ConfidenceClass: "partial"),
                new("oral", "Только устная договоренность", 0.4, Severity: "MEDIUM", RiskCode: "FND_INCOMPLETE_LEAVER_RULES", ConfidenceClass: "partial"),
                new("none", "Нет, условия выкупа одинаковы при любых обстоятельствах", 0.15, Severity: "MEDIUM", RiskCode: "FND_INCOMPLETE_LEAVER_RULES", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 11. FND-06 (Диагностика: ясность матрицы управления)
        new() {
            Id = "FND-06", SectionId = "founders", DimensionId = "governance", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Зафиксировано ли, какие решения требуют согласования между сооснователями?",
            Options = new() {
                new("written", "Письменно закреплен перечень ключевых совместных решений", 1.0, ConfidenceClass: "known"),
                new("verbal", "Общее понимание есть, но юридически перечень не оформлен", 0.75, ConfidenceClass: "known"),
                new("partial", "Зафиксирована только часть правил в стандартном уставе", 0.5, Severity: "MEDIUM", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: "partial"),
                new("all_together", "Все решения принимаем строго вместе без регламента", 0.25, Severity: "MEDIUM", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: "known"),
                new("none", "Правил нет, каждый действует по своему усмотрению", 0.0, Severity: "HIGH", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 12. FND-06A (Контекст: порядок принятия ключевых решений)
        new() {
            Id = "FND-06A", SectionId = "founders", DimensionId = "governance", Order = 12, Type = "single", ScoreMode = "context", Weight = 0, DimensionWeight = 0, WithinDimensionWeight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Какой порядок голосования и принятия решений установлен для ключевых вопросов компании?",
            Options = new() {
                new("different_thresholds", "Разные пороги для разных типов решений (операционные — большинство, ключевые — квалифицированное)", 1.0),
                new("majority", "Простое большинство голосов (>50%)", 1.0),
                new("material_unanimity", "Единогласие только по ключевым материальным вопросам (M&A, инвестиции, бюджет)", 1.0),
                new("broad_unanimity", "Единогласие по всем или почти всем решениям ('все согласны')", 1.0),
                new("undefined", "Порядок принятия решений четко не определен", 1.0),
                new("unknown", "Не уверен(а)", 1.0)
            }
        },

        // 13. FND-07 (Диагностика: Deadlock / тупик при голосовании)
        new() {
            Id = "FND-07", SectionId = "founders", DimensionId = "deadlock", Order = 13, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли зафиксированный механизм разрешения тупиковых разногласий (Deadlock), когда голоса равны 50/50?",
            Options = new() {
                new("full", "Да, оформлен полный юридический механизм (решающий голос / Russian Roulette / Texas Shootout / выкуп)", 1.0, ConfidenceClass: "known"),
                new("staged", "Предусмотрены поэтапные переговоры и эскалация", 0.85, ConfidenceClass: "known"),
                new("casting_vote", "Закреплен решающий голос конкретного фаундера (CEO)", 0.70, ConfidenceClass: "known"),
                new("mediator_only", "Договорились только о привлечении внешнего медиатора/эксперта", 0.55, ConfidenceClass: "partial"),
                new("only_agree", "Механизма нет, надеемся только на умение договариваться", 0.15, Severity: "HIGH", RiskCode: "FND_NO_DEADLOCK_PROTECTION", ConfidenceClass: "known"),
                new("none", "Вопрос тупика вообще не продуман", 0.0, Severity: "CRITICAL", RiskCode: "FND_DEADLOCK", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.10, ConfidenceClass: "unknown")
            }
        },

        // 14. FND-08 (Диагностика: передача дел и порядок выхода)
        new() {
            Id = "FND-08", SectionId = "founders", DimensionId = "exit_continuity", Order = 14, Type = "single", ScoreMode = "diagnostic", Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Определен ли заранее порядок передачи дел, доступов и полномочий при уходе основателя?",
            Options = new() {
                new("full", "Да, прописан полный регламент передачи доступов, прав на код, клиентов и документов", 1.0, ConfidenceClass: "known"),
                new("partial", "Есть базовое понимание и список критических доступов", 0.65, ConfidenceClass: "known"),
                new("oral", "Только устная договоренность", 0.40, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: "partial"),
                new("none", "Порядок не определен", 0.10, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: "known"),
                new("already_unresolved", "Кто-то уже ушел, и доступы/дела до конца не переданы", 0.0, Severity: "HIGH", RiskCode: "FND_DEPARTED_UNRESOLVED", ConfidenceClass: "known")
            }
        },

        // 15. FND-09 (Диагностика: личные вложения основателей)
        new() {
            Id = "FND-09", SectionId = "founders", DimensionId = "founder_contributions", Order = 15, Type = "single", ScoreMode = "diagnostic", Weight = 3, DimensionWeight = 3, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Если основатели вкладывают в проект личные деньги, понятно ли, как они учитываются?",
            Options = new() {
                new("none", "Личные деньги не вкладывались (только труд)", 1.0, ConfidenceClass: "known"),
                new("documented", "Все вложения оформлены как займы участников или вклады в капитал", 1.0, ConfidenceClass: "known"),
                new("small_partial", "Ведется учет расходов в таблице, но без договоров займа", 0.70, ConfidenceClass: "known"),
                new("material_unclear", "Вложены значительные личные суммы без юридического оформления", 0.25, Severity: "MEDIUM", RiskCode: "FND_CONTRIBUTION_AMBIGUITY", ConfidenceClass: "partial"),
                new("dispute", "Есть разногласия по поводу возврата вложенных личных средств", 0.0, Severity: "HIGH", RiskCode: "FND_CONTRIBUTION_AMBIGUITY", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.30, ConfidenceClass: "unknown")
            }
        },

        // 16. FND-10 (Диагностика: конфликт интересов и сторонние проекты)
        new() {
            Id = "FND-10", SectionId = "founders", DimensionId = "conflict_of_interest", Order = 16, Type = "single", ScoreMode = "diagnostic", Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли у сооснователей другая работа или сторонние проекты, которые могут пересекаться со стартапом?",
            Options = new() {
                new("none", "Нет, все сфокусированы только на этом проекте", 1.0, ConfidenceClass: "known"),
                new("unrelated", "Сторонняя работа есть, но она никак не связана со сферой стартапа", 0.9, ConfidenceClass: "known"),
                new("overlap_rules", "Возможное пересечение согласовано между фаундерами письменно с четкими правилами", 0.75, ConfidenceClass: "known"),
                new("potential_competitor", "Есть сторонний проект в смежной сфере без четкого разделения прав", 0.25, Severity: "HIGH", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: "partial"),
                new("employer_same_field", "Один из фаундеров параллельно работает по найму в смежной IT-компании", 0.25, Severity: "HIGH", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: "known"),
                new("active_competition", "Фаундер участвует в прямом конкурирующем бизнесе", 0.0, Severity: "CRITICAL", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.4, ConfidenceClass: "unknown")
            }
        },

        // 17. FND-11 (Диагностика: стратегическая согласованность)
        new() {
            Id = "FND-11", SectionId = "founders", DimensionId = "strategic_alignment", Order = 17, Type = "single", ScoreMode = "diagnostic", Weight = 3, DimensionWeight = 3, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Совпадают ли взгляды сооснователей на стратегию, темпы роста, привлечение инвестиций и возможную продажу компании?",
            Options = new() {
                new("aligned", "Полное совпадение видения по ключевым целям и финансированию", 1.0, ConfidenceClass: "known"),
                new("differences_discussed", "Есть рабочие дискуссии, но общее направление согласовано", 0.75, ConfidenceClass: "known"),
                new("not_discussed", "Стратегические цели и горизонт пока подробно не обсуждались", 0.50, ConfidenceClass: "partial"),
                new("material_difference", "Существенные различия во взглядах на темп роста или дивиденды/инвестиции", 0.20, Severity: "MEDIUM", RiskCode: "FND_STRATEGIC_MISALIGNMENT", ConfidenceClass: "partial"),
                new("conflict", "Принципиальный конфликт целей (быстрый экзит vs долгосрочный бизнес)", 0.0, Severity: "HIGH", RiskCode: "FND_STRATEGIC_MISALIGNMENT", ConfidenceClass: "known")
            }
        },

        // =====================================================================
        // БЛОК 2. КОРПОРАТИВНАЯ СТРУКТУРА (CORPORATE) — КАНОНИЧЕСКИЙ НАБОР v1.1
        // =====================================================================

        // 1. COR-C01 (Контекст: наличие юрлица)
        new() {
            Id = "COR-C01", SectionId = "corporate", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Зарегистрировано ли юридическое лицо, через которое работает проект?",
            Explanation = "Позволяет оценить уровень формализации бизнеса. Если компании пока нет, блок не будет занижать ваш Legal Score.",
            Options = new() {
                new("one", "Да, одна компания", 1.0, ConfidenceClass: "known"),
                new("multiple", "Да, несколько компаний (группа / холдинг)", 1.0, ConfidenceClass: "known"),
                new("registering", "Компания находится в процессе регистрации", 0.5, ConfidenceClass: "known"),
                new("none", "Нет, проект пока работает без юридического лица", 0.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.2, ConfidenceClass: "unknown")
            }
        },

        // 2. COR-C02A (Контекст: юрисдикция основной компании)
        new() {
            Id = "COR-C02A", SectionId = "corporate", Order = 2, Type = "jurisdiction_select", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple,registering" } },
            Question = "Где зарегистрирована основная компания?",
            Explanation = "Контекстный вопрос. Помогает определить систему права (Казахстан, английское право МФЦА, США, ОАЭ, Великобритания или др.).",
            Options = new() {
                new("kz", "Казахстан", 1.0, ConfidenceClass: "known"),
                new("aifc", "МФЦА (AIFC)", 1.0, ConfidenceClass: "known"),
                new("us", "США (Delaware / др.)", 1.0, ConfidenceClass: "known"),
                new("uae", "ОАЭ", 1.0, ConfidenceClass: "known"),
                new("uk", "Великобритания", 1.0, ConfidenceClass: "known"),
                new("other", "Другая страна", 1.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: "unknown")
            }
        },

        // 2B. COR-C02B (Контекст: количество компаний при группе)
        new() {
            Id = "COR-C02B", SectionId = "corporate", Order = 3, Type = "single", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "multiple,several" } },
            Question = "Сколько компаний сейчас используется в бизнесе?",
            Explanation = "Определяет количество юридических лиц в структуре бизнеса.",
            Options = new() {
                new("2", "2 компании", 1.0, ConfidenceClass: "known"),
                new("3", "3 компании", 1.0, ConfidenceClass: "known"),
                new("4plus", "4 и более компаний", 1.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: "unknown")
            }
        },

        // 2C. COR-C02C (Контекст: юрисдикции и роли остальных компаний группы)
        new() {
            Id = "COR-C02C", SectionId = "corporate", Order = 4, Type = "entity_builder", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "multiple,several" } },
            Question = "Где зарегистрированы остальные компании и для чего они используются?",
            Explanation = "Укажите страну регистрации и ключевые функции (холдинг, клиенты/платежи, IP, найм).",
            Options = new() {
                new("holding", "Владение долями других компаний (холдинг)", 1.0, ConfidenceClass: "known"),
                new("clients", "Работа с клиентами / заключение договоров", 1.0, ConfidenceClass: "known"),
                new("payments", "Получение платежей и выручки", 1.0, ConfidenceClass: "known"),
                new("ip_assets", "Владение продуктом или важными активами", 1.0, ConfidenceClass: "known"),
                new("hiring", "Найм команды и разработчиков", 1.0, ConfidenceClass: "known"),
                new("other", "Другое", 1.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: "unknown")
            }
        },

        // 3. COR-01 (Диагностика: соответствие владения)
        new() {
            Id = "COR-01", SectionId = "corporate", DimensionId = "ownership_accuracy", Order = 4, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Соответствует ли зарегистрированное в реестре владение тому, как вы фактически понимаете доли сооснователей?",
            Options = new() {
                new("match", "Зарегистрированные доли полностью соответствуют текущим договоренностям", 1.0, ConfidenceClass: "known"),
                new("planned_change", "В целом соответствуют, но есть запланированные изменения", 0.8, ConfidenceClass: "known"),
                new("future_unregistered", "Есть договоренности о будущем изменении долей, которые пока не оформлены", 0.5, Severity: "HIGH", RiskCode: "COR_OWNERSHIP_MISMATCH", ConfidenceClass: "partial"),
                new("material_mismatch", "Есть значимые расхождения между реестром и договоренностями", 0.2, Severity: "HIGH", RiskCode: "COR_OWNERSHIP_MISMATCH", ConfidenceClass: "known"),
                new("dispute", "Есть спор о том, кому фактически должна принадлежать часть компании", 0.0, Severity: "CRITICAL", RiskCode: "COR_OWNERSHIP_DISPUTE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 4. COR-02 (Диагностика: Cap table)
        new() {
            Id = "COR-02", SectionId = "corporate", DimensionId = "cap_table", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Насколько достоверно можно определить, кому принадлежит и может принадлежать капитал компании (Cap table)?",
            Options = new() {
                new("complete", "Есть актуальная таблица (Cap table), отражающая всех владельцев и известные будущие права", 1.0, ConfidenceClass: "known"),
                new("current_plus_separate", "Текущие владельцы отражены, отдельные будущие права учитываются отдельно", 0.8, ConfidenceClass: "known"),
                new("irregular", "Таблица есть, но обновляется нерегулярно", 0.5, Severity: "MEDIUM", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: "partial"),
                new("fragmented", "Информация находится в разных документах, таблицах или переписке", 0.25, Severity: "HIGH", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: "partial"),
                new("none", "Нет единого понимания структуры капитала", 0.0, Severity: "HIGH", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 5. COR-03 (Диагностика: обещания капитала / опционы)
        new() {
            Id = "COR-03", SectionId = "corporate", DimensionId = "equity_commitments", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Есть ли обещанные доли, акции или опционы (команде, адвайзерам, инвесторам), не отраженные в структуре?",
            Options = new() {
                new("none", "Нет, никаких неучтенных обещаний нет", 1.0, ConfidenceClass: "known"),
                new("documented_included", "Есть, обязательства документированы и учтены в таблице долей", 1.0, ConfidenceClass: "known"),
                new("documented_not_included", "Есть документированные обещания, но таблица долей их пока не отражает", 0.65, Severity: "MEDIUM", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: "partial"),
                new("informal", "Есть устные или неформальные обещания долей/опционов", 0.25, Severity: "HIGH", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: "known"),
                new("unclear_terms", "Есть обещания, по которым условия и проценты не до конца определены", 0.15, Severity: "HIGH", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: "partial"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 6. COR-04 (Диагностика: история изменений)
        new() {
            Id = "COR-04", SectionId = "corporate", DimensionId = "corporate_history", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Происходили ли в истории компании изменения состава участников, долей или выпусков акций?",
            Options = new() {
                new("none", "Изменений не было (состав тот же с момента создания)", 1.0, ConfidenceClass: "known"),
                new("complete", "Да, и все изменения полностью и корректно оформлены документами", 1.0, ConfidenceClass: "known"),
                new("main_docs", "Основные документы есть, но не уверен(а) в их полной комплектности", 0.7, ConfidenceClass: "partial"),
                new("partial", "Часть изменений оформлялась позднее или неполностью", 0.4, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: "partial"),
                new("missing", "Были изменения, по которым документы отсутствуют или утеряны", 0.1, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 7. COR-04A (Диагностика: непрерывность истории изменений)
        new() {
            Id = "COR-04A", SectionId = "corporate", DimensionId = "corporate_history", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "COR-04", Op = "in", Value = "complete,main_docs,partial,missing" } },
            Question = "Можно ли по имеющимся документам непрерывно восстановить последовательность всех прошлых изменений капитала?",
            Options = new() {
                new("yes", "Да, можно восстановить непрерывную цепочку всех изменений", 1.0, ConfidenceClass: "known"),
                new("partial", "Можно восстановить только частично", 0.5, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: "partial"),
                new("no", "Нет, цепочка прерывается / есть пробелы", 0.0, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 8. COR-05 (Диагностика: корпоративные решения / Approvals)
        new() {
            Id = "COR-05", SectionId = "corporate", DimensionId = "corporate_approvals", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Оформлялись ли корпоративные решения (протоколы собраний, согласия) по существенным действиям компании?",
            Options = new() {
                new("systematic", "Решения оформляются системно по всем значимым событиям", 1.0, ConfidenceClass: "known"),
                new("main", "По основным и ключевым вопросам решения оформляются", 0.75, ConfidenceClass: "known"),
                new("inconsistent", "Практика непоследовательная, часть решений принималась без протоколов", 0.5, Severity: "MEDIUM", RiskCode: "COR_APPROVAL_GAP", ConfidenceClass: "partial"),
                new("often_missing", "Решения часто принимаются и исполняются без отдельного корпоративного оформления", 0.2, Severity: "MEDIUM", RiskCode: "COR_APPROVAL_GAP", ConfidenceClass: "known"),
                new("no_events", "Таких событий пока не было / компания создана недавно", 1.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.1, ConfidenceClass: "unknown")
            }
        },

        // 9. COR-06 (Диагностика: полномочия и подписание сделок)
        new() {
            Id = "COR-06", SectionId = "corporate", DimensionId = "authority", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Четко ли определено, кто юридически имеет право подписывать договоры и принимать финансовые обязательства от имени компании?",
            Options = new() {
                new("clear_limits", "Полномочия и финансовые лимиты генерального директора / директоров четко определены в уставе", 1.0, ConfidenceClass: "known"),
                new("clear_no_limits", "Полномочия генерального директора понятны, специальных внутренних лимитов нет", 0.85, ConfidenceClass: "known"),
                new("multiple_partial", "Несколько человек подписывают документы и принимают обязательства, порядок не полностью формализован", 0.5, Severity: "MEDIUM", RiskCode: "COR_AUTHORITY_GAP", ConfidenceClass: "partial"),
                new("unclear", "Бывает, что обязательства и сделки принимаются людьми без понятных юридических полномочий", 0.15, Severity: "HIGH", RiskCode: "COR_AUTHORITY_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 10A. COR-07 (Для одной компании: оформление активов и отношений)
        new() {
            Id = "COR-07", SectionId = "corporate", DimensionId = "entity_alignment", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 13, DimensionWeight = 13, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,registering" } },
            Question = "Основные активы и отношения бизнеса оформлены на эту компанию?",
            Explanation = "Проверяет, чтобы ключевые права на продукт, договоры с клиентами и выручка не оставались на личных счетах или сторонних лицах.",
            Options = new() {
                new("aligned", "Да, ключевые активы, договоры с клиентами и выручка оформлены на эту компанию", 1.0, ConfidenceClass: "known"),
                new("minor_exceptions", "В целом да, но есть отдельные договоры или платежи через основателей", 0.75, ConfidenceClass: "known"),
                new("material_outside", "Существенная часть договоров, прав на код или оплат проходит через физлиц / сторонние лица", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 10B. COR-07_GROUP (Для группы компаний: распределение ролей в структуре)
        new() {
            Id = "COR-07_GROUP", SectionId = "corporate", DimensionId = "entity_alignment", Order = 12, Type = "single", ScoreMode = "diagnostic", Weight = 13, DimensionWeight = 13, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "multiple,several" } },
            Question = "Понятно ли, какую роль выполняет каждая компания в структуре бизнеса?",
            Explanation = "Проверяет, насколько последовательно разделены функции холдинга, операционной компании и владельца IP.",
            Options = new() {
                new("aligned", "Да, роли компаний четко разделены и понятны (холдинг, продажи, IP)", 1.0, ConfidenceClass: "known"),
                new("minor_exceptions", "В целом разделены, но бывают временные смешанные переводы или договоры", 0.75, ConfidenceClass: "known"),
                new("group_overlap", "Функции компаний заметно пересекаются, нет четкого разграничения оплат и договоров", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "partial"),
                new("historical_no_logic", "Структура сложилась хаотично, без четкой юридической логики", 0.2, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 11. COR-08 (Диагностика: сохранность корпоративных документов)
        new() {
            Id = "COR-08", SectionId = "corporate", DimensionId = "records", Order = 12, Type = "single", ScoreMode = "diagnostic", Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Можно ли оперативно собрать полный комплект основных корпоративных документов компании (устав, решения, договоры)?",
            Options = new() {
                new("organized", "Все основные документы систематизированы и хранятся в едином безопасном реестре", 1.0, ConfidenceClass: "known"),
                new("scattered", "Основные документы есть, но находятся в разных местах / у разных людей", 0.75, ConfidenceClass: "known"),
                new("reconstruct", "Часть документов утеряна и их приходится восстанавливать", 0.4, Severity: "LOW", RiskCode: "COR_RECORDS_GAP", ConfidenceClass: "partial"),
                new("missing", "Существенные корпоративные документы отсутствуют", 0.1, Severity: "MEDIUM", RiskCode: "COR_RECORDS_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 12. COR-T01 (Триггер: скрытый бенефициар / контроль)
        new() {
            Id = "COR-T01", SectionId = "corporate", Order = 13, Type = "single", ScoreMode = "trigger", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Есть ли в проекте лицо с фактическим экономическим интересом или контролем, которое не указано в официальных документах?",
            Options = new() {
                new("none", "Нет, все реальные бенефициары и владельцы указаны формально", 1.0, ConfidenceClass: "known"),
                new("formal", "Есть формально оформленная холдинговая структура, которую мы понимаем", 1.0, ConfidenceClass: "known"),
                new("indirect", "Есть косвенное или доверительное владение", 0.6, Severity: "HIGH", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: "partial"),
                new("informal", "Есть неформальная понятийная договоренность о скрытом контроле / доле", 0.0, Severity: "CRITICAL", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.3, ConfidenceClass: "unknown")
            }
        },

        // =====================================================================
        // БЛОК 3. ИНТЕЛЛЕКТУАЛЬНАЯ СОБСТВЕННОСТЬ И ПРАВА НА ПРОДУКТ (IP) v1.1
        // =====================================================================

        // 1. IP-01 (Контекст: стадия продукта)
        new() {
            Id = "IP-01", SectionId = "ip", Order = 1, Type = "single", ScoreMode = "context", Weight = 0,
            Question = "Есть ли уже созданный продукт или его часть?",
            Explanation = "Позволяет определить стадию разработки: на стадии идеи диагностика прав на продукт проходит по облегченному сценарию.",
            Options = new() {
                new("idea", "Пока есть только идея", 1.0, ConfidenceClass: "known"),
                new("prototype", "Есть прототип или тестовая версия", 1.0, ConfidenceClass: "known"),
                new("ready", "Есть готовый продукт", 1.0, ConfidenceClass: "known"),
                new("multiple", "Есть несколько продуктов", 1.0, ConfidenceClass: "known")
            }
        },

        // 2. IP-02 (Контекст: карта ключевых IP-активов)
        new() {
            Id = "IP-02", SectionId = "ip", Order = 2, Type = "multiple", ScoreMode = "context", Weight = 0,
            Question = "Что важно для работы продукта?",
            Explanation = "Формирует карту нематериальных активов проекта (код, приложения, базы данных, бренды).",
            Options = new() {
                new("code", "Программный код", 1.0, ConfidenceClass: "known"),
                new("app", "Мобильное приложение", 1.0, ConfidenceClass: "known"),
                new("web", "Сайт или веб-платформа", 1.0, ConfidenceClass: "known"),
                new("design", "Дизайн и интерфейс", 1.0, ConfidenceClass: "known"),
                new("database", "База данных", 1.0, ConfidenceClass: "known"),
                new("own_data", "Собственные данные или подборки данных", 1.0, ConfidenceClass: "known"),
                new("content", "Тексты, видео, изображения или другой контент", 1.0, ConfidenceClass: "known"),
                new("brand", "Название и бренд", 1.0, ConfidenceClass: "known"),
                new("domain", "Домен", 1.0, ConfidenceClass: "known"),
                new("technology", "Собственная технология или техническое решение", 1.0, ConfidenceClass: "known"),
                new("other", "Другое", 1.0, ConfidenceClass: "known")
            }
        },

        // 3. IP-03 (Контекст: создатели и авторы продукта)
        new() {
            Id = "IP-03", SectionId = "ip", Order = 3, Type = "multiple", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "Кто участвовал в создании продукта?",
            Explanation = "Определяет цепочки создания продукта для адаптивного ветвления вопросов о правах.",
            Options = new() {
                new("founders", "Я или другие основатели", 1.0, ConfidenceClass: "known"),
                new("employees", "Штатные сотрудники", 1.0, ConfidenceClass: "known"),
                new("contractors", "Фрилансеры или частные разработчики", 1.0, ConfidenceClass: "known"),
                new("studio", "Внешняя студия или компания-разработчик", 1.0, ConfidenceClass: "known"),
                new("former", "Бывшие сотрудники или подрядчики", 1.0, ConfidenceClass: "known"),
                new("acquired", "Купили готовую разработку у другого лица или компании", 1.0, ConfidenceClass: "known"),
                new("third_party", "Использовали готовые сторонние решения", 1.0, ConfidenceClass: "known"),
                new("unknown", "Не уверен", 0.5, ConfidenceClass: "unknown")
            }
        },

        // 4. IP-04 (Диагностика: права на продукт в целом)
        new() {
            Id = "IP-04", SectionId = "ip", DimensionId = "overall_rights", Order = 4, Type = "single", ScoreMode = "diagnostic", Weight = 22, DimensionWeight = 22, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "Есть ли документы, из которых понятно, что созданный продукт принадлежит компании?",
            Explanation = "Инвестор и Due Diligence проверяют наличие правовой цепочки перехода прав на ключевой продукт.",
            Options = new() {
                new("all", "Документы есть по всему ключевому продукту", 1.0, ConfidenceClass: "known"),
                new("main", "По основной части продукта документы есть", 0.75, ConfidenceClass: "known"),
                new("some", "Документы есть только по отдельным частям", 0.45, Severity: "MEDIUM", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: "partial"),
                new("informal", "Договорились, но специально не оформляли", 0.20, Severity: "HIGH", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: "known"),
                new("none", "Подтверждающих документов практически нет", 0.0, Severity: "CRITICAL", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: "unknown")
            }
        },

        // 5. IP-05 (Диагностика: вклад основателей)
        new() {
            Id = "IP-05", SectionId = "ip", DimensionId = "founder_rights", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "founders" } },
            Question = "Если продукт создавали основатели, оформляли ли передачу созданного компании?",
            Explanation = "Код и архитектура, созданные основателями до или во время работы компании, требуют официальной передачи (IP Assignment).",
            Options = new() {
                new("assigned", "Да, это оформлено документами (договор передачи / акт)", 1.0, ConfidenceClass: "known"),
                new("covered", "Предусмотрено в соглашении между основателями", 0.90, ConfidenceClass: "known"),
                new("partial", "Оформлена только часть прав", 0.50, Severity: "MEDIUM", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: "partial"),
                new("agreed", "Договорились передать, но пока не оформили", 0.35, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: "known"),
                new("founder_owned", "Нет, созданное пока остается оформлено на основателей", 0.10, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: "unknown")
            }
        },

        // 6. IP-06 (Диагностика: служебные произведения сотрудников)
        new() {
            Id = "IP-06", SectionId = "ip", DimensionId = "employee_rights", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "employees" } },
            Question = "Есть ли документы, регулирующие права на то, что сотрудники создают в работе?",
            Explanation = "Служебные произведения переходят компании только при наличии трудового договора, должностных инструкций и служебных заданий/актов.",
            Options = new() {
                new("all", "Да, по всем сотрудникам (трудовые договоры + положения об IP)", 1.0, ConfidenceClass: "known"),
                new("key_gaps", "По ключевым сотрудникам да, по некоторым есть пробелы", 0.70, ConfidenceClass: "known"),
                new("not_reviewed", "Договоры есть, но этот вопрос специально не проверяли", 0.50, Severity: "MEDIUM", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "partial"),
                new("missing_some", "По части разработчиков или сотрудников таких документов нет", 0.20, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, Severity: "MEDIUM", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "unknown")
            }
        },

        // 7. IP-07 (Диагностика: права на результат внешних разработчиков)
        new() {
            Id = "IP-07", SectionId = "ip", DimensionId = "external_creators", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "contractors" } },
            Question = "С внешними разработчиками есть документы, из которых понятно, кому принадлежит результат?",
            Explanation = "Оплата счета или инвойса не передает исключительные права автоматически. Нужен договор авторского заказа / услуг с явной передачей прав.",
            Options = new() {
                new("all", "Да, по всем ключевым подрядчикам оформлены договоры и акты передачи прав", 1.0, ConfidenceClass: "known"),
                new("most", "По большинству есть, но по отдельным людям есть пробелы", 0.70, ConfidenceClass: "known"),
                new("unclear_clause", "Договоры есть, но в них неясно, кому принадлежит созданный результат", 0.35, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "partial"),
                new("payment_only", "Есть только счета, акты или подтверждение оплаты без передачи прав", 0.20, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "known"),
                new("no_contract", "Письменных договоров не было", 0.0, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: "unknown")
            }
        },

        // 8. IP-08 (Диагностика: права ушедших авторов)
        new() {
            Id = "IP-08", SectionId = "ip", DimensionId = "external_creators", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "former" } },
            Question = "Есть ли среди создателей важной части продукта те, кто уже не работает?",
            Explanation = "Если ключевой разработчик ушел без подписанных актов передачи прав, после ухода закрыть такой разрыв значительно сложнее.",
            Options = new() {
                new("none", "Нет, все продолжают работать", 1.0, ConfidenceClass: "known"),
                new("complete", "Да, но все необходимые документы и акты подписаны", 1.0, ConfidenceClass: "known"),
                new("partial", "Да, и по отдельным ушедшим людям документы неполные", 0.50, Severity: "HIGH", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: "partial"),
                new("unresolved", "Да, и с кем-то вопрос о правах вообще не оформлялся", 0.10, Severity: "CRITICAL", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: "known"),
                new("dispute", "Есть открытый спор или претензии", 0.0, Severity: "CRITICAL", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: "unknown")
            }
        },

        // 9. IP-09 (Диагностика: разработка внешней студией)
        new() {
            Id = "IP-09", SectionId = "ip", DimensionId = "external_creators", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 20,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "studio" } },
            Question = "Если продукт делала внешняя компания, понятно ли, кто создавал код и переданы ли вам права на весь результат?",
            Explanation = "Студия могла привлекать субподрядчиков без прав на сублицензирование. Требуются прямые гарантии отчуждения исключительных прав.",
            Options = new() {
                new("confirmed", "Да, это понятно и подтверждено договором и актами", 1.0, ConfidenceClass: "known"),
                new("agency_only", "Договор со студией есть, но кто выполнял работы, не проверяли", 0.70, Severity: "MEDIUM", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: "known"),
                new("subcontractors_unchecked", "Привлекались субподрядчики, документы на них не проверяли", 0.40, Severity: "HIGH", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: "partial"),
                new("unknown_chain", "Не знаем, кто фактически писал код", 0.15, Severity: "HIGH", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.20, Severity: "MEDIUM", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: "unknown")
            }
        },

        // 10. IP-10 (Диагностика: работа основателя у стороннего работодателя)
        new() {
            Id = "IP-10", SectionId = "ip", DimensionId = "external_employer", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 40,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = "contains", Value = "founders" } },
            Question = "Создавал ли основатель продукт, одновременно работая в другой компании?",
            Explanation = "Если продукт создавался в период работы по найму в IT-сфере, прежний работодатель может заявить права на служебное произведение.",
            Options = new() {
                new("no", "Нет, создавал только вне найма", 1.0, ConfidenceClass: "known"),
                new("unrelated", "Да, но это никак не связано со сферой работодателя", 0.90, ConfidenceClass: "known"),
                new("lawyer_checked", "Да, и этот вопрос проверяли с юристом (есть согласие работодателя)", 1.0, ConfidenceClass: "known"),
                new("not_reviewed", "Да, но отдельно этот вопрос не проверяли", 0.35, ConfidenceClass: "partial"),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: "unknown")
            }
        },

        // 11. IP-10A (Диагностика: ресурсы стороннего работодателя)
        new() {
            Id = "IP-10A", SectionId = "ip", DimensionId = "external_employer", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 60,
            ShowIf = new() { new() { QuestionId = "IP-10", Op = "in", Value = "unrelated,lawyer_checked,not_reviewed,unknown" } },
            Question = "Использовались ли рабочее время, оборудование, данные или ресурсы той компании?",
            Explanation = "Использование корпоративного ноутбука или репозитория работодателя — главный триггер судебных споров о принадлежности кода (Moonlighting claim).",
            Options = new() {
                new("no", "Нет, использовались строго личные ресурсы и нерабочее время", 1.0, ConfidenceClass: "known"),
                new("possible", "Возможно (рабочий ноутбук, офисный интернет или репозитории)", 0.45, ConfidenceClass: "partial"),
                new("yes", "Да, использовались ресурсы работодателя", 0.10, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: "unknown")
            }
        },

        // 12. IP-11 (Контекст: готовый код и Open Source)
        new() {
            Id = "IP-11", SectionId = "ip", Order = 12, Type = "single", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "Использовали ли разработчики готовый код, библиотеки или сторонние компоненты?",
            Explanation = "Помогает оценить лицензионную чистоту используемых библиотек и зависимостей.",
            Options = new() {
                new("no", "Нет, только полностью собственный код", 1.0, ConfidenceClass: "known"),
                new("yes", "Да, используются Open Source библиотеки и фреймворки", 1.0, ConfidenceClass: "known"),
                new("likely", "Скорее всего да, но не знаю подробностей", 0.8, ConfidenceClass: "partial"),
                new("unknown", "Не уверен", 0.5, ConfidenceClass: "unknown")
            }
        },

        // 13. IP-11A (Диагностика: лицензионный аудит сторонних компонентов)
        new() {
            Id = "IP-11A", SectionId = "ip", DimensionId = "third_party_dependencies", Order = 13, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "IP-11", Op = "in", Value = "yes,likely,unknown" } },
            Question = "Проверяли ли, на каких условиях можно использовать готовые компоненты?",
            Explanation = "Вирусные лицензии (GPL/AGPL) могут обязать компанию раскрыть весь исходный коммерческий код в публичный доступ.",
            Options = new() {
                new("yes", "Да, это системно проверяется (нет вирусных GPL/AGPL-лицензий)", 1.0, ConfidenceClass: "known"),
                new("main", "Проверяли только основные компоненты", 0.75, ConfidenceClass: "known"),
                new("developers_only", "Разработчики сами следят, отдельно мы это не проверяли", 0.50, ConfidenceClass: "partial"),
                new("no", "Нет, аудит лицензий не проводился", 0.20, ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: "unknown")
            }
        },

        // 14. IP-12 (Диагностика: внешняя критическая зависимость)
        new() {
            Id = "IP-12", SectionId = "ip", DimensionId = "third_party_dependencies", Order = 14, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "Есть ли внешняя технология или сервис, без которого продукт не сможет нормально работать?",
            Explanation = "Зависимость от проприетарного API (OpenAI, Stripe, Google Maps) создает риски непрерывности бизнеса при блокировке или смене тарифов.",
            Options = new() {
                new("no", "Нет существенной зависимости (легко заменить)", 1.0, ConfidenceClass: "known"),
                new("known", "Есть, и условия использования понятны и защищены договором", 1.0, ConfidenceClass: "known"),
                new("unchecked", "Есть, но ограничения и риски блокировки не проверяли", 0.55, Severity: "MEDIUM", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: "partial"),
                new("critical", "Значительная часть продукта зависит от такого решения (риск вендор-лока)", 0.25, Severity: "HIGH", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.30, Severity: "MEDIUM", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: "unknown")
            }
        },

        // 15. IP-13 (Диагностика: контроль технических активов)
        new() {
            Id = "IP-13", SectionId = "ip", DimensionId = "technical_control", Order = 15, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "На чьи аккаунты оформлены важные сервисы и доступы продукта (GitHub, AWS, Google Cloud, App Store)?",
            Explanation = "Оформление репозиториев и серверов на личные почты сотрудников создает риск потери доступа к продукту при конфликте или уходе.",
            Options = new() {
                new("company", "Все критические аккаунты оформлены строго на корпоративную почту компании", 1.0, ConfidenceClass: "known"),
                new("mixed", "Часть на компанию, часть на личные почты основателей", 0.70, ConfidenceClass: "known"),
                new("one_founder", "Большинство ключевых аккаунтов оформлено на одного основателя", 0.40, Severity: "MEDIUM", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: "known"),
                new("worker", "Часть важных сервисов оформлена на личный аккаунт сотрудника или подрядчика", 0.15, Severity: "HIGH", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.25, Severity: "HIGH", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: "unknown")
            }
        },

        // 16. IP-14 (Диагностика: домен и бренд)
        new() {
            Id = "IP-14", SectionId = "ip", DimensionId = "brand_domain", Order = 16, Type = "single", ScoreMode = "diagnostic", Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "На кого оформлены основной домен и бренд?",
            Explanation = "Доменное имя и товарный знак должны принадлежать компании, чтобы исключить риски шантажа или потери трафика.",
            Options = new() {
                new("company", "Основной домен и оформленные права на бренд находятся у компании", 1.0, ConfidenceClass: "known"),
                new("mixed", "Часть на компанию, часть на основателей", 0.65, ConfidenceClass: "known"),
                new("founder", "Основной домен оформлен на физическое лицо — основателя", 0.40, Severity: "MEDIUM", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: "known"),
                new("worker", "Домен зарегистрирован на сотрудника или подрядчика", 0.15, Severity: "HIGH", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: "known"),
                new("brand_not_registered", "Бренд пока отдельно не регистрировали", 1.0, Severity: "INFO", RiskCode: "IP_BRAND_REGISTRATION_INFO", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.20, Severity: "MEDIUM", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: "unknown")
            }
        },

        // 17. IP-15 (Диагностика: происхождение данных и контента)
        new() {
            Id = "IP-15", SectionId = "ip", DimensionId = "content_provenance", Order = 17, Type = "single", ScoreMode = "diagnostic", Weight = 6, DimensionWeight = 6, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = "eq", Value = "true" } },
            Question = "Если данные или контент важны для продукта, понятно ли происхождение и право их использования?",
            Explanation = "Парсинг чужих баз данных или использование нелицензионных медиафайлов создает прямые риски судебных исков о нарушении авторских прав.",
            Options = new() {
                new("clear", "Да, происхождение и лицензии на все данные полностью понятны", 1.0, ConfidenceClass: "known"),
                new("mostly", "По основной части да, есть незначительные открытые вопросы", 0.75, ConfidenceClass: "known"),
                new("some_unknown", "По некоторым материалам/датасетам уверенности нет", 0.50, Severity: "MEDIUM", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: "partial"),
                new("external_unchecked", "Значительная часть получена парсингом/извне без проверки условий", 0.25, Severity: "HIGH", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.20, Severity: "HIGH", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: "unknown")
            }
        }
    };

    public static readonly List<RiskDefinition> Risks = new()
    {
                // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «СООСНОВАТЕЛИ» (CANONICAL §25 — 18 FINDINGS)
        // =====================================================================
        new() {
            Code = "FND_ACTIVE_DISPUTE",
            RootCauseGroup = "FOUNDER_CONFLICT",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Между основателями уже существует существенный конфликт",
            Finding = "По вашим ответам между основателями есть нерешенные разногласия, которые уже влияют или могут влиять на доли, управление, деньги, права на продукт или выход из компании.",
            WhyItMatters = "В такой ситуации стандартная профилактическая документация может быть недостаточной: сначала нужно определить фактические позиции сторон и существующие права.",
            Recommendation = "Зафиксировать предмет разногласий и позиции сторон до принятия новых существенных решений.",
            Recommendations = new() {
                "Зафиксировать предмет разногласий и позиции сторон.",
                "Проверить действующие корпоративные и договорные документы.",
                "Определить юридический сценарий урегулирования до новых существенных решений."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Урегулировать разногласия с Fenix Law"
        },
        new() {
            Code = "FND_EQUITY_DISPUTE",
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Принадлежность долей между основателями оспаривается или определена неоднозначно",
            Finding = "Система видит спор или существенную неопределенность относительно того, кому должна принадлежать часть компании.",
            WhyItMatters = "Неопределенность по долям напрямую влияет на контроль, экономические права и возможность безопасно менять структуру компании или привлекать инвестиции.",
            Recommendation = "Собрать все договоренности и документы о долях и сопоставить их с зарегистрированным владением.",
            Recommendations = new() {
                "Собрать все договоренности и документы о долях.",
                "Сопоставить их с официально зарегистрированным владением.",
                "До новых сделок определить и оформить согласованную структуру."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Зафиксировать структуру долей"
        },
        new() {
            Code = "FND_DEAD_EQUITY",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Существенная доля может остаться у человека, который больше не участвует в компании",
            Finding = "По вашим ответам полная доля одного из основателей не связана с продолжением его участия, при этом его вклад уже ниже ожидаемого, он неактивен или покинул проект.",
            WhyItMatters = "Это может повлиять на управление компанией, мотивацию действующей команды и будущую инвестиционную проверку.",
            Recommendation = "Определить, какая часть доли должна зависеть от продолжения участия, и согласовать механизм выкупа.",
            Recommendations = new() {
                "Определить, какая часть доли должна зависеть от продолжения участия.",
                "Согласовать последствия обычного и проблемного ухода.",
                "Оформить согласованный механизм в документах."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DEADLOCK",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Компания может оказаться неспособной принять ключевое решение",
            Finding = "У основателей сопоставимый контроль, существенные решения требуют совместного согласия, а понятный механизм выхода из тупиковой ситуации не определен.",
            WhyItMatters = "При серьезном разногласии риск состоит не только в конфликте, но и в фактической неспособности компании принять решение о финансировании, стратегии или другой критичной операции.",
            Recommendation = "Определить перечень совместных решений и зафиксировать правила разрешения тупика.",
            Recommendations = new() {
                "Определить перечень решений, где действительно необходимо совместное согласие.",
                "Согласовать этапы разрешения тупика и конечный механизм.",
                "Закрепить правила в документах между основателями и корпоративных документах."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DEPARTED_UNRESOLVED",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Уход одного из основателей юридически не завершен",
            Finding = "Человек уже перестал активно участвовать в компании, но его доля, полномочия, обязательства или иные последствия выхода остаются неурегулированными.",
            WhyItMatters = "Нерешенный выход может блокировать решения, создавать спор о долях и стать отдельным вопросом при инвестиционной проверке.",
            Recommendation = "Определить права ушедшего основателя и юридически закрыть передачу дел и доли.",
            Recommendations = new() {
                "Определить текущие права и полномочия ушедшего основателя.",
                "Урегулировать судьбу доли и передачу дел.",
                "Синхронизировать договоренности с корпоративными документами и доступами."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_CONFLICT_OF_INTEREST",
            RootCauseGroup = "FOUNDER_CONFLICT_OF_INTEREST",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Сторонняя деятельность основателя может пересекаться с интересами компании",
            Finding = "Один из основателей ведет или может вести деятельность, которая пересекается с бизнесом компании, а правила такого пересечения определены не полностью.",
            WhyItMatters = "Это может создавать спор о приоритетах, клиентах, технологиях или результатах работы и дополнительно влиять на права на продукт.",
            Recommendation = "Определить допустимые и недопустимые пересечения и зафиксировать правила конфликтов интересов.",
            Recommendations = new() {
                "Определить допустимые и недопустимые пересечения.",
                "Проверить обязательства перед внешним работодателем или другим бизнесом.",
                "Зафиксировать правила конфликтов интересов и использования результатов работы."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_ROLE_AMBIGUITY",
            RootCauseGroup = "FOUNDER_GOVERNANCE",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Ответственность за часть ключевых функций распределена не полностью",
            Finding = "По вашим ответам роли основателей понятны лишь частично либо значительная часть функций фактически остается общей.",
            WhyItMatters = "На ранней стадии это может работать неформально, но при росте повышает вероятность споров о полномочиях и ответственности.",
            Recommendation = "Определить владельца каждой ключевой функции и зафиксировать согласованную модель.",
            Recommendations = new() {
                "Определить владельца каждой ключевой функции.",
                "Разделить операционные и совместные решения.",
                "Зафиксировать согласованную модель в документах."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_COMMITMENT_MISMATCH",
            RootCauseGroup = "FOUNDER_COMMITMENT",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Фактический вклад одного из основателей ниже ожидаемого",
            Finding = "Участие одного или нескольких основателей заметно отличается от согласованного объема, а специальные правила на такой случай не определены.",
            WhyItMatters = "Если вклад и доля расходятся длительное время, это может привести к конфликту и проблеме неактивной доли.",
            Recommendation = "Сверить ожидаемую и фактическую занятость и проверить связь с долей.",
            Recommendations = new() {
                "Сверить ожидаемую и фактическую занятость.",
                "Согласовать срок и условия восстановления участия либо иной сценарий.",
                "Проверить, как эта ситуация связана с долей и правилами ухода."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EQUITY_NOT_FORMALIZED",
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Договоренность о долях не полностью оформлена",
            Finding = "Основатели в целом понимают распределение долей, но существующая договоренность подтверждается только частично или не доведена до юридического оформления.",
            WhyItMatters = "При изменении отношений или появлении инвестора устная либо предварительная договоренность может оказаться недостаточной для подтверждения структуры.",
            Recommendation = "Собрать текущую договоренность и оформить итоговую структуру в применимых документах.",
            Recommendations = new() {
                "Собрать текущую договоренность в одном месте.",
                "Сопоставить ее с зарегистрированными правами.",
                "Оформить итоговую структуру в применимых документах."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EQUITY_AMBIGUITY",
            RootCauseGroup = "FOUNDER_EQUITY",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "По долям существуют несколько несовпадающих договоренностей",
            Finding = "По вашим ответам есть разные обещания или неясность относительно распределения долей между основателями.",
            WhyItMatters = "Это может привести к спору о собственности и усложнить корпоративные изменения или инвестиционный раунд.",
            Recommendation = "Собрать все обещания, определить единую структуру и синхронизировать с корпоративными документами.",
            Recommendations = new() {
                "Собрать все обещания и версии договоренностей.",
                "Определить единую согласованную структуру.",
                "Синхронизировать ее с корпоративными документами."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_NO_VESTING",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "HIGH",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Полная доля не связана с продолжением участия основателя",
            Finding = "Сейчас основатель сохраняет всю долю независимо от того, как долго он продолжает работать над компанией.",
            WhyItMatters = "Пока все активно участвуют, это может не создавать непосредственной проблемы, но при раннем уходе в структуре капитала может остаться крупная доля неактивного участника.",
            Recommendation = "Обсудить механизм связи доли с продолжением участия и оформить согласованную модель.",
            Recommendations = new() {
                "Обсудить механизм связи доли с продолжением участия.",
                "Определить последствия раннего ухода.",
                "Оформить согласованную модель."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_INCOMPLETE_LEAVER_RULES",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Последствия разных сценариев ухода основателя определены не полностью",
            Finding = "Компания не полностью различает обычный добровольный уход и уход вследствие серьезного нарушения обязательств.",
            WhyItMatters = "Без заранее согласованных правил один и тот же механизм может применяться к существенно разным ситуациям и стать источником спора.",
            Recommendation = "Определить основные сценарии ухода и зафиксировать правила в документах.",
            Recommendations = new() {
                "Определить основные сценарии ухода.",
                "Согласовать последствия для доли, полномочий и передачи дел.",
                "Закрепить правила в документах."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_GOVERNANCE_AMBIGUITY",
            RootCauseGroup = "FOUNDER_GOVERNANCE",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила принятия решений между основателями определены не полностью",
            Finding = "Не по всем существенным вопросам понятно, кто может решать самостоятельно и где требуется совместное согласие.",
            WhyItMatters = "При росте числа решений и обязательств это повышает риск споров о полномочиях и замедляет управление.",
            Recommendation = "Разделить операционные и ключевые совместные решения и определить пороги согласования.",
            Recommendations = new() {
                "Разделить операционные и ключевые совместные решения.",
                "Определить пороги согласования.",
                "Синхронизировать договоренности с корпоративными полномочиями."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_NO_DEADLOCK_PROTECTION",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Не определен порядок действий при серьезном тупике между основателями",
            Finding = "По вашим ответам специального механизма на случай, если основатели не смогут договориться, нет либо он не доводит ситуацию до окончательного решения.",
            WhyItMatters = "При реальном конфликте переговоров может оказаться недостаточно для продолжения работы компании.",
            Recommendation = "Определить этапы эскалации и закрепить механизм разрешения тупика письменно.",
            Recommendations = new() {
                "Определить этапы эскалации.",
                "Согласовать финальный способ выхода из тупика.",
                "Закрепить механизм письменно."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_EXIT_RULES_MISSING",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила выхода основателя определены не полностью",
            Finding = "Заранее не определены все ключевые действия при уходе основателя: уведомление, передача дел, полномочия и судьба доли.",
            WhyItMatters = "Уход в таком случае приходится урегулировать уже после возникновения интересов сторон, что повышает вероятность конфликта.",
            Recommendation = "Определить процедуру выхода, связать ее с долей и предусмотреть передачу дел.",
            Recommendations = new() {
                "Определить процедуру выхода.",
                "Связать ее с долей и полномочиями.",
                "Предусмотреть передачу дел и доступов."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_CONTRIBUTION_AMBIGUITY",
            RootCauseGroup = "FOUNDER_FINANCING",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Личные вложения основателей учитываются неоднозначно",
            Finding = "В компанию вложены личные средства, но их статус как займа, вклада или расходов определен не полностью.",
            WhyItMatters = "В дальнейшем это может создать разные ожидания о возврате денег и правах основателей.",
            Recommendation = "Собрать историю вложений и оформить подтверждающие решения или договоры займа/вклада.",
            Recommendations = new() {
                "Собрать историю личных вложений.",
                "Определить юридический статус каждой существенной суммы.",
                "Оформить подтверждающие решения или договоры."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_STRATEGIC_MISALIGNMENT",
            RootCauseGroup = "FOUNDER_STRATEGY",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "У основателей различаются ожидания относительно будущего компании",
            Finding = "По вашим ответам есть существенные различия во взглядах на инвестиции, темп роста или возможную продажу компании.",
            WhyItMatters = "Такие различия способны перейти из стратегической дискуссии в спор о финансировании и управлении.",
            Recommendation = "Обсудить ключевые сценарии роста и зафиксировать договоренности, влияющие на управление.",
            Recommendations = new() {
                "Обсудить ключевые сценарии роста и финансирования.",
                "Определить решения, требующие общего согласия.",
                "Зафиксировать договоренности, влияющие на управление и выход."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "FND_DOCUMENTATION_GAP",
            RootCauseGroup = "FOUNDER_DOCUMENTATION",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила между основателями существуют, но закреплены не полностью",
            Finding = "Основные договоренности могут быть понятны участникам, однако система не видит подтверждения, что они собраны в подписанных документах.",
            WhyItMatters = "При изменении отношений доказать содержание устной договоренности или переписки сложнее, чем заранее оформленные правила.",
            Recommendation = "Собрать действующие договоренности и оформить единый согласованный набор правил.",
            Recommendations = new() {
                "Собрать действующие договоренности.",
                "Устранить противоречия между документами и перепиской.",
                "Оформить единый согласованный набор правил."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },

        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «КОРПОРАТИВНАЯ СТРУКТУРА» (v1.1)
        // =====================================================================
        new() {
            Code = "COR_OWNERSHIP_DISPUTE",
            RootCauseGroup = "OWNERSHIP",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существует спор о юридическом владении компанией",
            Finding = "По вашим ответам стороны по-разному понимают, кому должна принадлежать часть компании.",
            WhyItMatters = "Спор о владении влияет на контроль, экономические права и практически неизбежно станет центральным вопросом при сделке или инвестиционной проверке.",
            Recommendation = "Собрать официальные документы и урегулировать расхождения до новых выпусков долей.",
            Recommendations = new() {
                "Собрать официальные документы и договоренности о долях.",
                "Определить фактическую и зарегистрированную структуру владения.",
                "До новых выпусков или сделок юридически урегулировать расхождение."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_OWNERSHIP_MISMATCH",
            RootCauseGroup = "OWNERSHIP",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Юридическое владение компанией не полностью соответствует договоренностям",
            Finding = "Система видит существенное расхождение между тем, как участники понимают распределение долей, и тем, что оформлено в реестре сейчас.",
            WhyItMatters = "Такое расхождение может повлиять на голосование, выплаты дивидендов и привести к отказу инвестора при Due Diligence.",
            Recommendation = "Сопоставить зарегистрированные доли со всеми действующими договоренностями и внести изменения в реестр.",
            Recommendations = new() {
                "Сопоставить зарегистрированные доли со всеми действующими договоренностями.",
                "Определить, какие изменения должны быть оформлены.",
                "Провести необходимые корпоративные действия и обновить реестр/таблицу долей."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_UNDOCUMENTED_EQUITY",
            RootCauseGroup = "EQUITY_PROMISE",
            Severity = "HIGH",
            Priority = "30_DAYS",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существуют права на будущий капитал, которые не отражены полностью",
            Finding = "Одному или нескольким лицам обещано участие в капитале, но обязательства не полностью документированы или не учтены в структуре.",
            WhyItMatters = "Неучтенные обещания могут неожиданно изменить будущие доли и вызвать юридический конфликт с командой или инвестором.",
            Recommendation = "Собрать все обещания долей, опционов и зафиксировать их в официальной опционной программе (ESOP) или соглашении.",
            Recommendations = new() {
                "Собрать все обещания долей и их условия.",
                "Отразить документированные обязательства в единой таблице капитала (Cap table).",
                "Оформить неформальные обещания либо закрыть их до следующей сделки."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_CAP_TABLE_UNRELIABLE",
            RootCauseGroup = "OWNERSHIP",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Невозможно быстро подтвердить полную структуру капитала (Cap table)",
            Finding = "Информация о текущих и будущих правах на доли находится в разных местах либо единой достоверной картины нет.",
            WhyItMatters = "Без надежной картины капитала сложно безопасно выпускать новые доли, планировать раунд и объяснять структуру инвестору.",
            Recommendation = "Сформировать единую актуальную таблицу капитала (Cap Table) и ввести порядок ее обязательного обновления.",
            Recommendations = new() {
                "Собрать зарегистрированное владение, опционы, обещания и инвестиционные обязательства.",
                "Сформировать единую актуальную таблицу капитала.",
                "Ввести порядок обновления после каждого изменения."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_CORPORATE_HISTORY_GAP",
            RootCauseGroup = "CORPORATE_HISTORY",
            Severity = "HIGH",
            Priority = "30_DAYS",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "История изменений капитала подтверждается не полностью",
            Finding = "Часть прошлых изменений участников или долей оформлялась неполно либо последовательность документов нельзя восстановить полностью.",
            WhyItMatters = "Инвестору или покупателю важно понимать не только текущие доли, но и законную последовательность их возникновения.",
            Recommendation = "Собрать документы по каждому изменению капитала и восстановить недостающие решения.",
            Recommendations = new() {
                "Собрать документы по каждому изменению капитала.",
                "Восстановить недостающие решения и регистрационные подтверждения, где это возможно.",
                "Сопоставить историю с текущей таблицей долей."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_APPROVAL_GAP",
            RootCauseGroup = "CORPORATE_GOVERNANCE",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Существенные корпоративные решения оформляются непоследовательно",
            Finding = "Часть значимых действий компании принималась без системного документального оформления корпоративных решений.",
            WhyItMatters = "Это может усложнить подтверждение полномочий и истории существенных сделок при проверке компании.",
            Recommendation = "Определить перечень действий, требующих обязательного корпоративного решения, и ввести регламент.",
            Recommendations = new() {
                "Определить перечень действий, требующих корпоративного решения.",
                "Проверить исторические существенные события.",
                "Ввести единый порядок оформления решений."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_AUTHORITY_GAP",
            RootCauseGroup = "CORPORATE_GOVERNANCE",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Полномочия на подписание договоров и принятие обязательств не определены четко",
            Finding = "Обязательства принимаются лицами без понятных или формализованных полномочий.",
            WhyItMatters = "Сделки, подписанные без должных полномочий, могут быть оспорены контрагентами или участниками, создавая прямые финансовые убытки.",
            Recommendation = "Четко зафиксировать полномочия и финансовые лимиты единоличного исполнительного органа и выдать доверенности.",
            Recommendations = new() {
                "Четко зафиксировать полномочия и финансовые лимиты генерального директора.",
                "Выдать доверенности с однозначным объемом полномочий.",
                "Ввести внутренний регламент согласования договоров."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_ENTITY_MISMATCH",
            RootCauseGroup = "ENTITY_ALIGNMENT",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Ключевые активы или деятельность оформлены на неожиданных лиц / внешние структуры",
            Finding = "Существенная часть деятельности, прав на продукт или активов оформлена вне операционной компании.",
            WhyItMatters = "Инвестор вкладывает деньги в компанию, ожидая, что вся ценность находится внутри неё. Размытие активов блокирует инвестиционный раунд.",
            Recommendation = "Провести аудит нахождения прав и ключевых договоров и перевести их на операционную компанию проекта.",
            Recommendations = new() {
                "Провести аудит нахождения прав и ключевых договоров.",
                "Перевести активы и договоры на операционную компанию проекта.",
                "Разграничить функции компаний группы соглашениями."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_RECORDS_GAP",
            RootCauseGroup = "CORPORATE_RECORDS",
            Severity = "LOW",
            Priority = "LATER",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Корпоративные документы разрознены или требуют систематизации",
            Finding = "Основные документы компании находятся в разных местах или частично утеряны.",
            WhyItMatters = "Затягивает подготовку к Due Diligence и увеличивает операционные риски при любых сделках.",
            Recommendation = "Собрать все оригиналы и скан-копии уставов, свидетельств, решений и организовать защищенный Data Room.",
            Recommendations = new() {
                "Собрать все оригиналы и скан-копии корпоративных документов.",
                "Организовать защищенный цифровой архив (Data Room) компании."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_HIDDEN_CONTROL",
            RootCauseGroup = "HIDDEN_CONTROL",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Фактический контроль или экономический интерес не отражен формально",
            Finding = "Существует неформальная договоренность о контроле или доле лица, не указанного в официальных документах.",
            WhyItMatters = "Скрытый бенефициар — один из главных стоп-факторов для институциональных инвесторов и комплаенса банков.",
            Recommendation = "Провести индивидуальную консультацию с венчурным юристом для безопасной формализации структуры.",
            Recommendations = new() {
                "Провести консультацию с венчурным юристом.",
                "Определить безопасный вариант формализации отношений (опцион, конвертируемый заем, холдинг).",
                "Устранить неформальные риски до привлечения инвестиций."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "CORPORATE_CLEANUP"
        },
        new() {
            Code = "COR_NO_ENTITY_FOR_ACTIVITY",
            RootCauseGroup = "ENTITY_ALIGNMENT",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "corporate",
            Modules = new() { "corporate" },
            Title = "Бизнес уже работает, но отдельная юридическая оболочка еще не сформирована",
            Finding = "Проект уже ведет значимую деятельность, однако отдельная компания отсутствует или еще не завершила регистрацию.",
            WhyItMatters = "В такой ситуации договоры, деньги, права на продукт и обязательства могут возникать непосредственно у основателей, что усложняет последующее структурирование.",
            Recommendation = "Определить подходящую юридическую структуру для текущей модели, зафиксировать возникшие активы и перенести ключевые отношения на компанию.",
            Recommendations = new() {
                "Определить подходящую юридическую структуру для текущей модели.",
                "Зафиксировать, какие активы и обязательства уже возникли у founders.",
                "После регистрации перенести ключевые отношения на компанию."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "CORPORATE_CLEANUP"
        },

        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «ИНТЕЛЛЕКТУАЛЬНАЯ СОБСТВЕННОСТЬ» (IP) v1.1
        // =====================================================================
        new() {
            Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Принадлежность ключевого продукта компании не подтверждена",
            Finding = "Компания уже использует созданный продукт, но нет достаточного документального подтверждения прав компании на его ключевые элементы.",
            WhyItMatters = "Если права на основной технологический актив нельзя подтвердить, это ставит под угрозу коммерциализацию, лицензирование и привлекательность для инвесторов.",
            Recommendation = "Составить перечень ключевых элементов продукта, собрать договоры отчуждения прав и закрыть выявленные разрывы.",
            Recommendations = new() {
                "Составить перечень ключевых элементов продукта и их авторов.",
                "Собрать договоры и документы, подтверждающие переход прав на компанию.",
                "Оформить передачу недостающих прав отдельными соглашениями."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED",
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на часть продукта остаются связанными с основателем",
            Finding = "Один или несколько founders создавали продукт, но передача необходимых прав компании оформлена не полностью.",
            WhyItMatters = "При уходе, конфликте или раунде инвестор может потребовать подтверждения, вправе ли сама компания свободно распоряжаться кодом.",
            Recommendation = "Оформить передачу прав (IP Assignment) от основателей на компанию.",
            Recommendations = new() {
                "Определить, какие результаты были созданы основателями.",
                "Проверить действующие договоры и корпоративные документы.",
                "Оформить передачу недостающих прав компании."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_CONTRACTOR_RIGHTS_GAP",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на результат внешнего разработчика подтверждены не полностью",
            Finding = "Внешний специалист участвовал в создании продукта, но существующие документы не позволяют уверенно подтвердить принадлежность компании всего созданного результата.",
            WhyItMatters = "Факт оплаты работ сам по себе не означает автоматического перехода исключительных прав на код.",
            Recommendation = "Подписать акты приема-передачи с явным указанием отчуждения исключительных прав.",
            Recommendations = new() {
                "Определить вклад конкретного разработчика.",
                "Проверить договор, акты и переписку о правах.",
                "Оформить подтверждение передачи исключительных прав."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_FORMER_DEVELOPER_GAP",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на часть продукта, созданную бывшим разработчиком, требуют первоочередной проверки",
            Finding = "Бывший сотрудник или подрядчик участвовал в создании важной части продукта, а документы о правах неполны, отсутствуют или оспариваются.",
            WhyItMatters = "После прекращения отношений закрыть такой разрыв сложнее; бывший разработчик может потребовать компенсацию или заблокировать сделку.",
            Recommendation = "Собрать договоры, акты и подтверждения передачи прав, а также убедиться в отзыве всех технических доступов.",
            Recommendations = new() {
                "Определить весь вклад бывшего разработчика.",
                "Собрать договоры, акты и подтверждения передачи прав.",
                "Параллельно проверить, закрыты ли его технические доступы."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_STUDIO_RIGHTS_GAP",
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Цепочка прав через внешнюю студию подтверждена не полностью",
            Finding = "Договор с внешней студией существует, но не полностью понятно, кто фактически создавал продукт и могла ли студия передать права на весь результат.",
            WhyItMatters = "Если студия привлекала сторонних субподрядчиков без прав на сублицензирование, права компании на конечный продукт уязвимы.",
            Recommendation = "Запросить гарантии студии об отсутствии сторонних претензий и подтвердить цепочку передачи прав от авторов.",
            Recommendations = new() {
                "Уточнить состав исполнителей студии.",
                "Проверить договорные гарантии и передачу прав.",
                "Закрыть существенные пробелы по ключевым результатам."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_EMPLOYER_RISK",
            RootCauseGroup = "IP_EMPLOYER",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Создание продукта пересекается с работой основателя у другого работодателя",
            Finding = "Основатель создавал продукт в период работы в другой компании, а использование рабочего времени, оборудования, данных или иных ресурсов не исключено либо отдельно не проверялось.",
            WhyItMatters = "Прежний работодатель может заявить права на служебное произведение или потребовать долю в стартапе (Moonlighting dispute).",
            Recommendation = "Провести правовой аудит трудового договора основателя и при необходимости получить письменное подтверждение об отсутствии претензий.",
            Recommendations = new() {
                "Проверить трудовые и иные обязательства основателя перед работодателем.",
                "Определить, когда и с использованием каких ресурсов создавались ключевые результаты.",
                "При необходимости получить подтверждение отсутствия претензий (Release letter)."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_THIRD_PARTY_COMPONENTS",
            RootCauseGroup = "IP_DEPENDENCIES",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Условия использования готовых сторонних компонентов проверены не полностью",
            Finding = "Продукт использует код, библиотеки или другие компоненты, созданные не компанией, а условия их использования контролируются частично либо не проверялись.",
            WhyItMatters = "Отдельные лицензии (GPL, AGPL) могут налагать ограничения на распространение, закрытость кода или коммерческую модель.",
            Recommendation = "Провести аудит используемых Open Source библиотек на совместимость с коммерческой лицензией продукта.",
            Recommendations = new() {
                "Составить перечень ключевых сторонних компонентов.",
                "Определить применимые условия использования (MIT, Apache, GPL).",
                "Проверить компоненты, критичные для коммерческой модели продукта."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_EXTERNAL_DEPENDENCY",
            RootCauseGroup = "IP_DEPENDENCIES",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Ключевая функция продукта зависит от внешней технологии",
            Finding = "Значимая часть работы продукта зависит от сторонней технологии или сервиса, при этом ограничения такой зависимости проверены не полностью.",
            WhyItMatters = "Изменение условий, прекращение доступа или ограничение API может нарушить непрерывность сервиса и обязательства перед клиентами.",
            Recommendation = "Оценить технический и договорный запасной сценарий для критических внешних API.",
            Recommendations = new() {
                "Определить критичные внешние зависимости.",
                "Проверить условия использования и прекращения доступа.",
                "Оценить технический и договорный запасной сценарий."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_ACCESS_CONTROL",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Критически важные технические активы находятся под личным контролем",
            Finding = "Часть ключевых сервисов, репозиториев, доменов или иных технических активов оформлена на конкретного founder, сотрудника или подрядчика.",
            WhyItMatters = "При уходе или конфликте компания может потерять фактический доступ к инфраструктуре, даже если юридически считает себя владельцем.",
            Recommendation = "Перевести все учетные записи и репозитории на корпоративные аккаунты с двухфакторной аутентификацией и резервными правами доступа.",
            Recommendations = new() {
                "Определить перечень критических аккаунтов.",
                "Создать корпоративный контроль и резервные доступы.",
                "Связать изменение доступов с процедурой ухода людей."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_DOMAIN_BRAND_CONTROL",
            RootCauseGroup = "IP_CONTROL",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Домен или оформленные права на бренд находятся вне компании",
            Finding = "Основной домен или часть прав на бренд зарегистрированы на founder, сотрудника либо подрядчика, а не на операционную компанию.",
            WhyItMatters = "Такой актив может оказаться зависимым от отношений с конкретным человеком и потребовать отдельной процедуры передачи.",
            Recommendation = "Перенести домен на корпоративный аккаунт компании.",
            Recommendations = new() {
                "Проверить текущих владельцев домена и оформленных прав.",
                "Определить целевого владельца (компания).",
                "Оформить передачу и корпоративный контроль."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_CONTENT_RIGHTS",
            RootCauseGroup = "IP_CONTENT",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Происхождение части данных или контента как актива не подтверждено",
            Finding = "Значимая часть базы данных, изображений, видео, текстов или других материалов получена из внешних источников, а право использовать их в текущей модели проверено не полностью.",
            WhyItMatters = "Ограничения на использование внешних датасетов или контента могут повлечь претензии правообладателей и блокировку продукта.",
            Recommendation = "Проверить лицензии на используемые датасеты и медиаконтент.",
            Recommendations = new() {
                "Определить источники ключевых материалов.",
                "Проверить разрешения и условия использования.",
                "Заменить или оформить права на проблемные элементы."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_BRAND_REGISTRATION_INFO",
            RootCauseGroup = "IP_CONTROL",
            Severity = "INFO",
            Priority = "LATER",
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Бренд пока не оформлен как отдельный зарегистрированный актив",
            Finding = "Компания использует название или бренд, но отдельная регистрация товарного знака пока не проводилась.",
            WhyItMatters = "Это нормально на ранней стадии; вопрос становится более значимым по мере роста узнаваемости и выхода на новые рынки.",
            Recommendation = "Оценить необходимость и доступность регистрации товарного знака на целевых рынках.",
            Recommendations = new() {
                "Проверить, насколько бренд уже значим для бизнеса.",
                "Оценить доступность и необходимость регистрации на ключевых рынках."
            },
            LawyerRequired = false,
            Resolution = "self_service",
            ServiceCode = "IP_RIGHTS_REVIEW"
        }
    };
}
