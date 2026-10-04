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
    public string NotifyUsername { get; set; } = "";
    public string NotifyPassword { get; set; } = "";
    public bool AllowUnauthenticatedTestNotifications { get; set; } = false;
    public bool LogTestAuthorizationHeader { get; set; } = false;
}
