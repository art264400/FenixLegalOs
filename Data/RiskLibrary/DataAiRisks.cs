using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class DataAiRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «ДАННЫЕ И ИИ» (CANONICAL §25 — 15 FINDINGS)
        // =====================================================================

        // 1. DATA_MAP_INCOMPLETE
        new() {
            Code = "DATA_MAP_INCOMPLETE",
            RootCauseGroup = "DATA_FLOW",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Компания не полностью понимает, куда попадают данные пользователей",
            Finding = "Продукт получает пользовательскую информацию и использует внешние сервисы, но внутри компании нет единой картины того, какие данные куда передаются.",
            WhyItMatters = "Без карты движения данных сложно поддерживать точные документы, контролировать внешние сервисы и корректно удалять информацию.",
            Recommendation = "Составить карту типов данных и источников.",
            AffectedDimensions = new() { "data_map" },
            Recommendations = new() {
                "Составить карту типов данных и источников.",
                "Для каждого внешнего сервиса указать, что именно он получает.",
                "Использовать карту как основу для документов, сроков хранения и удаления."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 2. DATA_PRIVACY_NOTICE_MISSING
        new() {
            Code = "DATA_PRIVACY_NOTICE_MISSING",
            RootCauseGroup = "DATA_TRANSPARENCY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Пользователю не объясняется, как продукт работает с его данными",
            Finding = "Продукт получает информацию о людях, но система не видит актуального документа, описывающего основные виды данных и цели их использования.",
            WhyItMatters = "Пользователь не получает единой прозрачной картины, а компании сложнее доказать последовательность собственной модели обработки.",
            Recommendation = "Сначала составить фактическую карту движения данных и подготовить документ под реальную модель продукта.",
            AffectedDimensions = new() { "privacy_notice" },
            Recommendations = new() {
                "Сначала составить фактическую карту движения данных.",
                "Подготовить документ под реальную модель продукта.",
                "Синхронизировать документ с интерфейсом и внешними сервисами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 3. DATA_PRIVACY_NOTICE_OUTDATED
        new() {
            Code = "DATA_PRIVACY_NOTICE_OUTDATED",
            RootCauseGroup = "DATA_TRANSPARENCY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Правила работы с данными могут не соответствовать текущему продукту",
            Finding = "После подготовки документа продукт, внешние сервисы или ИИ-функции изменились, поэтому описание обработки может быть неполным.",
            WhyItMatters = "Устаревшая политика создает разрыв между тем, что компания заявляет пользователю, и тем, что фактически происходит с данными.",
            Recommendation = "Сопоставить текущий data flow с документом и добавить новые сервисы и цели.",
            AffectedDimensions = new() { "privacy_notice" },
            Recommendations = new() {
                "Сопоставить текущий data flow с документом.",
                "Добавить новые сервисы, цели и ИИ-функции.",
                "Настроить процесс обновления при существенных изменениях продукта."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 4. DATA_SECONDARY_USE_UNCLEAR
        new() {
            Code = "DATA_SECONDARY_USE_UNCLEAR",
            RootCauseGroup = "DATA_PURPOSES",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Дополнительное использование данных объясняется пользователю не полностью",
            Finding = "Данные используются не только для основной функции продукта, но и для аналитики, маркетинга, рекомендаций, партнеров или иных целей, которые раскрыты частично.",
            WhyItMatters = "Вторичное использование требует отдельной оценки прозрачности и юридического основания с учетом применимого права.",
            Recommendation = "Разделить основные и дополнительные цели использования и проверить информирование пользователя.",
            AffectedDimensions = new() { "secondary_use" },
            Recommendations = new() {
                "Разделить основные и дополнительные цели использования.",
                "Проверить, что именно сообщается пользователю.",
                "При необходимости изменить продуктовую механику или документы."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 5. DATA_THIRD_PARTY_UNKNOWN
        new() {
            Code = "DATA_THIRD_PARTY_UNKNOWN",
            RootCauseGroup = "DATA_THIRD_PARTY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Компания не полностью контролирует передачу данных внешним сервисам",
            Finding = "Внешние сервисы участвуют в работе продукта, но не по всем из них понятно, какие данные они получают и на каких условиях обрабатывают.",
            WhyItMatters = "Такая неопределенность влияет на точность пользовательских документов, международную передачу и удаление данных.",
            Recommendation = "Составить список основных внешних сервисов и проверить существенные условия работы с данными.",
            AffectedDimensions = new() { "third_party_services" },
            Recommendations = new() {
                "Составить список основных внешних сервисов.",
                "Для каждого определить набор передаваемых данных и назначение.",
                "Проверить существенные условия работы с данными."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 6. DATA_CROSS_BORDER_REVIEW
        new() {
            Code = "DATA_CROSS_BORDER_REVIEW",
            RootCauseGroup = "DATA_CROSS_BORDER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Международное движение данных требует отдельной проверки",
            Finding = "Пользователи, компания или внешние сервисы находятся в разных странах, а правила хранения и передачи данных между ними анализировались частично либо не анализировались.",
            WhyItMatters = "Применимые требования зависят от конкретных стран, ролей сторон и типов данных; одного общего ответа для всех рынков нет.",
            Recommendation = "Определить основные страны пользователей и хранения и проверить требования на приоритетных рынках.",
            AffectedDimensions = new() { "cross_border" },
            Recommendations = new() {
                "Определить основные страны пользователей и хранения.",
                "Сопоставить ключевые передачи данных между странами.",
                "Проверить требования на приоритетных рынках."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 7. DATA_RETENTION_UNDEFINED
        new() {
            Code = "DATA_RETENTION_UNDEFINED",
            RootCauseGroup = "DATA_LIFECYCLE",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Сроки хранения пользовательских данных не определены",
            Finding = "Компания хранит данные без единого понятного правила о том, когда информация больше не нужна и что происходит после этого.",
            WhyItMatters = "Неограниченное хранение увеличивает объем данных под контролем компании и усложняет выполнение запросов пользователей.",
            Recommendation = "Разделить данные по основным категориям и определить разумные сроки или события удаления.",
            AffectedDimensions = new() { "retention_deletion" },
            Recommendations = new() {
                "Разделить данные по основным категориям.",
                "Определить разумные сроки или события удаления.",
                "Связать правила с резервными копиями и внешними сервисами."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 8. DATA_DELETION_GAP
        new() {
            Code = "DATA_DELETION_GAP",
            RootCauseGroup = "DATA_LIFECYCLE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Удаление данных может быть неполным",
            Finding = "Компания может удалить информацию из основной системы, но не уверена, остается ли она в CRM, аналитике, внешнем ИИ или других сервисах.",
            WhyItMatters = "Пользовательский запрос может быть выполнен только частично, если нет единой карты хранения данных.",
            Recommendation = "Сопоставить точки хранения с процессом удаления и определить внешние системы, требующие отдельного действия.",
            AffectedDimensions = new() { "retention_deletion" },
            Recommendations = new() {
                "Сопоставить точки хранения с процессом удаления.",
                "Определить, какие внешние системы требуют отдельного действия.",
                "Документировать исключения и технические ограничения."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 9. DATA_ACCESS_TOO_BROAD
        new() {
            Code = "DATA_ACCESS_TOO_BROAD",
            RootCauseGroup = "DATA_ACCESS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Доступ команды к пользовательским данным слишком широк или плохо контролируется",
            Finding = "Данные пользователей доступны большому числу сотрудников либо единый контроль доступа отсутствует.",
            WhyItMatters = "Чем шире доступ, тем выше риск ненужного просмотра, копирования или сохранения информации после изменения роли человека.",
            Recommendation = "Определить, кому действительно нужны данные, и сузить права доступа по ролям.",
            AffectedDimensions = new() { "access_offboarding" },
            Recommendations = new() {
                "Определить, кому действительно нужны данные.",
                "Сузить права доступа по ролям.",
                "Регулярно пересматривать доступы и отзывать их при уходе."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 10. AI_USER_DATA_TRANSFER
        new() {
            Code = "AI_USER_DATA_TRANSFER",
            RootCauseGroup = "DATA_AI_TRANSPARENCY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Передача пользовательских данных во внешний сервис ИИ требует проверки",
            Finding = "Часть информации, которую пользователь вводит или загружает в продукт, передается внешнему сервису искусственного интеллекта.",
            WhyItMatters = "Компания должна понимать, какие данные уходят во внешний сервис, что с ними происходит и соответствует ли это заявленной пользователю модели.",
            Recommendation = "Определить точный набор передаваемых данных и проверить условия и настройки внешнего ИИ.",
            AffectedDimensions = new() { "ai_external_data" },
            Recommendations = new() {
                "Определить точный набор передаваемых данных.",
                "Проверить условия и настройки внешнего ИИ.",
                "Сопоставить фактическую передачу с пользовательскими документами и интерфейсом."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 11. AI_SENSITIVE_DATA_TRANSFER
        new() {
            Code = "AI_SENSITIVE_DATA_TRANSFER",
            SuppressCodes = new() { "AI_USER_DATA_TRANSFER", "AI_PROVIDER_TERMS_UNKNOWN" },
            RootCauseGroup = "DATA_AI_TRANSPARENCY",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Чувствительные данные пользователей передаются во внешний ИИ без достаточной прозрачности",
            Finding = "По вашим ответам чувствительная пользовательская информация может попадать во внешний сервис ИИ, при этом пользователь информирован не полностью и условия сервиса не проверены в достаточной степени.",
            WhyItMatters = "Комбинация чувствительности данных, внешнего поставщика и недостаточной прозрачности создает существенный юридический и продуктовый риск.",
            Recommendation = "Определить необходимость передачи чувствительных данных и проверить настройки и хранение данных внешним сервисом.",
            AffectedDimensions = new() { "ai_external_data" },
            Recommendations = new() {
                "Определить, действительно ли передача чувствительных данных необходима.",
                "Проверить настройки, хранение и дальнейшее использование данных внешним сервисом.",
                "Скорректировать продуктовую механику и пользовательскую информацию до масштабирования функции."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW",
            Cta = "Сверить работу с данными и AI с Fenix Law"
        },

        // 12. AI_PROVIDER_TERMS_UNKNOWN
        new() {
            Code = "AI_PROVIDER_TERMS_UNKNOWN",
            RootCauseGroup = "DATA_AI_TRANSPARENCY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Условия работы внешнего ИИ с пользовательскими данными не проверены",
            Finding = "Компания использует внешний ИИ, но не может подтвердить, как сервис хранит полученные данные и может ли использовать их для улучшения своих моделей.",
            WhyItMatters = "Без этого невозможно точно описать пользователю весь путь его информации и управлять риском внешнего поставщика.",
            Recommendation = "Проверить актуальные условия выбранного режима сервиса и настроить доступные ограничения.",
            AffectedDimensions = new() { "ai_external_data" },
            Recommendations = new() {
                "Проверить актуальные условия выбранного режима сервиса.",
                "Настроить доступные ограничения хранения/обучения.",
                "Зафиксировать результат в карте движения данных."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 13. AI_TRAINING_NOT_DISCLOSED
        new() {
            Code = "AI_TRAINING_NOT_DISCLOSED",
            RootCauseGroup = "AI_TRAINING",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Данные пользователей используются для развития ИИ сверх основной функции продукта",
            Finding = "Пользовательская информация используется для обучения или улучшения собственной модели, но такое использование объясняется не полностью.",
            WhyItMatters = "Использование данных для развития модели отличается от простой обработки запроса пользователя и требует отдельной прозрачности и правовой оценки.",
            Recommendation = "Определить, какие данные реально нужны для обучения, и обновить пользовательские документы.",
            AffectedDimensions = new() { "ai_training" },
            Recommendations = new() {
                "Определить, какие данные реально нужны для обучения.",
                "Проверить возможность обезличивания или ограничения набора.",
                "Обновить пользовательскую механику и документы."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 14. AI_AUTOMATED_DECISION
        new() {
            Code = "AI_AUTOMATED_DECISION",
            RootCauseGroup = "AI_DECISIONS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "ИИ может принимать решения, существенно влияющие на пользователя",
            Finding = "ИИ участвует в решениях о доступе, рейтинге, цене, отборе, здоровье или другом значимом результате, а участие человека или возможность пересмотра ограничены.",
            WhyItMatters = "Уровень риска зависит от сферы продукта и страны; для финансов, здоровья, найма и других значимых областей требуется отдельная оценка.",
            Recommendation = "Определить решения, где ИИ влияет на человека, и зафиксировать роль человека в проверке результата.",
            AffectedDimensions = new() { "ai_decisions" },
            Recommendations = new() {
                "Определить решения, где ИИ влияет на человека.",
                "Зафиксировать роль человека в проверке результата.",
                "Проверить требования к прозрачности и пересмотру на основных рынках."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        },

        // 15. AI_HUMAN_REVIEW_GAP
        new() {
            Code = "AI_HUMAN_REVIEW_GAP",
            RootCauseGroup = "AI_DECISIONS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "data",
            Modules = new() { "data" },
            Title = "Важные результаты ИИ используются без достаточной проверки человеком",
            Finding = "В значимой для пользователя сфере часть рекомендаций или решений может применяться без последовательной человеческой проверки.",
            WhyItMatters = "Ошибка модели может напрямую повлиять на пользователя, а распределение ответственности и процесс пересмотра остаются неясными.",
            Recommendation = "Определить категории результатов, требующие обязательной проверки, и назначить ответственного.",
            AffectedDimensions = new() { "ai_decisions" },
            Recommendations = new() {
                "Определить категории результатов, требующие обязательной проверки.",
                "Назначить ответственного человека или процедуру эскалации.",
                "Синхронизировать процесс с пользовательскими обещаниями и договорами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "DATA_AI_REVIEW"
        }
    };
}
