using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class ProductQuestions
{
    public static readonly List<DiagnosticQuestion> All = new()
    {
        // 1. PROD-01 (Контекст: стадия продукта и пользователи)
        new() {
            Id = "PROD-01", SectionId = "product", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Пользуются ли продуктом реальные пользователи/клиенты?",
            Explanation = "Стадия продукта определяет, какие требования к правилам, возвратам и платежам применимы уже сейчас.",
            Options = new() {
                new("prelaunch", "Пока нет, продукт находится в разработке или тестировании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("first", "Да, есть первые пользователи", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("regular", "Да, продукт уже работает с постоянными пользователями", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("large", "Да, продукт используется большим количеством пользователей", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 2. PROD-02 (Контекст: категории пользователей)
        new() {
            Id = "PROD-02", SectionId = "product", Order = 2, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Кто пользуется продуктом?",
            Explanation = "Категории пользователей определяют применимость потребительского законодательства, требований к B2B-договорам и правил работы с несовершеннолетними.",
            Options = new() {
                new("consumers", "Обычные физические лица", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("entrepreneurs", "Предприниматели", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("companies", "Компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("client_employees", "Сотрудники компаний-клиентов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("minors", "Дети или подростки", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другие категории пользователей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("undecided", "Пока не определились", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 3. PROD-03 (Контекст: формат доступа)
        new() {
            Id = "PROD-03", SectionId = "product", Order = 3, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Как пользователь получает доступ?",
            Explanation = "Формат доступа влияет на точку заключения пользовательского соглашения и требования к интерфейсу.",
            Options = new() {
                new("website", "Регистрируется на сайте", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("app", "Регистрируется в приложении", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("guest", "Покупает без регистрации", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("paid_access", "Получает доступ после оплаты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("invite", "Получает приглашение от компании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("individual_contract", "Договор заключается отдельно с каждым клиентом", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другой вариант", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 4. PROD-04 (Диагностика: наличие правил)
        new() {
            Id = "PROD-04", SectionId = "product", DimensionId = "rules_presence", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            Question = "Есть ли документ с основными правилами пользователя?",
            Explanation = "Пользовательское соглашение или оферта фиксируют права, обязанности и порядок разрешения споров с клиентами.",
            Options = new() {
                new("current", "Да, такой документ есть и используется сейчас", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("old", "Есть, но давно не обновлялся", 0.6, ConfidenceClass: ConfidenceClass.Known),
                new("template", "Есть шаблонный документ, который почти не меняли под продукт", 0.4, ConfidenceClass: ConfidenceClass.Known),
                new("preparing", "Документ сейчас готовится", null, ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Нет", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. PROD-05 (Диагностика: соответствие правил продукту)
        new() {
            Id = "PROD-05", SectionId = "product", DimensionId = "rules_match", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "product.userRulesStatus", Op = ConditionalOperator.In, Value = new List<string> { "current", "old", "template" } } },
            Question = "Описывает ли документ реальную работу продукта сейчас?",
            Explanation = "Расхождения между фактическим процессом работы сервиса и правилами лишают компанию правовой защиты при спорах.",
            Options = new() {
                new("yes", "Документ готовился именно под текущую работу продукта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В основном да, но продукт немного изменился", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("changed", "Продукт заметно изменился, а документ не обновляли", 0.3, ConfidenceClass: ConfidenceClass.Known),
                new("template_unchecked", "Документ взят из шаблона и отдельно не сверялся с продуктом", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. PROD-06 (Диагностика: ясность предложения)
        new() {
            Id = "PROD-06", SectionId = "product", DimensionId = "offer_clarity", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.liveUsers", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.userStage", Op = ConditionalOperator.Eq, Value = "prelaunch" }
                    }
                }
            },
            Question = "Понятно ли пользователю до оплаты/использования, что он получает?",
            Explanation = "Прозрачность описания тарифов и функциональности снижает риски претензий о введении в заблуждение и чарджбэков.",
            Options = new() {
                new("clear", "Услуга или функциональность описаны достаточно ясно", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В основном да", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some_unclear", "Есть отдельные условия, которые могут быть непонятны", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("mismatch", "Описание продукта и фактический результат могут заметно отличаться", 0.2, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. PROD-07 (Контекст: фактический поставщик / маркетплейс)
        new() {
            Id = "PROD-07", SectionId = "product", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Кто фактически предоставляет основной товар/услугу?",
            Explanation = "Разделение ролей определяет, выступает ли компания прямым продавцом, маркетплейсом или агентом.",
            Options = new() {
                new("company", "Наша компания сама", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("joint", "Наша компания вместе с партнером", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("marketplace", "Другой продавец или исполнитель, а наша компания предоставляет площадку", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("varies", "Зависит от конкретного продукта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. PROD-07A (Диагностика: разграничение ответственности сторон)
        new() {
            Id = "PROD-07A", SectionId = "product", DimensionId = "company_role", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "product.providerRole", Op = ConditionalOperator.In, Value = new List<string> { "joint", "marketplace", "varies", "unknown" } } },
            Question = "Понятно ли, за что отвечает компания, а за что другой продавец/исполнитель?",
            Explanation = "Неясное разграничение ответственности маркетплейса и третьих лиц создает риск возложения всех претензий покупателей на компанию.",
            Options = new() {
                new("clear", "Да, это четко разделено", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В основном понятно", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Описано частично", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("unclear", "Нет четкого разделения", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. PROD-08 (Диагностика: акцепт правил)
        new() {
            Id = "PROD-08", SectionId = "product", DimensionId = "terms_acceptance", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 70,
            ShowIf = new() {
                new() { QuestionId = "product.liveUsers", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "product.userRulesStatus", Op = ConditionalOperator.NotIn, Value = new List<string> { "none", "preparing" } }
            },
            Question = "Перед использованием пользователь подтверждает согласие с правилами?",
            Explanation = "Для юридической силы оферты требуется доказуемый акцепт (чекбокс, кнопка регистрации/оплаты со ссылкой на условия).",
            Options = new() {
                new("explicit", "Пользователь сам подтверждает согласие перед регистрацией или покупкой", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("link_only", "Пользователь видит ссылку на правила, но отдельного подтверждения нет", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("published_only", "Правила просто размещены на сайте или в приложении", 0.3, ConfidenceClass: ConfidenceClass.Known),
                new("no_rules", "Правил пока нет", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. PROD-09 (Диагностика: доказательства согласия)
        new() {
            Id = "PROD-09", SectionId = "product", DimensionId = "terms_acceptance", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() { QuestionId = "product.termsAcceptance", Op = ConditionalOperator.Eq, Value = "explicit" },
                new() { QuestionId = "product.liveUsers", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Сохраняется ли, когда и с какой версией правил согласился пользователь?",
            Explanation = "Логирование факта акцепта (timestamp, IP, версия документа) позволяет доказать заключение договора в суде или банке.",
            Options = new() {
                new("versioned", "Да, сохраняется время и версия правил", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("fact_only", "Сохраняется только факт согласия", 0.7, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Не сохраняется", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. PROD-10 (Контекст: модель монетизации)
        new() {
            Id = "PROD-10", SectionId = "product", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Платит ли пользователь компании?",
            Explanation = "Модель монетизации определяет финансовые и потребительские риски сервиса.",
            Options = new() {
                new("free", "Нет, продукт бесплатный", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("one_off", "Да, разовая оплата", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("subscription", "Да, регулярная подписка", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mixed", "Есть и разовые платежи, и подписка", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("commission", "Компания получает комиссию с операций", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другая модель", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 12. PROD-11 (Диагностика: прозрачность цен)
        new() {
            Id = "PROD-11", SectionId = "product", DimensionId = "payment_transparency", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "product.paid", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "До оплаты понятно, сколько и за что платит пользователь?",
            Explanation = "Скрытые комиссии, автоматические списания и неясная тарификация ведут к претензиям потребителей и чарджбэкам.",
            Options = new() {
                new("clear", "Полная стоимость понятна заранее", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В основном да, но могут возникать отдельные дополнительные платежи", 0.7, ConfidenceClass: ConfidenceClass.Known),
                new("late_fees", "Есть комиссии или платежи, которые становятся понятны позже", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 13. PROD-12 (Диагностика: политика возвратов)
        new() {
            Id = "PROD-12", SectionId = "product", DimensionId = "refunds", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "product.paid", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Понятно ли заранее, когда пользователь может вернуть деньги?",
            Explanation = "Условия возврата средств за цифровые товары и услуги регулируются специальными нормами защиты прав потребителей.",
            Options = new() {
                new("published", "Правила возврата определены и опубликованы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("case_policy", "Возвраты рассматриваются индивидуально, но общий подход есть", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("unclear", "Четких правил нет", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("no_refunds", "Возвраты обычно не предусмотрены", null, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 14. PROD-13 (Контекст: автопродление подписки)
        new() {
            Id = "PROD-13", SectionId = "product", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "product.subscription", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Продлевается ли подписка автоматически?",
            Explanation = "Автопродление подписок жестко регулируется платежными системами и законодательством о защите прав потребителей.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("depends", "Зависит от тарифа", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 15. PROD-13A (Диагностика: уведомление об автопродлении)
        new() {
            Id = "PROD-13A", SectionId = "product", DimensionId = "subscription_mechanics", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.autoRenew", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.autoRenew", Op = ConditionalOperator.Eq, Value = "depends" }
                    }
                }
            },
            Question = "Понятно ли до оплаты, что подписка продлится автоматически?",
            Explanation = "Требования Visa/Mastercard и регуляторов обязывают явно информировать о периодических списаниях до первого платежа.",
            Options = new() {
                new("clear", "Да, это явно показывается до оплаты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("terms_only", "Это указано только в правилах", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. PROD-14 (Диагностика: отмена подписки)
        new() {
            Id = "PROD-14", SectionId = "product", DimensionId = "subscription_mechanics", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 45,
            ShowIf = new() { new() { QuestionId = "product.subscription", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Может ли пользователь понятно отменить подписку?",
            Explanation = "Процесс отмены подписки должен быть не сложнее процесса оформления (в один клик в интерфейсе).",
            Options = new() {
                new("self_service", "Отмена доступна в аккаунте или приложении", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("support", "Нужно обратиться в поддержку", 0.6, ConfidenceClass: ConfidenceClass.Known),
                new("complex", "Процесс отмены сложный или зависит от ситуации", 0.3, ConfidenceClass: ConfidenceClass.Known),
                new("undefined", "Специальный порядок не определен", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. PROD-15 (Диагностика: списание после триала)
        new() {
            Id = "PROD-15", SectionId = "product", DimensionId = "subscription_mechanics", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 10,
            ShowIf = new() { new() { QuestionId = "product.subscription", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "После пробного периода автоматически списывается оплата?",
            Explanation = "Платные триалы требуют заблаговременного предупреждения пользователя перед первой автооплатой.",
            Options = new() {
                new("no_trial", "Пробного периода нет", null, ConfidenceClass: ConfidenceClass.Known),
                new("no_autocharge", "Есть пробный период, после него автоматического списания нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("clear", "После пробного периода оплата списывается автоматически, и пользователь явно уведомлен", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("terms_only", "Оплата списывается автоматически, это указано только в условиях", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("not_explained", "Оплата списывается автоматически, но этот момент специально не объясняется", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 18. PROD-16 (Диагностика: правила блокировки аккаунтов)
        new() {
            Id = "PROD-16", SectionId = "product", DimensionId = "account_restrictions", Order = 18, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 60,
            ShowIf = new() { new() { QuestionId = "product.liveUsers", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Описаны ли в правилах причины блокировки аккаунта или ограничения доступа?",
            Explanation = "Право сервиса приостановить обслуживание должно быть закреплено в оферте с понятным перечнем оснований.",
            Options = new() {
                new("clear", "Основные причины описаны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Описаны только некоторые случаи", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("case_by_case", "Компания принимает решения по ситуации", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Специальных правил нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 19. PROD-17 (Диагностика: баланс при блокировке)
        new() {
            Id = "PROD-17", SectionId = "product", DimensionId = "account_restrictions", Order = 19, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 40,
            ShowIf = new() {
                new() { QuestionId = "product.liveUsers", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "product.paid", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Понятно ли, что происходит с оплаченными услугами или балансом при блокировке?",
            Explanation = "Неурегулированность судьбы неизрасходованного баланса при блокировке аккаунта создает риски исков о неосновательном обогащении.",
            Options = new() {
                new("clear", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("cause_based", "Зависит от причины блокировки, и это описано", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("individual", "Решается индивидуально", 0.5, ConfidenceClass: ConfidenceClass.Known),
                new("undefined", "Не определено", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 20. PROD-18 (Контекст: пользовательский контент UGC)
        new() {
            Id = "PROD-18", SectionId = "product", Order = 20, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Могут ли пользователи публиковать контент (текст, фото, отзывы, товары)?",
            Explanation = "Пользовательский контент (UGC) требует правил модерации, условий об ответственности и механизмов удаления нарушений.",
            Options = new() {
                new("no", "Нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 21. PROD-18A (Диагностика: запрет неправомерного контента)
        new() {
            Id = "PROD-18A", SectionId = "product", DimensionId = "ugc", Order = 21, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Есть ли правила, запрещающие публикацию незаконного или чужого контента?",
            Explanation = "Запрет на размещение чужой интеллектуальной собственности и вредоносных материалов защищает платформу от солидарной ответственности.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("general", "Есть только общие ограничения", 0.6, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 22. PROD-18B (Диагностика: лицензия на контент пользователя)
        new() {
            Id = "PROD-18B", SectionId = "product", DimensionId = "ugc", Order = 22, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Определено ли, как компания может использовать контент пользователей?",
            Explanation = "Сервису требуется неисключительная безвозмездная лицензия на отображение, хранение и обработку контента пользователей.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 23. PROD-19 (Диагностика: обработка жалоб на UGC)
        new() {
            Id = "PROD-19", SectionId = "product", DimensionId = "ugc", Order = 23, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 5, DimensionWeight = 5, WithinDimensionWeight = 10,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.ugc", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Есть ли порядок рассмотрения жалоб на чужой контент или нарушения?",
            Explanation = "Процедура Notice-and-Takedown освобождает платформу от ответственности за контент третьих лиц.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично", 0.6, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Нет", 0.2, ConfidenceClass: ConfidenceClass.Known),
                new("not_needed", "Пока это не требуется для нашей модели", null, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 24. PROD-20 (Контекст: несовершеннолетние пользователи)
        new() {
            Id = "PROD-20", SectionId = "product", Order = 24, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.minorsPossible", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.userTypes", Op = ConditionalOperator.Contains, Value = "consumers" }
                    }
                }
            },
            Question = "Могут ли продуктом пользоваться дети или подростки (до 18 лет)?",
            Explanation = "Обслуживание несовершеннолетних требует согласия родителей и накладывает строгие ограничения на рекламу и обработку данных.",
            Options = new() {
                new("no", "Нет", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("possible", "Возможно, мы это не ограничиваем", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 25. PROD-20A (Диагностика: соответствие требованиям к несовершеннолетним)
        new() {
            Id = "PROD-20A", SectionId = "product", DimensionId = "special_context", Order = 25, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 50,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.minorsAllowed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "product.minorsAllowed", Op = ConditionalOperator.Eq, Value = "possible" },
                        new() { QuestionId = "product.minorsAllowed", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Проверялись ли специальные требования к работе с несовершеннолетними?",
            Explanation = "Сделки с несовершеннолетними без согласия законных представителей могут быть оспорены.",
            Options = new() {
                new("yes", "Да", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 26. PROD-21 (Контекст: география пользователей)
        new() {
            Id = "PROD-21", SectionId = "product", Order = 26, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Из каких стран ваши пользователи?",
            Explanation = "Трансграничные пользователи могут подпадать под действие законодательства стран их нахождения (GDPR, CCPA, локальные законы о защите прав потребителей).",
            Options = new() {
                new("one", "Только в одной стране", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("multiple", "В нескольких странах", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("global", "Продукт доступен глобально", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("not_tracked", "Пока не отслеживаем", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 27. PROD-21A (Диагностика: проверка законодательства других стран)
        new() {
            Id = "PROD-21A", SectionId = "product", DimensionId = "special_context", Order = 27, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 4, DimensionWeight = 4, WithinDimensionWeight = 50,
            ShowIf = new() { new() { QuestionId = "product.userGeography", Op = ConditionalOperator.In, Value = new List<string> { "multiple", "global", "not_tracked", "unknown" } } },
            Question = "Проверялись ли юридические требования в основных странах пользователей?",
            Explanation = "Выход на международные рынки требует проверки применимости местных правил потребителей, налогов и локализации.",
            Options = new() {
                new("main_markets", "Да, для основных рынков", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("initial_only", "Только для первоначальной страны запуска", 0.5, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 28. PROD-22 (Триггер: регулируемые функции)
        new() {
            Id = "PROD-22", SectionId = "product", Order = 28, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Trigger, Weight = 0,
            Question = "Есть ли в продукте специальные или регулируемые функции?",
            Explanation = "Специальные виды деятельности (финтех, медицина, криптовалюты) могут требовать лицензий, уведомлений или соблюдения профильных стандартов.",
            Options = new() {
                new("payments", "Платежи или хранение денег пользователей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("investments", "Инвестиции или торговля финансовыми инструментами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("loans", "Кредиты или займы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("crypto", "Криптовалюты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("health", "Медицинские рекомендации или информация о здоровье", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("hiring", "Поиск работы или подбор сотрудников", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("certificates", "Образовательные сертификаты или подтверждение квалификации", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("gambling", "Азартные игры или денежные призы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("marketplace", "Продажа товаров или услуг других продавцов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Ничего из перечисленного", 1.0, ConfidenceClass: ConfidenceClass.Known, Exclusive: true),
                new("other", "Другое", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        }
    };
}
