namespace FenixLegalOs.Models;

/// <summary>
/// Выбранный пользователем тариф для новой попытки оплаты.
/// Сумма всегда определяется на сервере и не должна приниматься из браузера.
/// </summary>
public sealed class StartPaymentRequest
{
    public string Tariff { get; init; } = "report";
}
