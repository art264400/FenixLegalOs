using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Степень определенности ответа пользователя для расчета общего индекса уверенности (Confidence Index).
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ConfidenceClass>))]
public enum ConfidenceClass
{
    /// <summary>
    /// Точный, подтвержденный ответ (полная уверенность по вопросу).
    /// </summary>
    [WireValue("known")]
    Known,

    /// <summary>
    /// Частичная определенность (промежуточный статус или устная договоренность).
    /// </summary>
    [WireValue("partial")]
    Partial,

    /// <summary>
    /// Ответ "Не уверен" или неизвестное состояние (понижает confidence итогового отчета).
    /// </summary>
    [WireValue("unknown")]
    Unknown
}
