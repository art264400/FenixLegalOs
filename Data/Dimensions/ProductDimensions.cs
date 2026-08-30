using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class ProductDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("rules_presence", "product", "Наличие правил для пользователей"),
        new("rules_match", "product", "Соответствие правил реальной работе продукта"),
        new("offer_clarity", "product", "Ясность предложения и условий до оплаты"),
        new("company_role", "product", "Роль компании и распределение ответственности"),
        new("terms_acceptance", "product", "Принятие условий и фиксация согласия"),
        new("payment_transparency", "product", "Прозрачность цен и платежей"),
        new("refunds", "product", "Политика возвратов"),
        new("subscription_mechanics", "product", "Механика подписок и автопродлений"),
        new("account_restrictions", "product", "Блокировки аккаунтов и ограничения"),
        new("ugc", "product", "Пользовательский контент (UGC)"),
        new("special_context", "product", "Специальные категории пользователей и география")
    };
}
