using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class CorporateQuestions
{
    public static readonly IReadOnlyList<DiagnosticQuestion> All = new List<DiagnosticQuestion>
    {
        // БЛОК 2. КОРПОРАТИВНАЯ СТРУКТУРА (CORPORATE) — КАНОНИЧЕСКИЙ НАБОР v1.1
        // =====================================================================

        // 1. COR-C01 (Контекст: наличие юрлица)
        new() {
            Id = "COR-C01", SectionId = "corporate", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Зарегистрировано ли юридическое лицо, через которое работает проект?",
            Explanation = "Позволяет оценить уровень формализации бизнеса. Если компании пока нет, блок не будет занижать ваш Legal Score.",
            Options = new() {
                new("one", "Да, одна компания", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("multiple", "Да, несколько компаний (группа / холдинг)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("registering", "Компания находится в процессе регистрации", 0.5, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Нет, проект пока работает без юридического лица", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.2, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 2. COR-C02A (Контекст: юрисдикция основной компании)
        new() {
            Id = "COR-C02A", SectionId = "corporate", Order = 2, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple,registering" } },
            Question = "Где зарегистрирована основная компания?",
            Explanation = "Контекстный вопрос. Помогает определить систему права (Казахстан, английское право МФЦА, США, ОАЭ, Великобритания или др.).",
            Options = new() {
                new("kz", "Казахстан", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("aifc", "МФЦА (AIFC)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("us", "США (Delaware / др.)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("uae", "ОАЭ", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("uk", "Великобритания", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другая страна", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 2B. COR-C02B (Контекст: количество компаний при группе)
        new() {
            Id = "COR-C02B", SectionId = "corporate", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "multiple,several" } },
            Question = "Сколько компаний сейчас используется в бизнесе?",
            Explanation = "Определяет количество юридических лиц в структуре бизнеса.",
            Options = new() {
                new("2", "2 компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("3", "3 компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("4plus", "4 и более компаний", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 2C. COR-C02C (Контекст: юрисдикции и роли остальных компаний группы)
        new() {
            Id = "COR-C02C", SectionId = "corporate", Order = 4, Type = QuestionType.EntityBuilder, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "multiple,several" } },
            Question = "Где зарегистрированы остальные компании и для чего они используются?",
            Explanation = "Укажите страну регистрации и ключевые функции (холдинг, клиенты/платежи, IP, найм).",
            Options = new() {
                new("holding", "Владение долями других компаний (холдинг)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("clients", "Работа с клиентами / заключение договоров", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("payments", "Получение платежей и выручки", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("ip_assets", "Владение продуктом или важными активами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("hiring", "Найм команды и разработчиков", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другое", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 3. COR-01 (Диагностика: соответствие владения)
        new() {
            Id = "COR-01", SectionId = "corporate", DimensionId = "ownership_accuracy", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Соответствует ли зарегистрированное в реестре владение тому, как вы фактически понимаете доли сооснователей?",
            Options = new() {
                new("match", "Зарегистрированные доли полностью соответствуют текущим договоренностям", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("planned_change", "В целом соответствуют, но есть запланированные изменения", 0.8, ConfidenceClass: ConfidenceClass.Known),
                new("future_unregistered", "Есть договоренности о будущем изменении долей, которые пока не оформлены", 0.5, Severity: "HIGH", RiskCode: "COR_OWNERSHIP_MISMATCH", ConfidenceClass: ConfidenceClass.Partial),
                new("material_mismatch", "Есть значимые расхождения между реестром и договоренностями", 0.2, Severity: "HIGH", RiskCode: "COR_OWNERSHIP_MISMATCH", ConfidenceClass: ConfidenceClass.Known),
                new("dispute", "Есть спор о том, кому фактически должна принадлежать часть компании", 0.0, Severity: "CRITICAL", RiskCode: "COR_OWNERSHIP_DISPUTE", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. COR-02 (Диагностика: Cap table)
        new() {
            Id = "COR-02", SectionId = "corporate", DimensionId = "cap_table", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Насколько достоверно можно определить, кому принадлежит и может принадлежать капитал компании (Cap table)?",
            Options = new() {
                new("complete", "Есть актуальная таблица (Cap table), отражающая всех владельцев и известные будущие права", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("current_plus_separate", "Текущие владельцы отражены, отдельные будущие права учитываются отдельно", 0.8, ConfidenceClass: ConfidenceClass.Known),
                new("irregular", "Таблица есть, но обновляется нерегулярно", 0.5, Severity: "MEDIUM", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: ConfidenceClass.Partial),
                new("fragmented", "Информация находится в разных документах, таблицах или переписке", 0.25, Severity: "HIGH", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Нет единого понимания структуры капитала", 0.0, Severity: "HIGH", RiskCode: "COR_CAP_TABLE_UNRELIABLE", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. COR-03 (Диагностика: обещания капитала / опционы)
        new() {
            Id = "COR-03", SectionId = "corporate", DimensionId = "equity_commitments", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Есть ли обещанные доли, акции или опционы (команде, адвайзерам, инвесторам), не отраженные в структуре?",
            Options = new() {
                new("none", "Нет, никаких неучтенных обещаний нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("documented_included", "Есть, обязательства документированы и учтены в таблице долей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("documented_not_included", "Есть документированные обещания, но таблица долей их пока не отражает", 0.65, Severity: "MEDIUM", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("informal", "Есть устные или неформальные обещания долей/опционов", 0.25, Severity: "HIGH", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: ConfidenceClass.Known),
                new("unclear_terms", "Есть обещания, по которым условия и проценты не до конца определены", 0.15, Severity: "HIGH", RiskCode: "COR_UNDOCUMENTED_EQUITY", ConfidenceClass: ConfidenceClass.Partial),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. COR-04 (Диагностика: история изменений)
        new() {
            Id = "COR-04", SectionId = "corporate", DimensionId = "corporate_history", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 70,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Происходили ли в истории компании изменения состава участников, долей или выпусков акций?",
            Options = new() {
                new("none", "Изменений не было (состав тот же с момента создания)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("complete", "Да, и все изменения полностью и корректно оформлены документами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main_docs", "Основные документы есть, но не уверен(а) в их полной комплектности", 0.7, ConfidenceClass: ConfidenceClass.Partial),
                new("partial", "Часть изменений оформлялась позднее или неполностью", 0.4, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("missing", "Были изменения, по которым документы отсутствуют или утеряны", 0.1, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. COR-04A (Диагностика: непрерывность истории изменений)
        new() {
            Id = "COR-04A", SectionId = "corporate", DimensionId = "corporate_history", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "COR-04", Op = ConditionalOperator.In, Value = "complete,main_docs,partial,missing" } },
            Question = "Можно ли по имеющимся документам непрерывно восстановить последовательность всех прошлых изменений капитала?",
            Options = new() {
                new("yes", "Да, можно восстановить непрерывную цепочку всех изменений", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Можно восстановить только частично", 0.5, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Нет, цепочка прерывается / есть пробелы", 0.0, Severity: "HIGH", RiskCode: "COR_CORPORATE_HISTORY_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. COR-05 (Диагностика: корпоративные решения / Approvals)
        new() {
            Id = "COR-05", SectionId = "corporate", DimensionId = "corporate_approvals", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Оформлялись ли корпоративные решения (протоколы собраний, согласия) по существенным действиям компании?",
            Options = new() {
                new("systematic", "Решения оформляются системно по всем значимым событиям", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "По основным и ключевым вопросам решения оформляются", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("inconsistent", "Практика непоследовательная, часть решений принималась без протоколов", 0.5, Severity: "MEDIUM", RiskCode: "COR_APPROVAL_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("often_missing", "Решения часто принимаются и исполняются без отдельного корпоративного оформления", 0.2, Severity: "MEDIUM", RiskCode: "COR_APPROVAL_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("no_events", "Таких событий пока не было / компания создана недавно", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.1, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. COR-06 (Диагностика: полномочия и подписание сделок)
        new() {
            Id = "COR-06", SectionId = "corporate", DimensionId = "authority", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Четко ли определено, кто юридически имеет право подписывать договоры и принимать финансовые обязательства от имени компании?",
            Options = new() {
                new("clear_limits", "Полномочия и финансовые лимиты генерального директора / директоров четко определены в уставе", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("clear_no_limits", "Полномочия генерального директора понятны, специальных внутренних лимитов нет", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("multiple_partial", "Несколько человек подписывают документы и принимают обязательства, порядок не полностью формализован", 0.5, Severity: "MEDIUM", RiskCode: "COR_AUTHORITY_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("unclear", "Бывает, что обязательства и сделки принимаются людьми без понятных юридических полномочий", 0.15, Severity: "HIGH", RiskCode: "COR_AUTHORITY_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10A. COR-07 (Для одной компании: оформление активов и отношений)
        new() {
            Id = "COR-07", SectionId = "corporate", DimensionId = "entity_alignment", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 13, DimensionWeight = 13, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,registering" } },
            Question = "Основные активы и отношения бизнеса оформлены на эту компанию?",
            Explanation = "Проверяет, чтобы ключевые права на продукт, договоры с клиентами и выручка не оставались на личных счетах или сторонних лицах.",
            Options = new() {
                new("aligned", "Да, ключевые активы, договоры с клиентами и выручка оформлены на эту компанию", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("minor_exceptions", "В целом да, но есть отдельные договоры или платежи через основателей", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("material_outside", "Существенная часть договоров, прав на код или оплат проходит через физлиц / сторонние лица", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10B. COR-07_GROUP (Для группы компаний: распределение ролей в структуре)
        new() {
            Id = "COR-07_GROUP", SectionId = "corporate", DimensionId = "entity_alignment", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 13, DimensionWeight = 13, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "multiple,several" } },
            Question = "Понятно ли, какую роль выполняет каждая компания в структуре бизнеса?",
            Explanation = "Проверяет, насколько последовательно разделены функции холдинга, операционной компании и владельца IP.",
            Options = new() {
                new("aligned", "Да, роли компаний четко разделены и понятны (холдинг, продажи, IP)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("minor_exceptions", "В целом разделены, но бывают временные смешанные переводы или договоры", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("group_overlap", "Функции компаний заметно пересекаются, нет четкого разграничения оплат и договоров", 0.3, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: ConfidenceClass.Partial),
                new("historical_no_logic", "Структура сложилась хаотично, без четкой юридической логики", 0.2, Severity: "HIGH", RiskCode: "COR_ENTITY_MISMATCH", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. COR-08 (Диагностика: сохранность корпоративных документов)
        new() {
            Id = "COR-08", SectionId = "corporate", DimensionId = "records", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Можно ли оперативно собрать полный комплект основных корпоративных документов компании (устав, решения, договоры)?",
            Options = new() {
                new("organized", "Все основные документы систематизированы и хранятся в едином безопасном реестре", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("scattered", "Основные документы есть, но находятся в разных местах / у разных людей", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("reconstruct", "Часть документов утеряна и их приходится восстанавливать", 0.4, Severity: "MEDIUM", RiskCode: "COR_RECORDS_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("missing", "Существенные корпоративные документы отсутствуют", 0.1, Severity: "MEDIUM", RiskCode: "COR_RECORDS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. COR-T01 (Триггер: скрытый бенефициар / контроль)
        new() {
            Id = "COR-T01", SectionId = "corporate", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Trigger, Weight = 0,
            ShowIf = new() { new() { QuestionId = "COR-C01", Op = ConditionalOperator.In, Value = "one,multiple" } },
            Question = "Есть ли в проекте лицо с фактическим экономическим интересом или контролем, которое не указано в официальных документах?",
            Options = new() {
                new("none", "Нет, все реальные бенефициары и владельцы указаны формально", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("formal", "Есть формально оформленная холдинговая структура, которую мы понимаем", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("indirect", "Есть косвенное или доверительное владение", 0.6, Severity: "HIGH", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: ConfidenceClass.Partial),
                new("informal", "Есть неформальная понятийная договоренность о скрытом контроле / доле", 0.0, Severity: "CRITICAL", RiskCode: "COR_HIDDEN_CONTROL", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.3, ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
