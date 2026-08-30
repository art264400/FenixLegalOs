using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Временной горизонт и срочность устранения выявленного риска в Action Plan.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<RiskPriority>))]
public enum RiskPriority
{
    /// <summary>
    /// Требует немедленного устранения (первоочередной фокус).
    /// </summary>
    [WireValue("NOW")]
    Now,

    /// <summary>
    /// Рекомендуется устранить в течение ближайших 30 дней.
    /// </summary>
    [WireValue("30_DAYS")]
    ThirtyDays,

    /// <summary>
    /// Необходимо устранить до выхода на инвестиционный раунд или масштабирования.
    /// </summary>
    [WireValue("BEFORE_ROUND")]
    BeforeRound,

    /// <summary>
    /// Плановое улучшение юридической гигиены без жестких сроков.
    /// </summary>
    [WireValue("LATER")]
    Later
}
