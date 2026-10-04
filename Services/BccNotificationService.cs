using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace FenixLegalOs.Services;

/// <summary>
/// Результат обработки входящего уведомления от шлюза BCC.
/// </summary>
public sealed class BccNotificationResult
{
    public int StatusCode { get; init; }
    public object? Value { get; init; }
    public string? WwwAuthenticateHeader { get; init; }
}

/// <summary>
/// Сервис для безопасной и идемпотентной обработки входящих серверных уведомлений BCC.
/// Проверяет HTTP Basic Auth с защитой от атак по времени (timing-safe comparison),
/// валидирует поля операции и атомарно переводит платёж, сессию и лид в актуальное состояние.
/// Пароли, MAC-ключ и данные платёжных карт не логируются.
/// </summary>
public sealed class BccNotificationService
{
    private readonly PaymentRepository _paymentRepository;
    private readonly BccPaymentOptions _options;

    public BccNotificationService(PaymentRepository paymentRepository, IConfiguration? configuration = null, BccPaymentOptions? options = null)
    {
        _paymentRepository = paymentRepository;
        _options = options ?? new BccPaymentOptions
        {
            Environment = configuration?["BCC_ENVIRONMENT"]?.Trim().ToLowerInvariant() ?? Environment.GetEnvironmentVariable("BCC_ENVIRONMENT")?.Trim().ToLowerInvariant() ?? "",
            TerminalId = configuration?["BCC_TERMINAL_ID"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_TERMINAL_ID")?.Trim() ?? "",
            GatewayUrl = configuration?["BCC_GATEWAY_URL"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_GATEWAY_URL")?.Trim() ?? "",
            NotifyUrl = configuration?["BCC_NOTIFY_URL"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_NOTIFY_URL")?.Trim() ?? "",
            ReturnUrl = configuration?["BCC_RETURN_URL"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_RETURN_URL")?.Trim() ?? "",
            MerchantId = configuration?["BCC_MERCHANT_ID"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_MERCHANT_ID")?.Trim() ?? "",
            MerchantName = configuration?["BCC_MERCHANT_NAME"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_MERCHANT_NAME")?.Trim() ?? "",
            MacKeyHex = configuration?["BCC_MAC_KEY"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_MAC_KEY")?.Trim() ?? "",
            NotifyUsername = configuration?["BCC_NOTIFY_USERNAME"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_NOTIFY_USERNAME")?.Trim() ?? "",
            NotifyPassword = configuration?["BCC_NOTIFY_PASSWORD"]?.Trim() ?? Environment.GetEnvironmentVariable("BCC_NOTIFY_PASSWORD")?.Trim() ?? "",
            AllowUnauthenticatedTestNotifications = ParseBool(configuration?["BCC_ALLOW_UNAUTHENTICATED_TEST_NOTIFICATIONS"] ?? Environment.GetEnvironmentVariable("BCC_ALLOW_UNAUTHENTICATED_TEST_NOTIFICATIONS"))
        };
    }

    private static bool ParseBool(string? value) =>
        bool.TryParse(value?.Trim(), out bool result) && result;

    public Task<BccNotificationResult> ProcessNotificationAsync(string? authHeader, IFormCollection? form)
    {
        // 1. Проверка авторизации:
        // - production: Basic Auth всегда обязателен, неавторизованные уведомления строго запрещены;
        // - test + AllowUnauthenticatedTestNotifications=true: разрешить уведомление без Authorization;
        // - test + флаг false: требовать настроенные логин/пароль либо возвращать 503;
        // - никогда не разрешать callback без авторизации в production.
        bool isTest = string.Equals(_options.Environment, "test", StringComparison.OrdinalIgnoreCase);
        bool allowUnauthenticated = isTest && _options.AllowUnauthenticatedTestNotifications;

        if (allowUnauthenticated && string.IsNullOrWhiteSpace(authHeader))
        {
            // В тестовой среде с разрешённым флагом уведомление принимается без заголовка Authorization
        }
        else
        {
            // Требуется настроенный HTTP Basic Auth
            if (string.IsNullOrWhiteSpace(_options.NotifyUsername) || string.IsNullOrWhiteSpace(_options.NotifyPassword))
            {
                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status503ServiceUnavailable,
                    Value = new
                    {
                        error = "bcc_notifications_not_configured",
                        message = "Приём уведомлений BCC ещё не настроен."
                    }
                });
            }

            if (!ValidateBasicAuth(authHeader))
            {
                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status401Unauthorized,
                    WwwAuthenticateHeader = "Basic realm=\"BCC Notify\"",
                    Value = new
                    {
                        error = "unauthorized",
                        message = "Неверная авторизация для приёма уведомлений BCC."
                    }
                });
            }
        }

        // 2. Извлечение полей BCC из application/x-www-form-urlencoded
        if (form == null)
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "missing_form_data",
                    message = "Отсутствуют данные формы уведомления."
                }
            });
        }

        string order = form["ORDER"].ToString()?.Trim() ?? "";
        string amountStr = form["AMOUNT"].ToString()?.Trim() ?? "";
        string currency = form["CURRENCY"].ToString()?.Trim() ?? "";
        string terminal = form["TERMINAL"].ToString()?.Trim() ?? "";
        string trType = form["TRTYPE"].ToString()?.Trim() ?? "";
        string action = form["ACTION"].ToString()?.Trim() ?? "";
        string rc = form["RC"].ToString()?.Trim() ?? "";
        string rrn = form["RRN"].ToString()?.Trim() ?? "";
        string intRef = form["INT_REF"].ToString()?.Trim() ?? "";
        string approval = form["APPROVAL"].ToString()?.Trim() ?? "";
        string madvCode = form["MADV_CODE"].ToString()?.Trim() ?? "";
        string bankMessage = form["TEXT"].ToString()?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(bankMessage))
        {
            bankMessage = form["BANK_MESSAGE"].ToString()?.Trim() ?? "";
        }

