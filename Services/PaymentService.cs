using System;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Repositories;

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

    public PaymentService(
        SessionRepository sessions,
        SettingsRepository settings,
        PaymentRepository payments,
        IPaymentGateway gateway,
        UserRepository users)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _payments = payments ?? throw new ArgumentNullException(nameof(payments));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _users = users ?? throw new ArgumentNullException(nameof(users));
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
    /// 8. Проверить размеры экрана.
    /// 9. Определить сумму на сервере.
    /// 10. Вызвать IPaymentGateway.CreatePaymentAsync.
    /// </summary>
    public async System.Threading.Tasks.Task<PaymentServiceResult> StartAsync(
        string sessionId,
        string? requestedTariff,
        int browserScreenHeight,
        int browserScreenWidth,
        string? clientIp,
        DiagnosticSession? cachedSession = null,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // 1. Найти диагностическую сессию
        var session = cachedSession ?? _sessions.GetSession(sessionId);
        if (session == null)
            return PaymentServiceResult.NotFound("session_not_found");

        // 2. Проверить завершение диагностики
        if (string.IsNullOrWhiteSpace(session.CompletedAt) || string.IsNullOrWhiteSpace(session.ResultJson))
        {
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
            return PaymentServiceResult.Conflict(new
            {
                error = "already_paid",
                message = "Полный отчёт по этой диагностике уже оплачен."
            });
        }

        if (refundedPayment != null)
        {
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
            return PaymentServiceResult.BadRequest("invalid_tariff", "Неизвестный тариф оплаты.");
        }

        // 5. Проверить существующую активную попытку
        var latestPayment = _payments.GetLatestBySessionId(sessionId);
        if (latestPayment != null && (latestPayment.Status == PaymentStatuses.Created || latestPayment.Status == PaymentStatuses.Pending))
        {
            if (!string.Equals(latestPayment.Tariff, tariff, StringComparison.OrdinalIgnoreCase))
            {
                return PaymentServiceResult.Conflict(new
                {
                    error = "payment_in_progress",
                    message = $"Уже существует активная попытка оплаты по тарифу '{latestPayment.Tariff}'. Завершите её или дождитесь истечения срока действия.",
                    activeOrderId = latestPayment.OrderId,
                    activeTariff = latestPayment.Tariff,
                    requestedTariff = tariff
                });
            }

            return PaymentServiceResult.Ok(new
            {
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

        // 6. Получить пользователя через session.UserId
        UserAccount? user = null;
        if (!string.IsNullOrWhiteSpace(session.UserId))
        {
            user = _users.GetUserById(session.UserId);
        }

        // 7. Проверить users.phone
        if (user == null || string.IsNullOrWhiteSpace(user.Phone) || !Infrastructure.PhoneHelper.TryNormalizePhone(user.Phone, out string normalizedPhone))
        {
            return PaymentServiceResult.BadRequest("phone_required", "Для оплаты необходимо указать корректный номер телефона.");
        }

        // 8. Проверить размеры экрана (от 1 до 999999)
        if (browserScreenHeight < 1 || browserScreenHeight > 999999 ||
            browserScreenWidth < 1 || browserScreenWidth > 999999)
        {
            return PaymentServiceResult.BadRequest("invalid_browser_dimensions", "Некорректные параметры экрана браузера.");
        }

        // 9. Определить сумму на сервере
        var pricing = _settings.GetPricing();
        int amountKzt = tariff == "consultation"
            ? pricing.ConsultationPriceKzt
            : pricing.PriceKzt;

        // 10. Вызвать IPaymentGateway.CreatePaymentAsync
        var initRequest = new PaymentGatewayInitRequest
        {
            SessionId = sessionId,
            Tariff = tariff,
            AmountKzt = amountKzt,
            Currency = "KZT",
            ClientIp = clientIp,
            Phone = normalizedPhone,
            BrowserScreenHeight = browserScreenHeight,
            BrowserScreenWidth = browserScreenWidth
        };

        var initResult = await _gateway.CreatePaymentAsync(initRequest, cancellationToken);
        if (!initResult.Success)
        {
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

        // Сохранить попытку в таблице payments
        string? providerMetadataJson = null;
        if (!string.IsNullOrWhiteSpace(initResult.MerchRnId))
        {
            providerMetadataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                merch_rn_id = initResult.MerchRnId
            });
        }

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
            ProviderMetadata = providerMetadataJson,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };

        _payments.Create(paymentRecord);

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
