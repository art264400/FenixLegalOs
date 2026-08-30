using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class DataAiQuestions
{
    public static readonly List<DiagnosticQuestion> All = new()
    {
        // =========================================================================
        // DATA QUESTIONS (DATA-01 .. DATA-19)
        // =========================================================================

        // 1. DATA-01 (Контекст: декларация обработки ПДн)
        new() {
            Id = "DATA-01", SectionId = "data", Order = 1, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Обрабатывает ли продукт/сервис персональные данные пользователей или клиентов?",
            Explanation = "Декларативный вопрос. Определяет применимость требований законодательства о персональных данных (142-ФЗ, 94-З РК, GDPR).",
            Options = new() {
                new("yes", "Да, обрабатываем данные пользователей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Нет, данные не собираются и не обрабатываются", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 2. DATA-02 (Контекст: категории данных — фактический опрос)
        new() {
            Id = "DATA-02", SectionId = "data", Order = 2, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Какие именно категории данных о пользователях собираются или обрабатываются?",
            Explanation = "Фактические категории данных имеют приоритет над общей декларацией. Наличие контактов или аккаунтов автоматически делает блок применимым.",
            Options = new() {
                new("contact", "Контактные данные (имя, email, телефон)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("account", "Учетные данные (логин, пароль, профиль)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("media", "Медиафайлы (фотографии, видео, документы)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("messages", "Переписка, сообщения, комментарии", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("location", "Геолокация или данные о местоположении", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("behavior", "Поведенческие данные, аналитика действий", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("device", "Технические данные об устройстве и IP-адреса", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("payments", "Платежная информация, банковские реквизиты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("work_edu", "Данные о работе, резюме, образовании", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("health", "Данные о здоровье, медицинские сведения", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("identity", "Паспортные данные, ИИН / ID, официальные документы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("biometric", "Биометрические данные (лицо, отпечатки, голос)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другие данные", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Никакие данные не собираются", 1.0, Exclusive: true, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 3. DATA-03 (Контекст: чувствительные данные)
        new() {
            Id = "DATA-03", SectionId = "data", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Обрабатываются ли чувствительные категории данных (здоровье, биометрия, финансовые, документы)?",
            Explanation = "Чувствительные данные служат контекстным модификатором строгости и не штрафуют скоринг сами по себе.",
            Options = new() {
                new("no", "Нет, только обычные данные", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("sometimes", "Иногда / в отдельных случаях", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("core", "Да, это ключевая часть продукта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. DATA-04 (Контекст: источники данных)
        new() {
            Id = "DATA-04", SectionId = "data", Order = 4, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Из каких источников поступают персональные данные?",
            Explanation = "Источники данных определяют требования к получению согласий и уведомлению субъектов.",
            Options = new() {
                new("user", "Напрямую от самих пользователей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("generated", "Генерируются автоматически при использовании продукта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("client_company", "От компаний-клиентов (B2B)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partners", "От партнеров или сторонних сервисов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("public", "Из открытых/публичных источников", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("external", "Из внешних баз данных или реестров", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("automatic", "Автоматический сбор (парсинг / трекеры)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другие источники", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. DATA-05 (Диагностика: карта данных / Data Map)
        new() {
            Id = "DATA-05", SectionId = "data", DimensionId = "data_map", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Ведется ли реестр или карта обработки данных (Data Map / RoPA)?",
            Explanation = "Реестр процессов обработки данных необходим для контроля потоков, ответов регуляторам и соблюдения принципа подотчетности.",
            Options = new() {
                new("clear", "Да, есть актуальный реестр всех процессов обработки данных", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В целом понятно, но реестр формально не ведется", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("developers_only", "Знают только разработчики по коду", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("main_only", "Понимание есть только по основным потокам", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Карты данных и реестра процессов нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. DATA-06 (Диагностика: наличие Privacy Policy)
        new() {
            Id = "DATA-06", SectionId = "data", DimensionId = "privacy_notice", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Опубликована ли политика конфиденциальности (Privacy Policy)?",
            Explanation = "Публикация документа обязательна до начала сбора любых персональных данных.",
            Options = new() {
                new("yes", "Да, актуальная политика опубликована", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("old", "Политика есть, но давно не обновлялась", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("template", "Используется типовой шаблон без адаптации под продукт", 0.35, ConfidenceClass: ConfidenceClass.Known),
                new("preparing", "Политика готовится, но пока не опубликована", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Политики конфиденциальности нет", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. DATA-07 (Диагностика: соответствие Privacy Policy реальности)
        new() {
            Id = "DATA-07", SectionId = "data", DimensionId = "privacy_notice", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 55,
            ShowIf = new() {
                new() { QuestionId = "data.privacyNotice", Op = ConditionalOperator.In, Value = new List<string> { "current_or_exists", "old", "template" } }
            },
            Question = "Соответствует ли опубликованная политика реальным процессам сбора и передачи данных?",
            Explanation = "Несоответствие текста политики реальным SDK, трекерам и базам данных влечет прямую ответственность за введение в заблуждение.",
            Options = new() {
                new("yes", "Да, полностью соответствует", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В основном соответствует, с мелкими расхождениями", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("changed", "Продукт изменился, а политика не учитывает новые данные", 0.30, ConfidenceClass: ConfidenceClass.Known),
                new("template_unchecked", "Шаблон не проверялся на соответствие реальным потокам данных", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. DATA-08 (Контекст: цели обработки данных)
        new() {
            Id = "DATA-08", SectionId = "data", Order = 8, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Для каких целей используются собираемые данные?",
            Explanation = "Вторичные цели (маркетинг, аналитика, обучение AI) требуют отдельного информирования и законных оснований.",
            Options = new() {
                new("core_service", "Предоставление основного сервиса/продукта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("analytics", "Внутренняя продуктовая аналитика и улучшение сервиса", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("marketing", "Прямой маркетинг, рассылки и коммуникации", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("ads", "Таргетированная реклама и монетизация", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("recommendations", "Персонализация и рекомендательные алгоритмы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("ai_training", "Обучение собственных или сторонних AI-моделей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partners", "Передача партнерам для их целей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("other", "Другие цели", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. DATA-09 (Диагностика: раскрытие вторичного использования)
        new() {
            Id = "DATA-09", SectionId = "data", DimensionId = "secondary_use", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() { QuestionId = "data.secondaryUse", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Раскрыто ли пользователям вторичное использование данных (маркетинг, реклама, обучение AI)?",
            Explanation = "Использование данных сверх необходимого для исполнения договора требует явного информирования или согласия.",
            Options = new() {
                new("clear", "Да, явно раскрыто и получено отдельное согласие", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("document_only", "Указано только мелким шрифтом в общей политике", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично раскрыто, согласия на отдельные цели нет", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Вторичное использование никак не раскрывается", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. DATA-10 (Контекст: внешние сервисы)
        new() {
            Id = "DATA-10", SectionId = "data", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Передаются ли данные внешним сервисам (хостинг, аналитика, CRM, рассылки)?",
            Explanation = "Использование сторонних обработчиков данных требует контроля условий и соглашений DPA.",
            Options = new() {
                new("no", "Нет, данные обрабатываются только на собственной инфраструктуре", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да, используются внешние сервисы и провайдеры", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. DATA-10A (Диагностика: реестр внешних сервисов / Sub-processors)
        new() {
            Id = "DATA-10A", SectionId = "data", DimensionId = "third_party_services", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 55,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.externalServicesUsed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.externalServicesUsed", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Есть ли понимание и список всех внешних сервисов, имеющих доступ к данным?",
            Explanation = "Компания отвечает перед пользователями за действия всех привлекаемых сторонних сервисов.",
            Options = new() {
                new("yes", "Да, есть полный перечень всех внешних обработчиков (Sub-processors)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "Известны основные ключевые сервисы", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Список неполный, сервисы подключаются разработчиками без учета", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Единого понимания и списка внешних сервисов нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. DATA-11 (Диагностика: договоры DPA с провайдерами)
        new() {
            Id = "DATA-11", SectionId = "data", DimensionId = "third_party_services", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.externalServicesUsed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.externalServicesUsed", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Проверялись ли условия и соглашения об обработке данных (DPA) с внешними провайдерами?",
            Explanation = "Соглашение об обработке данных (Data Processing Addendum) распределяет ответственность за утечки и инциденты.",
            Options = new() {
                new("main", "Да, со всеми ключевыми провайдерами подписаны/приняты DPA", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Проверялись условия только у части провайдеров", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("known_not_reviewed", "Провайдеры известны, но юридические условия не проверялись", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Условия работы с данными внешних сервисов не проверялись", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 13. DATA-12 (Контекст: география пользователей данных)
        new() {
            Id = "DATA-12", SectionId = "data", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Где географически находятся пользователи, чьи данные обрабатываются?",
            Explanation = "Наличие пользователей из ЕС, Великобритании, США или СНГ накладывает локальные законы о персональных данных.",
            Options = new() {
                new("one", "В одной стране", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("multiple", "В нескольких странах", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("global", "Глобально по всему миру", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("not_tracked", "География пользователей пока не отслеживается", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 14. DATA-13 (Диагностика: страны хранения серверов)
        new() {
            Id = "DATA-13", SectionId = "data", DimensionId = "cross_border", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 45,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Известно ли, в каких странах физически хранятся и обрабатываются базы данных?",
            Explanation = "Знание локации серверов необходимо для проверки соблюдения требований локализации баз данных.",
            Options = new() {
                new("yes", "Да, страны размещения серверов и баз данных точно известны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "Известно основное размещение серверов", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("foreign_unreviewed", "Серверы за рубежом, требования к передаче не проверялись", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Страны физического хранения данных неизвестны", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 15. DATA-14 (Диагностика: трансграничная передача и локализация)
        new() {
            Id = "DATA-14", SectionId = "data", DimensionId = "cross_border", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 55,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.userGeography", Op = ConditionalOperator.In, Value = new List<string> { "multiple", "global", "not_tracked", "unknown" } },
                        new() { QuestionId = "data.dataStoredAbroad", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.storageCountriesKnown", Op = ConditionalOperator.In, Value = new List<string> { "foreign_unreviewed", "no", "unknown" } }
                    }
                }
            },
            Question = "Проверялись ли требования к трансграничной передаче данных и локализации?",
            Explanation = "Многие страны требуют первичного хранения данных граждан на национальной территории или соблюдения условий трансграничной передачи.",
            Options = new() {
                new("yes", "Да, требования локализации и трансграничной передачи проверены", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Проверялись только базовые требования", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Требования трансграничной передачи не проверялись", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. DATA-15 (Диагностика: сроки хранения данных)
        new() {
            Id = "DATA-15", SectionId = "data", DimensionId = "retention_deletion", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Определены ли сроки хранения данных пользователей?",
            Explanation = "Принцип минимизации хранения требует удаления данных после достижения целей сбора.",
            Options = new() {
                new("defined", "Да, установлены четкие сроки хранения по типам данных", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("general", "Есть общее понимание сроков без жестких регламентов", 0.65, ConfidenceClass: ConfidenceClass.Known),
                new("keep_useful", "Данные хранятся бессрочно, пока могут быть полезны", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Сроки хранения данных не определены", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. DATA-16 (Диагностика: возможность удаления данных / Right to be Forgotten)
        new() {
            Id = "DATA-16", SectionId = "data", DimensionId = "retention_deletion", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 50,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Есть ли техническая возможность полностью удалить данные пользователя по запросу (Right to be Forgotten)?",
            Explanation = "Право на удаление данных — базовое право пользователя в GDPR и национальных законах.",
            Options = new() {
                new("process", "Да, реализован автоматический или регламентированный процесс удаления", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("manual", "Удаление возможно вручную по запросу в поддержку", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("possible_no_process", "Технически возможно, но регламента и скриптов нет", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("not_all_systems", "Удаляется только из основной БД, в бэкапах/логах остается", 0.20, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Возможности удаления данных нет", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 18. DATA-17 (Диагностика: процесс обработки обращений субъектов данных)
        new() {
            Id = "DATA-17", SectionId = "data", DimensionId = "retention_deletion", Order = 18, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 20,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.userInfoDeclared", Op = ConditionalOperator.Neq, Value = "false" }
                    }
                }
            },
            Question = "Налажен ли процесс обработки обращений пользователей по поводу их данных?",
            Explanation = "Закон устанавливает жесткие сроки ответов на запросы об изменении, выгрузке или удалении данных.",
            Options = new() {
                new("yes", "Да, есть выделенный контакт (DPO / email) и регламент ответов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("rare_but_known", "Запросы бывают редко, но процедура ответа понятна", 0.80, ConfidenceClass: ConfidenceClass.Known),
                new("manual_each", "Каждый запрос рассматривается индивидуально без регламента", 0.50, ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Процесса работы с запросами субъектов данных нет", 0.20, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 19. DATA-18 (Диагностика: разграничение доступа команды к данным)
        new() {
            Id = "DATA-18", SectionId = "data", DimensionId = "access_offboarding", Order = 19, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 65,
            ShowIf = new() {
                new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Neq, Value = "false" }
            },
            Question = "Как регулируется доступ членов команды к персональным данным клиентов/пользователей?",
            Explanation = "Принцип наименьших привилегий (Need-to-Know) минимизирует риски утечек через сотрудников.",
            Options = new() {
                new("need_to_know", "Доступ предоставляется строго по принципу служебной необходимости (Need-to-Know)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "Доступ разграничен для основных ролей, но есть исключения", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("broad", "Широкий доступ у большинства разработчиков и операторов", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("uncontrolled", "Доступ к базе данных не контролируется", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 20. DATA-19 (Диагностика: отзыв доступов к данным при увольнении)
        new() {
            Id = "DATA-19", SectionId = "data", DimensionId = "access_offboarding", Order = 20, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 35,
            ShowIf = new() {
                new() { QuestionId = "data.personalDataProcessed", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "team.offboardingProcess", Op = ConditionalOperator.In, Value = new List<string> { "unknown" } }
            },
            Question = "Осуществляется ли отзыв доступов к базам данных при прекращении работы с сотрудниками?",
            Explanation = "Вопрос отображается, если процесс офбординга команды не был определен в блоке Team.",
            Options = new() {
                new("systematic", "Да, доступы системно отзываются по чек-листу при уходе любого участника", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("usually", "Обычно закрываются оперативно, но регламента нет", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("case", "От случая к случаю, иногда доступы забывают отозвать", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Доступы ушедших сотрудников не отзываются системно", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // =========================================================================
        // AI QUESTIONS (AI-01 .. AI-08)
        // =========================================================================

        // 21. AI-01 (Контекст: использование AI/ML)
        new() {
            Id = "AI-01", SectionId = "data", Order = 21, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Используются ли в продукте технологии искусственного интеллекта или машинного обучения (AI/ML)?",
            Explanation = "Определяет применимость требований к сторонним AI-моделям, дообучению и автоматизированным решениям.",
            Options = new() {
                new("no", "Нет, AI/ML технологии не используются", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("external", "Да, используем сторонние AI-сервисы и API (OpenAI, Anthropic, Google Cloud и др.)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("own", "Да, обучаем и используем собственные ML-модели", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("both", "Да, используем как сторонние API, так и собственные модели", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 22. AI-02 (Диагностика: данные, передаваемые в сторонние AI)
        new() {
            Id = "AI-02", SectionId = "data", DimensionId = "ai_external_data", Order = 22, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "ai.external", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "ai.used", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Какие данные передаются во внешние AI-сервисы и модели?",
            Explanation = "Передача персональных или чувствительных данных во внешние API требует согласия и проверки условий провайдера.",
            Options = new() {
                new("none", "Данные пользователей не передаются (только системные промпты)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("deidentified", "Только обезличенные или агрегированные данные", 0.90, ConfidenceClass: ConfidenceClass.Known),
                new("ordinary", "Обычные пользовательские тексты/запросы без чувствительных данных", 0.60, ConfidenceClass: ConfidenceClass.Known),
                new("content", "Файлы, документы или медиа пользователей", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("sensitive", "Чувствительные данные (персональные, финансовые, медицинские, коммерческие)", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 23. AI-03 (Диагностика: уведомление пользователей об AI)
        new() {
            Id = "AI-03", SectionId = "data", DimensionId = "ai_external_data", Order = 23, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 25,
            ShowIf = new() {
                new() { QuestionId = "ai.external", Op = ConditionalOperator.Eq, Value = "true" },
                new() { QuestionId = "ai.userDataSent", Op = ConditionalOperator.Neq, Value = "none" }
            },
            Question = "Предупреждены ли пользователи о передаче их данных во внешние AI-системы?",
            Explanation = "Регуляторы (EU AI Act, FTC) требуют прозрачного раскрытия факта взаимодействия с AI и передачи данных провайдерам.",
            Options = new() {
                new("clear", "Да, это прямо и понятно раскрыто до отправки данных", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("document", "Указано только в общей политике конфиденциальности", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Частично раскрыто, но не для всех AI-функций", 0.40, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Пользователи не уведомляются об использовании стороннего AI", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 24. AI-04 (Диагностика: проверка условий внешних AI-провайдеров)
        new() {
            Id = "AI-04", SectionId = "data", DimensionId = "ai_external_data", Order = 24, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 25,
            ShowIf = new() {
                new() { QuestionId = "ai.external", Op = ConditionalOperator.Eq, Value = "true" }
            },
            Question = "Проверялись ли условия использования внешних AI-провайдеров (Terms of Service / Data Processing Addendum)?",
            Explanation = "Необходимо убедиться, что провайдер AI не использует передаваемые клиентские данные для обучения своих базовых моделей (Zero Data Retention).",
            Options = new() {
                new("full", "Да, проверены условия коммерческого API (гарантии необучения на наших данных, DPA)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("main", "Проверены основные условия провайдера", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("not_specific", "Используется стандартный потребительский аккаунт без проверки условий", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Условия и политики провайдеров AI не проверялись", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 25. AI-05 (Диагностика: передача чувствительных данных в сторонние AI)
        new() {
            Id = "AI-05", SectionId = "data", DimensionId = "ai_external_data", Order = 25, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 20,
            ShowIf = new() {
                new() { QuestionId = "ai.external", Op = ConditionalOperator.Eq, Value = "true" },
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "data.sensitiveData", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "ai.sensitiveDataSent", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.sensitiveData", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Передаются ли в сторонние AI конфиденциальные или чувствительные данные клиентов?",
            Explanation = "Передача врачебной, банковской тайны или персональных данных в сторонние нейросети без согласия создает высокие регуляторные риски.",
            Options = new() {
                new("no", "Нет, передача чувствительных данных строго исключена", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("deidentified", "Передаются только после предварительной маскировки/деидентификации", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("sometimes", "Иногда передаются в отдельных сценариях", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("core", "Да, это основа обработки сервиса", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 26. AI-06 (Диагностика: обучение собственных моделей на данных)
        new() {
            Id = "AI-06", SectionId = "data", DimensionId = "ai_training", Order = 26, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 6, DimensionWeight = 6, WithinDimensionWeight = 40,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "ai.ownModel", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "data.purposes", Op = ConditionalOperator.Contains, Value = "ai_training" }
                    }
                }
            },
            Question = "Используются ли данные пользователей для дообучения или обучения AI-моделей?",
            Explanation = "Обучение моделей на пользовательских данных требует явного правового основания и соблюдения прав правообладателей контента.",
            Options = new() {
                new("no", "Нет, данные пользователей не используются для обучения моделей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("deidentified", "Используются только в обезличенном и агрегированном виде", 0.85, ConfidenceClass: ConfidenceClass.Known),
                new("user_data", "Используются реальные данные пользователей с их согласия", 0.40, ConfidenceClass: ConfidenceClass.Known),
                new("possible_undefined", "Используются, но четких правил и согласий нет", 0.20, ConfidenceClass: ConfidenceClass.Partial),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 27. AI-06A (Диагностика: согласие и отказ от обучения / Opt-Out)
        new() {
            Id = "AI-06A", SectionId = "data", DimensionId = "ai_training", Order = 27, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 6, DimensionWeight = 6, WithinDimensionWeight = 60,
            ShowIf = new() {
                new() { QuestionId = "ai.trainingUse", Op = ConditionalOperator.In, Value = new List<string> { "deidentified", "true", "possible_undefined", "unknown" } }
            },
            Question = "Раскрыт ли пользователям факт обучения моделей на их данных и предусмотрен ли отказ (Opt-Out)?",
            Explanation = "Пользователи должны иметь возможность запретить использование своих материалов для трейнинга AI.",
            Options = new() {
                new("yes", "Да, раскрыто в условиях и есть возможность отказаться (Opt-Out)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Раскрыто, но возможности отказаться нет", 0.50, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Факт обучения моделей не раскрывается", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 28. AI-07 (Диагностика: автоматические решения с правовыми последствиями)
        new() {
            Id = "AI-07", SectionId = "data", DimensionId = "ai_decisions", Order = 28, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 9, DimensionWeight = 9, WithinDimensionWeight = 35,
            ShowIf = new() {
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "ai.used", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "ai.used", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Принимает ли AI решения, имеющие юридические, финансовые или существенные последствия для пользователей?",
            Explanation = "Скоринг, отказ в обслуживании, блокировки, найм или финансовые лимиты без участия человека жестко ограничены законодательством.",
            Options = new() {
                new("no", "Нет, существенные решения не принимаются", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("assist", "AI только формирует рекомендации, решение принимает человек", 0.90, ConfidenceClass: ConfidenceClass.Known),
                new("ai_human_check", "AI формирует решение, но человек выборочно или обязательно утверждает его", 0.70, ConfidenceClass: ConfidenceClass.Known),
                new("automatic", "AI принимает решения полностью автоматически", 0.30, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 29. AI-07A (Диагностика: объяснимость автоматических решений)
        new() {
            Id = "AI-07A", SectionId = "data", DimensionId = "ai_decisions", Order = 29, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 9, DimensionWeight = 9, WithinDimensionWeight = 30,
            ShowIf = new() {
                new() { QuestionId = "ai.materialDecisionUse", Op = ConditionalOperator.Eq, Value = "automatic" }
            },
            Question = "Проверялись ли требования к прозрачности и объяснимости полностью автоматических решений (Explainable AI / Right to Explanation)?",
            Explanation = "Пользователь имеет право знать основные критерии и логику алгоритма, на основании которых в отношении него принято автоматическое решение.",
            Options = new() {
                new("yes", "Да, логика решений понятна и может быть объяснена пользователю", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("partial", "Логика понятна лишь частично / 'черный ящик'", 0.50, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Требования к объяснимости решений не проверялись", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 30. AI-08 (Диагностика: оспаривание и участие человека / Human-in-the-Loop)
        new() {
            Id = "AI-08", SectionId = "data", DimensionId = "ai_decisions", Order = 30, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 9, DimensionWeight = 9, WithinDimensionWeight = 35,
            ShowIf = new() {
                new() { QuestionId = "ai.used", Op = ConditionalOperator.Eq, Value = "true" },
                new() {
                    Any = new List<ConditionalRule> {
                        new() { QuestionId = "product.regulatedFunctions", Op = ConditionalOperator.In, Value = new List<string> { "health", "investments", "payments", "loans", "hiring", "certificates" } },
                        new() { QuestionId = "ai.materialDecisionUse", Op = ConditionalOperator.In, Value = new List<string> { "human_check", "automatic" } }
                    }
                }
            },
            Question = "Предусмотрена ли возможность для пользователя оспорить автоматическое решение и потребовать проверки человеком (Human-in-the-Loop)?",
            Explanation = "Право на пересмотр решения живым специалистом является обязательной гарантией защиты прав пользователей.",
            Options = new() {
                new("yes", "Да, есть регламент оспаривания и обязательного ручного пересмотра", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("sometimes", "Пересмотр возможен в исключительных случаях через поддержку", 0.55, ConfidenceClass: ConfidenceClass.Known),
                new("no", "Процедура оспаривания или проверки человеком отсутствует", 0.10, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
