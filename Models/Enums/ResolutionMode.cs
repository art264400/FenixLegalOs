using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Канонический формат устранения юридического риска (vNext).
/// Определяет требуемый уровень экспертизы и способ реализации действия.
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ResolutionMode>))]
public enum ResolutionMode
{
    /// <summary>
    /// Внутреннее действие команды без привлечения юриста (сбор информации, перечней, доступов).
    /// </summary>
    [WireValue("internal_action")]
    InternalAction = 1,

    /// <summary>
    /// Юридическая проверка существующих материалов, условий сторонних сервисов или типовых форм.
    /// </summary>
    [WireValue("legal_review")]
    LegalReview = 2,

    /// <summary>
    /// Профессиональная разработка или изменение юридической конструкции / документации (SHA, IP, DPA и др.).
    /// </summary>
    [WireValue("legal_work")]
    LegalWork = 3,

    /// <summary>
    /// Комплексная задача: юридическая логика требует продуктовой / технической реализации (consent flow, age gate, deletion).
    /// </summary>
    [WireValue("legal_and_product")]
    LegalAndProduct = 4
}
