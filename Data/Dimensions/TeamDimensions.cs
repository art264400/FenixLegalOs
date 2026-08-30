using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class TeamDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("written_agreements", "team", "Письменные договоры с командой"),
        new("key_person_dependency", "team", "Зависимость от ключевых специалистов (Key Persons)"),
        new("work_format", "team", "Формат работы и трудовая квалификация подрядчиков"),
        new("terms_clarity", "team", "Ясность условий и обязанностей в договорах"),
        new("confidentiality", "team", "Защита конфиденциальной информации (NDA)"),
        new("work_rights", "team", "Переход прав на создаваемые результаты (IP)"),
        new("access_accounts", "team", "Контроль учетных записей и критических доступов"),
        new("offboarding", "team", "Регламент прекращения работы и отзыва доступов"),
        new("former_people", "team", "Отсутствие претензий и закрытие доступов бывших участников"),
        new("foreign_team", "team", "Трансграничное оформление иностранных специалистов"),
        new("team_equity", "team", "Опционные обещания и планы для команды (ESOP)")
    };
}
