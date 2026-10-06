using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FenixLegalOs.Services;

/// <summary>
/// Реализация платёжного шлюза для банка Банк ЦентрКредит (BCC).
/// Инкапсулирует настройки, подписи P_SIGN, MAC и параметры BCC.
/// </summary>
public sealed class BccPaymentGateway : IPaymentGateway
{
    private readonly BccPaymentOptions _options;
    private readonly ILogger<BccPaymentGateway> _logger;
    private readonly System.Net.Http.IHttpClientFactory? _httpClientFactory;
    private readonly bool _logTestPayloads;

    public const string HttpClientName = "BccPaymentGatewayClient";

    public BccPaymentGateway(
        IConfiguration? configuration = null,
        ILogger<BccPaymentGateway>? logger = null,
        System.Net.Http.IHttpClientFactory? httpClientFactory = null)
    {
        _logger = logger ?? NullLogger<BccPaymentGateway>.Instance;
        _httpClientFactory = httpClientFactory;
        _logTestPayloads = bool.TryParse(configuration?["BCC_LOG_TEST_PAYLOADS"]?.Trim(), out bool logPayloads) && logPayloads;
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

        LogTestMacKeyFingerprint();
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

    /// <summary>
    /// Записывает безопасный отпечаток MAC-ключа только в тестовом окружении.
    /// Сам ключ в журнал не попадает; отпечаток позволяет подтвердить, какой ключ загрузил процесс.
    /// </summary>
    private void LogTestMacKeyFingerprint()
    {
        if (!string.Equals(_options.Environment, "test", StringComparison.OrdinalIgnoreCase) ||
            !IsValidHexKey(_options.MacKeyHex))
        {
            return;
        }

        byte[] keyBytes = Convert.FromHexString(_options.MacKeyHex);
        byte[] fingerprintBytes = System.Security.Cryptography.SHA256.HashData(keyBytes);
        string fingerprint = Convert.ToHexString(fingerprintBytes)[..16];

        System.Security.Cryptography.CryptographicOperations.ZeroMemory(keyBytes);
        System.Security.Cryptography.CryptographicOperations.ZeroMemory(fingerprintBytes);

        _logger.LogInformation(
            PaymentEvents.BccTestConfigurationLoaded,
            "Загружена тестовая конфигурация BCC: окружение {Environment}, терминал {TerminalId}, fingerprint MAC-ключа {MacKeyFingerprint}",
            _options.Environment,
            _options.TerminalId,
            fingerprint);
    }

    private void LogTestRequest(string trType, string orderId, IReadOnlyDictionary<string, string> fields)
    {
        if (!_logTestPayloads || !string.Equals(_options.Environment, "test", StringComparison.OrdinalIgnoreCase))
            return;

        string curlRequest = BuildCurlRequest(_options.GatewayUrl, fields);
        _logger.LogInformation(
            PaymentEvents.PaymentRefundGatewaySent,
            "Полный тестовый запрос BCC TRTYPE={TrType}, ORDER={OrderId}:\n{CurlRequest}",
            trType,
            orderId,
            curlRequest);
    }

    /// <summary>
    /// Формирует представление запроса в формате cURL, совпадающем с примерами BCC.
    /// Команда предназначена только для тестового журнала и не выполняется приложением.
    /// </summary>
    internal static string BuildCurlRequest(string gatewayUrl, IReadOnlyDictionary<string, string> fields)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("curl --location '")
            .Append(EscapeCurlSingleQuotedValue(gatewayUrl))
            .Append('\'');

        foreach (var field in fields)
        {
            sb.Append(" \\\n--data-urlencode '")
                .Append(EscapeCurlSingleQuotedValue(field.Key))
                .Append('=')
                .Append(EscapeCurlSingleQuotedValue(field.Value))
                .Append('\'');
        }

        return sb.ToString();
    }

    private static string EscapeCurlSingleQuotedValue(string value) =>
        value.Replace("'", "'\"'\"'", StringComparison.Ordinal);

