using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FenixLegalOs.Services;

/// <summary>
/// Сервисный результат выполнения операции возврата.
/// </summary>
public sealed class PaymentRefundResult
{
    public int StatusCode { get; init; }
    public object Value { get; init; } = new();

    public static PaymentRefundResult Ok(object value) => new() { StatusCode = 200, Value = value };
    public static PaymentRefundResult Accepted(object value) => new() { StatusCode = 202, Value = value };
    public static PaymentRefundResult BadRequest(string error, string message) => new()
    {
        StatusCode = 400,
        Value = new { error, message }
    };
    public static PaymentRefundResult NotFound(string error, string message) => new()
    {
        StatusCode = 404,
        Value = new { error, message }
    };
    public static PaymentRefundResult Conflict(string error, string message) => new()
    {
        StatusCode = 409,
        Value = new { error, message }
    };
    public static PaymentRefundResult BadGateway(string error, string message, object? detail = null) => new()
    {
        StatusCode = 502,
        Value = new { error, message, detail }
    };
    public static PaymentRefundResult Error(int statusCode, string error, string message) => new()
    {
        StatusCode = statusCode,
        Value = new { error, message }
    };
}

/// <summary>
/// Провайдер-независимая бизнес-логика выполнения полного возврата средств.
/// Не знает о деталях реализации шлюзов (BCC, Kaspi и т.д.) и работает через IPaymentGateway.
/// </summary>
public sealed class PaymentRefundService
{
    private readonly PaymentRepository _paymentRepo;
    private readonly PaymentRefundRepository _refundRepo;
    private readonly IPaymentGateway _gateway;
    private readonly LeadRepository _leads;
    private readonly ILogger<PaymentRefundService> _logger;

    public PaymentRefundService(
        PaymentRepository paymentRepo,
        PaymentRefundRepository refundRepo,
        IPaymentGateway gateway,
        LeadRepository leads,
        ILogger<PaymentRefundService>? logger = null)
    {
        _paymentRepo = paymentRepo;
        _refundRepo = refundRepo;
        _gateway = gateway;
        _leads = leads;
        _logger = logger ?? NullLogger<PaymentRefundService>.Instance;
    }

    public string GatewayEnvironment => _gateway.Environment;
    public string GatewayProvider => _gateway.Provider;

