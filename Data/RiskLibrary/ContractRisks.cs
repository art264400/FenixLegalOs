using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Data.RiskLibrary;

public static class ContractRisks
{
    public static readonly IReadOnlyList<RiskDefinition> All = new List<RiskDefinition>
    {
        // =====================================================================
        // РЕЕСТР РИСКОВ БЛОКА «ДОГОВОРЫ С КЛИЕНТАМИ И ПАРТНЕРАМИ» (CANONICAL §25 — 6 FINDINGS)
        // =====================================================================

        // 1. CONTRACTS_NOT_FORMALIZED
        new() {
            Code = "CONTRACTS_NOT_FORMALIZED",
            RootCauseGroup = "COMMERCIAL_CONTRACTS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Существенные коммерческие договоренности оформлены не полностью",
            Finding = "Часть важных условий сотрудничества с клиентами или партнерами остается в переписке, счетах или устных договоренностях.",
            WhyItMatters = "При разногласии сложнее доказать, кто что обещал, за какую цену и на каких условиях.",
            Recommendation = "Определить существенные отношения без полноценного договора.",
            AffectedDimensions = new() { "written_form" },
            Recommendations = new() {
                "Определить существенные отношения без полноценного договора.",
                "В первую очередь оформить ключевых клиентов и партнеров.",
                "Перенести критичные коммерческие условия из переписки в договор."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        },

        // 2. CONTRACT_SCOPE_UNCLEAR
        new() {
            Code = "CONTRACT_SCOPE_UNCLEAR",
            RootCauseGroup = "COMMERCIAL_CONTRACTS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Из договоров не всегда понятно, какой результат должна предоставить компания",
            Finding = "Часть объема услуг, сроков или критериев выполнения согласуется отдельно от основного договора либо сформулирована слишком общо.",
            WhyItMatters = "Это повышает вероятность того, что компания и клиент по-разному понимают объем обязательств.",
            Recommendation = "Сопоставить договор с реальной услугой или продуктом.",
            AffectedDimensions = new() { "scope" },
            Recommendations = new() {
                "Сопоставить договор с реальной услугой или продуктом.",
                "Вынести существенный объем работ и критерии в договор или приложение.",
                "Установить порядок изменения объема."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        },

        // 3. CONTRACT_RISK_ALLOCATION_WEAK
        new() {
            Code = "CONTRACT_RISK_ALLOCATION_WEAK",
            RootCauseGroup = "COMMERCIAL_CONTRACTS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Последствия нарушения договора определены недостаточно ясно",
            Finding = "Из основных договоров нельзя уверенно определить, как распределяется ответственность, если возникает задержка, сбой, потеря информации или другое существенное нарушение.",
            WhyItMatters = "При проблеме стороны могут иметь совершенно разные ожидания о последствиях и размере ответственности.",
            Recommendation = "Выделить реальные рисковые сценарии бизнеса.",
            AffectedDimensions = new() { "risk_allocation" },
            Recommendations = new() {
                "Выделить реальные рисковые сценарии бизнеса.",
                "Проверить, как они распределены в текущем шаблоне.",
                "Адаптировать ответственность и способы защиты под модель компании."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        },

        // 4. CONTRACT_MODEL_MISMATCH
        new() {
            Code = "CONTRACT_MODEL_MISMATCH",
            RootCauseGroup = "COMMERCIAL_CONTRACTS",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Договоры могут не соответствовать текущей бизнес-модели",
            Finding = "Компания в основном использует готовые или исторические шаблоны, которые отдельно не сверялись с тем, как продукт и продажи работают сейчас.",
            WhyItMatters = "Формальное наличие договора не защищает от разрыва между документом и фактическими обязательствами бизнеса.",
            Recommendation = "Сопоставить текущий шаблон с продажами и поставкой продукта.",
            AffectedDimensions = new() { "model_match" },
            Recommendations = new() {
                "Сопоставить текущий шаблон с продажами и поставкой продукта.",
                "Убрать положения, не относящиеся к модели.",
                "Добавить реальные коммерческие и технологические риски."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        },

        // 5. CONTRACT_COUNTERPARTY_DEPENDENCY
        new() {
            Code = "CONTRACT_COUNTERPARTY_DEPENDENCY",
            RootCauseGroup = "COUNTERPARTY_DEPENDENCY",
            Severity = RiskSeverity.High,
            Priority = RiskPriority.Now,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Бизнес существенно зависит от одного контрагента",
            Finding = "Значительная часть выручки или работы продукта зависит от одной стороны, которая может прекратить сотрудничество на условиях, способных заметно повлиять на компанию.",
            WhyItMatters = "Даже хороший договор не устраняет концентрационный риск, если у компании нет достаточного времени или альтернативы для замены контрагента.",
            Recommendation = "Проверить условия прекращения ключевого договора.",
            AffectedDimensions = new() { "dependency_large_deals" },
            Recommendations = new() {
                "Проверить условия прекращения ключевого договора.",
                "Оценить реальный срок и стоимость замены.",
                "Подготовить резервный коммерческий или технический сценарий."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        },

        // 6. CONTRACT_LARGE_DEAL_REVIEW
        new() {
            Code = "CONTRACT_LARGE_DEAL_REVIEW",
            RootCauseGroup = "COMMERCIAL_CONTRACTS",
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.ThirtyDays,
            SectionId = "contracts",
            Modules = new() { "contracts" },
            Title = "Существенные нестандартные договоры иногда подписываются без отдельной проверки",
            Finding = "Компания принимает отдельные крупные обязательства без предварительной проверки их соответствия бизнес-модели и рискам сделки.",
            WhyItMatters = "Чем выше стоимость и нестандартность договора, тем больше потенциальный эффект одного неудачного условия.",
            Recommendation = "Определить порог сделок, требующих обязательной проверки.",
            AffectedDimensions = new() { "dependency_large_deals" },
            Recommendations = new() {
                "Определить порог сделок, требующих обязательной проверки.",
                "Создать короткий внутренний процесс согласования.",
                "Проверять ответственность, прекращение и нестандартные обязательства до подписания."
            },
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = "CONTRACTS_REVIEW"
        }
    };
}
