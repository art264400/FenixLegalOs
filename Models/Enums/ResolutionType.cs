using System.Text.Json.Serialization;

namespace FenixLegalOs.Models.Enums;

/// <summary>
/// Формат устранения риска (требуемый уровень юридической экспертизы).
/// </summary>
[JsonConverter(typeof(WireEnumConverter<ResolutionType>))]
public enum ResolutionType
{
    /// <summary>
    /// Риск устраняется фаундерами самостоятельно по инструкциям и шаблонам платформы.
    /// </summary>
    [WireValue("self_service")]
    SelfService,

    /// <summary>
    /// Документы готовятся самостоятельно, но рекомендуется финальная проверка юристом.
    /// </summary>
    [WireValue("check_with_lawyer")]
    CheckWithLawyer,

    /// <summary>
    /// Для корректного устранения риска требуется участие венчурного или корпоративного юриста.
    /// </summary>
    [WireValue("lawyer_required")]
    LawyerRequired
}
