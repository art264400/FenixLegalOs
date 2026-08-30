using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class CorporateDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("ownership_accuracy", "corporate", "Соответствие юридической структуры и долей"),
        new("cap_table", "corporate", "Таблица долей и оформление реестра участников (Cap table)"),
        new("equity_commitments", "corporate", "Отсутствие скрытых обещаний долей"),
        new("corporate_history", "corporate", "Чистая корпоративная история изменений"),
        new("corporate_approvals", "corporate", "Корпоративные решения и одобрения (Approvals)"),
        new("authority", "corporate", "Контроль полномочий руководства"),
        new("entity_alignment", "corporate", "Соответствие структуры бизнес-модели"),
        new("records", "corporate", "Порядок в корпоративном архиве")
    };
}
