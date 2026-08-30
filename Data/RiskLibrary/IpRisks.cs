using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class IpRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «ИНТЕЛЛЕКТУАЛЬНАЯ СОБСТВЕННОСТЬ» (IP) v1.1
        // =====================================================================
        new() {
            Code = "IP_PRODUCT_RIGHTS_UNCONFIRMED",
            SuppressCodes = new() { "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED", "IP_CONTRACTOR_RIGHTS_GAP", "IP_STUDIO_RIGHTS_GAP" },
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Принадлежность ключевого продукта компании не подтверждена",
            Finding = "Компания уже использует созданный продукт, но нет достаточного документального подтверждения прав компании на его ключевые элементы.",
            WhyItMatters = "Если права на основной технологический актив нельзя подтвердить, это ставит под угрозу коммерциализацию, лицензирование и привлекательность для инвесторов.",
            Recommendation = "Составить перечень ключевых элементов продукта, собрать договоры отчуждения прав и закрыть выявленные разрывы.",
            AffectedDimensions = new() { "overall_rights" },
            Recommendations = new() {
                "Составить перечень ключевых элементов продукта и их авторов.",
                "Собрать договоры и документы, подтверждающие переход прав на компанию.",
                "Оформить передачу недостающих прав отдельными соглашениями."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED",
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на часть продукта остаются связанными с основателем",
            Finding = "Один или несколько founders создавали продукт, но передача необходимых прав компании оформлена не полностью.",
            WhyItMatters = "При уходе, конфликте или раунде инвестор может потребовать подтверждения, вправе ли сама компания свободно распоряжаться кодом.",
            Recommendation = "Оформить передачу прав (IP Assignment) от основателей на компанию.",
            AffectedDimensions = new() { "founder_rights" },
            Recommendations = new() {
                "Определить, какие результаты были созданы основателями.",
                "Проверить действующие договоры и корпоративные документы.",
                "Оформить передачу недостающих прав компании."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_CONTRACTOR_RIGHTS_GAP",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на результат внешнего разработчика подтверждены не полностью",
            Finding = "Внешний специалист участвовал в создании продукта, но существующие документы не позволяют уверенно подтвердить принадлежность компании всего созданного результата.",
            WhyItMatters = "Факт оплаты работ сам по себе не означает автоматического перехода исключительных прав на код.",
            Recommendation = "Подписать акты приема-передачи с явным указанием отчуждения исключительных прав.",
            AffectedDimensions = new() { "external_creators", "employee_rights" },
            Recommendations = new() {
                "Определить вклад конкретного разработчика.",
                "Проверить договор, акты и переписку о правах.",
                "Оформить подтверждение передачи исключительных прав."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_FORMER_DEVELOPER_GAP",
            SuppressCodes = new() { "IP_CONTRACTOR_RIGHTS_GAP", "TEAM_FORMER_ACCESS_RISK" },
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.Critical,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Права на часть продукта, созданную бывшим разработчиком, требуют первоочередной проверки",
            Finding = "Бывший сотрудник или подрядчик участвовал в создании важной части продукта, а документы о правах неполны, отсутствуют или оспариваются.",
            WhyItMatters = "После прекращения отношений закрыть такой разрыв сложнее; бывший разработчик может потребовать компенсацию или заблокировать сделку.",
            Recommendation = "Собрать договоры, акты и подтверждения передачи прав, а также убедиться в отзыве всех технических доступов.",
            AffectedDimensions = new() { "external_creators" },
            Recommendations = new() {
                "Определить весь вклад бывшего разработчика.",
                "Собрать договоры, акты и подтверждения передачи прав.",
                "Параллельно проверить, закрыты ли его технические доступы."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_STUDIO_RIGHTS_GAP",
            RootCauseGroup = "IP_OWNERSHIP",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Цепочка прав через внешнюю студию подтверждена не полностью",
            Finding = "Договор с внешней студией существует, но не полностью понятно, кто фактически создавал продукт и могла ли студия передать права на весь результат.",
            WhyItMatters = "Если студия привлекала сторонних субподрядчиков без прав на сублицензирование, права компании на конечный продукт уязвимы.",
            Recommendation = "Запросить гарантии студии об отсутствии сторонних претензий и подтвердить цепочку передачи прав от авторов.",
            AffectedDimensions = new() { "external_creators" },
            Recommendations = new() {
                "Уточнить состав исполнителей студии.",
                "Проверить договорные гарантии и передачу прав.",
                "Закрыть существенные пробелы по ключевым результатам."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_EMPLOYER_RISK",
            RootCauseGroup = "IP_EMPLOYER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Создание продукта пересекается с работой основателя у другого работодателя",
            Finding = "Основатель создавал продукт в период работы в другой компании, а использование рабочего времени, оборудования, данных или иных ресурсов не исключено либо отдельно не проверялось.",
            WhyItMatters = "Прежний работодатель может заявить права на служебное произведение или потребовать долю в стартапе (Moonlighting dispute).",
            Recommendation = "Провести правовой аудит трудового договора основателя и при необходимости получить письменное подтверждение об отсутствии претензий.",
            AffectedDimensions = new() { "external_employer" },
            Recommendations = new() {
                "Проверить трудовые и иные обязательства основателя перед работодателем.",
                "Определить, когда и с использованием каких ресурсов создавались ключевые результаты.",
                "При необходимости получить подтверждение отсутствия претензий (Release letter)."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_THIRD_PARTY_COMPONENTS",
            RootCauseGroup = "IP_DEPENDENCIES",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Условия использования готовых сторонних компонентов проверены не полностью",
            Finding = "Продукт использует код, библиотеки или другие компоненты, созданные не компанией, а условия их использования контролируются частично либо не проверялись.",
            WhyItMatters = "Отдельные лицензии (GPL, AGPL) могут налагать ограничения на распространение, закрытость кода или коммерческую модель.",
            Recommendation = "Провести аудит используемых Open Source библиотек на совместимость с коммерческой лицензией продукта.",
            AffectedDimensions = new() { "third_party_dependencies" },
            Recommendations = new() {
                "Составить перечень ключевых сторонних компонентов.",
                "Определить применимые условия использования (MIT, Apache, GPL).",
                "Проверить компоненты, критичные для коммерческой модели продукта."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_EXTERNAL_DEPENDENCY",
            RootCauseGroup = "IP_DEPENDENCIES",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Ключевая функция продукта зависит от внешней технологии",
            Finding = "Значимая часть работы продукта зависит от сторонней технологии или сервиса, при этом ограничения такой зависимости проверены не полностью.",
            WhyItMatters = "Изменение условий, прекращение доступа или ограничение API может нарушить непрерывность сервиса и обязательства перед клиентами.",
            Recommendation = "Оценить технический и договорный запасной сценарий для критических внешних API.",
            AffectedDimensions = new() { "third_party_dependencies" },
            Recommendations = new() {
                "Определить критичные внешние зависимости.",
                "Проверить условия использования и прекращения доступа.",
                "Оценить технический и договорный запасной сценарий."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_ACCESS_CONTROL",
            RootCauseGroup = "KEY_DEVELOPER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Критически важные технические активы находятся под личным контролем",
            Finding = "Часть ключевых сервисов, репозиториев, доменов или иных технических активов оформлена на конкретного founder, сотрудника или подрядчика.",
            WhyItMatters = "При уходе или конфликте компания может потерять фактический доступ к инфраструктуре, даже если юридически считает себя владельцем.",
            Recommendation = "Перевести все учетные записи и репозитории на корпоративные аккаунты с двухфакторной аутентификацией и резервными правами доступа.",
            AffectedDimensions = new() { "technical_control" },
            Recommendations = new() {
                "Определить перечень критических аккаунтов.",
                "Создать корпоративный контроль и резервные доступы.",
                "Связать изменение доступов с процедурой ухода людей."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_DOMAIN_BRAND_CONTROL",
            RootCauseGroup = "IP_CONTROL",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Домен или оформленные права на бренд находятся вне компании",
            Finding = "Основной домен или часть прав на бренд зарегистрированы на founder, сотрудника либо подрядчика, а не на операционную компанию.",
            WhyItMatters = "Такой актив может оказаться зависимым от отношений с конкретным человеком и потребовать отдельной процедуры передачи.",
            Recommendation = "Перенести домен на корпоративный аккаунт компании.",
            AffectedDimensions = new() { "brand_domain" },
            Recommendations = new() {
                "Проверить текущих владельцев домена и оформленных прав.",
                "Определить целевого владельца (компания).",
                "Оформить передачу и корпоративный контроль."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_CONTENT_RIGHTS",
            RootCauseGroup = "IP_CONTENT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Происхождение части данных или контента как актива не подтверждено",
            Finding = "Значимая часть базы данных, изображений, видео, текстов или других материалов получена из внешних источников, а право использовать их в текущей модели проверено не полностью.",
            WhyItMatters = "Ограничения на использование внешних датасетов или контента могут повлечь претензии правообладателей и блокировку продукта.",
            Recommendation = "Проверить лицензии на используемые датасеты и медиаконтент.",
            AffectedDimensions = new() { "content_provenance" },
            Recommendations = new() {
                "Определить источники ключевых материалов.",
                "Проверить разрешения и условия использования.",
                "Заменить или оформить права на проблемные элементы."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "IP_RIGHTS_REVIEW"
        },
        new() {
            Code = "IP_BRAND_REGISTRATION_INFO",
            RootCauseGroup = "IP_CONTROL",
            Severity = RiskSeverity.Info,
            Priority = RiskPriority.Later,
            SectionId = "ip",
            Modules = new() { "ip" },
            Title = "Бренд пока не оформлен как отдельный зарегистрированный актив",
            Finding = "Компания использует название или бренд, но отдельная регистрация товарного знака пока не проводилась.",
            WhyItMatters = "Это нормально на ранней стадии; вопрос становится более значимым по мере роста узнаваемости и выхода на новые рынки.",
            Recommendation = "Оценить необходимость и доступность регистрации товарного знака на целевых рынках.",
            AffectedDimensions = new() { "brand_domain" },
            Recommendations = new() {
                "Проверить, насколько бренд уже значим для бизнеса.",
                "Оценить доступность и необходимость регистрации на ключевых рынках."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "IP_RIGHTS_REVIEW"
        }
    };
}
