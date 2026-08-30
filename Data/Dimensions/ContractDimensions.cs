using System.Collections.Generic;
using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class ContractDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("written_form", "contracts", "Письменная форма и фиксация договоренностей"),
        new("scope", "contracts", "Предмет, объем обязательств и приемка"),
        new("payment_termination", "contracts", "Оплата, расторжение и штрафные санкции"),
        new("risk_allocation", "contracts", "Ограничение ответственности и распределение рисков"),
        new("model_match", "contracts", "Соответствие договоров бизнес-модели"),
        new("dependency_large_deals", "contracts", "Крупные сделки и концентрация контрагентов")
    };
}
