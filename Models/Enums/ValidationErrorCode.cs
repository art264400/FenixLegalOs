using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Канонический код ошибки валидации входных ответов API.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ValidationErrorCode>))]
public enum ValidationErrorCode
{
    /// <summary>
    /// Идентификатор вопроса отсутствует в банке вопросов.
    /// </summary>
    [WireValue("UNKNOWN_QUESTION")]
    UnknownQuestion,

    /// <summary>
    /// Передано значение null.
    /// </summary>
    [WireValue("NULL_VALUE")]
    NullValue,

    /// <summary>
    /// Передано пустое или состоящее только из пробелов значение.
    /// </summary>
    [WireValue("EMPTY_VALUE")]
    EmptyValue,

    /// <summary>
    /// Тип JSON-структуры не соответствует ожидаемому типу вопроса.
    /// </summary>
    [WireValue("INVALID_TYPE")]
    InvalidType,

    /// <summary>
    /// Вариант ответа не входит в допустимый список опций вопроса.
    /// </summary>
    [WireValue("INVALID_OPTION")]
    InvalidOption,

    /// <summary>
    /// Для вопроса множественного выбора передан пустой список.
    /// </summary>
    [WireValue("EMPTY_SELECTION")]
    EmptySelection,

    /// <summary>
    /// Вопрос множественного выбора содержит пустой строковый элемент.
    /// </summary>
    [WireValue("EMPTY_ITEM")]
    EmptyItem,

    /// <summary>
    /// Одновременно выбраны взаимоисключающие варианты (например, "none" и конкретная опция).
    /// </summary>
    [WireValue("MUTUALLY_EXCLUSIVE_CONFLICT")]
    MutuallyExclusiveConflict,

    /// <summary>
    /// Невозможно распознать числовое значение.
    /// </summary>
    [WireValue("INVALID_NUMBER")]
    InvalidNumber,

    /// <summary>
    /// В вопросе распределения долей отсутствует хотя бы одно значение.
    /// </summary>
    [WireValue("EMPTY_SHARES")]
    EmptyShares,

    /// <summary>
    /// Процент доли сооснователя выходит за границы (0, 100].
    /// </summary>
    [WireValue("OUT_OF_RANGE_SHARE")]
    OutOfRangeShare,

    /// <summary>
    /// Элемент структуры компаний в entity builder не является валидным объектом.
    /// </summary>
    [WireValue("INVALID_ENTITY_FORMAT")]
    InvalidEntityFormat,

    /// <summary>
    /// Указан неподдерживаемый код юрисдикции.
    /// </summary>
    [WireValue("INVALID_JURISDICTION")]
    InvalidJurisdiction
}
