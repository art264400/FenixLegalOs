using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class IpQuestions
{
    public static readonly IReadOnlyList<DiagnosticQuestion> All = new List<DiagnosticQuestion>
    {
        // БЛОК 3. ИНТЕЛЛЕКТУАЛЬНАЯ СОБСТВЕННОСТЬ И ПРАВА НА ПРОДУКТ (IP) v1.1
        // =====================================================================

        // 1. IP-01 (Контекст: стадия продукта)
        new() {
            Id = "IP-01", SectionId = "ip", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Есть ли уже созданный продукт или его часть?",
            Explanation = "Позволяет определить стадию разработки: на стадии идеи диагностика прав на продукт проходит по облегченному сценарию.",
            Options = new() {
                new("idea", "Пока есть только идея", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("prototype", "Есть прототип или тестовая версия", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("ready", "Есть готовый продукт", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("multiple", "Есть несколько продуктов", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 2. IP-02 (Контекст: карта ключевых IP-активов)
        new() {
            Id = "IP-02", SectionId = "ip", Order = 2, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Что важно для работы продукта?",
            Explanation = "Формирует карту нематериальных активов проекта (код, приложения, базы данных, бренды).",
            Options = new() {
                new("code", "Программный код", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("app", "Мобильное приложение", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("web", "Сайт или веб-платформа", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("design", "Дизайн и интерфейс", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("database", "База данных", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("own_data", "Собственные данные или подборки данных", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("content", "Тексты, видео, изображения или другой контент", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("brand", "Название и бренд", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("domain", "Домен", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("technology", "Собственная технология или техническое решение", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другое", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 3. IP-03 (Контекст: создатели и авторы продукта)
        new() {
            Id = "IP-03", SectionId = "ip", Order = 3, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Кто участвовал в создании продукта?",
            Explanation = "Определяет цепочки создания продукта для адаптивного ветвления вопросов о правах.",
            Options = new() {
                new("founders", "Я или другие основатели", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("employees", "Штатные сотрудники", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("contractors", "Фрилансеры или частные разработчики", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("studio", "Внешняя студия или компания-разработчик", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("former", "Бывшие сотрудники или подрядчики", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("acquired", "Купили готовую разработку у другого лица или компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("third_party", "Использовали готовые сторонние решения", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен", 0.5, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. IP-04 (Диагностика: права на продукт в целом)
        new() {
            Id = "IP-04", SectionId = "ip", DimensionId = "overall_rights", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 22, DimensionWeight = 22, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Есть ли документы, из которых понятно, что созданный продукт принадлежит компании?",
            Explanation = "Инвестор и Due Diligence проверяют наличие правовой цепочки перехода прав на ключевой продукт.",
            Options = new() {
                new("all", "Документы есть по всему ключевому продукту", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "По основной части продукта документы есть", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Документы есть только по отдельным частям", 0.45, Severity: "MEDIUM", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: ConfidenceClass.Partial),
                new("informal", "Договорились, но специально не оформляли", 0.20, Severity: "HIGH", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: ConfidenceClass.Known),
                new("none", "Подтверждающих документов практически нет", 0.0, Severity: "CRITICAL", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_PRODUCT_RIGHTS_UNCONFIRMED", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. IP-05 (Диагностика: вклад основателей)
        new() {
            Id = "IP-05", SectionId = "ip", DimensionId = "founder_rights", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "founders" } },
            Question = "Если продукт создавали основатели, оформляли ли передачу созданного компании?",
            Explanation = "Код и архитектура, созданные основателями до или во время работы компании, требуют официальной передачи (IP Assignment).",
            Options = new() {
                new("assigned", "Да, это оформлено документами (договор передачи / акт)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("covered", "Предусмотрено в соглашении между основателями", 0.90, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Оформлена только часть прав", 0.50, Severity: "MEDIUM", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: ConfidenceClass.Partial),
                new("agreed", "Договорились передать, но пока не оформили", 0.35, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: ConfidenceClass.Known),
                new("founder_owned", "Нет, созданное пока остается оформлено на основателей", 0.10, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. IP-06 (Диагностика: служебные произведения сотрудников)
        new() {
            Id = "IP-06", SectionId = "ip", DimensionId = "employee_rights", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "employees" } },
            Question = "Есть ли документы, регулирующие права на то, что сотрудники создают в работе?",
            Explanation = "Служебные произведения переходят компании только при наличии трудового договора, должностных инструкций и служебных заданий/актов.",
            Options = new() {
                new("all", "Да, по всем сотрудникам (трудовые договоры + положения об IP)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("key_gaps", "По ключевым сотрудникам да, по некоторым есть пробелы", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("not_reviewed", "Договоры есть, но этот вопрос специально не проверяли", 0.50, Severity: "MEDIUM", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("missing_some", "По части разработчиков или сотрудников таких документов нет", 0.20, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, Severity: "MEDIUM", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. IP-07 (Диагностика: права на результат внешних разработчиков)
        new() {
            Id = "IP-07", SectionId = "ip", DimensionId = "external_creators", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "contractors" } },
            Question = "С внешними разработчиками есть документы, из которых понятно, кому принадлежит результат?",
            Explanation = "Оплата счета или инвойса не передает исключительные права автоматически. Нужен договор авторского заказа / услуг с явной передачей прав.",
            Options = new() {
                new("all", "Да, по всем ключевым подрядчикам оформлены договоры и акты передачи прав", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("most", "По большинству есть, но по отдельным людям есть пробелы", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("unclear_clause", "Договоры есть, но в них неясно, кому принадлежит созданный результат", 0.35, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("payment_only", "Есть только счета, акты или подтверждение оплаты без передачи прав", 0.20, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("no_contract", "Письменных договоров не было", 0.0, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_CONTRACTOR_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. IP-08 (Диагностика: права ушедших авторов)
        new() {
            Id = "IP-08", SectionId = "ip", DimensionId = "external_creators", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 30,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "former" } },
            Question = "Есть ли среди создателей важной части продукта те, кто уже не работает?",
            Explanation = "Если ключевой разработчик ушел без подписанных актов передачи прав, после ухода закрыть такой разрыв значительно сложнее.",
            Options = new() {
                new("none", "Нет, все продолжают работать", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("complete", "Да, но все необходимые документы и акты подписаны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Да, и по отдельным ушедшим людям документы неполные", 0.50, Severity: "HIGH", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("unresolved", "Да, и с кем-то вопрос о правах вообще не оформлялся", 0.10, Severity: "CRITICAL", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("dispute", "Есть открытый спор или претензии", 0.0, Severity: "CRITICAL", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, Severity: "HIGH", RiskCode: "IP_FORMER_DEVELOPER_GAP", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. IP-09 (Диагностика: разработка внешней студией)
        new() {
            Id = "IP-09", SectionId = "ip", DimensionId = "external_creators", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 20, DimensionWeight = 20, WithinDimensionWeight = 20,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "studio" } },
            Question = "Если продукт делала внешняя компания, понятно ли, кто создавал код и переданы ли вам права на весь результат?",
            Explanation = "Студия могла привлекать субподрядчиков без прав на сублицензирование. Требуются прямые гарантии отчуждения исключительных прав.",
            Options = new() {
                new("confirmed", "Да, это понятно и подтверждено договором и актами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("agency_only", "Договор со студией есть, но кто выполнял работы, не проверяли", 0.70, Severity: "MEDIUM", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("subcontractors_unchecked", "Привлекались субподрядчики, документы на них не проверяли", 0.40, Severity: "HIGH", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Partial),
                new("unknown_chain", "Не знаем, кто фактически писал код", 0.15, Severity: "HIGH", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.20, Severity: "MEDIUM", RiskCode: "IP_STUDIO_RIGHTS_GAP", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. IP-10 (Диагностика: работа основателя у стороннего работодателя)
        new() {
            Id = "IP-10", SectionId = "ip", DimensionId = "external_employer", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 40,
            ShowIf = new() { new() { QuestionId = "ip.creators", Op = ConditionalOperator.Contains, Value = "founders" } },
            Question = "Создавал ли основатель продукт, одновременно работая в другой компании?",
            Explanation = "Если продукт создавался в период работы по найму в IT-сфере, прежний работодатель может заявить права на служебное произведение.",
            Options = new() {
                new("no", "Нет, создавал только вне найма", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unrelated", "Да, но это никак не связано со сферой работодателя", 0.90, ConfidenceClass: ConfidenceClass.Known),
                new("lawyer_checked", "Да, и этот вопрос проверяли с юристом (есть согласие работодателя)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("not_reviewed", "Да, но отдельно этот вопрос не проверяли", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. IP-10A (Диагностика: ресурсы стороннего работодателя)
        new() {
            Id = "IP-10A", SectionId = "ip", DimensionId = "external_employer", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 60,
            ShowIf = new() { new() { QuestionId = "IP-10", Op = ConditionalOperator.In, Value = "unrelated,lawyer_checked,not_reviewed,unknown" } },
            Question = "Использовались ли рабочее время, оборудование, данные или ресурсы той компании?",
            Explanation = "Использование корпоративного ноутбука или репозитория работодателя — главный триггер судебных споров о принадлежности кода (Moonlighting claim).",
            Options = new() {
                new("no", "Нет, использовались строго личные ресурсы и нерабочее время", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("possible", "Возможно (рабочий ноутбук, офисный интернет или репозитории)", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("yes", "Да, использовались ресурсы работодателя", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. IP-11 (Контекст: готовый код и Open Source)
        new() {
            Id = "IP-11", SectionId = "ip", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Использовали ли разработчики готовый код, библиотеки или сторонние компоненты?",
            Explanation = "Помогает оценить лицензионную чистоту используемых библиотек и зависимостей.",
            Options = new() {
                new("no", "Нет, только полностью собственный код", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да, используются Open Source библиотеки и фреймворки", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("likely", "Скорее всего да, но не знаю подробностей", 0.8, ConfidenceClass: ConfidenceClass.Partial),
                new("unknown", "Не уверен", 0.5, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 13. IP-11A (Диагностика: лицензионный аудит сторонних компонентов)
        new() {
            Id = "IP-11A", SectionId = "ip", DimensionId = "third_party_dependencies", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "IP-11", Op = ConditionalOperator.In, Value = "yes,likely,unknown" } },
            Question = "Проверяли ли, на каких условиях можно использовать готовые компоненты?",
            Explanation = "Вирусные лицензии (GPL/AGPL) могут обязать компанию раскрыть весь исходный коммерческий код в публичный доступ.",
            Options = new() {
                new("yes", "Да, это системно проверяется (нет вирусных GPL/AGPL-лицензий)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "Проверяли только основные компоненты", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("developers_only", "Разработчики сами следят, отдельно мы это не проверяли", 0.50, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Нет, аудит лицензий не проводился", 0.20, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.20, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 14. IP-12 (Диагностика: внешняя критическая зависимость)
        new() {
            Id = "IP-12", SectionId = "ip", DimensionId = "third_party_dependencies", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Есть ли внешняя технология или сервис, без которого продукт не сможет нормально работать?",
            Explanation = "Зависимость от проприетарного API (OpenAI, Stripe, Google Maps) создает риски непрерывности бизнеса при блокировке или смене тарифов.",
            Options = new() {
                new("no", "Нет существенной зависимости (легко заменить)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("known", "Есть, и условия использования понятны и защищены договором", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unchecked", "Есть, но ограничения и риски блокировки не проверяли", 0.55, Severity: "MEDIUM", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: ConfidenceClass.Partial),
                new("critical", "Значительная часть продукта зависит от такого решения (риск вендор-лока)", 0.25, Severity: "HIGH", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.30, Severity: "MEDIUM", RiskCode: "IP_EXTERNAL_DEPENDENCY", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 15. IP-13 (Диагностика: контроль технических активов)
        new() {
            Id = "IP-13", SectionId = "ip", DimensionId = "technical_control", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "На чьи аккаунты оформлены важные сервисы и доступы продукта (GitHub, AWS, Google Cloud, App Store)?",
            Explanation = "Оформление репозиториев и серверов на личные почты сотрудников создает риск потери доступа к продукту при конфликте или уходе.",
            Options = new() {
                new("company", "Все критические аккаунты оформлены строго на корпоративную почту компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mixed", "Часть на компанию, часть на личные почты основателей", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("one_founder", "Большинство ключевых аккаунтов оформлено на одного основателя", 0.40, Severity: "MEDIUM", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: ConfidenceClass.Known),
                new("worker", "Часть важных сервисов оформлена на личный аккаунт сотрудника или подрядчика", 0.15, Severity: "HIGH", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.25, Severity: "HIGH", RiskCode: "IP_ACCESS_CONTROL", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. IP-14 (Диагностика: домен и бренд)
        new() {
            Id = "IP-14", SectionId = "ip", DimensionId = "brand_domain", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "На кого оформлены основной домен и бренд?",
            Explanation = "Доменное имя и товарный знак должны принадлежать компании, чтобы исключить риски шантажа или потери трафика.",
            Options = new() {
                new("company", "Основной домен и оформленные права на бренд находятся у компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mixed", "Часть на компанию, часть на основателей", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("founder", "Основной домен оформлен на физическое лицо — основателя", 0.40, Severity: "MEDIUM", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: ConfidenceClass.Known),
                new("worker", "Домен зарегистрирован на сотрудника или подрядчика", 0.15, Severity: "HIGH", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: ConfidenceClass.Known),
                new("brand_not_registered", "Бренд пока отдельно не регистрировали", 1.0, Severity: "INFO", RiskCode: "IP_BRAND_REGISTRATION_INFO", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.20, Severity: "MEDIUM", RiskCode: "IP_DOMAIN_BRAND_CONTROL", ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. IP-15 (Диагностика: происхождение данных и контента)
        new() {
            Id = "IP-15", SectionId = "ip", DimensionId = "content_provenance", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 6, DimensionWeight = 6, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "ip.coreProductExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Если данные или контент важны для продукта, понятно ли происхождение и право их использования?",
            Explanation = "Парсинг чужих баз данных или использование нелицензионных медиафайлов создает прямые риски судебных исков о нарушении авторских прав.",
            Options = new() {
                new("clear", "Да, происхождение и лицензии на все данные полностью понятны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "По основной части да, есть незначительные открытые вопросы", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some_unknown", "По некоторым материалам/датасетам уверенности нет", 0.50, Severity: "MEDIUM", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: ConfidenceClass.Partial),
                new("external_unchecked", "Значительная часть получена парсингом/извне без проверки условий", 0.25, Severity: "HIGH", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.20, Severity: "HIGH", RiskCode: "IP_CONTENT_RIGHTS", ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