        // 3. Поиск платежа по полю ORDER
        if (string.IsNullOrWhiteSpace(order))
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "order_required",
                    message = "В уведомлении отсутствует поле ORDER."
                }
            });
        }

        var payment = _paymentRepository.GetByOrderId(order);
        if (payment == null)
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "order_not_found",
                    message = "Платёж с указанным ORDER не найден."
                }
            });
        }

        // 4. Проверка обязательных полей ACTION и RC:
        // Неполное или некорректное уведомление отклоняется без изменения статуса платежа.
        if (string.IsNullOrWhiteSpace(action))
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "missing_action",
                    message = "В уведомлении отсутствует обязательное поле ACTION."
                }
            });
        }

        if (string.IsNullOrWhiteSpace(rc))
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "missing_rc",
                    message = "В уведомлении отсутствует обязательное поле RC."
                }
            });
        }

        // 5. Проверка терминала
        string expectedTerminal = !string.IsNullOrWhiteSpace(payment.TerminalId) ? payment.TerminalId : _options.TerminalId;
        if (string.IsNullOrWhiteSpace(terminal) || !string.Equals(terminal, expectedTerminal, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "terminal_mismatch",
                    message = "Терминал в уведомлении не совпадает с данными платежа."
                }
            });
        }

        // 6. Проверка суммы (точное сравнение decimal без округления)
        if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount))
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "invalid_amount",
                    message = "Некорректный формат суммы AMOUNT."
                }
            });
        }

        if (parsedAmount != payment.AmountKzt)
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "amount_mismatch",
                    message = "Сумма операции не совпадает с суммой заказа."
                }
            });
        }

        // 7. Проверка валюты (398 - код KZT в BCC согласно ISO 4217, либо буквенный код KZT)
        bool currencyMatches = string.Equals(currency, payment.Currency, StringComparison.OrdinalIgnoreCase)
            || (string.Equals(payment.Currency, "KZT", StringComparison.OrdinalIgnoreCase) && currency == "398");
        if (!currencyMatches)
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "currency_mismatch",
                    message = "Валюта операции не совпадает с валютой заказа."
                }
            });
        }

        // 8. Проверка типа операции TRTYPE (TRTYPE=1: Покупка)
        if (trType != "1")
        {
            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "invalid_trtype",
                    message = "Неподдерживаемый тип операции TRTYPE."
                }
            });
        }

        // 9. Определение статуса по спецификации BCC Way4:
        // Успех: ACTION == "0" и код ответа RC равен "00" или "0".
        // Любой иной ответ банка считается отклонением/ошибкой операции.
        bool isSuccess = action == "0" && (rc == "00" || rc == "0");
        string targetStatus = isSuccess ? PaymentStatuses.Paid : PaymentStatuses.Failed;
        string notificationReceivedAt = DateTime.UtcNow.ToString("o");

        // 10. Атомарное обновление через PaymentRepository.UpdateStatus:
        // Атомарно обновляет payment, session и lead.
        // Защищает от понижения статуса paid/refunded при запоздалом отказе.
        // При повторном успешном уведомлении (paid -> paid) дата paid_at не перезаписывается.
        _paymentRepository.UpdateStatus(
            orderId: payment.OrderId,
            status: targetStatus,
            rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
            intRef: string.IsNullOrWhiteSpace(intRef) ? null : intRef,
            approvalCode: string.IsNullOrWhiteSpace(approval) ? null : approval,
            actionCode: string.IsNullOrWhiteSpace(action) ? null : action,
            responseCode: string.IsNullOrWhiteSpace(rc) ? null : rc,
            merchantAdviceCode: string.IsNullOrWhiteSpace(madvCode) ? null : madvCode,
            bankMessage: string.IsNullOrWhiteSpace(bankMessage) ? null : bankMessage,
            notificationReceivedAt: notificationReceivedAt
        );

        // 11. Перечитываем платёж из БД для возврата фактического статуса:
        // Запоздалое уведомление не должно возвращать paid, если запись фактически осталась refunded.
        var actualPayment = _paymentRepository.GetByOrderId(payment.OrderId) ?? payment;

        return Task.FromResult(new BccNotificationResult
        {
            StatusCode = StatusCodes.Status200OK,
            Value = new
            {
                status = "ok",
                orderId = payment.OrderId,
                paymentStatus = actualPayment.Status
            }
        });
    }

    private bool ValidateBasicAuth(string? authHeader)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string base64 = authHeader.Substring("Basic ".Length).Trim();
        string decoded;
        try
        {
            byte[] bytes = Convert.FromBase64String(base64);
            decoded = Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return false;
        }

        int colon = decoded.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        string username = decoded.Substring(0, colon);
        string password = decoded.Substring(colon + 1);

        byte[] userBytes = Encoding.UTF8.GetBytes(username);
        byte[] expectedUserBytes = Encoding.UTF8.GetBytes(_options.NotifyUsername);
        byte[] passBytes = Encoding.UTF8.GetBytes(password);
        byte[] expectedPassBytes = Encoding.UTF8.GetBytes(_options.NotifyPassword);

        bool userMatch = CryptographicOperations.FixedTimeEquals(userBytes, expectedUserBytes);
        bool passMatch = CryptographicOperations.FixedTimeEquals(passBytes, expectedPassBytes);

        return userMatch && passMatch;
    }
}
