using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Уровень существенности юридического риска в реестре находок (Risk Finding) согласно Developer Architecture v1.1 (§5, §25).
/// </summary>
[JsonConverter(typeof(WireEnumConverter<RiskSeverity>))]
public enum RiskSeverity
{
    /// <summary>
    /// Информационная рекомендация или наблюдение без прямого правового ущерба.
    /// </summary>
    [WireValue("INFO")]
    Info,

    /// <summary>
    /// Средний уровень риска: уязвимость, требующая планового устранения.
    /// </summary>
    [WireValue("MEDIUM")]
    Medium,

    /// <summary>
    /// Высокий уровень риска: существенный риск потери прав, корпоративного спора или штрафов.
    /// </summary>
    [WireValue("HIGH")]
    High,

    /// <summary>
    /// Критический риск: прямая угроза бизнесу, инвестициям или ключевым активам.
    /// </summary>
    [WireValue("CRITICAL")]
    Critical,

    /// <summary>
    /// Блокирующий дефект: делает невозможной инвестиционную сделку или продажу компании до его устранения.
    /// </summary>
    [WireValue("BLOCKER")]
    Blocker
}
