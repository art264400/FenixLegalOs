using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
    private readonly PaymentRefundRepository? _refundRepository;
    private readonly BccPaymentOptions _options;
    private readonly ILogger<BccNotificationService> _logger;

    public BccNotificationService(
        PaymentRepository paymentRepository,
        IConfiguration? configuration = null,
        BccPaymentOptions? options = null,
        ILogger<BccNotificationService>? logger = null,
        PaymentRefundRepository? refundRepository = null)
    {
        _paymentRepository = paymentRepository;
        _refundRepository = refundRepository;
        _logger = logger ?? NullLogger<BccNotificationService>.Instance;
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

    public Task<BccNotificationResult> ProcessNotificationAsync(string? authHeader, IFormCollection? form, string? traceIdentifier = null)
    {
        bool authHeaderPresent = !string.IsNullOrWhiteSpace(authHeader);

        _logger.LogInformation(
            PaymentEvents.BccCallbackReceived,
            "Callback BCC получен. AuthorizationPresent: {AuthorizationPresent}, Environment: {Environment}, TraceIdentifier: {TraceIdentifier}",
            authHeaderPresent,
            _options.Environment,
            traceIdentifier);

        // 1. Проверка авторизации:
        // - production: Basic Auth всегда обязателен, неавторизованные уведомления строго запрещены;
        // - test + AllowUnauthenticatedTestNotifications=true: разрешить уведомление без Authorization;
        // - test + флаг false: требовать настроенные логин/пароль либо возвращать 503;
        // - никогда не разрешать callback без авторизации в production.
        // Логин, пароль, заголовок Authorization и MAC-ключ ни при каких условиях не логируются.
        bool isTest = string.Equals(_options.Environment, "test", StringComparison.OrdinalIgnoreCase);
        bool allowUnauthenticated = isTest && _options.AllowUnauthenticatedTestNotifications;

        if (allowUnauthenticated && string.IsNullOrWhiteSpace(authHeader))
        {
            // В тестовой среде с разрешённым флагом уведомление принимается без заголовка Authorization
            _logger.LogWarning(
                PaymentEvents.BccUnauthenticatedTestCallbackAccepted,
                "В тестовом окружении принимается callback без авторизации (AllowUnauthenticatedTestNotifications=true)");
        }
        else
        {
            // Требуется настроенный HTTP Basic Auth
            if (string.IsNullOrWhiteSpace(_options.NotifyUsername) || string.IsNullOrWhiteSpace(_options.NotifyPassword))
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Basic Auth не настроен для приёма уведомлений BCC в окружении {Environment}",
                    _options.Environment);

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
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Authorization отсутствует или неверен при обработке callback BCC");

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
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "Отсутствуют данные формы в callback BCC");

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
        string tranTrType = form["TRAN_TRTYPE"].ToString()?.Trim() ?? "";
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
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "В callback BCC отсутствует обязательное поле ORDER");

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

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OrderId"] = order
        });

        Payment? payment;
        try
        {
            payment = _paymentRepository.GetByOrderId(order);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccCallbackError,
                ex,
                "Неожиданное исключение поиска платежа {OrderId} при обработке callback",
                order);
            throw;
        }

        if (payment == null)
        {
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "Платёж с ORDER {OrderId} не найден в БД",
                order);

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

        _logger.LogInformation(
            PaymentEvents.BccCallbackReceived,
            "Платёж найден для callback: заказ {OrderId}, текущий статус {CurrentStatus}",
            payment.OrderId,
            payment.Status);

        // Проверка соответствия провайдера
        if (!string.Equals(payment.Provider, "bcc", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "Провайдер платежа {OrderId} '{PaymentProvider}' не соответствует BCC",
                payment.OrderId,
                payment.Provider);

            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "provider_mismatch",
                    message = "Провайдер платежа не соответствует BCC."
                }
            });
        }

        // Проверка соответствия окружения
        if (!string.Equals(payment.Environment, _options.Environment, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "Окружение платежа {OrderId} '{PaymentEnvironment}' не соответствует текущему окружению шлюза '{CurrentEnvironment}'",
                payment.OrderId,
                payment.Environment,
                _options.Environment);

            return Task.FromResult(new BccNotificationResult
            {
                StatusCode = StatusCodes.Status400BadRequest,
                Value = new
                {
                    error = "environment_mismatch",
                    message = "Окружение платежа не соответствует текущему окружению шлюза."
                }
            });
        }

        // 4. Проверка обязательных полей ACTION и RC:
        // Для TRTYPE=1 и TRTYPE=14 поля ACTION и RC обязательны.
        // Для TRTYPE=90 неполные ACTION/RC обрабатываются в секции TRTYPE=90 (остаются pending).
        if (trType != "90")
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "В callback для заказа {OrderId} отсутствует обязательное поле ACTION",
                    payment.OrderId);

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
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "В callback для заказа {OrderId} отсутствует обязательное поле RC",
                    payment.OrderId);

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
        }

        // 5. Проверка терминала
        string expectedTerminal = !string.IsNullOrWhiteSpace(payment.TerminalId) ? payment.TerminalId : _options.TerminalId;
        if (string.IsNullOrWhiteSpace(terminal) || !string.Equals(terminal, expectedTerminal, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "Терминал {ReceivedTerminal} в callback не совпадает с ожидаемым {ExpectedTerminal} для заказа {OrderId}",
                terminal,
                expectedTerminal,
                payment.OrderId);

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
        if (trType != "90")
        {
            if (!decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedAmount))
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "AMOUNT имеет некорректный формат '{AmountStr}' для заказа {OrderId}",
                    amountStr,
                    payment.OrderId);

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
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Сумма операции {ReceivedAmount} не совпадает с суммой заказа {ExpectedAmount} для заказа {OrderId}",
                    parsedAmount,
                    payment.AmountKzt,
                    payment.OrderId);

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
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Валюта {ReceivedCurrency} не совпадает с валютой заказа {ExpectedCurrency} для заказа {OrderId}",
                    currency,
                    payment.Currency,
                    payment.OrderId);

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
        }

        // 8. Проверка типа операции TRTYPE (TRTYPE=1: Покупка, TRTYPE=14: Возврат, TRTYPE=90: Сверка статуса)
        if (trType != "1" && trType != "14" && trType != "90")
        {
            _logger.LogWarning(
                PaymentEvents.BccCallbackRejected,
                "TRTYPE '{TrType}' не поддерживается для заказа {OrderId}",
                trType,
                payment.OrderId);

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

        _logger.LogInformation(
            PaymentEvents.BccCallbackReceived,
            "Банковский результат принят: заказ {OrderId}, TRTYPE {TrType}, TRAN_TRTYPE {TranTrType}, ACTION {Action}, RC {ResponseCode}, MADV_CODE {MadvCode}, RRN {Rrn}, INT_REF {IntRef}",
            payment.OrderId,
            trType,
            tranTrType,
            action,
            rc,
            madvCode,
            rrn,
            intRef);

        // 9. Определение статуса по спецификации BCC Way4:
        // Успех: ACTION == "0" и код ответа RC равен "00" или "0".
        // Любой иной ответ банка считается отклонением/ошибкой операции.
        bool isSuccess = action == "0" && (rc == "00" || rc == "0");
        string notificationReceivedAt = DateTime.UtcNow.ToString("o");

        bool updated = false;
        string targetStatus;

        if (trType == "90")
        {
            // Требование: обрабатывать возврат только при TRTYPE=90 и TRAN_TRTYPE=14;
            // другие TRAN_TRTYPE не изменяют возврат.
            if (tranTrType != "14")
            {
                _logger.LogInformation(
                    PaymentEvents.BccCallbackCompleted,
                    "Callback TRTYPE=90 для заказа {OrderId} имеет TRAN_TRTYPE='{TranTrType}' (не 14). Возврат не изменяется.",
                    payment.OrderId,
                    tranTrType);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    Value = new
                    {
                        status = "ok",
                        orderId = payment.OrderId,
                        paymentStatus = payment.Status,
                        message = "Уведомление TRTYPE=90 не относится к возврату (TRAN_TRTYPE != 14). Статус не изменён."
                    }
                });
            }

            // TRAN_TRTYPE == "14": проверяем локальную запись возврата
            var existingRefund = _refundRepository?.GetLatestByOrderId(payment.OrderId);
            if (existingRefund == null)
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Получен callback возврата TRTYPE=90 (TRAN_TRTYPE=14) для заказа {OrderId}, но локальная запись возврата в payment_refunds не найдена. Операция отклонена без изменения платежа.",
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Value = new
                    {
                        error = "refund_record_not_found",
                        message = "Локальная операция возврата не найдена для данного заказа."
                    }
                });
            }

            // Проверяем соответствие параметров возврата и платежа
            if (!string.Equals(existingRefund.OrderId, payment.OrderId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existingRefund.PaymentId, payment.Id, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Параметры локального возврата {RefundId} не соответствуют платежу {PaymentId} для заказа {OrderId}.",
                    existingRefund.Id,
                    payment.Id,
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Value = new
                    {
                        error = "refund_parameters_mismatch",
                        message = "Параметры локального возврата не совпадают с платежом."
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(amountStr) &&
                decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRefAmt) &&
                parsedRefAmt != existingRefund.AmountKzt)
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Сумма {ReceivedAmount} в callback TRTYPE=90 не совпадает с суммой возврата {ExpectedAmount} для заказа {OrderId}",
                    parsedRefAmt,
                    existingRefund.AmountKzt,
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    Value = new
                    {
                        error = "amount_mismatch",
                        message = "Сумма в уведомлении не совпадает с суммой возврата."
                    }
                });
            }

            // Неполный или неоднозначный ответ банка — оставить pending
            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(rc))
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackReceived,
                    "Callback TRTYPE=90 для заказа {OrderId} не содержит полных кодов ACTION/RC. Возврат оставлен pending.",
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    Value = new
                    {
                        status = "pending",
                        orderId = payment.OrderId,
                        refundId = existingRefund.Id,
                        paymentStatus = payment.Status,
                        message = "Ответ банка на проверку статуса неполный или неоднозначный. Статус возврата оставлен pending."
                    }
                });
            }

            // ACTION=0 и RC=00/0 — атомарно завершить возврат
            bool isRefundSuccess = action == "0" && (rc == "00" || rc == "0");

            if (isRefundSuccess)
            {
                // Идемпотентность: если уже succeeded, не повторяем транзакцию, возвращаем успех
                if (existingRefund.Status == PaymentRefundStatuses.Succeeded)
                {
                    _logger.LogInformation(
                        PaymentEvents.PaymentRefundDuplicateCallback,
                        "Повторный callback TRTYPE=90 для заказа {OrderId}: возврат {RefundId} уже находится в статусе succeeded (идемпотентно).",
                        payment.OrderId,
                        existingRefund.Id);

                    return Task.FromResult(new BccNotificationResult
                    {
                        StatusCode = StatusCodes.Status200OK,
                        Value = new
                        {
                            status = "ok",
                            orderId = payment.OrderId,
                            refundId = existingRefund.Id,
                            paymentStatus = PaymentStatuses.Refunded
                        }
                    });
                }

                var completeResult = _refundRepository != null
                    ? _refundRepository.CompleteRefundTransaction(
                        refundId: existingRefund.Id,
                        orderId: payment.OrderId,
                        actionCode: action,
                        responseCode: rc,
                        rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
                        intRef: string.IsNullOrWhiteSpace(intRef) ? null : intRef,
                        bankMessage: string.IsNullOrWhiteSpace(bankMessage) ? null : bankMessage,
                        completedAt: notificationReceivedAt)
                    : CompleteRefundTransactionResult.Fail("no_repository", "Репозиторий возвратов не инициализирован.");

                if (!completeResult.Success)
                {
                    _logger.LogError(
                        PaymentEvents.PaymentRefundFailed,
                        "Не удалось завершить возврат для заказа {OrderId} по callback TRTYPE=90: {ErrorCode} - {ErrorMessage}",
                        payment.OrderId,
                        completeResult.ErrorCode,
                        completeResult.ErrorMessage);

                    return Task.FromResult(new BccNotificationResult
                    {
                        StatusCode = StatusCodes.Status500InternalServerError,
                        Value = new
                        {
                            error = "refund_completion_failed",
                            message = completeResult.ErrorMessage ?? "Не удалось завершить операцию возврата в базе данных."
                        }
                    });
                }

                _logger.LogInformation(
                    PaymentEvents.PaymentRefundSucceeded,
                    "Возврат {RefundId} для заказа {OrderId} успешно завершён по callback TRTYPE=90 (TRAN_TRTYPE=14)",
                    existingRefund.Id,
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    Value = new
                    {
                        status = "ok",
                        orderId = payment.OrderId,
                        refundId = existingRefund.Id,
                        paymentStatus = PaymentStatuses.Refunded
                    }
                });
            }
            else
            {
                // Однозначный отказ банка: перевести pending в failed.
                // Идемпотентность: не понижать succeeded в failed при запоздалом/повторном отказе.
                if (existingRefund.Status == PaymentRefundStatuses.Succeeded)
                {
                    _logger.LogWarning(
                        PaymentEvents.PaymentRefundDuplicateCallback,
                        "Получен отказной callback TRTYPE=90 для заказа {OrderId}, но локальный возврат {RefundId} уже завершён со статусом succeeded. Статус не понижается.",
                        payment.OrderId,
                        existingRefund.Id);
                }
                else if (existingRefund.Status == PaymentRefundStatuses.Pending)
                {
                    _refundRepository?.UpdateStatus(
                        refundId: existingRefund.Id,
                        status: PaymentRefundStatuses.Failed,
                        actionCode: action,
                        responseCode: rc,
                        rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
                        intRef: string.IsNullOrWhiteSpace(intRef) ? null : intRef,
                        bankMessage: string.IsNullOrWhiteSpace(bankMessage) ? null : bankMessage,
                        completedAt: notificationReceivedAt);

                    _logger.LogWarning(
                        PaymentEvents.PaymentRefundFailed,
                        "Возврат {RefundId} для заказа {OrderId} переведён в failed по callback TRTYPE=90 (ACTION {Action}, RC {ResponseCode})",
                        existingRefund.Id,
                        payment.OrderId,
                        action,
                        rc);
                }

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status200OK,
                    Value = new
                    {
                        status = "failed",
                        orderId = payment.OrderId,
                        refundId = existingRefund.Id,
                        paymentStatus = payment.Status,
                        actionCode = action,
                        responseCode = rc
                    }
                });
            }
        }
        else if (trType == "14")
        {
            // Обработка уведомления возврата TRTYPE=14
            var existingRefund = _refundRepository?.GetLatestByOrderId(payment.OrderId);

            // Требование 5: Успешный callback не должен переводить payment в refunded, если в payment_refunds
            // нет соответствующей локальной операции возврата.
            if (existingRefund == null)
            {
                _logger.LogWarning(
                    PaymentEvents.BccCallbackRejected,
                    "Получен callback возврата TRTYPE=14 для заказа {OrderId}, но локальная запись возврата в payment_refunds не найдена. Операция отклонена без изменения платежа.",
                    payment.OrderId);

                return Task.FromResult(new BccNotificationResult
                {
                    StatusCode = StatusCodes.Status409Conflict,
                    Value = new
                    {
                        error = "refund_record_not_found",
                        message = "Локальная операция возврата не найдена для данного заказа."
                    }
                });
            }

            if (isSuccess)
            {
                targetStatus = PaymentStatuses.Refunded;

                // Если возврат уже был отмечен как succeeded — идемпотентно подтверждаем
                if (existingRefund.Status == PaymentRefundStatuses.Succeeded)
                {
                    _logger.LogInformation(
                        PaymentEvents.PaymentRefundDuplicateCallback,
                        "Получена повторная успешная нотификация возврата TRTYPE=14 для заказа {OrderId}",
                        payment.OrderId);
                }
                else
                {
                    _logger.LogInformation(
                        PaymentEvents.PaymentRefundSucceeded,
                        "Возврат {RefundId} для заказа {OrderId} подтверждён по callback BCC",
                        existingRefund.Id,
                        payment.OrderId);
                }

                // Атомарно и идемпотентно завершаем возврат в БД (payment_refunds, payments, sessions, leads)
                CompleteRefundTransactionResult completeResult;
                try
                {
                    completeResult = _refundRepository != null
                        ? _refundRepository.CompleteRefundTransaction(
                            refundId: existingRefund.Id,
                            orderId: payment.OrderId,
                            actionCode: action,
                            responseCode: rc,
                            rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
                            intRef: string.IsNullOrWhiteSpace(intRef) ? null : intRef,
                            bankMessage: string.IsNullOrWhiteSpace(bankMessage) ? null : bankMessage,
                            completedAt: notificationReceivedAt)
                        : CompleteRefundTransactionResult.Fail("no_repository", "Репозиторий возвратов не инициализирован.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        PaymentEvents.BccCallbackError,
                        ex,
                        "Неожиданное исключение атомарного обновления статуса возврата {OrderId} в БД",
                        payment.OrderId);
                    throw;
                }

                updated = completeResult.Success;

                if (!completeResult.Success)
                {
                    _logger.LogError(
                        PaymentEvents.PaymentRefundFailed,
                        "Не удалось завершить возврат для заказа {OrderId} по callback BCC: {ErrorCode} - {ErrorMessage}",
                        payment.OrderId,
                        completeResult.ErrorCode,
                        completeResult.ErrorMessage);

                    return Task.FromResult(new BccNotificationResult
                    {
                        StatusCode = StatusCodes.Status500InternalServerError,
                        Value = new
                        {
                            error = "refund_completion_failed",
                            message = completeResult.ErrorMessage ?? "Не удалось завершить операцию возврата в базе данных."
                        }
                    });
                }
            }
            else
            {
                // Неуспешный возврат: платёж остаётся paid, а запись возврата переводится в failed
                // Требование: не позволять неуспешному callback понизить succeeded в failed
                targetStatus = payment.Status;

                if (existingRefund.Status == PaymentRefundStatuses.Succeeded)
                {
                    _logger.LogWarning(
                        PaymentEvents.PaymentRefundDuplicateCallback,
                        "Получен отказной callback для заказа {OrderId}, но локальный возврат {RefundId} уже завершён со статусом succeeded. Статус не понижается.",
                        payment.OrderId,
                        existingRefund.Id);
                }
                else if (existingRefund.Status == PaymentRefundStatuses.Pending)
                {
                    _refundRepository?.UpdateStatus(
                        refundId: existingRefund.Id,
                        status: PaymentRefundStatuses.Failed,
                        actionCode: action,
                        responseCode: rc,
                        rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
                        intRef: string.IsNullOrWhiteSpace(intRef) ? null : intRef,
                        bankMessage: string.IsNullOrWhiteSpace(bankMessage) ? null : bankMessage,
                        completedAt: notificationReceivedAt);

                    _logger.LogWarning(
                        PaymentEvents.PaymentRefundFailed,
                        "Возврат {RefundId} для заказа {OrderId} отклонён по callback BCC (ACTION {Action}, RC {ResponseCode})",
                        existingRefund.Id,
                        payment.OrderId,
                        action,
                        rc);
                }
            }
        }
        else
        {
            // Стандартная обработка покупки TRTYPE=1
            targetStatus = isSuccess ? PaymentStatuses.Paid : PaymentStatuses.Failed;

            _logger.LogInformation(
                PaymentEvents.PaymentStatusChanged,
                "Рассчитан целевой статус {TargetStatus} для заказа {OrderId}",
                targetStatus,
                payment.OrderId);

            try
            {
                updated = _paymentRepository.UpdateStatus(
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
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    PaymentEvents.BccCallbackError,
                    ex,
                    "Неожиданное исключение обновления статуса платежа {OrderId} в БД",
                    payment.OrderId);
                throw;
            }
        }

        if (updated)
        {
            _logger.LogInformation(
                PaymentEvents.PaymentStatusChanged,
                "Запись платежа {OrderId} успешно обновлена в БД на статус {TargetStatus}",
                payment.OrderId,
                targetStatus);
        }
        else
        {
            _logger.LogWarning(
                PaymentEvents.PaymentStatusChanged,
                "Переход статуса в {TargetStatus} для платежа {OrderId} был отклонён или не изменил запись в БД",
                targetStatus,
                payment.OrderId);
        }

        // 11. Перечитываем платёж из БД для возврата фактического статуса:
        // Запоздалое уведомление не должно возвращать paid, если запись фактически осталась refunded.
        var actualPayment = _paymentRepository.GetByOrderId(payment.OrderId) ?? payment;

        _logger.LogInformation(
            PaymentEvents.PaymentStatusChanged,
            "Фактический статус платежа {OrderId} после перечитывания из БД: {ActualStatus}",
            payment.OrderId,
            actualPayment.Status);

        if (!updated)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentStatusChanged,
                "Фактический статус платежа {OrderId} остался {ActualStatus}, целевой статус был {TargetStatus}",
                payment.OrderId,
                actualPayment.Status,
                targetStatus);
        }

        _logger.LogInformation(
            PaymentEvents.BccCallbackCompleted,
            "Callback BCC успешно обработан: заказ {OrderId}, итоговый статус {FinalStatus}",
            payment.OrderId,
            actualPayment.Status);

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
