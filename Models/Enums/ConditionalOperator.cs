using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Оператор условной логики видимости вопросов и срабатывания правил (Routing / ShowIf).
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ConditionalOperator>))]
public enum ConditionalOperator
{
    /// <summary>
    /// Значение равно ожидаемому.
    /// </summary>
    [WireValue("eq")]
    Eq,

    /// <summary>
    /// Значение не равно ожидаемому.
    /// </summary>
    [WireValue("neq")]
    Neq,

    /// <summary>
    /// Значение входит в указанный список вариантов.
    /// </summary>
    [WireValue("in")]
    In,

    /// <summary>
    /// Значение не входит в указанный список вариантов.
    /// </summary>
    [WireValue("notIn")]
    NotIn,

    /// <summary>
    /// Коллекция или строка через запятую содержит заданное значение.
    /// </summary>
    [WireValue("contains")]
    Contains,

    /// <summary>
    /// Коллекция или строка через запятую не содержит заданное значение.
    /// </summary>
    [WireValue("notContains")]
    NotContains,

    /// <summary>
    /// На вопрос предоставлен непустой ответ.
    /// </summary>
    [WireValue("answered")]
    Answered,

    /// <summary>
    /// Числовое значение больше или равно заданному порогу.
    /// </summary>
    [WireValue("gte")]
    Gte,

    /// <summary>
    /// Числовое значение меньше или равно заданному порогу.
    /// </summary>
    [WireValue("lte")]
    Lte
}
