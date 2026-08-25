using FenixLegalOs.Models;

namespace FenixLegalOs.Data;

public static class DataBank
{
    public const string QuestionBankVersion = "1.1.0-founders-focus";
    public const string ScoringEngineVersion = "1.1.0";
    public const string RiskLibraryVersion = "1.1.0";

    public static readonly List<DiagnosticSection> Sections = new()
    {
        new("founders", 1, "Сооснователи", "Founders", 18),
        new("corporate", 2, "Корпоративная структура", "Corporate", 12)
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

        // 2. FND-C02 (Контекст: распределение долей)
        new() {
            Id = "FND-C02", SectionId = "founders", Order = 2, Type = "equity_inputs", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Если доли уже согласованы, как они распределены между сооснователями?",
            Explanation = "От количества сооснователей зависит сложность фиксирования взаимоотношений в команде",
            Options = new() {
                new("not_agreed_yet", "Доли пока окончательно не распределены / в процессе обсуждения", 0.5),
                new("unknown", "Не уверен(а)", 0.5)
            }
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
                new("unresolved", "Да, есть нерешённые вопросы по доле или компенсации", 0, "HIGH", "R_FOUNDERS_NO_LEAVER"),
                new("dispute", "Да, возник открытый конфликт / претензии", 0, "CRITICAL", "R_FOUNDERS_EQUITY_UNFIXED"),
                new("unknown", "Не уверен(а)", 0.5)
            }
        },

