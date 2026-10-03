using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;

namespace FenixLegalOs.Services;

/// <summary>
/// Универсальный контракт платёжного шлюза для поддержки любых провайдеров (BCC, Kaspi, Halyk, Stripe и т.д.).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Идентификатор / название провайдера (например "bcc", "kaspi", "halyk").
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Текущее окружение шлюза ("test" или "production").
    /// </summary>
    string Environment { get; }

    /// <summary>
    /// Признак готовности и валидности конфигурации шлюза (ключи, терминалы, адреса).
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Инициализация параметров платёжной попытки (orderId, nonce, timestamp, подпись и т.д.).
    /// </summary>
    Task<PaymentGatewayInitResult> CreatePaymentAsync(PaymentGatewayInitRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверка актуального статуса платежа в шлюзе.
    /// </summary>
    Task<PaymentGatewayCheckResult> CheckStatusAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Отмена или возврат платежа.
    /// </summary>
    Task<PaymentGatewayRefundResult> RefundAsync(string orderId, int amountKzt, CancellationToken cancellationToken = default);
}

public sealed class PaymentGatewayInitRequest
{
    public string SessionId { get; init; } = "";
    public string Tariff { get; init; } = "report";
    public int AmountKzt { get; init; }
    public string Currency { get; init; } = "KZT";

    /// <summary>
    /// IP-адрес клиента (покупателя). Получается только на сервере.
    /// </summary>
    public string? ClientIp { get; init; }

    /// <summary>
    /// Серверный номер телефона пользователя (из users.phone).
    /// </summary>
    public string? Phone { get; init; }

    /// <summary>
    /// Высота экрана браузера (window.outerHeight).
    /// </summary>
    public int BrowserScreenHeight { get; init; }

    /// <summary>
    /// Ширина экрана браузера (window.outerWidth).
    /// </summary>
    public int BrowserScreenWidth { get; init; }
}

public sealed class PaymentGatewayInitResult
{
    public bool Success { get; init; }
    public string? OrderId { get; init; }
    public string? TerminalId { get; init; }
    public string? Nonce { get; init; }
    public string? MerchRnId { get; init; }
    public string? RequestTimestamp { get; init; }
    public string? PaymentUrl { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// URL для перехода / отправки формы (эквивалент action URL формы или страницы оплаты).
    /// </summary>
    public string? ActionUrl => PaymentUrl;

    /// <summary>
    /// HTTP-метод запуска оплаты ("POST" для HTML-формы BCC, "GET" для redirect-ссылок).
    /// </summary>
    public string Method { get; init; } = "POST";

    /// <summary>
    /// Тип сценария checkout ("form_post", "redirect", "qr", "widget").
    /// </summary>
    public string CheckoutType { get; init; } = "form_post";

    /// <summary>
    /// Подписанные параметры формы для POST-отправки (включая P_SIGN, TERMINAL, ORDER, AMOUNT, NONCE, TIMESTAMP и т.д.).
    /// </summary>
    public IReadOnlyDictionary<string, string> FormFields { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Произвольные метаданные провайдера в формате JSON или словаря.
    /// </summary>
    public string? ProviderMetadata { get; init; }
}

public sealed class PaymentGatewayCheckResult
{
    public string Status { get; init; } = PaymentStatuses.Unknown;
    public string? Rrn { get; init; }
    public string? IntRef { get; init; }
    public string? ApprovalCode { get; init; }
    public string? BankMessage { get; init; }
    public string? PaidAt { get; init; }
}

public sealed class PaymentGatewayRefundResult
{
    public bool Success { get; init; }
    public string? BankMessage { get; init; }
}
