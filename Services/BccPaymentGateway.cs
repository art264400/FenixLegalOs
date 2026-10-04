using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using Microsoft.Extensions.Configuration;

namespace FenixLegalOs.Services;

/// <summary>
/// Реализация платёжного шлюза для банка Банк ЦентрКредит (BCC).
/// Инкапсулирует настройки, подписи P_SIGN, MAC и параметры BCC.
/// </summary>
public sealed class BccPaymentGateway : IPaymentGateway
{
    private readonly BccPaymentOptions _options;

    public BccPaymentGateway(IConfiguration? configuration = null)
    {
        _options = new BccPaymentOptions
        {
            Environment = configuration?["BCC_ENVIRONMENT"]?.Trim().ToLowerInvariant() ?? "",
            TerminalId = configuration?["BCC_TERMINAL_ID"]?.Trim() ?? "",
            GatewayUrl = configuration?["BCC_GATEWAY_URL"]?.Trim() ?? "",
            NotifyUrl = configuration?["BCC_NOTIFY_URL"]?.Trim() ?? "",
            ReturnUrl = configuration?["BCC_RETURN_URL"]?.Trim() ?? "",
            MerchantId = configuration?["BCC_MERCHANT_ID"]?.Trim() ?? "",
            MerchantName = configuration?["BCC_MERCHANT_NAME"]?.Trim() ?? "",
            MacKeyHex = configuration?["BCC_MAC_KEY"]?.Trim() ?? "",
            NotifyUsername = configuration?["BCC_NOTIFY_USERNAME"]?.Trim() ?? "",
            NotifyPassword = configuration?["BCC_NOTIFY_PASSWORD"]?.Trim() ?? "",
            AllowUnauthenticatedTestNotifications = bool.TryParse(configuration?["BCC_ALLOW_UNAUTHENTICATED_TEST_NOTIFICATIONS"]?.Trim(), out bool allowUnauth) && allowUnauth
        };
    }

    public string Provider => "bcc";

    public string Environment => _options.Environment;
    public string TerminalId => _options.TerminalId;

