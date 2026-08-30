using System.Collections.Generic;
using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class DataAiDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("data_map", "data", "Карта данных и учет процессов"),
        new("privacy_notice", "data", "Политика конфиденциальности и прозрачность"),
        new("secondary_use", "data", "Вторичное использование данных и маркетинг"),
        new("third_party_services", "data", "Внешние сервисы и обработчики данных"),
        new("cross_border", "data", "Трансграничная передача данных"),
        new("retention_deletion", "data", "Хранение и удаление данных"),
        new("access_offboarding", "data", "Контроль доступа команды к данным"),
        new("ai_external_data", "data", "Передача данных во внешние AI-модели"),
        new("ai_training", "data", "Обучение собственных AI-моделей на данных"),
        new("ai_decisions", "data", "Автоматические решения AI и человеческий контроль")
    };
}
