using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Категория общего юридического здоровья компании (Legal Health Level).
/// </summary>
[JsonConverter(typeof(WireEnumConverter<LegalScoreLevel>))]
public enum LegalScoreLevel
{
    /// <summary>
    /// Сильная правовая основа (80–100 баллов). Точечные зоны для усиления.
    /// </summary>
    [WireValue("strong")]
    Strong,

    /// <summary>
    /// Базовая основа сформирована (60–79 баллов), но есть вопросы, требующие внимания.
    /// </summary>
    [WireValue("attention")]
    Attention,

    /// <summary>
    /// Существенные юридические пробелы (40–59 баллов), создающие уязвимости.
    /// </summary>
    [WireValue("material_gaps")]
    MaterialGaps,

    /// <summary>
    /// Структурные риски (0–39 баллов): правовой контур фрагментарен.
    /// </summary>
    [WireValue("structural_risks")]
    StructuralRisks
}