    private void LogTestResponse(string trType, string orderId, int httpStatus, string body)
    {
        if (!_logTestPayloads || !string.Equals(_options.Environment, "test", StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogInformation(
            PaymentEvents.PaymentRefundGatewaySent,
            "Тестовый ответ BCC TRTYPE={TrType}, ORDER={OrderId}, HTTP={HttpStatus}: {ResponseBody}",
            trType,
            orderId,
            httpStatus,
            body);
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
        _logger.LogInformation(
            PaymentEvents.BccFormPreparing,
            "Началось формирование формы BCC для сессии {SessionId}, тариф {Tariff}, сумма {AmountKzt} {Currency}",
            request.SessionId,
            request.Tariff,
            request.AmountKzt,
            request.Currency);

        if (!IsConfigured)
        {
            var missingSettings = new List<string>();
            if (string.IsNullOrWhiteSpace(_options.Environment)) missingSettings.Add(nameof(_options.Environment));
            if (string.IsNullOrWhiteSpace(_options.TerminalId)) missingSettings.Add(nameof(_options.TerminalId));
            if (string.IsNullOrWhiteSpace(_options.GatewayUrl)) missingSettings.Add(nameof(_options.GatewayUrl));
            if (string.IsNullOrWhiteSpace(_options.NotifyUrl)) missingSettings.Add(nameof(_options.NotifyUrl));
            if (string.IsNullOrWhiteSpace(_options.ReturnUrl)) missingSettings.Add(nameof(_options.ReturnUrl));
            if (string.IsNullOrWhiteSpace(_options.MerchantId)) missingSettings.Add(nameof(_options.MerchantId));
            if (string.IsNullOrWhiteSpace(_options.MerchantName)) missingSettings.Add(nameof(_options.MerchantName));
            if (!IsValidHexKey(_options.MacKeyHex)) missingSettings.Add(nameof(_options.MacKeyHex));

            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Платёжный шлюз BCC не настроен. Отсутствующие настройки: {MissingSettings}",
                string.Join(", ", missingSettings));

            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "payment_gateway_not_configured",
                ErrorMessage = "Платёжный шлюз BCC ещё не настроен."
            });
        }

