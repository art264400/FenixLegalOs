namespace FenixLegalOs.Options;

/// <summary>
/// Настройки подключения к шлюзу BCC.
/// Все параметры считываются из конфигурации / переменных окружения без значений по умолчанию.
/// </summary>
public sealed class BccPaymentOptions
{
    public string Environment { get; set; } = "";
    public string TerminalId { get; set; } = "";
    public string GatewayUrl { get; set; } = "";
    public string NotifyUrl { get; set; } = "";
    public string ReturnUrl { get; set; } = "";
    public string MerchantId { get; set; } = "";
    public string MerchantName { get; set; } = "";
    public string MacKeyHex { get; set; } = "";

    // TODO: Уточнить у BCC требования к billAddrLine1 и заменить временное значение при необходимости.
    public string BillingAddressLine1 { get; set; } = "Казахстан, Астана";
}
