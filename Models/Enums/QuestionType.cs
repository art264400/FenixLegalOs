using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Тип вопроса и формат ожидаемого пользовательского ввода.
/// Определяет правила валидации и способ отображения компонента в интерфейсе.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<QuestionType>))]
public enum QuestionType
{
    /// <summary>
    /// Одиночный выбор из фиксированного списка вариантов.
    /// </summary>
    [WireValue("single")]
    Single,

    /// <summary>
    /// Множественный выбор нескольких вариантов ответа.
    /// </summary>
    [WireValue("multiple")]
    Multiple,

    /// <summary>
    /// Логический вопрос (Да / Нет).
    /// </summary>
    [WireValue("boolean")]
    Boolean,

    /// <summary>
    /// Произвольный текстовый ввод.
    /// </summary>
    [WireValue("text")]
    Text,

    /// <summary>
    /// Числовой ввод.
    /// </summary>
    [WireValue("number")]
    Number,

    /// <summary>
    /// Специализированный ввод процентного распределения долей между сооснователями.
    /// </summary>
    [WireValue("equity_inputs")]
    EquityInputs,

    /// <summary>
    /// Конструктор структуры группы компаний и юрисдикций владения.
    /// </summary>
    [WireValue("entity_builder")]
    EntityBuilder
}
