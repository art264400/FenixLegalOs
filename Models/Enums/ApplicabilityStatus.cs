using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Статус применимости диагностического модуля к профилю компании.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ApplicabilityStatus>))]
public enum ApplicabilityStatus
{
    /// <summary>
    /// Модуль применим к бизнесу и участвует в расчете общего балла (Overall Score).
    /// </summary>
    [WireValue("APPLICABLE")]
    Applicable,

    /// <summary>
    /// Модуль неприменим к профилю компании (его вес исключается из знаменателя Overall Score).
    /// </summary>
    [WireValue("N_A")]
    NotApplicable
}
