using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class FoundersDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("existing_dispute", "founders", "Отсутствие споров между основателями"),
        new("roles", "founders", "Четкое разделение ролей и зон ответственности"),
        new("commitment", "founders", "Согласованная занятость и вовлеченность фаундеров"),
        new("equity_clarity", "founders", "Распределение долей основателей (Соответствие долей)"),
        new("early_exit_equity", "founders", "Защита от раннего ухода фаундеров (Vesting / Leaver)"),
        new("governance", "founders", "Корпоративное управление и прозрачный порядок принятия решений"),
        new("deadlock", "founders", "Защита от тупиковых ситуаций в голосовании (Deadlock)"),
        new("exit_continuity", "founders", "Порядок выхода фаундера и передачи дел"),
        new("founder_contributions", "founders", "Учет личных инвестиций и займов"),
        new("conflict_of_interest", "founders", "Отсутствие конфликта интересов у основателей"),
        new("strategic_alignment", "founders", "Единое стратегическое видение развития (Стратегия)")
    };
}
