using FenixLegalOs.Models;

namespace FenixLegalOs.Data;

public static class DataBank
{
    public const string QuestionBankVersion = "1.1.0-founders-focus";
    public const string ScoringEngineVersion = "1.1.0";
    public const string RiskLibraryVersion = "1.1.0";

    public static readonly List<DiagnosticSection> Sections = new()
    {
        new("founders", 1, "Сооснователи", "Founders", 100)
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

        // 2. FND-C03 (Триггер: ушедшие фаундеры с долями)
        new() {
            Id = "FND-C03", SectionId = "founders", Order = 2, Type = "single", ScoreMode = "trigger", Weight = 0,
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
        }
    };
}