        // 3. FND-C04 (Контекст: форма фиксации соглашений)
        new() {
            Id = "FND-C04", SectionId = "founders", Order = 3, Type = "single", ScoreMode = "context", Weight = 0,
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

        // 4. FND-01 (Диагностика: разногласия и конфликты)
        new() {
            Id = "FND-01", SectionId = "founders", DimensionId = "existing_dispute", Order = 4, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли сейчас нерешённые разногласия по долям, ролям, деньгам или выходу?",
            Options = new() {
                new("none", "Нет, все ключевые вопросы согласованы", 1.0, ConfidenceClass: "known"),
                new("minor", "Есть отдельные рабочие дискуссии, но без риска конфликта", 0.75, ConfidenceClass: "known"),
                new("significant", "Есть существенные нерешённые вопросы, вызывающие напряжение", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_AGREEMENT", ConfidenceClass: "partial"),
                new("active_conflict", "Активный конфликт между сооснователями", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("formal_dispute", "Формальный спор / претензии / угроза суда", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known")
            }
        },

        // 5. FND-02 (Диагностика: разделение ролей)
        new() {
            Id = "FND-02", SectionId = "founders", DimensionId = "roles", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько чётко закреплены роли и зоны ответственности каждого сооснователя?",
            Options = new() {
                new("written", "Закреплены письменно в соглашении с понятными KPI и обязанностями", 1.0, ConfidenceClass: "known"),
                new("verbal_clear", "Понятны всем основателям, но зафиксированы только на словах", 0.75, ConfidenceClass: "known"),
                new("overlap", "Есть пересечения и споры, кто за что отвечает", 0.5, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "partial"),
                new("shared", "Многое решаем 'вместе', конкретных зон ответственности нет", 0.25, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "partial"),
                new("dispute", "Постоянные разногласия по поводу вклада и обязанностей", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "known")
            }
        },

        // 6. FND-03 (Диагностика: занятость и вовлеченность)
        new() {
            Id = "FND-03", SectionId = "founders", DimensionId = "commitment", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Соответствует ли фактическая занятость каждого сооснователя договорённостям?",
            Options = new() {
                new("full", "Да, все основатели работают над проектом full-time", 1.0, ConfidenceClass: "known"),
                new("parttime_agreed", "Часть совмещает, но это согласовано всеми и отражено в долях", 0.85, ConfidenceClass: "known"),
                new("accepted_diff", "Вклад по времени различается, но пока всех устраивает", 0.65, ConfidenceClass: "known"),
                new("less_no_rules", "Кто-то уделяет проекту гораздо меньше времени без ясных правил", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_VESTING", ConfidenceClass: "partial"),
                new("stopped", "Один из сооснователей фактически прекратил работу, сохраняя долю", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known")
            }
        },

        // 7. FND-04 (Диагностика: определенность долей)
        new() {
            Id = "FND-04", SectionId = "founders", DimensionId = "equity_clarity", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Насколько определённо зафиксировано распределение долей между сооснователями?",
            Options = new() {
                new("registered", "Оформлено в уставе компании / реестре участников", 1.0, ConfidenceClass: "known"),
                new("written_agreed", "Зафиксировано в подписанном корпоративном договоре", 0.8, ConfidenceClass: "known"),
                new("preliminary", "Есть подписанный Term Sheet / предварительный меморандум", 0.6, ConfidenceClass: "partial"),
                new("verbal", "Только устная договоренность, в документах не отражено", 0.4, Severity: "HIGH", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("ambiguous", "Есть несколько противоречивых обещаний долей", 0.15, Severity: "HIGH", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "partial"),
                new("dispute", "Есть открытый спор о распределении долей", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 8. FND-05 (Диагностика: Vesting и ранний уход)
        new() {
            Id = "FND-05", SectionId = "founders", DimensionId = "early_exit_equity", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Что происходит с долей основателя, если он прекращает работу раньше оговоренного срока?",
            Options = new() {
                new("vesting", "Оформлен график постепенного закрепления долей (Vesting с периодом Cliff)", 1.0, ConfidenceClass: "known"),
                new("repurchase", "Оформлено обязательство обратного выкупа доли компанией / другими фаундерами", 0.9, ConfidenceClass: "known"),
                new("verbal_rule", "Договорились устно, но юридически не закрепили", 0.55, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_VESTING", ConfidenceClass: "partial"),
                new("retains_all", "Сохраняет всю свою долю независимо от продолжения работы", 0.1, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("not_discussed", "Этот вопрос вообще не обсуждался", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 9. FND-05A (Диагностика: Good/Bad Leaver)
        new() {
            Id = "FND-05A", SectionId = "founders", DimensionId = "early_exit_equity", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Предусмотрены ли разные условия выкупа доли при обычном уходе и уходе из-за нарушения (Good / Bad Leaver)?",
            Options = new() {
                new("yes", "Да, правила Good/Bad Leaver прописаны документально", 1.0, ConfidenceClass: "known"),
                new("partial", "Частично зафиксированы", 0.7, ConfidenceClass: "partial"),
                new("verbal", "Только устно", 0.4, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "partial"),
                new("no", "Нет, условия выкупа одинаковы при любых обстоятельствах", 0.15, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_NO_LEAVER", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 10. FND-06 (Диагностика: матрица принятия решений)
        new() {
            Id = "FND-06", SectionId = "founders", DimensionId = "governance", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Зафиксировано ли, какие решения требуют единогласного согласия всех сооснователей?",
            Options = new() {
                new("written", "Письменно закреплен перечень ключевых решений (инвестиции, продажа, найм C-level)", 1.0, ConfidenceClass: "known"),
                new("verbal", "Общее понимание есть, но юридически перечень не оформлен", 0.75, ConfidenceClass: "known"),
                new("partial", "Зафиксирована только часть правил в стандартном уставе", 0.5, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "partial"),
                new("all_together", "Все решения принимаем строго вместе без регламента", 0.25, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("none", "Правил нет, каждый действует по своему усмотрению", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known")
            }
        },

        // 11. FND-07 (Диагностика: Deadlock / тупик при голосовании)
        new() {
            Id = "FND-07", SectionId = "founders", DimensionId = "deadlock", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли зафиксированный механизм разрешения тупиковых разногласий (Deadlock), когда голоса равны 50/50?",
            Options = new() {
                new("mechanism", "Да, оформлен юридический механизм (решающий голос / Russian Roulette / Texas Shootout / медиация)", 1.0, ConfidenceClass: "known"),
                new("stages", "Предусмотрены формализованные этапы переговоров", 0.85, ConfidenceClass: "known"),
                new("casting_vote", "Закреплен решающий голос конкретного фаундера (CEO)", 0.7, ConfidenceClass: "known"),
                new("mediator", "Договорились привлекать внешнего эксперта/эдвайзера", 0.55, ConfidenceClass: "partial"),
                new("only_agree", "Механизма нет, надеемся только на умение договариваться", 0.15, Severity: "HIGH", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("none", "Вопрос тупика вообще не продуман", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_DECISIONS", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.1, ConfidenceClass: "unknown")
            }
        },

        // 12. FND-08 (Диагностика: передача дел при уходе)
        new() {
            Id = "FND-08", SectionId = "founders", DimensionId = "exit_continuity", Order = 12, Type = "single", ScoreMode = "diagnostic", Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Определено ли заранее, как происходит передача дел, доступов и полномочий при уходе основателя?",
            Options = new() {
                new("full", "Да, прописан порядок передачи доступов, прав на код, клиентов и документов", 1.0, ConfidenceClass: "known"),
                new("basic", "Есть базовое понимание и список критических доступов", 0.65, ConfidenceClass: "known"),
                new("verbal", "Только устная договоренность", 0.4, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "partial"),
                new("no", "Порядок не определен", 0.1, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "known"),
                new("departed_unresolved", "Кто-то уже ушел, и доступы/дела до конца не переданы", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_ROLES", ConfidenceClass: "known")
            }
        },

        // 13. FND-09 (Диагностика: личные вложения основателей)
        new() {
            Id = "FND-09", SectionId = "founders", DimensionId = "founder_contributions", Order = 13, Type = "single", ScoreMode = "diagnostic", Weight = 3, DimensionWeight = 3, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Если основатели вкладывают в проект личные деньги, понятно ли, как они учитываются?",
            Options = new() {
                new("no_funds", "Личные деньги не вкладывались (только труд)", 1.0, ConfidenceClass: "known"),
                new("documented", "Все вложения оформлены как займы участников или вклады в капитал", 1.0, ConfidenceClass: "known"),
                new("part_expenses", "Ведется учет расходов в таблице, но без договоров займа", 0.7, ConfidenceClass: "known"),
                new("significant_untracked", "Вложены значительные личные суммы без юридического оформления", 0.25, Severity: "MEDIUM", RiskCode: "R_FOUNDERS_AGREEMENT_PARTIAL", ConfidenceClass: "partial"),
                new("dispute", "Есть разногласия по поводу возврата вложенных личных средств", 0.0, Severity: "HIGH", RiskCode: "R_FOUNDERS_AGREEMENT_PARTIAL", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.3, ConfidenceClass: "unknown")
            }
        },

        // 14. FND-10 (Диагностика: конфликт интересов и сторонние проекты)
        new() {
            Id = "FND-10", SectionId = "founders", DimensionId = "conflict_of_interest", Order = 14, Type = "single", ScoreMode = "diagnostic", Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = "neq", Value = "solo" } },
            Question = "Есть ли у сооснователей другая работа или сторонние проекты, которые могут пересекаться со стартапом?",
            Options = new() {
                new("none", "Нет, все сфокусированы только на этом проекте", 1.0, ConfidenceClass: "known"),
                new("no_overlap", "Сторонняя работа есть, но она никак не связана со сферой стартапа", 0.9, ConfidenceClass: "known"),
                new("settled", "Возможное пересечение согласовано между фаундерами письменно (Non-Compete)", 0.75, ConfidenceClass: "known"),
                new("competing", "Есть сторонний проект в смежной сфере без четкого разделения прав", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_AGREEMENT_PARTIAL", ConfidenceClass: "partial"),
                new("employer", "Один из фаундеров параллельно работает по найму в смежной IT-компании", 0.25, Severity: "HIGH", RiskCode: "R_FOUNDERS_AGREEMENT_PARTIAL", ConfidenceClass: "known"),
                new("active_competition", "Фаундер участвует в прямом конкурирующем бизнесе", 0.0, Severity: "CRITICAL", RiskCode: "R_FOUNDERS_EQUITY_UNFIXED", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.4, ConfidenceClass: "unknown")
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

        // 2. COR-C02 (Контекст: юрисдикция основной компании)
        new() {
            Id = "COR-C02", SectionId = "corporate", Order = 2, Type = "single", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Где зарегистрирована основная компания?",
            Explanation = "Контекстный вопрос, определяет юрисдикцию и применяемую систему права.",
            Options = new() {
                new("kz", "Казахстан", 1.0, ConfidenceClass: "known"),
                new("aifc", "МФЦА", 1.0, ConfidenceClass: "known"),
                new("english_law", "Делавэр, DIFC, ADGM или иные юрисдикции английского права", 1.0, ConfidenceClass: "known"),
                new("other", "Другое", 1.0, ConfidenceClass: "known")
            }
        },

        // 2A. COR-C02A (Контекст: состав группы / другие компании)
        new() {
            Id = "COR-C02A", SectionId = "corporate", Order = 3, Type = "multiple", ScoreMode = "context", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Есть ли в структуре бизнеса другие компании?",
            Explanation = "Помогает понять структуру владения активами и распределение функций между компаниями.",
            Options = new() {
                new("opco", "Есть операционная (-нные) компания", 1.0, ConfidenceClass: "known"),
                new("holdco", "Есть холдинговая компания", 1.0, ConfidenceClass: "known"),
                new("ipco", "Есть отдельная компания с интеллектуальной собственностью", 1.0, ConfidenceClass: "known"),
                new("other_entities", "Иные компании", 1.0, ConfidenceClass: "known"),
                new("none", "Нет других компаний (только одна основная)", 1.0, Exclusive: true, ConfidenceClass: "known")
            }
        },

        // 3. COR-01 (Диагностика: соответствие владения)
        new() {
            Id = "COR-01", SectionId = "corporate", DimensionId = "ownership_accuracy", Order = 3, Type = "single", ScoreMode = "diagnostic", Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
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
            Id = "COR-02", SectionId = "corporate", DimensionId = "cap_table", Order = 4, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
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
            Id = "COR-03", SectionId = "corporate", DimensionId = "equity_commitments", Order = 5, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
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
            Id = "COR-04", SectionId = "corporate", DimensionId = "corporate_history", Order = 6, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 70,
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
            Id = "COR-04A", SectionId = "corporate", DimensionId = "corporate_history", Order = 7, Type = "single", ScoreMode = "diagnostic", Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 30,
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
            Id = "COR-05", SectionId = "corporate", DimensionId = "corporate_approvals", Order = 8, Type = "single", ScoreMode = "diagnostic", Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
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
            Id = "COR-06", SectionId = "corporate", DimensionId = "authority", Order = 9, Type = "single", ScoreMode = "diagnostic", Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
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

        // 10. COR-07 (Диагностика: принадлежность активов / структура группы)
        new() {
            Id = "COR-07", SectionId = "corporate", DimensionId = "entity_alignment", Order = 10, Type = "single", ScoreMode = "diagnostic", Weight = 13, DimensionWeight = 13, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Оформлены ли основные активы, контракты и выручка на операционную компанию, и понятны ли роли компаний в группе?",
            Options = new() {
                new("aligned", "Основные активы и отношения находятся в операционной компании; роли компаний группы четко разделены", 1.0, ConfidenceClass: "known"),
                new("minor_exceptions", "Есть отдельные исторические исключения или небольшие пересечения", 0.75, ConfidenceClass: "known"),
                new("material_outside", "Существенная часть деятельности, прав или активов оформлена на основателей или сторонние лица", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "known"),
                new("group_overlap", "Функции нескольких компаний группы заметно пересекаются и распределены неясно", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "partial"),
                new("historical_no_logic", "Структура сложилась исторически и хаотично, без четкой юридической логики", 0.2, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: "unknown")
            }
        },

        // 11. COR-08 (Диагностика: сохранность корпоративных документов)
        new() {
            Id = "COR-08", SectionId = "corporate", DimensionId = "records", Order = 11, Type = "single", ScoreMode = "diagnostic", Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 100,
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
            Id = "COR-T01", SectionId = "corporate", Order = 12, Type = "single", ScoreMode = "trigger", Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = "in", Value = "one,multiple" } },
            Question = "Есть ли в проекте лицо с фактическим экономическим интересом или контролем, которое не указано в официальных документах?",
            Options = new() {
                new("none", "Нет, все реальные бенефициары и владельцы указаны формально", 1.0, ConfidenceClass: "known"),
                new("formal", "Есть формально оформленная холдинговая структура, которую мы понимаем", 1.0, ConfidenceClass: "known"),
                new("indirect", "Есть косвенное или доверительное владение", 0.6, Severity: "HIGH", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: "partial"),
                new("informal", "Есть неформальная понятийная договоренность о скрытом контроле / доле", 0.0, Severity: "CRITICAL", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: "known"),
                new("unknown", "Не уверен(а)", 0.3, ConfidenceClass: "unknown")
            }
        }
    };

    public static readonly List<RiskDefinition> Risks = new()
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «СООСНОВАТЕЛИ»
        // =====================================================================
        new() {
            Code = "R_FOUNDERS_EQUITY_UNFIXED",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "CRITICAL",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Доли сооснователей не зафиксированы документально",
            Finding = "Доли сооснователей согласованы только на словах или имеют неясный статус без юридического оформления.",
            WhyItMatters = "Устная договорённость работает, пока всё спокойно. При первом конфликте или появлении крупных денег юридически считается, что долей нет, либо компания оформлена на номинала.",
            Recommendation = "Подписать Корпоративный договор / Founder Agreement с фиксацией долей, прав и вкладов каждого сооснователя.",
            Recommendations = new() {
                "Составить и подписать соглашение сооснователей (Founder Agreement) с точными долями.",
                "При зарегистрированном юрлице — синхронизировать фактические доли с официальным реестром участников."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Разобрать структуру между основателями"
        },
        new() {
            Code = "R_FOUNDERS_NO_AGREEMENT",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Отсутствует соглашение сооснователей (Founder Agreement)",
            Finding = "Отношения между фаундерами строятся без оформленного письменного соглашения.",
            WhyItMatters = "Стандартный типовой устав ТОО/LLC не защищает от тупиков при голосовании, внезапного ухода фаундера и кражи интеллектуальной собственности сооснователем.",
            Recommendation = "Разработать персональный Founder Agreement под юрисдикцию компании (Казахстан / МФЦА / Delaware).",
            Recommendations = new() {
                "Разработать правила голосования и утверждения бюджета.",
                "Закрепить передачу создаваемой сооснователями интеллектуальной собственности на компанию."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW",
            Cta = "Разработать соглашение сооснователей"
        },
        new() {
            Code = "R_FOUNDERS_NO_LEAVER",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Не определены правила выхода фаундера (Bad / Good Leaver)",
            Finding = "Нет зафиксированного механизма выкупа доли в случае, если один из сооснователей перестает работать над проектом.",
            WhyItMatters = "Если основатель уйдет через 3 месяца с 40% доли, компания получит 'мертвый капитал' (Dead Equity). Ни один венчурный инвестор не инвестирует в стартап, где крупная доля принадлежит неработающему человеку.",
            Recommendation = "Внедрить концепции Good Leaver и Bad Leaver с дифференцированной ценой выкупа доли.",
            Recommendations = new() {
                "Установить, что при недобросовестном уходе (Bad Leaver) незакрепленная доля выкупается по номинальной стоимости.",
                "Определить порядок рассрочки выкупа, чтобы не вымывать оборотный капитал стартапа."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "R_FOUNDERS_NO_VESTING",
            RootCauseGroup = "FOUNDER_EXIT",
            Severity = "HIGH",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Vesting долей сооснователей не оформлен",
            Finding = "Доли распределены сразу на 100% без привязки к сроку работы над проектом (Reverse Vesting).",
            WhyItMatters = "Стандарт венчурного рынка — вестинг на 3–4 года с 1 годом Cliff. Без вестинга риск потери контроля при уходе фаундера максимален.",
            Recommendation = "Подписать график вестинга долей сооснователей.",
            Recommendations = new() {
                "Оформить опционную схему или договор обратного выкупа (Reverse Vesting).",
                "Установить 1-летний cliff-период для новых сооснователей."
            },
            LawyerRequired = true,
            Resolution = "lawyer_required",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "R_FOUNDERS_DECISIONS",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "HIGH",
            Priority = "NOW",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Отсутствует механизм разрешения тупика (Deadlock Resolution)",
            Finding = "При равенстве голосов 50/50 нет регламента, как принимается финальное решение по ключевым вопросам.",
            WhyItMatters = "Корпоративный тупик полностью парализует бизнес: нельзя привлечь раунд, продлить аренду, нанять команду или закрыть сделку.",
            Recommendation = "Определить процедуру разрешения Deadlock (право решающего голоса CEO, механизм Russian Roulette / Texas Shootout или медиация).",
            Recommendations = new() {
                "Закрепить матрицу полномочий: операционные вопросы решает CEO, стратегические — большинством голосов.",
                "Прописать финальный механизм при неразрешимом конфликте."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "R_FOUNDERS_ROLES",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Роли и зоны ответственности не формализованы",
            Finding = "Разделение обязанностей между сооснователями существует только на словах.",
            WhyItMatters = "Приводит к конфликтам ожиданий, дублированию функций или 'проседанию' направлений (продажи, финансы, комплаенс).",
            Recommendation = "Составить и утвердить соглашение о ролях, ключевых метриках и полномочиях сооснователей.",
            Recommendations = new() {
                "Закрепить должности и сферы единоличного принятия решений для каждого фаундера."
            },
            LawyerRequired = false,
            Resolution = "check_with_lawyer",
            ServiceCode = "FOUNDERS_REVIEW"
        },
        new() {
            Code = "R_FOUNDERS_AGREEMENT_PARTIAL",
            RootCauseGroup = "FOUNDER_CONTROL",
            Severity = "MEDIUM",
            Priority = "30_DAYS",
            SectionId = "founders",
            Modules = new() { "founders" },
            Title = "Правила между сооснователями зафиксированы частично",
            Finding = "Часть договоренностей (Non-Compete, займы, передача IP) не отражена в действующих документах.",
            WhyItMatters = "Неурегулированные вопросы становятся триггерами споров при первом успехе компании или при привлечении раунда.",
            Recommendation = "Дополнить действующие соглашения недостающими пунктами (Non-Compete, учет личных вложений).",
            Recommendations = new() {
                "Провести ревизию всех договоренностей и свести их в единый понятный документ."
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
        }
    };
}
