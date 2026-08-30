using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class ProductRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «ПРОДУКТ И ПОЛЬЗОВАТЕЛИ» (v1.1)
        // =====================================================================
        new() {
            Code = "PROD_RULES_MISSING",
            RootCauseGroup = "PRODUCT_RULES",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Отношения с пользователями практически не урегулированы",
            Finding = "Продукт уже используется реальными пользователями, но система не видит единого актуального набора правил использования, оплаты, ответственности и прекращения доступа.",
            WhyItMatters = "При споре компании будет сложнее показать, на каких условиях пользователь получал продукт и что стороны согласовали заранее.",
            Recommendation = "Подготовить пользовательские условия под реальную модель и встроить их принятие в интерфейс.",
            AffectedDimensions = new() { "rules_presence" },
            Recommendations = new() {
                "Описать фактический путь пользователя и основные коммерческие правила.",
                "Подготовить пользовательские условия под реальную модель.",
                "Встроить принятие условий в интерфейс продукта."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_RULES_MISMATCH",
            RootCauseGroup = "PRODUCT_RULES",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Правила продукта могут не соответствовать его текущей работе",
            Finding = "После подготовки пользовательских условий продукт заметно менялся либо документ изначально использовался как шаблон без полной сверки.",
            WhyItMatters = "Документ, который описывает старую модель, может создавать ложное ощущение защиты и расходиться с фактическими обещаниями пользователю.",
            Recommendation = "Сопоставить текущий пользовательский путь с документом и зафиксировать новую версию.",
            AffectedDimensions = new() { "rules_match" },
            Recommendations = new() {
                "Сопоставить текущий пользовательский путь с документом.",
                "Обновить функциональность, оплату, ограничения и роли сторон.",
                "После обновления зафиксировать новую версию и принятие пользователем."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_OFFER_UNCLEAR",
            RootCauseGroup = "PRODUCT_OFFER",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Пользователю не всегда ясно, что именно он получает",
            Finding = "Описание продукта и фактический результат могут заметно расходиться либо часть существенных условий становится понятна только после начала использования.",
            WhyItMatters = "Разное понимание предмета услуги часто становится основой претензий о качестве, оплате и возврате денег.",
            Recommendation = "Сверить маркетинговое описание и фактическую функциональность до оплаты.",
            AffectedDimensions = new() { "offer_clarity" },
            Recommendations = new() {
                "Сверить маркетинговое описание и фактическую функциональность.",
                "Выделить существенные ограничения до оплаты.",
                "Синхронизировать интерфейс, рекламу и пользовательские правила."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_ROLE_UNCLEAR",
            RootCauseGroup = "PRODUCT_ROLE",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Роль компании в сделке с пользователем определена недостаточно ясно",
            Finding = "Продукт соединяет пользователя с партнером, продавцом или исполнителем, но границы ответственности между ними описаны не полностью.",
            WhyItMatters = "Пользователь может считать именно вашу компанию продавцом или исполнителем там, где бизнес-модель предполагает иную роль.",
            Recommendation = "Определить фактическую роль каждой стороны и отразить ее в правилах.",
            AffectedDimensions = new() { "company_role" },
            Recommendations = new() {
                "Определить фактическую роль каждой стороны.",
                "Отразить ее в интерфейсе и пользовательских правилах.",
                "Согласовать эту модель с договорами с партнерами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_ACCEPTANCE_WEAK",
            RootCauseGroup = "PRODUCT_RULES",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Может быть сложно подтвердить принятие пользователем правил",
            Finding = "Условия существуют, но пользователь либо только видит ссылку, либо система не сохраняет достаточное подтверждение принятой версии.",
            WhyItMatters = "При споре важно не только наличие документа, но и возможность показать, что конкретный пользователь согласился с применимой версией.",
            Recommendation = "Определить момент принятия условий и сохранять факт согласия с версией.",
            AffectedDimensions = new() { "terms_acceptance" },
            Recommendations = new() {
                "Определить момент принятия условий в пользовательском пути.",
                "Сделать действие пользователя однозначным.",
                "Сохранять факт и версию принятых правил, где это технически оправдано."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_PAYMENT_TRANSPARENCY",
            RootCauseGroup = "PRODUCT_PAYMENTS",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Условия оплаты могут быть недостаточно прозрачны до покупки",
            Finding = "Часть комиссий или дополнительных платежей становится понятна пользователю только после начала покупки или использования.",
            WhyItMatters = "Это повышает риск претензий по цене и возвратам, особенно в массовом продукте для физических лиц.",
            Recommendation = "Показать полную стоимость до оплаты и выделить переменные комиссии.",
            AffectedDimensions = new() { "payment_transparency" },
            Recommendations = new() {
                "Показать полную стоимость до оплаты.",
                "Отдельно выделить переменные комиссии и условия их расчета.",
                "Сверить интерфейс оплаты с пользовательскими правилами."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_REFUND_RULES",
            RootCauseGroup = "PRODUCT_PAYMENTS",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Правила возврата денег определены не полностью",
            Finding = "Возвраты решаются по ситуации или единого понятного подхода для пользователя нет.",
            WhyItMatters = "Непоследовательная практика повышает риск конфликтов и должна оцениваться с учетом типа продукта, пользователя и страны.",
            Recommendation = "Определить фактическую политику возвратов и проверить ее на основных рынках.",
            AffectedDimensions = new() { "refunds" },
            Recommendations = new() {
                "Определить фактическую бизнес-политику возвратов.",
                "Проверить ее применимость на основных рынках.",
                "Согласовать интерфейс, поддержку и пользовательские правила."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_SUBSCRIPTION_RULES",
            RootCauseGroup = "PRODUCT_SUBSCRIPTION",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Механика подписки требует пересмотра",
            Finding = "Подписка может продлеваться автоматически, а уведомление о продлении, пробном периоде или порядок отмены недостаточно очевидны.",
            WhyItMatters = "Повторяющиеся списания и сложная отмена являются частым источником пользовательских претензий и зависят от требований конкретного рынка.",
            Recommendation = "Проверить информирование до оплаты и упростить отмену подписки.",
            AffectedDimensions = new() { "subscription_mechanics" },
            Recommendations = new() {
                "Проверить, как автопродление показывается до оплаты.",
                "Упростить и описать порядок отмены.",
                "Сверить пробный период, списания и возвраты с пользовательскими правилами."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_ACCOUNT_RESTRICTIONS",
            RootCauseGroup = "PRODUCT_RULES",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Последствия блокировки аккаунта определены не полностью",
            Finding = "Не во всех случаях понятно, когда компания может ограничить доступ и что происходит с уже уплаченными деньгами.",
            WhyItMatters = "При блокировке пользователь особенно чувствителен к основаниям решения и финансовым последствиям.",
            Recommendation = "Определить основания ограничения доступа и судьбу уплаченных средств.",
            AffectedDimensions = new() { "account_restrictions" },
            Recommendations = new() {
                "Определить основные основания ограничения доступа.",
                "Разделить временную блокировку и прекращение отношений.",
                "Описать судьбу оплаченного периода или средств."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_USER_CONTENT_RULES",
            RootCauseGroup = "USER_CONTENT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Правила работы с пользовательским контентом определены не полностью",
            Finding = "Пользователи размещают собственные материалы, но ограничения на контент, права компании на его использование или процесс жалоб описаны частично.",
            WhyItMatters = "Для платформы это создает одновременно пользовательские, контентные и потенциально IP/data вопросы.",
            Recommendation = "Определить запрещенный контент, права компании на материалы и процедуру жалоб.",
            AffectedDimensions = new() { "ugc" },
            Recommendations = new() {
                "Определить запрещенный контент и процесс жалоб.",
                "Описать, что компания вправе делать с материалами пользователя.",
                "Передать сигналы о рекламе/данных в IP и Data блоки."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_MINORS_REVIEW",
            RootCauseGroup = "MINORS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Работа с несовершеннолетними пользователями требует отдельной проверки",
            Finding = "Продукт доступен детям или подросткам либо возраст не ограничивается, а специальные правила для такой аудитории не прорабатывались полностью.",
            WhyItMatters = "Требования к продукту и данным несовершеннолетних могут существенно отличаться по странам и модели сервиса.",
            Recommendation = "Проверить требования к несовершеннолетним на ключевых рынках и настроить ограничения.",
            AffectedDimensions = new() { "special_context" },
            Recommendations = new() {
                "Определить фактический возрастной профиль пользователей.",
                "Проверить правила продукта и обработки данных на основных рынках.",
                "Настроить необходимые возрастные ограничения или механики согласия."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_MULTI_COUNTRY_REVIEW",
            RootCauseGroup = "PRODUCT_CROSS_BORDER",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Правила продукта не полностью проверены для основных стран пользователей",
            Finding = "Продукт используется в нескольких странах или глобально, но пользовательская модель анализировалась только для первоначального рынка либо не анализировалась отдельно.",
            WhyItMatters = "Обязательные правила для потребителей, подписок, возвратов и платформ могут различаться между странами.",
            Recommendation = "Выделить основные рынки и адаптировать продуктовые документы под локальные правила.",
            AffectedDimensions = new() { "special_context" },
            Recommendations = new() {
                "Выделить основные рынки по числу пользователей и выручке.",
                "Определить наиболее существенные локальные различия.",
                "Адаптировать продуктовые документы там, где это необходимо."
            },
            LawyerRequired = false,
            Resolution = ResolutionType.CheckWithLawyer,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        },
        new() {
            Code = "PROD_REGULATORY_REVIEW",
            RootCauseGroup = "REGULATED_PRODUCT",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "product",
            Modules = new() { "product" },
            Title = "Некоторые функции продукта требуют отдельной правовой проверки",
            Finding = "Продукт работает в сфере платежей, инвестиций, кредитования, криптовалют, здоровья, найма, сертификатов, азартных игр, площадок или другой специальной модели.",
            WhyItMatters = "Автоматическая анкета не может определить, требуется ли конкретное разрешение или специальный режим только по одному функциональному признаку.",
            Recommendation = "Провести отдельный анализ применимых регуляторных требований до масштабирования функции.",
            AffectedDimensions = new() { "special_context" },
            Recommendations = new() {
                "Описать фактическую роль компании и денежные/информационные потоки.",
                "Определить страны запуска.",
                "Провести отдельный анализ применимых специальных требований до масштабирования функции."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "PRODUCT_LEGAL_REVIEW"
        }
    };
}