    /// <summary>
    /// Выполняет полный возврат средств по заказу.
    /// </summary>
    public async Task<PaymentRefundResult> RefundAsync(
        string orderId,
        string reason,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["OrderId"] = orderId
        });

        _logger.LogInformation(
            PaymentEvents.PaymentRefundRequested,
            "Администратор запросил возврат платежа {OrderId}. Причина: {Reason}",
            orderId,
            reason);

        // 1. Валидация причины возврата (10 - 500 символов)
        string trimmedReason = reason?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(trimmedReason) || trimmedReason.Length < 10 || trimmedReason.Length > 500)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён локальной проверкой: некорректная длина причины ({ReasonLength}) для заказа {OrderId}",
                trimmedReason.Length,
                orderId);

            return PaymentRefundResult.BadRequest(
                "invalid_reason",
                "Причина возврата обязательна и должна содержать от 10 до 500 символов.");
        }

        // 2. Поиск платежа
        var payment = _paymentRepo.GetByOrderId(orderId);
        if (payment == null)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: платёж {OrderId} не найден в БД",
                orderId);

            return PaymentRefundResult.NotFound("payment_not_found", "Платёж с указанным номером заказа не найден.");
        }

        // 3. Проверка статуса (только paid)
        if (payment.Status == PaymentStatuses.Refunded)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: платёж {OrderId} уже возвращён",
                orderId);

            return PaymentRefundResult.Conflict("already_refunded", "По данному платежу уже выполнен полный возврат средств.");
        }

        if (payment.Status != PaymentStatuses.Paid)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: платёж {OrderId} имеет неподходящий статус '{PaymentStatus}'",
                payment.Status,
                orderId);

            return PaymentRefundResult.Conflict(
                "invalid_status_for_refund",
                $"Возврат возможен только для успешно оплаченных платежей (текущий статус: '{payment.Status}').");
        }

        // 4. Проверка соответствия провайдера и окружения шлюза
        if (!string.Equals(payment.Provider, _gateway.Provider, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: провайдер платежа '{PaymentProvider}' не соответствует текущему шлюзу '{GatewayProvider}'",
                payment.Provider,
                _gateway.Provider);

            return PaymentRefundResult.BadRequest(
                "provider_mismatch",
                $"Провайдер платежа '{payment.Provider}' не поддерживается текущим шлюзом.");
        }

        if (!string.Equals(payment.Environment?.Trim(), _gateway.Environment?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: окружение платежа '{PaymentEnvironment}' не соответствует окружению текущего шлюза '{GatewayEnvironment}' для заказа {OrderId}",
                payment.Environment,
                _gateway.Environment,
                orderId);

            return PaymentRefundResult.Conflict(
                "environment_mismatch",
                $"Окружение платежа ('{payment.Environment}') не совпадает с активным окружением платёжного шлюза ('{_gateway.Environment}'). Выполнение возврата заблокировано во избежание списаний в неверном контуре.");
        }

        // 5. Проверка обязательных банковских реквизитов (MERCH_RN_ID, RRN, INT_REF, TerminalId)
        if (string.IsNullOrWhiteSpace(payment.MerchRnId))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: в платеже {OrderId} отсутствует MERCH_RN_ID",
                orderId);

            return PaymentRefundResult.BadRequest(
                "missing_merch_rn_id",
                "Отсутствует банковский идентификатор покупки (MERCH_RN_ID). Возврат невозможен.");
        }

        if (string.IsNullOrWhiteSpace(payment.Rrn))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: в платеже {OrderId} отсутствует RRN",
                orderId);

            return PaymentRefundResult.BadRequest(
                "missing_rrn",
                "Отсутствует ссылочный номер транзакции (RRN). Возврат невозможен.");
        }

        if (string.IsNullOrWhiteSpace(payment.IntRef))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: в платеже {OrderId} отсутствует INT_REF",
                orderId);

            return PaymentRefundResult.BadRequest(
                "missing_int_ref",
                "Отсутствует внутренний банковский номер (INT_REF). Возврат невозможен.");
        }

        string terminalId = !string.IsNullOrWhiteSpace(payment.TerminalId) ? payment.TerminalId : _gateway.TerminalId;
        if (string.IsNullOrWhiteSpace(terminalId))
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: отсутствует TerminalId для заказа {OrderId}",
                orderId);

            return PaymentRefundResult.BadRequest(
                "missing_terminal_id",
                "Отсутствует идентификатор терминала платежа.");
        }

        // 6. Проверка на существующий активный или завершённый возврат
        var existingActive = _refundRepo.GetActiveOrSucceededByPaymentId(payment.Id);
        if (existingActive != null)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Возврат отклонён: по платежу {OrderId} уже имеется возврат {RefundId} в статусе '{RefundStatus}'",
                orderId,
                existingActive.Id,
                existingActive.Status);

            return PaymentRefundResult.Conflict(
                "refund_already_in_progress",
                $"По данному платежу уже зафиксирована операция возврата в статусе '{existingActive.Status}'.");
        }

        // Сумма возврата берётся СТРОГО из БД
        int refundAmount = payment.AmountKzt;
        var now = DateTime.UtcNow.ToString("o");
        string requestTimestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        string nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));

        var refundRecord = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = payment.OrderId,
            Provider = payment.Provider,
            Environment = payment.Environment ?? "test",
            AmountKzt = refundAmount,
            Reason = trimmedReason,
            Status = PaymentRefundStatuses.Pending,
            RequestTimestamp = requestTimestamp,
            Nonce = nonce,
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };

        // 7. Атомарное создание записи возврата со статусом pending (защита от двойного клика)
        try
        {
            _refundRepo.Create(refundRecord);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            _logger.LogWarning(
                PaymentEvents.PaymentRefundRejected,
                "Конфликт параллельного создания возврата для платежа {OrderId}",
                orderId);

            return PaymentRefundResult.Conflict(
                "concurrent_refund",
                "Операция возврата уже выполняется в параллельном запросе.");
        }

        // 8. Вызов провайдера
        var gatewayReq = new PaymentGatewayRefundRequest
        {
            OrderId = payment.OrderId,
            MerchRnId = payment.MerchRnId,
            OriginalAmountKzt = payment.AmountKzt,
            RefundAmountKzt = refundAmount,
            Currency = payment.Currency,
            TerminalId = terminalId,
            Rrn = payment.Rrn,
            IntRef = payment.IntRef,
            RequestTimestamp = requestTimestamp,
            Nonce = nonce
        };

        PaymentGatewayRefundResult gatewayResult;
        try
        {
            gatewayResult = await _gateway.RefundAsync(gatewayReq, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.PaymentRefundFailed,
                "Неожиданная ошибка вызова шлюза возврата для заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            _leads.AuditLog(
                "admin",
                "refund_error",
                $"OrderId: {orderId}, Reason: {trimmedReason}, Result: ExceptionType {ex.GetType().Name}");

            // При неожиданном исключении возврат остаётся pending до выяснения статуса
            return PaymentRefundResult.BadGateway(
                "gateway_exception",
                "Шлюз возврата не ответил. Операция оставлена в ожидании проверки статуса.");
        }

        // 9. Обработка результата шлюза:
        // А) Подтверждённый успех
        if (gatewayResult.IsFinal && gatewayResult.Success)
        {
            var completionResult = _refundRepo.CompleteRefundTransaction(
                refundId: refundRecord.Id,
                orderId: payment.OrderId,
                actionCode: gatewayResult.ActionCode,
                responseCode: gatewayResult.ResponseCode,
                rrn: gatewayResult.Rrn,
                intRef: gatewayResult.IntRef,
                bankMessage: gatewayResult.BankMessage,
                completedAt: DateTime.UtcNow.ToString("o"));

            if (!completionResult.Success)
            {
                _logger.LogError(
                    PaymentEvents.PaymentRefundFailed,
                    "Критическая ошибка: шлюз подтвердил возврат заказа {OrderId}, но CompleteRefundTransaction завершился с ошибкой {ErrorCode}: {ErrorMessage}",
                    orderId,
                    completionResult.ErrorCode,
                    completionResult.ErrorMessage);

                return PaymentRefundResult.Error(
                    StatusCodes.Status500InternalServerError,
                    "refund_completion_failed",
                    "Шлюз подтвердил операцию возврата, но не удалось применить изменения в базе данных. Операция оставлена для ручной сверки.");
            }

            _logger.LogInformation(
                PaymentEvents.PaymentRefundSucceeded,
                "Полный возврат заказа {OrderId} на сумму {AmountKzt} KZT успешно завершён",
                orderId,
                refundAmount);

            _leads.AuditLog("admin", "refund_succeeded", $"OrderId: {orderId}, Amount: {refundAmount}, Reason: {trimmedReason}");

            return PaymentRefundResult.Ok(new
            {
                refundId = refundRecord.Id,
                orderId = payment.OrderId,
                amountKzt = refundAmount,
                status = PaymentRefundStatuses.Succeeded,
                paymentStatus = PaymentStatuses.Refunded,
                message = "Возврат средств успешно выполнен и подтверждён платёжным шлюзом."
            });
        }

        // Б) Однозначный отказ шлюза
        if (gatewayResult.IsFinal && !gatewayResult.Success)
        {
            _refundRepo.UpdateStatus(
                refundId: refundRecord.Id,
                status: PaymentRefundStatuses.Failed,
                actionCode: gatewayResult.ActionCode,
                responseCode: gatewayResult.ResponseCode,
                rrn: gatewayResult.Rrn,
                intRef: gatewayResult.IntRef,
                bankMessage: gatewayResult.BankMessage,
                completedAt: DateTime.UtcNow.ToString("o"));

            _logger.LogWarning(
                PaymentEvents.PaymentRefundFailed,
                "Шлюз однозначно отклонил возврат заказа {OrderId}: Action {ActionCode}, RC {ResponseCode}",
                orderId,
                gatewayResult.ActionCode,
                gatewayResult.ResponseCode);

            _leads.AuditLog("admin", "refund_failed", $"OrderId: {orderId}, Reason: {trimmedReason}, Result: Rejected by gateway (RC: {gatewayResult.ResponseCode})");

            return PaymentRefundResult.BadGateway(
                "refund_rejected",
                gatewayResult.ErrorMessage ?? "Платёжный шлюз отклонил операцию возврата.",
                new
                {
                    actionCode = gatewayResult.ActionCode,
                    responseCode = gatewayResult.ResponseCode,
                    bankMessage = gatewayResult.BankMessage
                });
        }

        // В) Запрос принят, ожидает callback или ручной сверки (202 Accepted)
        _logger.LogInformation(
            PaymentEvents.PaymentRefundAwaitingCallback,
            "Запрос возврата заказа {OrderId} принят шлюзом (Accepted=true, IsFinal=false), ожидает callback",
            orderId);

        _leads.AuditLog("admin", "refund_accepted_pending", $"OrderId: {orderId}, Amount: {refundAmount}, Reason: {trimmedReason}");

        return PaymentRefundResult.Accepted(new
        {
            refundId = refundRecord.Id,
            orderId = payment.OrderId,
            amountKzt = refundAmount,
            status = PaymentRefundStatuses.Pending,
            paymentStatus = payment.Status,
            message = "Запрос на возврат отправлен в банк. Ожидается подтверждение платёжного шлюза."
        });
    }

    /// <summary>
    /// Выполняет сверку существующего возврата через операцию проверки со стороны торговца TRTYPE=90.
    /// Не отправляет повторный запрос возврата TRTYPE=14.
    /// Pending-возврат синхронизируется с подтверждённым банковским результатом.
    /// Для уже завершённого возврата проверка идемпотентна и не понижает локальный успешный статус.
    /// При неоднозначном ответе / сетевой ошибке существующий локальный статус сохраняется.
    /// </summary>
    public async Task<PaymentRefundResult> CheckRefundStatusAsync(string orderId, string createdBy = "admin", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return PaymentRefundResult.BadRequest("invalid_order_id", "Номер заказа не указан.");
        }

        var payment = _paymentRepo.GetByOrderId(orderId);
        if (payment == null)
        {
            return PaymentRefundResult.NotFound("payment_not_found", $"Платёж для заказа {orderId} не найден.");
        }

        var refund = _refundRepo.GetLatestByOrderId(orderId);
        if (refund == null)
        {
            return PaymentRefundResult.NotFound("refund_not_found", $"Операция возврата для заказа {orderId} не найдена.");
        }

        bool isPending = string.Equals(refund.Status, PaymentRefundStatuses.Pending, StringComparison.OrdinalIgnoreCase);
        bool isSucceeded = string.Equals(refund.Status, PaymentRefundStatuses.Succeeded, StringComparison.OrdinalIgnoreCase);
        bool isReconciliationRequired = string.Equals(refund.Status, PaymentRefundStatuses.ReconciliationRequired, StringComparison.OrdinalIgnoreCase);

        // Проверка совпадения провайдера и среды
        if (!string.Equals(payment.Provider, _gateway.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return PaymentRefundResult.Conflict("provider_mismatch", "Провайдер платежа не совпадает с активным шлюзом.");
        }

        if (!string.Equals(payment.Environment?.Trim(), _gateway.Environment?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return PaymentRefundResult.Conflict("environment_mismatch", "Окружение платежа не совпадает с окружением шлюза.");
        }

        // Ограничение BCC: TRTYPE=90 доступен только в течение 24 часов после исходной операции.
        // Требование: если дату создания возврата невозможно прочитать, не отправлять запрос в BCC, вернуть 409 с сообщением о необходимости ручной сверки.
        if (string.IsNullOrWhiteSpace(refund.CreatedAt) ||
            !DateTime.TryParse(refund.CreatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsedRefundTime))
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Проверка статуса TRTYPE=90 для заказа {OrderId} отклонена: дату создания возврата невозможно прочитать ('{CreatedAt}'). Запрос в BCC отменён.",
                orderId,
                refund.CreatedAt);

            return PaymentRefundResult.Conflict(
                "invalid_refund_created_at",
                "Невозможно определить дату создания операции возврата. Автоматическая проверка через BCC заблокирована, необходима ручная сверка с выпиской банка.");
        }

        DateTime operationTime = parsedRefundTime.ToUniversalTime();
        if ((DateTime.UtcNow - operationTime) > TimeSpan.FromHours(24))
        {
            _logger.LogWarning(
                PaymentEvents.BccGatewayWarning,
                "Проверка статуса TRTYPE=90 для заказа {OrderId} отклонена: прошло более 24 часов с момента создания возврата ({OperationTime:o}). Запрос в BCC отменён.",
                orderId,
                operationTime);

            return PaymentRefundResult.Conflict(
                "status_check_expired",
                "Автоматическая проверка статуса через BCC (TRTYPE=90) доступна только в течение 24 часов после исходной операции. Срок истёк, необходима ручная сверка с выпиской банка.");
        }

        _logger.LogInformation(
            PaymentEvents.PaymentRefundGatewaySent,
            "Инициирована сверка статуса возврата TRTYPE=90 для заказа {OrderId}, локальный статус {RefundStatus}",
            orderId,
            refund.Status);

        PaymentGatewayCheckResult checkResult;
        try
        {
            // Вызываем проверку статуса (TRTYPE=90), повторный TRTYPE=14 НЕ отправляется!
            checkResult = await _gateway.CheckStatusAsync(
                orderId,
                cancellationToken,
                tranTrType: "14");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccGatewayError,
                "Ошибка при запросе проверки статуса TRTYPE=90 для заказа {OrderId}. Тип ошибки: {ErrorType}",
                orderId,
                ex.GetType().Name);

            return PaymentRefundResult.Accepted(new
            {
                orderId,
                refundId = refund.Id,
                status = refund.Status,
                message = "Не удалось прочитать ответ BCC. Существующий локальный статус возврата сохранён."
            });
        }

        // А) Однозначное подтверждение возврата банком
        if (checkResult.IsFinal && checkResult.Success)
        {
            var completionResult = _refundRepo.CompleteRefundTransaction(
                refundId: refund.Id,
                orderId: orderId,
                actionCode: checkResult.ActionCode,
                responseCode: checkResult.ResponseCode,
                rrn: checkResult.Rrn,
                intRef: checkResult.IntRef,
                bankMessage: checkResult.BankMessage,
                completedAt: DateTime.UtcNow.ToString("o"),
                allowSupersededAttempt: !isPending && !isSucceeded);

            if (!completionResult.Success)
            {
                _logger.LogError(
                    PaymentEvents.PaymentRefundFailed,
                    "Шлюз подтвердил возврат при сверке {OrderId}, но CompleteRefundTransaction вернул ошибку: {Error}",
                    orderId,
                    completionResult.ErrorMessage);

                return PaymentRefundResult.Error(
                    StatusCodes.Status500InternalServerError,
                    "refund_completion_failed",
                    "Шлюз подтвердил возврат, но не удалось применить изменения в базе данных.");
            }

            _leads.AuditLog(createdBy, "refund_status_succeeded", $"OrderId: {orderId}, Bank checked (TRTYPE=90): Success");

            string confirmedStatus = completionResult.ReconciliationRequired
                ? PaymentRefundStatuses.ReconciliationRequired
                : PaymentRefundStatuses.Succeeded;

            return PaymentRefundResult.Ok(new
            {
                orderId,
                refundId = refund.Id,
                status = confirmedStatus,
                paymentStatus = PaymentStatuses.Refunded,
                actionCode = checkResult.ActionCode,
                responseCode = checkResult.ResponseCode,
                rrn = checkResult.Rrn,
                message = completionResult.ReconciliationRequired
                    ? "Банк подтвердил возврат, но обнаружена более новая попытка. Требуется ручная сверка."
                    : "Возврат средств успешно подтверждён платёжным шлюзом (TRTYPE=90)."
            });
        }

        // Б) Однозначный отказ банка
        if (checkResult.IsFinal && !checkResult.Success)
        {
            // Уже подтверждённый возврат не понижаем из-за противоречивого ответа проверки.
            if (isSucceeded)
            {
                _logger.LogWarning(
                    PaymentEvents.BccGatewayWarning,
                    "BCC вернул отказ при проверке уже успешного возврата {OrderId}. Локальный статус succeeded сохранён. ACTION {Action}, RC {ResponseCode}",
                    orderId,
                    checkResult.ActionCode,
                    checkResult.ResponseCode);

                _leads.AuditLog(createdBy, "refund_status_conflict", $"OrderId: {orderId}, Local=succeeded, Bank check rejected Action={checkResult.ActionCode}, RC={checkResult.ResponseCode}");

                return PaymentRefundResult.Ok(new
                {
                    orderId,
                    refundId = refund.Id,
                    status = PaymentRefundStatuses.Succeeded,
                    paymentStatus = PaymentStatuses.Refunded,
                    actionCode = checkResult.ActionCode,
                    responseCode = checkResult.ResponseCode,
                    message = "Банк вернул противоречивый отказ при проверке уже успешного возврата. Локальный успешный статус сохранён; требуется ручная сверка."
                });
            }

            string rejectedStatus = isReconciliationRequired
                ? PaymentRefundStatuses.ReconciliationRequired
                : PaymentRefundStatuses.Failed;

            _refundRepo.UpdateStatus(
                refundId: refund.Id,
                status: rejectedStatus,
                actionCode: checkResult.ActionCode,
                responseCode: checkResult.ResponseCode,
                rrn: checkResult.Rrn,
                intRef: checkResult.IntRef,
                bankMessage: checkResult.BankMessage,
                completedAt: DateTime.UtcNow.ToString("o"));

            _leads.AuditLog(createdBy, "refund_status_failed", $"OrderId: {orderId}, Bank checked (TRTYPE=90): Rejection Action={checkResult.ActionCode}, RC={checkResult.ResponseCode}");

            return PaymentRefundResult.Ok(new
            {
                orderId,
                refundId = refund.Id,
                status = rejectedStatus,
                actionCode = checkResult.ActionCode,
                responseCode = checkResult.ResponseCode,
                message = isReconciliationRequired
                    ? "Банк отклонил проверяемую операцию, но локальный статус ручной сверки сохранён."
                    : "Банк однозначно отклонил возврат средств по заказу."
            });
        }

        // В) Неоднозначный ответ / ответ для TRTYPE=1 / ошибка связи.
        // Pending остаётся pending; любой терминальный локальный статус также сохраняется.
        if (isPending)
        {
            _refundRepo.UpdateStatus(
                refundId: refund.Id,
                status: PaymentRefundStatuses.Pending,
                actionCode: checkResult.ActionCode,
                responseCode: checkResult.ResponseCode,
                rrn: checkResult.Rrn,
                intRef: checkResult.IntRef,
                bankMessage: checkResult.BankMessage);
        }

        string pendingMsg = !string.IsNullOrWhiteSpace(checkResult.BankMessage)
            ? checkResult.BankMessage
            : isPending
                ? "Статус возврата не подтверждён однозначно. Операция остаётся в обработке (pending) для ручной сверки."
                : $"Статус возврата не подтверждён однозначно. Локальный статус '{refund.Status}' сохранён.";

        return PaymentRefundResult.Accepted(new
        {
            orderId,
            refundId = refund.Id,
            status = refund.Status,
            actionCode = checkResult.ActionCode,
            responseCode = checkResult.ResponseCode,
            message = pendingMsg
        });
    }
}