        if (string.IsNullOrWhiteSpace(request.ClientIp))
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Отсутствует Client IP для сессии {SessionId}",
                request.SessionId);

            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "missing_client_ip",
                ErrorMessage = "Для проведения платежа BCC требуется указать IP-адрес клиента (ClientIp)."
            });
        }

        if (string.IsNullOrWhiteSpace(request.Phone))
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Отсутствует телефон пользователя для сессии {SessionId}",
                request.SessionId);

            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "phone_required",
                ErrorMessage = "Для проведения платежа BCC требуется указать номер телефона."
            });
        }

        if (string.IsNullOrWhiteSpace(request.BillingAddress))
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Отсутствует адрес плательщика для сессии {SessionId}",
                request.SessionId);

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
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Адрес плательщика длиннее 50 символов (длина {AddressLength}) для сессии {SessionId}",
                trimmedAddress.Length,
                request.SessionId);

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
        catch (ArgumentException)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Некорректны параметры экрана или контекст платежа для сессии {SessionId}",
                request.SessionId);

            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = false,
                ErrorCode = "invalid_payment_context",
                ErrorMessage = "Не удалось подготовить данные для проведения платежа."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccGatewayError,
                "Ошибка создания M_INFO для сессии {SessionId}. Тип ошибки: {ErrorType}",
                request.SessionId,
                ex.GetType().Name);

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

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            _logger.LogInformation(
                PaymentEvents.BccFormPrepared,
                "Сформирован новый OrderId {OrderId} для сессии {SessionId}",
                orderId,
                request.SessionId);
        }

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
        string pSign;
        try
        {
            pSign = SignMac(macData);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccGatewayError,
                "Ошибка расчёта P_SIGN для заказа {OrderId}, сессия {SessionId}. Тип ошибки: {ErrorType}",
                orderId,
                request.SessionId,
                ex.GetType().Name);
            throw;
        }

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

        LogTestRequest(trType, orderId, formFields);

        _logger.LogInformation(
            PaymentEvents.BccFormPrepared,
            "Форма BCC успешно подготовлена: заказ {OrderId}, сессия {SessionId}, терминал {TerminalId}, сумма {AmountKzt} {Currency}, тариф {Tariff}, окружение {Environment}",
            orderId,
            request.SessionId,
            _options.TerminalId,
            request.AmountKzt,
            request.Currency,
            request.Tariff,
            _options.Environment);

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

    /// <summary>
    /// Собирает строку источника для макирования по протоколу BCC для проверки статуса (TRTYPE=90).
    /// Строгий порядок полей по документации BCC:
    /// 1. ORDER
    /// 2. TERMINAL
    /// 3. TIMESTAMP
    /// 4. TRTYPE
    /// 5. NONCE
    /// </summary>
    public static string BuildStatusCheckMacDataString(
        string order,
        string terminal,
        string timestamp,
        string trType,
        string nonce)
    {
        var sb = new System.Text.StringBuilder();
        AppendField(sb, order);
        AppendField(sb, terminal);
        AppendField(sb, timestamp);
        AppendField(sb, trType);
        AppendField(sb, nonce);
        return sb.ToString();
    }

    /// <summary>
    /// Выполняет запрос проверки статуса транзакции TRTYPE=90 со стороны мерчанта.
    /// Тип исходной операции задаётся в TRAN_TRTYPE: 1 для покупки и 14 для возврата.
    /// BCC может не повторять TRTYPE=90 в синхронном ответе, поэтому контекст ответа
    /// проверяется по TRAN_TRTYPE, ORDER и TERMINAL. Явно неверный TRTYPE отклоняется.
    /// </summary>
    public async Task<PaymentGatewayCheckResult> CheckStatusAsync(
        string orderId,
        CancellationToken cancellationToken = default,
        string tranTrType = "14")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);

        if (!IsConfigured)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Проверка статуса TRTYPE=90 не может быть выполнена: шлюз не настроен для заказа {OrderId}",
                orderId);

            return new PaymentGatewayCheckResult
            {
                Status = PaymentStatuses.Unknown,
                Success = false,
                IsFinal = false,
                ErrorCode = "gateway_not_configured",
                ErrorMessage = "Шлюз BCC не настроен."
            };
        }

        if (tranTrType is not ("1" or "14"))
            throw new ArgumentOutOfRangeException(nameof(tranTrType), "TRAN_TRTYPE должен быть равен 1 или 14.");

        string terminalId = _options.TerminalId;
        string timestamp = GenerateTimestamp();
        string nonce = GenerateNonce();
        string trType = "90";

        string macData = BuildStatusCheckMacDataString(orderId, terminalId, timestamp, trType, nonce);
        string pSign = SignMac(macData);

        var formData = new Dictionary<string, string>
        {
            ["ORDER"] = orderId,
            ["TERMINAL"] = terminalId,
            ["TRTYPE"] = trType,
            ["TIMESTAMP"] = timestamp,
            ["NONCE"] = nonce,
            ["P_SIGN"] = pSign,
            ["MERCH_GMT"] = "0",
            ["TRAN_TRTYPE"] = tranTrType,
            ["NOTIFY_URL"] = _options.NotifyUrl
        };

        LogTestRequest(trType, orderId, formData);

        _logger.LogInformation(
            PaymentEvents.PaymentRefundGatewaySent,
            "Отправлен запрос проверки статуса TRTYPE=90 в BCC: заказ {OrderId}, терминал {TerminalId}",
            orderId,
            terminalId);

        using var client = _httpClientFactory != null
            ? _httpClientFactory.CreateClient(HttpClientName)
            : new System.Net.Http.HttpClient();

        System.Net.Http.HttpResponseMessage httpResponse;
        try
        {
            using var requestMessage = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post,
                _options.GatewayUrl)
            {
                Content = new System.Net.Http.FormUrlEncodedContent(formData)
            };
            httpResponse = await client.SendAsync(
                requestMessage,
                System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccGatewayError,
                "Сетевая ошибка при запросе проверки статуса TRTYPE=90 в BCC для заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            return new PaymentGatewayCheckResult
            {
                Status = PaymentStatuses.Unknown,
                Success = false,
                IsFinal = false,
                ErrorCode = "network_error",
                ErrorMessage = "Сетевая ошибка при обращении к BCC."
            };
        }

        string rawBody;
        try
        {
            rawBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Не удалось прочитать ответ BCC для проверки статуса заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            return new PaymentGatewayCheckResult
            {
                Status = PaymentStatuses.Unknown,
                Success = false,
                IsFinal = false,
                ErrorCode = "response_read_error",
                ErrorMessage = "Не удалось прочитать ответ BCC. Требуется повторная проверка статуса."
            };
        }

        int statusCode = (int)httpResponse.StatusCode;
        LogTestResponse(trType, orderId, statusCode, rawBody);

        // Синхронный ответ BCC на TRTYPE=90 может быть пустым.
        // В этом случае запрос считается отправленным, но результат остаётся pending (IsFinal=false).
        // Не помечаем возврат успешным или неуспешным только по HTTP-статусу.
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            _logger.LogInformation(
                PaymentEvents.PaymentRefundGatewaySent,
                "Синхронный ответ BCC на TRTYPE=90 для заказа {OrderId} пуст (HTTP {StatusCode}). Запрос отправлен, результат остаётся в ожидании (pending).",
                orderId,
                statusCode);

            return new PaymentGatewayCheckResult
            {
                Status = PaymentStatuses.Unknown,
                Success = false,
                IsFinal = false,
                BankMessage = "Синхронный ответ BCC на TRTYPE=90 пуст. Запрос отправлен, результат остаётся pending (ожидается callback или ручная сверка)."
            };
        }

        var dict = ExtractFieldsFromBody(rawBody);

        string? action = dict.GetValueOrDefault("ACTION");
        string? rc = dict.GetValueOrDefault("RC");
        string? respTrType = dict.GetValueOrDefault("TRTYPE");
        string? respTranTrType = dict.GetValueOrDefault("TRAN_TRTYPE");
        string? respOrder = dict.GetValueOrDefault("ORDER");
        string? respTerminal = dict.GetValueOrDefault("TERMINAL");
        string? rrn = dict.GetValueOrDefault("RRN");
        string? intRef = dict.GetValueOrDefault("INT_REF");
        string? approvalCode = dict.GetValueOrDefault("APPROVAL")
            ?? dict.GetValueOrDefault("APPROVAL_CODE")
            ?? dict.GetValueOrDefault("AUTH_CODE");
        string? text = dict.GetValueOrDefault("RC_TEXT")
            ?? dict.GetValueOrDefault("TEXT")
            ?? dict.GetValueOrDefault("BANK_MESSAGE");

        // В реальном синхронном ответе BCC на TRTYPE=90 поле TRTYPE может отсутствовать.
        // Это допустимо только когда остальные поля однозначно связывают ответ с запросом.
        // Если TRTYPE присутствует, он обязан быть равен 90.
        bool responseTrTypeMatches = string.IsNullOrWhiteSpace(respTrType)
            || string.Equals(respTrType, "90", StringComparison.OrdinalIgnoreCase);
        bool isExpectedStatus = responseTrTypeMatches
            && string.Equals(respTranTrType, tranTrType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(respOrder, orderId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(respTerminal, terminalId, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(action) && !string.IsNullOrWhiteSpace(rc))
        {
            if (isExpectedStatus)
            {
                bool isSuccess = action == "0" && (rc == "00" || rc == "0");
                return new PaymentGatewayCheckResult
                {
                    Status = isSuccess
                        ? (tranTrType == "14" ? PaymentStatuses.Refunded : PaymentStatuses.Paid)
                        : PaymentStatuses.Failed,
                    Success = isSuccess,
                    IsFinal = true,
                    ActionCode = action,
                    ResponseCode = rc,
                    Rrn = rrn,
                    IntRef = intRef,
                    ApprovalCode = approvalCode,
                    BankMessage = text,
                    TrType = tranTrType
                };
            }
            else
            {
                string operationName = tranTrType == "14" ? "возврата" : "покупки";

                _logger.LogWarning(
                    PaymentEvents.BccGatewayWarning,
                    "Ответ BCC не соответствует запросу проверки статуса {OperationName} для заказа {OrderId}: TRTYPE={TrType}, TRAN_TRTYPE={TranTrType}, ORDER={ResponseOrder}, TERMINAL={ResponseTerminal}. Результат остаётся неопределённым для ручной сверки.",
                    operationName,
                    orderId,
                    respTrType ?? "null",
                    respTranTrType ?? "null",
                    respOrder ?? "null",
                    respTerminal ?? "null");

                return new PaymentGatewayCheckResult
                {
                    Status = PaymentStatuses.Unknown,
                    Success = false,
                    IsFinal = false,
                    ActionCode = action,
                    ResponseCode = rc,
                    Rrn = rrn,
                    IntRef = intRef,
                    ApprovalCode = approvalCode,
                    BankMessage = $"Ответ шлюза не соответствует запросу проверки статуса {operationName}. Требуется ручная сверка с выпиской банка.",
                    TrType = respTrType,
                    ErrorCode = "response_context_mismatch",
                    ErrorMessage = "Реквизиты ответа BCC не совпадают с отправленным запросом."
                };
            }
        }

        return new PaymentGatewayCheckResult
        {
            Status = PaymentStatuses.Unknown,
            Success = false,
            IsFinal = false,
            BankMessage = !string.IsNullOrWhiteSpace(text)
                ? text
                : $"Шлюз вернул HTTP {statusCode} без однозначных банковских кодов ACTION/RC. Результат оставлен pending."
        };
    }

    /// <summary>
    /// Собирает строку источника для макирования по протоколу BCC для возврата (TRTYPE=14):
    /// Порядок строго:
    /// 1. ORDER
    /// 2. ORG_AMOUNT
    /// 3. AMOUNT
    /// 4. CURRENCY
    /// 5. RRN
    /// 6. INT_REF
    /// 7. TERMINAL
    /// 8. TIMESTAMP
    /// 9. TRTYPE
    /// 10. NONCE
    /// </summary>
    public static string BuildRefundMacDataString(
        string order,
        string orgAmount,
        string amount,
        string currency,
        string rrn,
        string intRef,
        string terminal,
        string timestamp,
        string trType,
        string nonce)
    {
        var sb = new System.Text.StringBuilder();
        AppendField(sb, order);
        AppendField(sb, orgAmount);
        AppendField(sb, amount);
        AppendField(sb, currency);
        AppendField(sb, rrn);
        AppendField(sb, intRef);
        AppendField(sb, terminal);
        AppendField(sb, timestamp);
        AppendField(sb, trType);
        AppendField(sb, nonce);
        return sb.ToString();
    }

    public async Task<PaymentGatewayRefundResult> RefundAsync(PaymentGatewayRefundRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsConfigured)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат BCC не может быть выполнен: шлюз не настроен для заказа {OrderId}",
                request.OrderId);

            return new PaymentGatewayRefundResult
            {
                Success = false,
                Accepted = false,
                IsFinal = true,
                ErrorCode = "gateway_not_configured",
                ErrorMessage = "Платёжный шлюз BCC не настроен."
            };
        }

        string orderId = request.OrderId.Trim();
        string merchRnId = request.MerchRnId.Trim();
        string rrn = request.Rrn.Trim();
        string intRef = request.IntRef.Trim();
        string terminalId = !string.IsNullOrWhiteSpace(request.TerminalId) ? request.TerminalId.Trim() : _options.TerminalId;
        string currencyCode = "398"; // Код тенге ISO 4217 в BCC
        string trType = "14";
        string timestamp = string.IsNullOrWhiteSpace(request.RequestTimestamp)
            ? GenerateTimestamp()
            : request.RequestTimestamp.Trim();
        string nonce = string.IsNullOrWhiteSpace(request.Nonce)
            ? GenerateNonce()
            : request.Nonce.Trim();
        string notifyUrl = !string.IsNullOrWhiteSpace(request.NotifyUrl) ? request.NotifyUrl : _options.NotifyUrl;

        string orgAmountStr = request.OriginalAmountKzt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        string amountStr = request.RefundAmountKzt.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

        string macData = BuildRefundMacDataString(
            order: orderId,
            orgAmount: orgAmountStr,
            amount: amountStr,
            currency: currencyCode,
            rrn: rrn,
            intRef: intRef,
            terminal: terminalId,
            timestamp: timestamp,
            trType: trType,
            nonce: nonce);

        string pSign = SignMac(macData);

        var formData = new Dictionary<string, string>
        {
            ["ORG_AMOUNT"] = orgAmountStr,
            ["AMOUNT"] = amountStr,
            ["CURRENCY"] = currencyCode,
            ["ORDER"] = orderId,
            ["MERCH_RN_ID"] = merchRnId,
            ["RRN"] = rrn,
            ["INT_REF"] = intRef,
            ["TERMINAL"] = terminalId,
            ["TIMESTAMP"] = timestamp,
            ["MERCH_GMT"] = "0",
            ["TRTYPE"] = trType,
            ["LANG"] = "ru",
            ["NONCE"] = nonce,
            ["P_SIGN"] = pSign,
            ["NOTIFY_URL"] = notifyUrl
        };

        LogTestRequest(trType, orderId, formData);

        _logger.LogInformation(
            PaymentEvents.PaymentRefundGatewaySent,
            "Отправлен запрос возврата TRTYPE=14 в BCC: заказ {OrderId}, сумма {AmountKzt} KZT, терминал {TerminalId}",
            orderId,
            request.RefundAmountKzt,
            terminalId);

        using var client = _httpClientFactory != null
            ? _httpClientFactory.CreateClient(HttpClientName)
            : new System.Net.Http.HttpClient();

        System.Net.Http.HttpResponseMessage httpResponse;
        try
        {
            using var requestMessage = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post,
                _options.GatewayUrl)
            {
                Content = new System.Net.Http.FormUrlEncodedContent(formData)
            };
            httpResponse = await client.SendAsync(
                requestMessage,
                System.Net.Http.HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccGatewayError,
                "Сетевая ошибка при отправке запроса возврата TRTYPE=14 в BCC для заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            return new PaymentGatewayRefundResult
            {
                Accepted = false,
                IsFinal = false,
                Success = false,
                ErrorCode = "network_error",
                ErrorMessage = "Сетевая ошибка при обращении к BCC."
            };
        }

        string rawBody;
        try
        {
            rawBody = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Не удалось прочитать ответ BCC для возврата заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            return new PaymentGatewayRefundResult
            {
                Accepted = false,
                IsFinal = false,
                Success = false,
                ErrorCode = "response_read_error",
                ErrorMessage = "Не удалось прочитать ответ BCC. Требуется проверка статуса возврата."
            };
        }

        int statusCode = (int)httpResponse.StatusCode;
        LogTestResponse(trType, orderId, statusCode, rawBody);

        // Если получен HTTP 4xx или 5xx статус:
        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "BCC шлюз вернул HTTP статус {StatusCode} при возврате заказа {OrderId}",
                statusCode,
                orderId);

            // Сначала пробуем извлечь банковский результат (ACTION и RC) из тела ответа даже при HTTP 4xx/5xx:
            var parsedFromError = TryParseDefinitiveBankResult(rawBody, orderId, terminalId);
            if (parsedFromError != null)
            {
                return parsedFromError;
            }

            // Если ACTION/RC отсутствуют или тело невозможно проверить:
            // Accepted = false, IsFinal = false, Success = false. Статус локального возврата остаётся pending.
            return new PaymentGatewayRefundResult
            {
                Accepted = false,
                IsFinal = false,
                Success = false,
                ErrorCode = "gateway_http_indeterminate",
                ErrorMessage = $"Шлюз BCC вернул HTTP {statusCode} без однозначных банковских кодов подтверждения."
            };
        }

        return ParseRefundResponse(rawBody, orderId, terminalId);
    }

    /// <summary>
    /// Проверяет тело ответа на наличие однозначных банковских кодов ACTION и RC.
    /// </summary>
    private PaymentGatewayRefundResult? TryParseDefinitiveBankResult(string body, string orderId, string terminalId)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        // Если ответ содержит HTML без банковских кодов — это не подтверждение результата
        if (body.Contains("<html", StringComparison.OrdinalIgnoreCase) &&
            !body.Contains("ACTION=", StringComparison.OrdinalIgnoreCase) &&
            !body.Contains("RC=", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var dict = ExtractFieldsFromBody(body);
        string? action = dict.GetValueOrDefault("ACTION");
        string? rc = dict.GetValueOrDefault("RC");
        string? rrn = dict.GetValueOrDefault("RRN");
        string? intRef = dict.GetValueOrDefault("INT_REF");
        string? text = dict.GetValueOrDefault("TEXT") ?? dict.GetValueOrDefault("BANK_MESSAGE");
        string? responseOrder = dict.GetValueOrDefault("ORDER");
        string? responseTrType = dict.GetValueOrDefault("TRTYPE");
        string? responseTerminal = dict.GetValueOrDefault("TERMINAL");

        bool contextMismatch =
            (!string.IsNullOrWhiteSpace(responseOrder) && !string.Equals(responseOrder, orderId, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(responseTrType) && !string.Equals(responseTrType, "14", StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(responseTerminal) && !string.Equals(responseTerminal, terminalId, StringComparison.OrdinalIgnoreCase));

        if (contextMismatch)
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Реквизиты ответа BCC не совпали с запросом возврата заказа {OrderId}; результат оставлен неопределённым.",
                orderId);

            return new PaymentGatewayRefundResult
            {
                Accepted = false,
                IsFinal = false,
                Success = false,
                ErrorCode = "response_context_mismatch",
                ErrorMessage = "Реквизиты ответа BCC не совпадают с отправленным запросом возврата."
            };
        }

        if (action != null && rc != null)
        {
            bool isSuccess = action == "0" && (rc == "00" || rc == "0");
            _logger.LogInformation(
                isSuccess ? PaymentEvents.PaymentRefundSucceeded : PaymentEvents.PaymentRefundFailed,
                "Получен банковский результат возврата BCC: заказ {OrderId}, ACTION {Action}, RC {ResponseCode}, Success: {Success}",
                orderId,
                action,
                rc,
                isSuccess);

            return new PaymentGatewayRefundResult
            {
                Accepted = isSuccess,
                IsFinal = true,
                Success = isSuccess,
                ActionCode = action,
                ResponseCode = rc,
                Rrn = rrn,
                IntRef = intRef,
                BankMessage = text
            };
        }

        return null;
    }

    /// <summary>
    /// Парсит синхронный ответ BCC шлюза (form urlencoded, json или текстовый ответ cgi_link).
    /// </summary>
    private PaymentGatewayRefundResult ParseRefundResponse(string body, string orderId, string terminalId)
    {
        var definitive = TryParseDefinitiveBankResult(body, orderId, terminalId);
        if (definitive != null)
        {
            return definitive;
        }

        // Если тело пустое или HTML или не содержит ACTION/RC — возврат принят в обработку, ожидает callback
        _logger.LogInformation(
            PaymentEvents.PaymentRefundAwaitingCallback,
            "Синхронный ответ BCC не содержит явных кодов ACTION/RC для заказа {OrderId}, ожидается callback",
            orderId);

        var dict = ExtractFieldsFromBody(body);
        string? text = dict.GetValueOrDefault("TEXT") ?? dict.GetValueOrDefault("BANK_MESSAGE");

        return new PaymentGatewayRefundResult
        {
            Accepted = true,
            IsFinal = false,
            Success = false,
            BankMessage = text
        };
    }

    /// <summary>
    /// Безопасно извлекает поля формы / JSON из тела ответа.
    /// </summary>
    private static Dictionary<string, string> ExtractFieldsFromBody(string body)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(body))
            return dict;

        // 1. Попытка распарсить как query string / form urlencoded / построчно
        string[] pairs = body.Split(new[] { '&', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            int eqIdx = pair.IndexOf('=');
            if (eqIdx > 0)
            {
                string key = System.Net.WebUtility.UrlDecode(pair[..eqIdx].Trim());
                string val = System.Net.WebUtility.UrlDecode(pair[(eqIdx + 1)..].Trim());
                dict[key] = val;
            }
        }

        // 2. Попытка распарсить JSON, если пары не найдены
        if (!dict.ContainsKey("ACTION") && !dict.ContainsKey("RC") && body.TrimStart().StartsWith("{"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    dict[prop.Name] = prop.Value.ToString();
                }
            }
            catch { }
        }

        return dict;
    }
}
