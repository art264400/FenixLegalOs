using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class FoundersQuestions
{
    public static readonly IReadOnlyList<DiagnosticQuestion> All = new List<DiagnosticQuestion>
    {
        // БЛОК 1. СООСНОВАТЕЛИ (FOUNDERS) — ПОЛНЫЙ КАНОНИЧЕСКИЙ НАБОР v1.1
        // =====================================================================

        // 1. FND-C01 (Контекст: количество фаундеров)
        new() {
            Id = "FND-C01", SectionId = "founders", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
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
            Id = "FND-C02", SectionId = "founders", Order = 2, Type = QuestionType.EquityInputs, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Если доли уже согласованы, как они распределены между сооснователями?",
            Explanation = "От соотношения долей зависит наличие контроля и вероятность корпоративного тупика (Deadlock).",
            Options = new()
        },

        // 3. FND-C03 (Триггер: ушедшие фаундеры с долями)
        new() {
            Id = "FND-C03", SectionId = "founders", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Trigger, Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
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
            Id = "FND-C04", SectionId = "founders", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
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
            Id = "FND-01", SectionId = "founders", DimensionId = "existing_dispute", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Есть ли сейчас нерешённые разногласия по долям, ролям, деньгам или выходу?",
            Options = new() {
                new("none", "Нет, все ключевые вопросы согласованы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("minor", "Есть отдельные рабочие дискуссии, но без риска конфликта", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("material", "Есть существенные нерешённые вопросы, вызывающие напряжение", 0.25, Severity: "HIGH", RiskCode: "FND_DOCUMENTATION_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("active_conflict", "Активный конфликт между сооснователями", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: ConfidenceClass.Known),
                new("formal_dispute", "Формальный спор / претензии / угроза суда", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 6. FND-02 (Диагностика: разделение ролей)
        new() {
            Id = "FND-02", SectionId = "founders", DimensionId = "roles", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Насколько чётко закреплены роли и зоны ответственности каждого сооснователя?",
            Options = new() {
                new("written", "Закреплены письменно в соглашении с понятными KPI и обязанностями", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("clear_oral", "Понятны всем основателям, но зафиксированы только на словах", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("overlap", "Есть пересечения и споры, кто за что отвечает", 0.25, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("disputed", "Постоянные разногласия по поводу вклада и обязанностей", 0.0, Severity: "HIGH", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 7. FND-03 (Диагностика: занятость и вовлеченность)
        new() {
            Id = "FND-03", SectionId = "founders", DimensionId = "commitment", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Соответствует ли фактическая занятость каждого сооснователя договорённостям?",
            Options = new() {
                new("aligned", "Да, все основатели работают над проектом full-time", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("temporary_part_time", "Часть временно совмещает, но это согласовано всеми и отражено в долях", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("different_accepted", "Вклад по времени различается, но пока всех устраивает", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("below_expected", "Кто-то уделяет проекту гораздо меньше времени без ясных правил", 0.25, Severity: "HIGH", RiskCode: "FND_COMMITMENT_MISMATCH", ConfidenceClass: ConfidenceClass.Partial),
                new("stopped", "Один из сооснователей фактически прекратил работу, сохраняя долю", 0.0, Severity: "CRITICAL", RiskCode: "FND_DEAD_EQUITY", ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 8. FND-04 (Диагностика: определенность долей)
        new() {
            Id = "FND-04", SectionId = "founders", DimensionId = "equity_clarity", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Насколько определённо зафиксировано распределение долей между сооснователями?",
            Options = new() {
                new("registered", "Оформлено в уставе компании / реестре участников", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("written_agreed", "Зафиксировано в подписанном корпоративном договоре", 0.8, ConfidenceClass: ConfidenceClass.Known),
                new("preliminary", "Есть подписанный Term Sheet / предварительный меморандум", 0.6, ConfidenceClass: ConfidenceClass.Partial),
                new("verbal", "Только устная договоренность, в документах не отражено", 0.4, Severity: "MEDIUM", RiskCode: "FND_EQUITY_NOT_FORMALIZED", ConfidenceClass: ConfidenceClass.Known),
                new("ambiguous", "Есть несколько противоречивых обещаний долей", 0.15, Severity: "HIGH", RiskCode: "FND_EQUITY_AMBIGUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("dispute", "Есть открытый спор о распределении долей", 0.0, Severity: "CRITICAL", RiskCode: "FND_EQUITY_DISPUTE", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. FND-05 (Диагностика: Vesting и ранний уход)
        new() {
            Id = "FND-05", SectionId = "founders", DimensionId = "early_exit_equity", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Что происходит с долей основателя, если он прекращает работу раньше оговоренного срока?",
            Options = new() {
                new("vesting", "Оформлен график постепенного закрепления долей (Vesting с периодом Cliff)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("repurchase", "Оформлено обязательство обратного выкупа доли компанией / другими фаундерами", 0.9, ConfidenceClass: ConfidenceClass.Known),
                new("verbal_rule", "Договорились устно, но юридически не закрепили", 0.55, Severity: "MEDIUM", RiskCode: "FND_NO_VESTING", ConfidenceClass: ConfidenceClass.Partial),
                new("retains_all", "Сохраняет всю свою долю независимо от продолжения работы", 0.1, Severity: "HIGH", RiskCode: "FND_EXIT_RULES_MISSING", ConfidenceClass: ConfidenceClass.Known),
                new("not_discussed", "Этот вопрос вообще не обсуждался", 0.0, Severity: "HIGH", RiskCode: "FND_EXIT_RULES_MISSING", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. FND-05A (Диагностика: Good/Bad Leaver)
        new() {
            Id = "FND-05A", SectionId = "founders", DimensionId = "early_exit_equity", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Предусмотрены ли разные условия выкупа доли при обычном уходе и уходе из-за нарушения (Good / Bad Leaver)?",
            Options = new() {
                new("defined", "Да, правила Good/Bad Leaver прописаны документально", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично зафиксированы", 0.7, ConfidenceClass: ConfidenceClass.Partial),
                new("oral", "Только устная договоренность", 0.4, Severity: "MEDIUM", RiskCode: "FND_INCOMPLETE_LEAVER_RULES", ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Нет, условия выкупа одинаковы при любых обстоятельствах", 0.15, Severity: "MEDIUM", RiskCode: "FND_INCOMPLETE_LEAVER_RULES", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. FND-06 (Диагностика: ясность матрицы управления)
        new() {
            Id = "FND-06", SectionId = "founders", DimensionId = "governance", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Зафиксировано ли, какие решения требуют согласования между сооснователями?",
            Options = new() {
                new("written", "Письменно закреплен перечень ключевых совместных решений", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("verbal", "Общее понимание есть, но юридически перечень не оформлен", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Зафиксирована только часть правил в стандартном уставе", 0.5, Severity: "MEDIUM", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("all_together", "Все решения принимаем строго вместе без регламента", 0.25, Severity: "MEDIUM", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Known),
                new("none", "Правил нет, каждый действует по своему усмотрению", 0.0, Severity: "HIGH", RiskCode: "FND_GOVERNANCE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. FND-06A (Контекст: порядок принятия ключевых решений)
        new() {
            Id = "FND-06A", SectionId = "founders", DimensionId = "governance", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0, DimensionWeight = 0, WithinDimensionWeight = 0,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
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
            Id = "FND-07", SectionId = "founders", DimensionId = "deadlock", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Есть ли зафиксированный механизм разрешения тупиковых разногласий (Deadlock), когда голоса равны 50/50?",
            Options = new() {
                new("full", "Да, оформлен полный юридический механизм (решающий голос / Russian Roulette / Texas Shootout / выкуп)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("staged", "Предусмотрены поэтапные переговоры и эскалация", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("casting_vote", "Закреплен решающий голос конкретного фаундера (CEO)", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("mediator_only", "Договорились только о привлечении внешнего медиатора/эксперта", 0.55, ConfidenceClass: ConfidenceClass.Partial),
                new("only_agree", "Механизма нет, надеемся только на умение договариваться", 0.15, Severity: "HIGH", RiskCode: "FND_NO_DEADLOCK_PROTECTION", ConfidenceClass: ConfidenceClass.Known),
                new("none", "Вопрос тупика вообще не продуман", 0.0, Severity: "CRITICAL", RiskCode: "FND_DEADLOCK", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.10, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 14. FND-08 (Диагностика: передача дел и порядок выхода)
        new() {
            Id = "FND-08", SectionId = "founders", DimensionId = "exit_continuity", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Определен ли заранее порядок передачи дел, доступов и полномочий при уходе основателя?",
            Options = new() {
                new("full", "Да, прописан полный регламент передачи доступов, прав на код, клиентов и документов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Есть базовое понимание и список критических доступов", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("oral", "Только устная договоренность", 0.40, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Порядок не определен", 0.10, Severity: "MEDIUM", RiskCode: "FND_ROLE_AMBIGUITY", ConfidenceClass: ConfidenceClass.Known),
                new("already_unresolved", "Кто-то уже ушел, и доступы/дела до конца не переданы", 0.0, Severity: "HIGH", RiskCode: "FND_DEPARTED_UNRESOLVED", ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 15. FND-09 (Диагностика: личные вложения основателей)
        new() {
            Id = "FND-09", SectionId = "founders", DimensionId = "founder_contributions", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 3, DimensionWeight = 3, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Если основатели вкладывают в проект личные деньги, понятно ли, как они учитываются?",
            Options = new() {
                new("none", "Личные деньги не вкладывались (только труд)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("documented", "Все вложения оформлены как займы участников или вклады в капитал", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("small_partial", "Ведется учет расходов в таблице, но без договоров займа", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("material_unclear", "Вложены значительные личные суммы без юридического оформления", 0.25, Severity: "MEDIUM", RiskCode: "FND_CONTRIBUTION_AMBIGUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("dispute", "Есть разногласия по поводу возврата вложенных личных средств", 0.0, Severity: "HIGH", RiskCode: "FND_CONTRIBUTION_AMBIGUITY", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.30, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. FND-10 (Диагностика: конфликт интересов и сторонние проекты)
        new() {
            Id = "FND-10", SectionId = "founders", DimensionId = "conflict_of_interest", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Есть ли у сооснователей другая работа или сторонние проекты, которые могут пересекаться со стартапом?",
            Options = new() {
                new("none", "Нет, все сфокусированы только на этом проекте", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unrelated", "Сторонняя работа есть, но она никак не связана со сферой стартапа", 0.9, ConfidenceClass: ConfidenceClass.Known),
                new("overlap_rules", "Возможное пересечение согласовано между фаундерами письменно с четкими правилами", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("potential_competitor", "Есть сторонний проект в смежной сфере без четкого разделения прав", 0.25, Severity: "HIGH", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: ConfidenceClass.Partial),
                new("employer_same_field", "Один из фаундеров параллельно работает по найму в смежной IT-компании", 0.25, Severity: "HIGH", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: ConfidenceClass.Known),
                new("active_competition", "Фаундер участвует в прямом конкурирующем бизнесе", 0.0, Severity: "CRITICAL", RiskCode: "FND_CONFLICT_OF_INTEREST", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.4, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. FND-11 (Диагностика: стратегическая согласованность)
        new() {
            Id = "FND-11", SectionId = "founders", DimensionId = "strategic_alignment", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 3, DimensionWeight = 3, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "FND-C01", Op = ConditionalOperator.Neq, Value = "solo" } },
            Question = "Совпадают ли взгляды сооснователей на стратегию, темпы роста, привлечение инвестиций и возможную продажу компании?",
            Options = new() {
                new("aligned", "Полное совпадение видения по ключевым целям и финансированию", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("differences_discussed", "Есть рабочие дискуссии, но общее направление согласовано", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("not_discussed", "Стратегические цели и горизонт пока подробно не обсуждались", 0.50, ConfidenceClass: ConfidenceClass.Partial),
                new("material_difference", "Существенные различия во взглядах на темп роста или дивиденды/инвестиции", 0.20, Severity: "MEDIUM", RiskCode: "FND_STRATEGIC_MISALIGNMENT", ConfidenceClass: ConfidenceClass.Partial),
                new("conflict", "Принципиальный конфликт целей (быстрый экзит vs долгосрочный бизнес)", 0.0, Severity: "HIGH", RiskCode: "FND_STRATEGIC_MISALIGNMENT", ConfidenceClass: ConfidenceClass.Known)
            }
        }
    };
}
