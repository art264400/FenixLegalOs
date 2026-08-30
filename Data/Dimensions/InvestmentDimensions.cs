using System.Collections.Generic;
using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class InvestmentDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("prior_investments", "investment", "Прошлые инвестиции и обязательства"),
        new("future_ownership", "investment", "Будущая структура долей"),
        new("dilution", "investment", "Изменение долей после раунда"),
        new("round_definition", "investment", "Размер раунда и цель денег"),
        new("runway", "investment", "Финансовый запас"),
        new("financial_model", "investment", "Финансовая модель"),
        new("metrics_evidence", "investment", "Подтверждаемость показателей"),
        new("dd_documents", "investment", "Документы для проверки"),
        new("deal_terms", "investment", "Условия инвестиционной сделки"),
        new("deal_review", "investment", "Проверка условий сделки")
    };
}
