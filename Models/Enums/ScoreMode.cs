using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Режим влияния вопроса на оценку зрелости и логику пайплайна скоринга.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ScoreMode>))]
public enum ScoreMode
{
    /// <summary>
    /// Контекстный вопрос: не участвует напрямую в расчете баллов,
    /// формирует канонические факты для маршрутизации и применимости модулей.
    /// </summary>
    [WireValue("context")]
    Context,

    /// <summary>
    /// Диагностический вопрос: участвует в математическом расчете балла измерения (Dimension).
    /// </summary>
    [WireValue("diagnostic")]
    Diagnostic,

    /// <summary>
    /// Вопрос-триггер: предназначен для выявления блокеров или активации специальных веток.
    /// </summary>
    [WireValue("trigger")]
    Trigger
}