    /// <summary>
    /// Шлюз считается настроенным только тогда, когда заданы все обязательные параметры
    /// (Environment, TerminalId, GatewayUrl, NotifyUrl, ReturnUrl, MerchantId, MerchantName)
    /// и MacKeyHex является корректной HEX-строкой, декодируемой через Convert.FromHexString.
    /// При невалидном ключе шлюз считается ненастроенным.
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Environment) &&
        !string.IsNullOrWhiteSpace(_options.TerminalId) &&
        !string.IsNullOrWhiteSpace(_options.GatewayUrl) &&
        !string.IsNullOrWhiteSpace(_options.NotifyUrl) &&
        !string.IsNullOrWhiteSpace(_options.ReturnUrl) &&
        !string.IsNullOrWhiteSpace(_options.MerchantId) &&
        !string.IsNullOrWhiteSpace(_options.MerchantName) &&
        IsValidHexKey(_options.MacKeyHex);

    private static bool IsValidHexKey(string? hexKey)
    {
        if (string.IsNullOrWhiteSpace(hexKey))
            return false;

        try
        {
            byte[] bytes = Convert.FromHexString(hexKey.Trim());
            return bytes.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public string GenerateNonce() => Guid.NewGuid().ToString("N").ToUpperInvariant();
    public string GenerateTimestamp() => DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    /// <summary>
    /// Генерирует уникальный идентификатор мерчанта MERCH_RN_ID ровно из 16 буквенно-цифровых символов.
    /// Не входит в строку макирования для TRTYPE=1.
    /// </summary>
    public static string GenerateMerchRnId()
    {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        return System.Security.Cryptography.RandomNumberGenerator.GetString(chars, 16);
    }

    public Task<PaymentGatewayInitResult> CreatePaymentAsync(PaymentGatewayInitRequest request, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "payment_gateway_not_configured",
                ErrorMessage = "Платёжный шлюз BCC ещё не настроен."
            });
        }

        if (string.IsNullOrWhiteSpace(request.ClientIp))
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "missing_client_ip",
                ErrorMessage = "Для проведения платежа BCC требуется указать IP-адрес клиента (ClientIp)."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "phone_required",
                ErrorMessage = "Для проведения платежа BCC требуется указать номер телефона."
            });
        }

        if (string.IsNullOrWhiteSpace(request.BillingAddress))
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "billing_address_required",
                ErrorMessage = "Для проведения платежа BCC требуется указать адрес плательщика."
            });
        }

        string trimmedAddress = request.BillingAddress.Trim();
        if (trimmedAddress.Length > 50)
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "billing_address_too_long",
                ErrorMessage = "Адрес плательщика не должен превышать 50 символов."
            });
        }

        // Формирование M_INFO для 3-D Secure: строго mobilePhone, строковые размеры экрана, billAddrLine1
        string mInfo;
        try
        {
            mInfo = BuildMInfo(request.BrowserScreenHeight, request.BrowserScreenWidth, request.Phone, trimmedAddress);
        }
        catch
        {
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "invalid_payment_context",
                ErrorMessage = "Не удалось подготовить данные для проведения платежа."
            });
        }

        // По спецификации BCC: ORDER должен быть длиной от 6 до 32 символов (только цифры/символы)
        string orderId = !string.IsNullOrWhiteSpace(request.OrderId)
            ? request.OrderId
            : $"{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        string nonce = GenerateNonce();
        string timestamp = GenerateTimestamp();
        string merchRnId = GenerateMerchRnId();
        string amountStr = $"{request.AmountKzt}.00";
        string currencyCode = "398"; // 398 = KZT (тенге) согласно ISO 4217 и спецификации BCC
        string merchGmt = "0";       // актуальное значение в BCC e-Commerce
        string trType = "1";         // 1 = Покупка
        string merchant = _options.MerchantId;
        string merchName = _options.MerchantName.ToUpperInvariant();
        string desc = $"Diagnostic payment tariff {request.Tariff}";

        // Сборка строки источника макирования для TRTYPE=1:
        // AMOUNT, CURRENCY, ORDER, MERCHANT, TERMINAL, MERCH_GMT, TIMESTAMP, TRTYPE, NONCE
        string macData = BuildMacDataString(amountStr, currencyCode, orderId, merchant, _options.TerminalId, merchGmt, timestamp, trType, nonce);

        // Вычисление цифровой подписи P_SIGN (HMAC-SHA1 с HEX-ключом)
        string pSign = SignMac(macData);

        // Подготовка параметров формы для шлюза BCC (TRTYPE=1, шаг #1)
        var formFields = new Dictionary<string, string>
        {
            ["AMOUNT"] = amountStr,
            ["CURRENCY"] = currencyCode,
            ["ORDER"] = orderId,
            ["DESC"] = desc,
            ["MERCHANT"] = merchant,
            ["MERCH_NAME"] = merchName,
            ["MERCH_RN_ID"] = merchRnId,
            ["TERMINAL"] = _options.TerminalId,
            ["TIMESTAMP"] = timestamp,
            ["MERCH_GMT"] = merchGmt,
            ["TRTYPE"] = trType,
            ["BACKREF"] = _options.ReturnUrl,
            ["NOTIFY_URL"] = _options.NotifyUrl,
            ["LANG"] = "ru",
            ["NONCE"] = nonce,
            ["CLIENT_IP"] = request.ClientIp.Trim(),
            ["M_INFO"] = mInfo,
            ["P_SIGN"] = pSign
        };

        return Task.FromResult(new PaymentGatewayInitResult
        {
            Success = true,
            OrderId = orderId,
            TerminalId = _options.TerminalId,
            Nonce = nonce,
            MerchRnId = merchRnId,
            RequestTimestamp = timestamp,
            PaymentUrl = _options.GatewayUrl,
            Method = "POST",
            CheckoutType = "form_post",
            FormFields = formFields
        });
    }

    /// <summary>
    /// Собирает строку источника для макирования по протоколу BCC / Way4:
    /// Каждому непустому полю предшествует длина значения этого поля (${length}${value}).
    /// Для TRTYPE=1: AMOUNT, CURRENCY, ORDER, MERCHANT, TERMINAL, MERCH_GMT, TIMESTAMP, TRTYPE, NONCE.
    /// </summary>
    public static string BuildMacDataString(
        string amount, string currency, string order, string merchant,
        string terminal, string merchGmt, string timestamp, string trType, string nonce)
    {
        var sb = new System.Text.StringBuilder();
        AppendField(sb, amount);
        AppendField(sb, currency);
        AppendField(sb, order);
        AppendField(sb, merchant);
        AppendField(sb, terminal);
        AppendField(sb, merchGmt);
        AppendField(sb, timestamp);
        AppendField(sb, trType);
        AppendField(sb, nonce);
        return sb.ToString();
    }

    private static void AppendField(System.Text.StringBuilder sb, string? val)
    {
        if (string.IsNullOrEmpty(val))
        {
            sb.Append('-');
        }
        else
        {
            sb.Append(val.Length).Append(val);
        }
    }

    /// <summary>
    /// Вычисляет цифровой код аутентификации сообщений P_SIGN (MAC) по алгоритму HMAC-SHA1.
    /// MAC-ключ мерчанта декодируется из шестнадцатеричной строки (HEX).
    /// Fallback на UTF-8 исключён спецификацией BCC. При невалидном HEX выбрасывается исключение.
    /// </summary>
    public string SignMac(string macData, string? macKey = null)
    {
        string rawKey = (!string.IsNullOrWhiteSpace(macKey) ? macKey : _options.MacKeyHex).Trim();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            throw new InvalidOperationException("MAC-ключ BCC не настроен.");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromHexString(rawKey);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("MAC-ключ BCC не является корректной шестнадцатеричной (HEX) строкой.", nameof(macKey), ex);
        }

        using var hmac = new System.Security.Cryptography.HMACSHA1(keyBytes);
        byte[] hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(macData));
        return Convert.ToHexString(hash).ToUpperInvariant();
    }

    /// <summary>
    /// Извлекает код страны (cc="7") и 10 цифр абонента (subscriber) для M_INFO 3-D Secure.
    /// </summary>
    public static (string Cc, string Subscriber) FormatPhoneForMInfo(string phone)
    {
        if (FenixLegalOs.Infrastructure.PhoneHelper.TryExtractMInfoPhone(phone, out string cc, out string subscriber))
        {
            return (cc, subscriber);
        }
        throw new ArgumentException("Некорректный номер телефона для M_INFO.", nameof(phone));
    }

    /// <summary>
    /// Формирует Base64 JSON для поля M_INFO по спецификации BCC 3-D Secure:
    /// {
    ///   "browserScreenHeight": "1080",
    ///   "browserScreenWidth": "1920",
    ///   "mobilePhone": {
    ///     "cc": "7",
    ///     "subscriber": "7001234567"
    ///   },
    ///   "billAddrLine1": "г. Астана, ул. Абая, д. 10, кв. 5"
    /// }
    /// </summary>
    public static string BuildMInfo(int browserScreenHeight, int browserScreenWidth, string phone, string billAddrLine1)
    {
        var (cc, subscriber) = FormatPhoneForMInfo(phone);

        var payload = new
        {
            browserScreenHeight = browserScreenHeight.ToString(),
            browserScreenWidth = browserScreenWidth.ToString(),
            mobilePhone = new
            {
                cc = cc,
                subscriber = subscriber
            },
            billAddrLine1 = billAddrLine1
        };

        byte[] jsonUtf8Bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        return Convert.ToBase64String(jsonUtf8Bytes);
    }

    public Task<PaymentGatewayCheckResult> CheckStatusAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentGatewayCheckResult
        {
            Status = PaymentStatuses.Unknown
        });
    }

    public Task<PaymentGatewayRefundResult> RefundAsync(string orderId, int amountKzt, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new PaymentGatewayRefundResult
        {
            Success = false,
            BankMessage = "BCC refund not configured."
        });
    }
}
