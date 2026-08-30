using FenixLegalOs.Models;

namespace FenixLegalOs.Data.Dimensions;

public static class IpDimensions
{
    public static readonly List<DimensionDefinition> All = new()
    {
        new("overall_rights", "ip", "Полный объем прав на ключевой продукт"),
        new("founder_rights", "ip", "Оформление прав на разработки фаундеров"),
        new("employee_rights", "ip", "Оформление служебных произведений сотрудников"),
        new("external_creators", "ip", "Передача прав от подрядчиков и студий"),
        new("external_employer", "ip", "Чистота прав от притязаний работодателей"),
        new("third_party_dependencies", "ip", "Лицензионная чистота Open Source компонентов"),
        new("technical_control", "ip", "Технический контроль над критической инфраструктурой"),
        new("brand_domain", "ip", "Защита бренда и владение доменами"),
        new("content_provenance", "ip", "Лицензионная чистота медиа-контента")
    };
}
