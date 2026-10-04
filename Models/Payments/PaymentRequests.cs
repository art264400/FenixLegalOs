namespace FenixLegalOs.Models.Payments;

/// <summary>
/// Выбранный пользователем тариф для новой попытки оплаты.
/// Сумма всегда определяется на сервере и не должна приниматься из браузера.
/// </summary>
public sealed class StartPaymentRequest
{
    public string Tariff { get; init; } = "report";
    public int BrowserScreenHeight { get; init; }
    public int BrowserScreenWidth { get; init; }

    /// <summary>
    /// Адрес плательщика, введенный пользователем (максимум 50 символов).
    /// </summary>
    public string? BillingAddress { get; init; }
}
