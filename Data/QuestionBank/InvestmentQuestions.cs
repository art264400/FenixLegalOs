using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class InvestmentQuestions
{
    public static readonly List<DiagnosticQuestion> All = new()
    {
        // =========================================================================
        // INVESTMENT QUESTIONS (INVEST-01 .. INVEST-15)
        // =========================================================================

        // 1. INVEST-01 (Контекст: горизонт привлечения инвестиций)
        new() {
            Id = "INVEST-01", SectionId = "investment", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Планируете ли привлекать инвестиции?",
            Explanation = "Определяет стадию готовности к инвестициям и горизонт фандрайзинга.",
            Options = new() {
                new("none", "Нет, пока не планируем", null, ConfidenceClass: ConfidenceClass.Known),
                new("possible_year", "Возможно в течение ближайшего года", null, ConfidenceClass: ConfidenceClass.Known),
                new("6_12", "Планируем в ближайшие 6–12 месяцев", null, ConfidenceClass: ConfidenceClass.Known),
                new("3_6", "Планируем в ближайшие 3–6 месяцев", null, ConfidenceClass: ConfidenceClass.Known),
                new("searching", "Уже ищем инвесторов", null, ConfidenceClass: ConfidenceClass.Known),
                new("specific", "Уже обсуждаем условия с конкретным инвестором", null, ConfidenceClass: ConfidenceClass.Known),
                new("terms", "Уже получили предложение с условиями сделки", null, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 2. INVEST-02 (Диагностика: наличие прошлых инвестиций)
        new() {
            Id = "INVEST-02", SectionId = "investment", DimensionId = "prior_investments", Order = 2, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 50,
            Question = "Получала ли компания раньше деньги в обмен на долю или право получить долю в будущем?",
            Explanation = "Прошлые инвестиции определяют юридическую чистоту структуры капитала и обязательств.",
            Options = new() {
                new("no", "Нет", null, ConfidenceClass: ConfidenceClass.Known),
                new("formal", "Да, все оформлено документами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Да, часть договоренностей оформлена не полностью", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("informal", "Да, были только переводы денег или устные договоренности", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 3. INVEST-02A (Диагностика: четкость прав прошлых инвесторов)
        new() {
            Id = "INVEST-02A", SectionId = "investment", DimensionId = "prior_investments", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 50,
            ShowIf = new() {
                new() {
                    Any = new() {
                        new() { QuestionId = "investment.priorInvestment", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "investment.priorInvestment", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Можете ли точно объяснить, какие права получит каждый прошлый инвестор и когда?",
            Explanation = "Неясные права прошлых инвесторов создают риски для основателей и будущих раундов.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "По основным инвесторам да", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("unclear", "Есть условия, которые мне не до конца понятны", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. INVEST-03 (Диагностика: будущая структура долей)
        new() {
            Id = "INVEST-03", SectionId = "investment", DimensionId = "future_ownership", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new() {
                        new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" },
                        new() { QuestionId = "investment.priorInvestment", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "capital.equityPromises", Op = ConditionalOperator.NotIn, Value = new List<string> { "none", "unknown" } }
                    }
                }
            },
            Question = "Понимаете ли, как будут выглядеть доли после учета обещаний и прошлых инвестиций?",
            Explanation = "Понимание будущей структуры долей (fully diluted cap table) критично для основателей.",
            Options = new() {
                new("exact", "Да, можем это точно посчитать", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly_promises", "В целом да, но есть несколько неоформленных обещаний", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("current_only", "Знаем текущие доли, но будущие изменения не считали", 0.40, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Нет полной картины", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. INVEST-04 (Диагностика: размытие долей основателей)
        new() {
            Id = "INVEST-04", SectionId = "investment", DimensionId = "dilution", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Считали ли, как изменятся доли основателей после нового инвестиционного раунда?",
            Explanation = "Модель размытия позволяет оценить долю фаундеров после входа нового инвестора и создания опционного пула.",
            Options = new() {
                new("yes", "Да, понимаем примерно, сколько останется у каждого", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("one_scenario", "Считали только основной сценарий", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("rough", "Примерно понимаем, но подробно не считали", 0.50, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. INVEST-05 (Диагностика: размер раунда и использование средств)
        new() {
            Id = "INVEST-05", SectionId = "investment", DimensionId = "round_definition", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Определили ли, сколько денег хотите привлечь и зачем?",
            Explanation = "Четкость размера раунда и целей расходования средств необходима для обоснования оценки инвесторам.",
            Options = new() {
                new("clear", "Сумма и основные направления расходов определены", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("amount_rough", "Сумма определена примерно", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("use_clear_amount_pending", "Понимаем, на что нужны деньги, но сумму еще считаем", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("max_possible", "Хотим привлечь максимально возможную сумму", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Не определили", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. INVEST-06 (Диагностика: знание финансового запаса)
        new() {
            Id = "INVEST-06", SectionId = "investment", DimensionId = "runway", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 60,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Знаете ли, на сколько месяцев хватит текущих денег без инвестиций?",
            Explanation = "Регулярный контроль финансового запаса (runway) определяет переговорную позицию стартапа.",
            Options = new() {
                new("regular", "Да, считаем это регулярно", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("rough", "Примерно знаем", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("old", "Считали давно", 0.40, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. INVEST-06A (Диагностика: фактический диапазон runway)
        new() {
            Id = "INVEST-06A", SectionId = "investment", DimensionId = "runway", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 40,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" },
                new() { QuestionId = "investment.runwayKnown", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "На сколько месяцев хватит текущих средств компании?",
            Explanation = "Категория финансового запаса компании (критично менее 3 месяцев).",
            Options = new() {
                new("lt3", "Меньше 3 месяцев", 0.20, ConfidenceClass: ConfidenceClass.Known),
                new("3_6", "3–6 месяцев", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("6_12", "6–12 месяцев", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("gt12", "Более 12 месяцев", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. INVEST-07 (Диагностика: финансовая модель)
        new() {
            Id = "INVEST-07", SectionId = "investment", DimensionId = "financial_model", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Есть ли финансовый план доходов, расходов и потребности в деньгах?",
            Explanation = "Актуальная финансовая модель подтверждает понимание юнит-экономики и структуры затрат.",
            Options = new() {
                new("current", "Есть актуальный финансовый план, регулярно обновляем", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("simple", "Есть, но он достаточно простой", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("old", "Есть, но давно не обновлялся", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("fragments", "Есть только отдельные расчеты", 0.30, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. INVEST-08 (Диагностика: подтверждаемость показателей)
        new() {
            Id = "INVEST-08", SectionId = "investment", DimensionId = "metrics_evidence", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Можете ли подтвердить инвестору основные цифры о компании?",
            Explanation = "Подтверждаемость метрик (выручка, пользователи, когорты, расходы) выписками и аналитикой.",
            Options = new() {
                new("yes", "Основные показатели подтверждаются отчетами и документами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("most", "Большинство можно подтвердить", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("approx", "Часть показателей считается вручную или приблизительно", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("hard", "Есть важные показатели, которые подтвердить будет сложно", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. INVEST-09 (Диагностика: папка документов для DD)
        new() {
            Id = "INVEST-09", SectionId = "investment", DimensionId = "dd_documents", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 85,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Сможете ли быстро собрать основные документы для проверки инвестора?",
            Explanation = "Готовность Data Room и систематизация договоров, корпоративных решений и прав на продукт.",
            Options = new() {
                new("organized", "Документы уже собраны и систематизированы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "Основные документы собраны, потребуется немного времени", 0.80, ConfidenceClass: ConfidenceClass.Known),
                new("scattered", "Документы есть, но находятся в разных местах", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("reconstruct", "Часть документов придется искать или восстанавливать", 0.30, ConfidenceClass: ConfidenceClass.Known),
                new("missing", "Значительной части документов пока нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. INVEST-10 (Триггер / самооценка: известные проблемы)
        new() {
            Id = "INVEST-10", SectionId = "investment", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Trigger, Weight = 0,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Есть ли известные юридические или финансовые вопросы, о которых инвестор спросит?",
            Explanation = "Самооценка фаундеров о наличии нерешенных проблем и спорных ситуаций.",
            Options = new() {
                new("none", "Нет известных существенных проблем", null, ConfidenceClass: ConfidenceClass.Known),
                new("small", "Есть несколько небольших вопросов, и мы их исправляем", null, ConfidenceClass: ConfidenceClass.Known),
                new("material_plan", "Есть существенный вопрос, но понимаем, как его решить", null, ConfidenceClass: ConfidenceClass.Known),
                new("material_unresolved", "Есть существенные нерешенные проблемы", null, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", null, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 13. INVEST-11 (Диагностика: презентация pitch deck)
        new() {
            Id = "INVEST-11", SectionId = "investment", DimensionId = "dd_documents", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 15,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Есть ли актуальная презентация для инвестора (pitch deck)?",
            Explanation = "Презентация продукта и инвестиционного предложения.",
            Options = new() {
                new("current", "Да, презентация соответствует текущему состоянию бизнеса", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("old", "Есть, но требует обновления", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("preparing", "Сейчас готовим", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Нет", 0.20, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 14. INVEST-12 (Диагностика: понимание условий сделки)
        new() {
            Id = "INVEST-12", SectionId = "investment", DimensionId = "deal_terms", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 40,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.In, Value = new List<string> { "specific_investor", "terms_received" } }
            },
            Question = "Понимаете ли кроме оценки и процента остальные условия сделки?",
            Explanation = "Понимание юридических и финансовых условий Term Sheet / SHA.",
            Options = new() {
                new("yes", "Да, основные последствия условий понятны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В целом понимаю, но есть несколько непонятных пунктов", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("price_only", "Смотрели в основном на сумму инвестиций и процент", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("unclear", "Большая часть условий непонятна", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("not_reviewed", "Документы еще не анализировали", 0.15, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 15. INVEST-13 (Диагностика: контроль и право вето инвестора)
        new() {
            Id = "INVEST-13", SectionId = "investment", DimensionId = "deal_terms", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.In, Value = new List<string> { "specific_investor", "terms_received" } }
            },
            Question = "Сможет ли инвестор блокировать обычные решения или контролировать управление?",
            Explanation = "Условия корпоративного управления, право вето и зарезервированные вопросы (Reserved Matters).",
            Options = new() {
                new("reserved_only", "Согласие инвестора требуется только по отдельным важным вопросам", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("extra_known", "Есть дополнительные ограничения, но они понятны", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("material", "Инвестор получает значительное влияние на решения", 0.40, ConfidenceClass: ConfidenceClass.Known),
                new("broad_veto", "Без согласия инвестора нельзя принимать многие обычные решения", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не знаю", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. INVEST-14 (Диагностика: экономика выхода и очередность платежей)
        new() {
            Id = "INVEST-14", SectionId = "investment", DimensionId = "deal_terms", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.In, Value = new List<string> { "specific_investor", "terms_received" } }
            },
            Question = "Понятно ли, сколько инвестор получит первым при продаже и как распределится остальное?",
            Explanation = "Ликвидационная привилегия (Liquidation Preference) и экономика распределения при выходе.",
            Options = new() {
                new("yes", "Да, понимаю", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("check_math", "В целом понимаю, но нужно проверить расчеты", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("seen_unclear", "Видел такое условие, но не понимаю последствий", 0.30, ConfidenceClass: ConfidenceClass.Known),
                new("not_discussed", "Такой вопрос не обсуждался", null, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не знаю", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. INVEST-15 (Диагностика: правовая проверка условий сделки)
        new() {
            Id = "INVEST-15", SectionId = "investment", DimensionId = "deal_review", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "investment.timing", Op = ConditionalOperator.In, Value = new List<string> { "specific_investor", "terms_received" } }
            },
            Question = "Проверял ли кто-нибудь документы сделки со стороны компании?",
            Explanation = "Правовая экспертиза инвестиционных документов независимым юристом компании.",
            Options = new() {
                new("specialist", "Проверял юрист с опытом инвестиционных сделок", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("lawyer_unclear", "Проверял юрист, но не уверен в опыте таких сделок", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("self", "Разбираем самостоятельно", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Пока никто не проверял", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
