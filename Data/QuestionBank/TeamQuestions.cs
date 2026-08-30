using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.QuestionBank;

public static class TeamQuestions
{
    public static readonly List<DiagnosticQuestion> All = new()
    {
        // 1. TEAM-01 (Контекст: состав команды)
        new() {
            Id = "TEAM-01", SectionId = "team", Order = 1, Type = QuestionType.Multiple, ScoreMode = ScoreMode.Context, Weight = 0,
            Question = "Кто работает над проектом помимо сооснователей?",
            Explanation = "Определяет состав привлеченной команды (сотрудники, фрилансеры, агентства) для адаптивного ветвления вопросов.",
            Options = new() {
                new("employees", "Штатные сотрудники", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("freelancers", "Фрилансеры или частные специалисты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("external_devs", "Внешние разработчики или подрядчики", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("studios", "Внешняя студия или агентство", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("advisors", "Советники или менторы (Advisors)", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("interns", "Стажеры или практиканты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("none", "Никого, работают только основатели", 1.0, ConfidenceClass: ConfidenceClass.Known, Exclusive: true),
                new("other", "Другое", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 2. TEAM-02 (Контекст: размер команды)
        new() {
            Id = "TEAM-02", SectionId = "team", Order = 2, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Сколько человек сейчас работает в команде (без учета фаундеров)?",
            Explanation = "Помогает определить масштаб команды и сложность процессов оформления.",
            Options = new() {
                new("1_2", "1–2 человека", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("3_5", "3–5 человек", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("6_10", "6–10 человек", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("11_30", "11–30 человек", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("30_plus", "Более 30 человек", 1.0, ConfidenceClass: ConfidenceClass.Known)
            }
        },

        // 3. TEAM-03 (Диагностика: письменные договоры)
        new() {
            Id = "TEAM-03", SectionId = "team", DimensionId = "written_agreements", Order = 3, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 18, DimensionWeight = 18, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Оформлены ли письменные договоры со всеми членами команды?",
            Options = new() {
                new("all", "Да, со всеми членами команды", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("key_only", "Только с ключевыми участниками", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("half", "Примерно с половиной команды", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("many_missing", "С большинством нет письменных договоров", 0.2, ConfidenceClass: ConfidenceClass.Partial),
                new("almost_none", "Практически ни с кем не оформлены", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 4. TEAM-04 (Диагностика: зависимость от ключевых людей)
        new() {
            Id = "TEAM-04", SectionId = "team", DimensionId = "key_person_dependency", Order = 4, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 40,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Есть ли в команде ключевые специалисты, от которых критически зависит бизнес (Key Persons)?",
            Options = new() {
                new("none", "Нет, все процессы и знания распределены или дублируются", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mitigated", "Есть, но их уход компенсируется понятным планом передачи дел", 0.9, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Есть несколько человек, уход которых замедлит проект", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("critical", "Да, есть люди, потеря которых парализует ключевой продукт или продажи", 0.2, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 5. TEAM-05 (Диагностика: формат работы и риски переквалификации)
        new() {
            Id = "TEAM-05", SectionId = "team", DimensionId = "work_format", Order = 5, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 15, DimensionWeight = 15, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new() {
                        new() { QuestionId = "team.workerTypes", Op = ConditionalOperator.Contains, Value = "freelancers" },
                        new() { QuestionId = "team.workerTypes", Op = ConditionalOperator.Contains, Value = "external_devs" }
                    }
                }
            },
            Question = "Работают ли внештатные специалисты (ИП / ГПХ / фрилансеры) по графику компании и под постоянным контролем?",
            Options = new() {
                new("no", "Нет, работают полностью независимо по конкретным ТЗ", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("few", "1–2 человека работают как штатные, но оформлены как подрядчики", 0.7, ConfidenceClass: ConfidenceClass.Known),
                new("several", "Несколько человек фактически работают full-time как сотрудники", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("many", "Большинство подрядчиков работают как штатный персонал", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.2, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 6. TEAM-06 (Диагностика: ясность условий)
        new() {
            Id = "TEAM-06", SectionId = "team", DimensionId = "terms_clarity", Order = 6, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Насколько четко в договорах описаны обязанности, условия оплаты и результаты работы?",
            Options = new() {
                new("clear", "Четко описаны конкретные обязанности, KPI и правила оплаты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В целом описаны понятно, но есть общие формулировки", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("partly_informal", "Часть условий согласована только в переписке/устно", 0.5, ConfidenceClass: ConfidenceClass.Partial),
                new("generic", "Используются типовые шаблонные договоры без специфики", 0.25, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 7. TEAM-07 (Диагностика: конфиденциальность / NDA)
        new() {
            Id = "TEAM-07", SectionId = "team", DimensionId = "confidentiality", Order = 7, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 8, DimensionWeight = 8, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Подписаны ли соглашения о неразглашении конфиденциальной информации (NDA)?",
            Options = new() {
                new("all", "Да, со всеми сотрудниками и подрядчиками", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("key", "Только с теми, кто имеет доступ к чувствительным данным", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Подписаны только с отдельными участниками", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("none", "NDA не подписывались", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 8. TEAM-08 (Контекст: создание ценных результатов)
        new() {
            Id = "TEAM-08", SectionId = "team", Order = 8, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Создают ли члены команды интеллектуальную собственность (код, дизайн, тексты, методики)?",
            Options = new() {
                new("no", "Нет, занимаются только операционной/рутинной деятельностью", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да, создают код, дизайн, материалы или иные результаты", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 9. TEAM-08A (Диагностика: права на результаты работы)
        new() {
            Id = "TEAM-08A", SectionId = "team", DimensionId = "work_rights", Order = 9, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 10, DimensionWeight = 10, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new() {
                        new() { QuestionId = "team.createsImportantWork", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "team.createsImportantWork", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Закреплен ли в договорах автоматический переход исключительных прав на созданные результаты к компании?",
            Options = new() {
                new("all", "Да, во всех договорах есть четкие положения о передаче IP", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("most", "В большинстве договоров права передаются компании", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("some", "Только в договорах с отдельными разработчиками/дизайнерами", 0.45, ConfidenceClass: ConfidenceClass.Partial),
                new("no", "Положения о правах отсутствуют или размыты", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 10. TEAM-09 (Диагностика: контроль доступа к аккаунтам)
        new() {
            Id = "TEAM-09", SectionId = "team", DimensionId = "access_accounts", Order = 10, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 55,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Как организован доступ команды к репозиториям, серверам и рабочим системам?",
            Options = new() {
                new("controlled", "Централизованный доступ с разграничением прав и 2FA", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "Доступы выдаются по необходимости, но есть общий контроль", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("ad_hoc", "Доступы раздаются хаотично, единого реестра нет", 0.4, ConfidenceClass: ConfidenceClass.Partial),
                new("unknown_access", "Неизвестно, у кого из текущих участников какие доступы", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 11. TEAM-10 (Диагностика: личные аккаунты)
        new() {
            Id = "TEAM-10", SectionId = "team", DimensionId = "access_accounts", Order = 11, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 45,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Зарегистрированы ли критические сервисы (домены, хостинг, сторы, репозитории) на личные аккаунты сотрудников/подрядчиков?",
            Options = new() {
                new("company", "Нет, все оформлено на компанию или корпоративные почты основателей", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("minor", "Только второстепенные сервисы и подписки", 0.8, ConfidenceClass: ConfidenceClass.Known),
                new("important", "Некоторые важные сервисы зарегистрированы на личные аккаунты", 0.4, ConfidenceClass: ConfidenceClass.Partial),
                new("critical", "Критические сервисы (хостинг, стор, домен) оформлены на личные аккаунты разработчиков", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 12. TEAM-11 (Диагностика: процесс офбординга)
        new() {
            Id = "TEAM-11", SectionId = "team", DimensionId = "offboarding", Order = 12, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 12, DimensionWeight = 12, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Есть ли регламент отзыва доступов и передачи дел при увольнении или прекращении работы?",
            Options = new() {
                new("systematic", "Да, есть чек-лист отзыва доступов и передачи кода/материалов", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("informal", "Процесс не документирован, но доступы всегда оперативно закрываются", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("case_by_case", "Делается от случая к случаю, иногда доступы забывают отозвать", 0.4, ConfidenceClass: ConfidenceClass.Partial),
                new("none", "Регламента нет, системный отзыв доступов не проводится", 0.15, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 13. TEAM-12 (Диагностика: бывшие сотрудники и подрядчики)
        new() {
            Id = "TEAM-12", SectionId = "team", DimensionId = "former_people", Order = 13, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Были ли случаи ухода сотрудников или подрядчиков, и закрыты ли с ними все вопросы?",
            Options = new() {
                new("none", "Никто еще не уходил из проекта", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("closed", "Уходили, но со всеми подписаны акты и закрыты доступы", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("not_sure", "Уходили, но нет уверенности, что все доступы закрыты", 0.4, ConfidenceClass: ConfidenceClass.Partial),
                new("retained", "У ушедших участников остались доступы или рабочие материалы", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("conflict", "Есть нерешенные конфликты или взаимные претензии с бывшими участниками", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 14. TEAM-13 (Диагностика: непрерывность ключевых людей)
        new() {
            Id = "TEAM-13", SectionId = "team", DimensionId = "key_person_dependency", Order = 14, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 7, DimensionWeight = 7, WithinDimensionWeight = 60,
            ShowIf = new() { new() { QuestionId = "team.keyPersonExists", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Что произойдет, если ключевой специалист внезапно покинет команду?",
            Options = new() {
                new("continuity", "Команда продолжит работу: код задокументирован, есть замена", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("time_needed", "Потребуется время на поиск и передачу дел, но фатального риска нет", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("knowledge_only", "Уникальные знания утеряются, разработка сильно затормозится", 0.35, ConfidenceClass: ConfidenceClass.Partial),
                new("stop", "Проект временно остановится, так как никто больше не знает систему", 0.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 15. TEAM-14 (Контекст: иностранные специалисты)
        new() {
            Id = "TEAM-14", SectionId = "team", Order = 15, Type = QuestionType.Single, ScoreMode = ScoreMode.Context, Weight = 0,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Привлекаются ли к работе специалисты или подрядчики из других стран (нерезиденты)?",
            Options = new() {
                new("no", "Нет, все участники являются налоговыми резидентами основной юрисдикции", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("yes", "Да, есть специалисты или подрядчики из других стран", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 1.0, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 16. TEAM-14A (Диагностика: оформление нерезидентов)
        new() {
            Id = "TEAM-14A", SectionId = "team", DimensionId = "foreign_team", Order = 16, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 1.5, DimensionWeight = 1.5, WithinDimensionWeight = 100,
            ShowIf = new() {
                new() {
                    Any = new() {
                        new() { QuestionId = "team.foreignWorkers", Op = ConditionalOperator.Eq, Value = "true" },
                        new() { QuestionId = "team.foreignWorkers", Op = ConditionalOperator.Eq, Value = "unknown" }
                    }
                }
            },
            Question = "Проверены ли договоры с иностранными специалистами на соответствие валютному и налоговому законодательству?",
            Options = new() {
                new("yes", "Да, договоры и выплаты структурированы с учетом трансграничных правил", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("mostly", "В целом проверены, явных валютных нарушений нет", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("ordinary_unchecked", "Используются обычные договоры без проверки трансграничной специфики", 0.4, ConfidenceClass: ConfidenceClass.Partial),
                new("no_contract", "Оформление отсутствует или выплаты проводятся неформально", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        },

        // 17. TEAM-15 (Диагностика: опционы и обещания долей команде)
        new() {
            Id = "TEAM-15", SectionId = "team", DimensionId = "team_equity", Order = 17, Type = QuestionType.Single, ScoreMode = ScoreMode.Diagnostic, Weight = 1.5, DimensionWeight = 1.5, WithinDimensionWeight = 100,
            ShowIf = new() { new() { QuestionId = "team.hasNonFounderTeam", Op = ConditionalOperator.Eq, Value = "true" } },
            Question = "Обещаны ли кому-то из команды доли, акции или опционы (ESOP / опционная программа)?",
            Options = new() {
                new("none", "Нет, опционы или доли никому из команды не обещаны", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("formal", "Да, утвержден официальный опционный план (ESOP) с подписанными договорами", 1.0, ConfidenceClass: ConfidenceClass.Known),
                new("written_pending", "Есть письменные договоренности, оформление планируется позже", 0.75, ConfidenceClass: ConfidenceClass.Known),
                new("oral", "Есть устные обещания долей ключевым участникам", 0.25, ConfidenceClass: ConfidenceClass.Partial),
                new("undefined", "Есть общее обещание 'поделиться долей при успехе' без конкретных цифр", 0.1, ConfidenceClass: ConfidenceClass.Known),
                new("unknown", "Не уверен(а)", 0.15, ConfidenceClass: ConfidenceClass.Unknown)
            }
        }
    };
}
