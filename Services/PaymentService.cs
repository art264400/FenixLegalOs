using System;
using System.Collections.Generic;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FenixLegalOs.Services;

/// <summary>
/// Бизнес-логика платежей и проверка финансовых состояний сессий.
/// Полностью отвязана от конкретного банка благодаря абстракции IPaymentGateway.
/// </summary>
public sealed class PaymentService
{
    private readonly SessionRepository _sessions;
    private readonly SettingsRepository _settings;
    private readonly PaymentRepository _payments;
    private readonly IPaymentGateway _gateway;
    private readonly UserRepository _users;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        SessionRepository sessions,
        SettingsRepository settings,
        PaymentRepository payments,
        IPaymentGateway gateway,
        UserRepository users,
        ILogger<PaymentService>? logger = null)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _logger = logger ?? NullLogger<PaymentService>.Instance;
    }

    /// <summary>
    /// Инициализация платежа с проверкой прав доступа, тарифа, телефона пользователя и параметров экрана.
    /// Порядок проверок:
    /// 1. Найти диагностическую сессию.
    /// 2. Проверить завершение диагностики.
    /// 3. Проверить paid/refunded.
    /// 4. Проверить тариф.
    /// 5. Проверить существующую активную попытку.
    /// 6. Получить пользователя через session.UserId.
    /// 7. Проверить users.phone.
    /// 8. Проверить адрес плательщика.
    /// 9. Проверить размеры экрана.
    /// 10. Определить сумму на сервере.
    /// 11. Вызвать IPaymentGateway.CreatePaymentAsync.
    /// </summary>
    public async System.Threading.Tasks.Task<PaymentServiceResult> StartAsync(
        string sessionId,
        string? requestedTariff,
        int browserScreenHeight,
        int browserScreenWidth,
        string? clientIp,
        string? billingAddress = null,
        DiagnosticSession? cachedSession = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SessionId"] = sessionId
        });

        _logger.LogInformation(
            PaymentEvents.PaymentStartRequested,
            "Поступил запрос на запуск оплаты: сессия {SessionId}, тариф {Tariff}, провайдер {Provider}, окружение {Environment}",
            sessionId,
            requestedTariff,
            _gateway.Provider,
            _gateway.Environment);

        // 1. Найти диагностическую сессию
        var session = cachedSession ?? _sessions.GetSession(sessionId);
        if (session == null)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Сессия не найдена: {SessionId}",
                sessionId);
            return PaymentServiceResult.NotFound("session_not_found");
        }

        // 2. Проверить завершение диагностики
        if (string.IsNullOrWhiteSpace(session.CompletedAt) || string.IsNullOrWhiteSpace(session.ResultJson))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Диагностика ещё не завершена для сессии {SessionId}",
                sessionId);
            return PaymentServiceResult.Conflict(new
            {
                error = "diagnostic_not_completed",
                message = "Оплата доступна только после завершения диагностики."
            });
        }

        // 3. Проверить paid/refunded
        var paidPayment = _payments.GetSuccessfulPaymentBySessionId(sessionId);
        var refundedPayment = _payments.GetLatestRefundedPaymentBySessionId(sessionId);

        if (paidPayment != null || session.Paid)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Сессия {SessionId} уже оплачена",
                sessionId);
            return PaymentServiceResult.Conflict(new
            {
                error = "already_paid",
                message = "Полный отчёт по этой диагностике уже оплачен."
            });
        }

        if (refundedPayment != null)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "По сессии {SessionId} ранее был возврат средств",
                sessionId);
            return PaymentServiceResult.Conflict(new
            {
                error = "payment_refunded",
                message = "Оплата по этой диагностике была возвращена. Повторная оплата этой сессии невозможна, пройдите новую диагностику."
            });
        }

        // 4. Проверить тариф
        string tariff = requestedTariff?.Trim().ToLowerInvariant() ?? "";
        if (tariff is not ("report" or "consultation"))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Передан неизвестный тариф '{Tariff}' для сессии {SessionId}",
                requestedTariff,
                sessionId);
            return PaymentServiceResult.BadRequest("invalid_tariff", "Неизвестный тариф оплаты.");
        }

        // 5. Проверить существующую активную попытку
        var latestPayment = _payments.GetLatestBySessionId(sessionId);
        bool hasActivePayment = latestPayment != null && (latestPayment.Status == PaymentStatuses.Created || latestPayment.Status == PaymentStatuses.Pending);
        if (hasActivePayment)
        {
            if (!string.Equals(latestPayment!.Tariff, tariff, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    PaymentEvents.PaymentRejected,
                    "Активная попытка оплаты уже существует для другого тарифа '{ActiveTariff}', запрошен '{RequestedTariff}', OrderId {OrderId}",
                    latestPayment.Tariff,
                    tariff,
                    latestPayment.OrderId);
                return PaymentServiceResult.Conflict(new
                {
                    error = "payment_in_progress",
                    message = $"Уже существует активная попытка оплаты по тарифу '{latestPayment.Tariff}'. Завершите её или дождитесь истечения срока действия.",
                    activeOrderId = latestPayment.OrderId,
                    activeTariff = latestPayment.Tariff,
                    requestedTariff = tariff
                });
            }
        }

        // 6. Получить пользователя через session.UserId
        UserAccount? user = null;
        if (!string.IsNullOrWhiteSpace(session.UserId))
        {
            user = _users.GetUserById(session.UserId);
        }

        // 7. Проверить users.phone (не логировать номер телефона!)
        if (user == null || string.IsNullOrWhiteSpace(user.Phone) || !Infrastructure.PhoneHelper.TryNormalizePhone(user.Phone, out string normalizedPhone))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Телефон пользователя отсутствует или некорректен для сессии {SessionId}",
                sessionId);
            return PaymentServiceResult.BadRequest("phone_required", "Для оплаты необходимо указать корректный номер телефона.");
        }

        // 8. Проверить адрес плательщика (не логировать адрес!)
        string trimmedBillingAddress = billingAddress?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedBillingAddress))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Адрес плательщика отсутствует для сессии {SessionId}",
                sessionId);
            return PaymentServiceResult.BadRequest("billing_address_required", "Для перехода к оплате необходимо указать адрес плательщика.");
        }
        if (trimmedBillingAddress.Length > 50)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Адрес плательщика слишком длинный (длина {AddressLength} > 50) для сессии {SessionId}",
                trimmedBillingAddress.Length,
                sessionId);
            return PaymentServiceResult.BadRequest("billing_address_too_long", "Адрес плательщика не должен превышать 50 символов.");
        }

        // 9. Проверить размеры экрана (от 1 до 999999)
        if (browserScreenHeight < 1 || browserScreenHeight > 999999 ||
            browserScreenWidth < 1 || browserScreenWidth > 999999)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Размеры экрана некорректны ({ScreenWidth}x{ScreenHeight}) для сессии {SessionId}",
                browserScreenWidth,
                browserScreenHeight,
                sessionId);
            return PaymentServiceResult.BadRequest("invalid_browser_dimensions", "Некорректные параметры экрана браузера.");
        }

        // 10. Определить сумму на сервере
        int amountKzt;
        if (hasActivePayment)
        {
            amountKzt = latestPayment!.AmountKzt;
        }
        else
        {
            var pricing = _settings.GetPricing();
            amountKzt = tariff == "consultation"
                ? pricing.ConsultationPriceKzt
                : pricing.PriceKzt;
        }

        // 11. Вызвать IPaymentGateway.CreatePaymentAsync
        var initRequest = new PaymentGatewayInitRequest
        {
            SessionId = sessionId,
            Tariff = tariff,
            AmountKzt = amountKzt,
            Currency = "KZT",
            OrderId = hasActivePayment ? latestPayment!.OrderId : null,
            ClientIp = clientIp,
            Phone = normalizedPhone,
            BillingAddress = trimmedBillingAddress,
            BrowserScreenHeight = browserScreenHeight,
            BrowserScreenWidth = browserScreenWidth
        };

        var initResult = await _gateway.CreatePaymentAsync(initRequest, cancellationToken);
        if (!initResult.Success)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Шлюз {_GatewayProvider} вернул ожидаемую ошибку {ErrorCode} для сессии {SessionId}",
                _gateway.Provider,
                initResult.ErrorCode,
                sessionId);

            if (initResult.ErrorCode == "payment_gateway_not_configured")
            {
                return PaymentServiceResult.ServiceUnavailable(new
                {
                    error = "payment_gateway_not_configured",
                    message = $"Платёжный шлюз {_gateway.Provider.ToUpperInvariant()} ещё не настроен.",
                    provider = _gateway.Provider,
                    environment = _gateway.Environment,
                    sessionId,
                    tariff,
                    amountKzt,
                    currency = "KZT"
                });
            }

            return PaymentServiceResult.BadRequest(
                initResult.ErrorCode ?? "payment_failed",
                initResult.ErrorMessage ?? "Не удалось создать платёж в платёжном шлюзе.");
        }

        _logger.LogInformation(
            PaymentEvents.PaymentStartRequested,
            "Шлюз {_GatewayProvider} успешно сформировал параметры оплаты для заказа {OrderId}, сессия {SessionId}",
            _gateway.Provider,
            initResult.OrderId,
            sessionId);

        // Формируем метаданные шлюза (MERCH_RN_ID должен быть идентичен возвращённому в formFields)
        string? providerMetadataJson = null;
        if (!string.IsNullOrWhiteSpace(initResult.MerchRnId))
        {
            providerMetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                merch_rn_id = initResult.MerchRnId
            });
        }

        if (hasActivePayment)
        {
            _logger.LogInformation(
                PaymentEvents.PaymentActiveAttemptReopened,
                "Повторно открывается существующая активная попытка {OrderId} для сессии {SessionId}",
                latestPayment!.OrderId,
                sessionId);

            // Атомарно обновляем параметры активной попытки в БД. Разрешено только для статусов created и pending.
            bool updated = _payments.UpdateAttempt(
                latestPayment!.OrderId,
                initResult.Nonce ?? "",
                initResult.RequestTimestamp ?? "",
                providerMetadataJson,
                initResult.MerchRnId);

            if (!updated)
            {
                // Запись уже изменила статус параллельно (например, стала paid, refunded или failed)
                var current = _payments.GetByOrderId(latestPayment.OrderId);
                _logger.LogWarning(
                    PaymentEvents.PaymentRejected,
                    "Произошёл конфликт обновления попытки оплаты {OrderId}, текущий статус {PaymentStatus}, сессия {SessionId}",
                    latestPayment.OrderId,
                    current?.Status,
                    sessionId);

                if (current != null && current.Status == PaymentStatuses.Paid)
                {
                    return PaymentServiceResult.Conflict(new
                    {
                        error = "already_paid",
                        message = "Диагностика уже оплачена. Повторная оплата не требуется.",
                        orderId = current.OrderId,
                        isPaid = true
                    });
                }
                if (current != null && current.Status == PaymentStatuses.Refunded)
                {
                    return PaymentServiceResult.Conflict(new
                    {
                        error = "payment_refunded",
                        message = "По данной сессии уже был выполнен возврат средств. Создание новых платежей запрещено.",
                        refundedOrderId = current.OrderId
                    });
                }

                return PaymentServiceResult.Conflict(new
                {
                    error = "payment_conflict",
                    message = $"Состояние платежа изменилось (текущий статус: '{current?.Status ?? "неизвестен"}'). Повторное открытие формы невозможно.",
                    activeOrderId = latestPayment.OrderId,
                    status = current?.Status
                });
            }

            _logger.LogInformation(
                PaymentEvents.PaymentStartCompleted,
                "Запуск оплаты успешно завершён для существующего заказа {OrderId}, сессия {SessionId}",
                latestPayment.OrderId,
                sessionId);

            return PaymentServiceResult.Ok(new
            {
                orderId = initResult.OrderId,
                actionUrl = initResult.ActionUrl,
                method = initResult.Method,
                checkoutType = initResult.CheckoutType,
                formFields = initResult.FormFields,
                tariff = latestPayment.Tariff,
                amountKzt = latestPayment.AmountKzt,
                provider = latestPayment.Provider,
                payment = new ActivePaymentDto
                {
                    OrderId = latestPayment.OrderId,
                    Tariff = latestPayment.Tariff,
                    AmountKzt = latestPayment.AmountKzt,
                    Currency = latestPayment.Currency,
                    Status = latestPayment.Status,
                    Provider = latestPayment.Provider,
                    Environment = latestPayment.Environment
                },
                message = "Используется существующая активная попытка оплаты."
            });
        }

        // Сохранить новую попытку в таблице payments
        var paymentRecord = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = initResult.OrderId ?? "",
            Tariff = tariff,
            AmountKzt = amountKzt,
            Currency = "KZT",
            Provider = _gateway.Provider,
            Environment = _gateway.Environment,
            TerminalId = initResult.TerminalId,
            Status = PaymentStatuses.Created,
            Nonce = initResult.Nonce,
            RequestTimestamp = initResult.RequestTimestamp,
            MerchRnId = initResult.MerchRnId,
            ProviderMetadata = providerMetadataJson,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };

        try
        {
            _payments.Create(paymentRecord);
            _logger.LogInformation(
                PaymentEvents.PaymentCreated,
                "Новая запись создана в payments: заказ {OrderId}, тариф {Tariff}, сумма {AmountKzt} {Currency}, провайдер {Provider}, окружение {Environment}",
                paymentRecord.OrderId,
                paymentRecord.Tariff,
                paymentRecord.AmountKzt,
                paymentRecord.Currency,
                paymentRecord.Provider,
                paymentRecord.Environment);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            // Перехват конфликта параллельного создания активного платежа (частичный уникальный индекс uq_payments_active_session)
            _logger.LogWarning(
                PaymentEvents.PaymentRejected,
                "Произошёл конфликт параллельного создания платежа для сессии {SessionId}",
                sessionId);

            var existing = _payments.GetLatestBySessionId(sessionId);
            return PaymentServiceResult.Conflict(new
            {
                error = "payment_in_progress",
                message = "Уже существует активная попытка оплаты для данной сессии.",
                activeOrderId = existing?.OrderId,
                activeTariff = existing?.Tariff,
                requestedTariff = tariff
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.PaymentError,
                "Ошибка при создании платежа {OrderId} для сессии {SessionId}. Тип ошибки: {ErrorType}",
                paymentRecord.OrderId,
                sessionId,
                ex.GetType().Name);
            throw;
        }

        _logger.LogInformation(
            PaymentEvents.PaymentStartCompleted,
            "Запуск оплаты успешно завершён для нового заказа {OrderId}, сессия {SessionId}",
            initResult.OrderId,
            sessionId);

        return PaymentServiceResult.Ok(new
        {
            orderId = initResult.OrderId,
            actionUrl = initResult.ActionUrl,
            method = initResult.Method,
            checkoutType = initResult.CheckoutType,
            formFields = initResult.FormFields,
            tariff,
            amountKzt,
            provider = _gateway.Provider
        });
    }

    public PaymentServiceResult GetStatus(string sessionId, DiagnosticSession? cachedSession = null)
    {
        var session = cachedSession ?? _sessions.GetSession(sessionId);
        if (session == null)
            return PaymentServiceResult.NotFound("session_not_found");

        // Выбор правильной записи: paid / refunded / latest
        var paidPayment = _payments.GetSuccessfulPaymentBySessionId(sessionId);
        var refundedPayment = _payments.GetLatestRefundedPaymentBySessionId(sessionId);
        var latestPayment = _payments.GetLatestBySessionId(sessionId);

        Payment? activePayment;
        bool isPaid;
        string status;

        if (paidPayment != null)
        {
            // paid имеет безусловный приоритет
            activePayment = paidPayment;
            isPaid = true;
            status = PaymentStatuses.Paid;
        }
        else if (refundedPayment != null)
        {
            // refunded
            activePayment = refundedPayment;
            isPaid = false;
            status = PaymentStatuses.Refunded;
        }
        else if (session.Paid)
        {
            // Старая оплаченная сессия (legacy) без записи в payments
            activePayment = latestPayment;
            isPaid = true;
            status = PaymentStatuses.Paid;
        }
        else
        {
            // latestPayment или not_started
            activePayment = latestPayment;
            isPaid = false;
            status = activePayment?.Status ?? "not_started";
        }

        var payingPayment = isPaid ? (paidPayment ?? activePayment) : null;
        string? resolvedTariff = activePayment?.Tariff;

        return PaymentServiceResult.Ok(new
        {
            provider = (isPaid ? payingPayment?.Provider : activePayment?.Provider) ?? session.PaymentMethod ?? _gateway.Provider,
            environment = (isPaid ? payingPayment?.Environment : activePayment?.Environment) ?? _gateway.Environment,
            sessionId,
            orderId = isPaid ? payingPayment?.OrderId : activePayment?.OrderId,
            paid = isPaid,
            status,
            paidAt = isPaid ? (payingPayment?.PaidAt ?? session.PaidAt) : null,
            amountKzt = isPaid ? (payingPayment?.AmountKzt ?? session.PaymentAmount) : activePayment?.AmountKzt,
            tariff = resolvedTariff,
            method = isPaid ? (payingPayment?.Provider ?? session.PaymentMethod ?? _gateway.Provider) : activePayment?.Provider
        });
    }
}
