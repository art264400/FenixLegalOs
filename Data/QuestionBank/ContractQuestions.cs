using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class ContractQuestions
{
    public static readonly List<DiagnosticQuestion> All = new()
    {
        // =========================================================================
        // CONTRACTS QUESTIONS (CONTRACT-01 .. CONTRACT-08A)
        // =========================================================================

        // 1. CONTRACT-01 (Контекст: наличие B2B контрагентов)
        new() {
            Id = "CONTRACT-01", SectionId = "contracts", Order = 1, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Есть ли договоры с корпоративными клиентами, крупными партнерами или важными поставщиками?",
            Explanation = "Декларативный вопрос. Определяет применимость требований к коммерческим договорам (B2B).",
            Options = new() {
                new("clients", "С корпоративными клиентами / B2B-заказчиками", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partners", "С крупными коммерческими партнерами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("suppliers", "С ключевыми поставщиками или подрядчиками", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Есть отдельные договоры, но типовой схемы нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Нет договоров с B2B-клиентами или партнерами", 1.0, Exclusive: true, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 2. CONTRACT-02 (Диагностика: письменная форма договоров)
        new() {
            Id = "CONTRACT-02", SectionId = "contracts", DimensionId = "written_form", Order = 2, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "В какой форме заключаются договоры с корпоративными клиентами / партнерами?",
            Explanation = "Письменная форма (договор, оферта, счет-договор) обязательна для юридической защиты и признания выручки.",
            Options = new() {
                new("always", "Всегда в письменной форме (подписанный договор / ЭДО / акцепт оферты)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("some_in_messages", "В основном письменные, но часть договоренностей только в переписке", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("material_informal", "Существенная часть обязательств строится на устных договоренностях", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("mostly_informal", "Большинство отношений без формальных договоров", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 3. CONTRACT-03 (Диагностика: четкость предмета и критериев приемки)
        new() {
            Id = "CONTRACT-03", SectionId = "contracts", DimensionId = "scope", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Насколько четко в договорах зафиксированы предмет, объем услуг и критерии приемки?",
            Explanation = "Размытый предмет договора — главная причина отказов от оплаты и судебных споров.",
            Options = new() {
                new("clear", "Четко определены: есть ТЗ, SLA, метрики или конкретные этапы и критерии приемки", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В целом понятно, но некоторые формулировки допускают двоякое толкование", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("outside", "Часто выполняются работы за рамками договора без допсоглашений (scope creep)", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("generic", "Предмет описан общими фразами без конкретики и критериев результата", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. CONTRACT-04 (Диагностика: порядок оплаты и условия расторжения)
        new() {
            Id = "CONTRACT-04", SectionId = "contracts", DimensionId = "payment_termination", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Как в договорах урегулированы порядок оплаты, пени и условия расторжения?",
            Explanation = "Четкие условия расторжения и штрафные санкции защищают кассовый разрыв и предотвращают внезапный уход клиента.",
            Options = new() {
                new("clear", "Четко: установлены сроки оплаты, неустойка и порядок одностороннего отказа", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "Порядок оплаты понятен, но условия расторжения или штрафы прописаны слабо", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some_unclear", "Условия оплаты зависят от внешних факторов (клиент платит, когда захочет)", 0.50, ConfidenceClass: ConfidenceClass.Known),
                new("case", "Условия каждый раз разные, расторжение и споры никак не урегулированы", 0.20, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. CONTRACT-05 (Диагностика: ограничение ответственности и распределение рисков)
        new() {
            Id = "CONTRACT-05", SectionId = "contracts", DimensionId = "risk_allocation", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Предусмотрено ли в договорах ограничение ответственности (Liability Cap) и распределение рисков?",
            Explanation = "Ограничение ответственности суммой договора защищает бизнес от катастрофических убытков при сбоях.",
            Options = new() {
                new("clear", "Да, есть четкий лимит ответственности (Liability Cap) и исключение косвенных убытков", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "Ограничение ответственности есть, но не во всех договорах или формулировки слабые", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("general", "Ответственность по закону / без ограничений (полная ответственность стартапа)", 0.40, ConfidenceClass: ConfidenceClass.Known),
                new("weak", "Договоры на стороне клиента с жесткими штрафами и неограниченной ответственностью", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. CONTRACT-06 (Диагностика: соответствие договоров бизнес-модели)
        new() {
            Id = "CONTRACT-06", SectionId = "contracts", DimensionId = "model_match", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Насколько используемые договоры соответствуют реальной бизнес-модели продукта?",
            Explanation = "Использование чужих или устаревших шаблонов создает скрытые налоговые и юридические риски (переквалификация в трудовые отношения, недействительность лицензий).",
            Options = new() {
                new("custom", "Договоры разработаны юристами специально под продукт и бизнес-модель", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("adapted", "Использовались типовые шаблоны, но адаптированы юристом под специфику продукта", 0.80, ConfidenceClass: ConfidenceClass.Known),
                new("templates", "Используются типовые шаблоны из интернета без глубокой юридической адаптации", 0.45, ConfidenceClass: ConfidenceClass.Known),
                new("copied", "Скопированы у конкурентов без понимания рисков и специфики", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. CONTRACT-07 (Диагностика: проверка крупных сделок)
        new() {
            Id = "CONTRACT-07", SectionId = "contracts", DimensionId = "dependency_large_deals", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 40,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" },
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.userTypes", Op = ConditionalOperator.Contains, Value = "companies" },
                        new() { QuestionId = "contracts.counterpartyTypes", Op = ConditionalOperator.Contains, Value = "clients" }
                    }
                }
            },
            Question = "Проходят ли договоры по крупным/нестандартным сделкам юридическую проверку?",
            Explanation = "Крупные сделки на условиях клиента часто содержат скрытые штрафы, эксклюзивность или кабальные обязательства.",
            Options = new() {
                new("reviewed", "Да, все крупные или нестандартные договоры проверяются юристом", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("sometimes", "Проверяются выборочно или силами фаундеров", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("often", "Часто подписываются типовые формы клиентов без правовой экспертизы", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("no_large", "Крупных или нестандартных сделок нет (все клиенты на стандартной оферте)", null, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. CONTRACT-08 (Диагностика: зависимость от ключевых контрагентов)
        new() {
            Id = "CONTRACT-08", SectionId = "contracts", DimensionId = "dependency_large_deals", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 25,
            ShowIf = new() {
                new() { QuestionId = "contracts.b2bRelevant", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Есть ли критическая зависимость от одного или нескольких ключевых клиентов/партнеров?",
            Explanation = "Концентрация выручки на 1–2 клиентах (>30-50%) создает риск кассового разрыва при их уходе.",
            Options = new() {
                new("no", "Нет, клиентская база диверсифицирована (ни один клиент не дает >20% выручки)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("noticeable", "Есть 1–2 крупных клиента (20–40% выручки), но потеря не убьет бизнес", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("material", "Существенная зависимость: один клиент дает 40–70% выручки", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("near_total", "Критическая зависимость: один клиент дает >70% выручки", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. CONTRACT-08A (Диагностика: защита при расторжении с ключевым клиентом)
        new() {
            Id = "CONTRACT-08A", SectionId = "contracts", DimensionId = "dependency_large_deals", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 35,
            ShowIf = new() {
                new() { QuestionId = "contracts.counterpartyDependency", Op = ConditionalOperator.In, Value = new List<string> { "noticeable", "material", "near_total", "unknown" } }
            },
            Question = "Защищен ли бизнес юридически на случай внезапного расторжения с ключевым клиентом/партнером?",
            Explanation = "Наличие длинных сроков уведомления о расторжении, компенсаций и гарантий дает время перестроиться.",
            Options = new() {
                new("protected", "Да: длительный срок уведомления о расторжении (60+ дней), минимальные объемы или компенсация", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("backup", "Есть базовый срок уведомления (30 дней), но компенсаций или гарантий объемов нет", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("serious", "Клиент может выйти в любой момент без штрафов и компенсаций", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
