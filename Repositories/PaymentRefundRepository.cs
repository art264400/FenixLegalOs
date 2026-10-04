using System;
using System.Collections.Generic;
using Dapper;
using FenixLegalOs.Models.Payments;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

/// <summary>
/// Репозиторий операций возврата платежей (payment_refunds).
/// </summary>
public class PaymentRefundRepository
{
    private readonly DbInitializer _db;

    public PaymentRefundRepository(DbInitializer db)
    {
        _db = db;
    }

    private SqliteConnection GetConn()
    {
        var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("PRAGMA foreign_keys = ON;");
        return conn;
    }

    /// <summary>
    /// Создает новую запись возврата в статусе pending.
    /// Благодаря частичному уникальному индексу uq_payment_refunds_active_or_succeeded
    /// гарантирует атомарную защиту от параллельного создания второго возврата для того же платежа.
    /// </summary>
    public void Create(PaymentRefund refund)
    {
        ArgumentNullException.ThrowIfNull(refund);

        if (string.IsNullOrWhiteSpace(refund.PaymentId))
            throw new ArgumentException("PaymentId обязателен.", nameof(refund));

        if (string.IsNullOrWhiteSpace(refund.OrderId))
            throw new ArgumentException("OrderId обязателен.", nameof(refund));

        if (refund.AmountKzt <= 0)
            throw new ArgumentException("Сумма возврата должна быть больше нуля.", nameof(refund));

        if (string.IsNullOrWhiteSpace(refund.Reason))
            throw new ArgumentException("Причина возврата обязательна.", nameof(refund));

        using var conn = GetConn();
        conn.Execute(@"
            INSERT INTO payment_refunds (
                id, payment_id, order_id, provider, environment,
                amount_kzt, reason, status, request_timestamp, nonce,
                action_code, response_code, rrn, int_ref, bank_message,
                created_by, created_at, updated_at, completed_at
            ) VALUES (
                @Id, @PaymentId, @OrderId, @Provider, @Environment,
                @AmountKzt, @Reason, @Status, @RequestTimestamp, @Nonce,
                @ActionCode, @ResponseCode, @Rrn, @IntRef, @BankMessage,
                @CreatedBy, @CreatedAt, @UpdatedAt, @CompletedAt
            )", refund);
    }

    public PaymentRefund? GetById(string id)
    {
        using var conn = GetConn();
        return conn.QuerySingleOrDefault<PaymentRefund>(@"
            SELECT
                id AS Id, payment_id AS PaymentId, order_id AS OrderId,
                provider AS Provider, environment AS Environment,
                amount_kzt AS AmountKzt, reason AS Reason, status AS Status,
                request_timestamp AS RequestTimestamp, nonce AS Nonce,
                action_code AS ActionCode, response_code AS ResponseCode,
                rrn AS Rrn, int_ref AS IntRef, bank_message AS BankMessage,
                created_by AS CreatedBy, created_at AS CreatedAt,
                updated_at AS UpdatedAt, completed_at AS CompletedAt
            FROM payment_refunds
            WHERE id = @id", new { id });
    }

    public PaymentRefund? GetLatestByOrderId(string orderId)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<PaymentRefund>(@"
            SELECT
                id AS Id, payment_id AS PaymentId, order_id AS OrderId,
                provider AS Provider, environment AS Environment,
                amount_kzt AS AmountKzt, reason AS Reason, status AS Status,
                request_timestamp AS RequestTimestamp, nonce AS Nonce,
                action_code AS ActionCode, response_code AS ResponseCode,
                rrn AS Rrn, int_ref AS IntRef, bank_message AS BankMessage,
                created_by AS CreatedBy, created_at AS CreatedAt,
                updated_at AS UpdatedAt, completed_at AS CompletedAt
            FROM payment_refunds
            WHERE order_id = @orderId
            ORDER BY created_at DESC
            LIMIT 1", new { orderId });
    }

    public PaymentRefund? GetActiveOrSucceededByPaymentId(string paymentId)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<PaymentRefund>(@"
            SELECT
                id AS Id, payment_id AS PaymentId, order_id AS OrderId,
                provider AS Provider, environment AS Environment,
                amount_kzt AS AmountKzt, reason AS Reason, status AS Status,
                request_timestamp AS RequestTimestamp, nonce AS Nonce,
                action_code AS ActionCode, response_code AS ResponseCode,
                rrn AS Rrn, int_ref AS IntRef, bank_message AS BankMessage,
                created_by AS CreatedBy, created_at AS CreatedAt,
                updated_at AS UpdatedAt, completed_at AS CompletedAt
            FROM payment_refunds
            WHERE payment_id = @paymentId AND status IN ('pending', 'succeeded')
            ORDER BY created_at DESC
            LIMIT 1", new { paymentId });
    }

    public List<PaymentRefund> GetByOrderId(string orderId)
    {
        using var conn = GetConn();
        return conn.Query<PaymentRefund>(@"
            SELECT
                id AS Id, payment_id AS PaymentId, order_id AS OrderId,
                provider AS Provider, environment AS Environment,
                amount_kzt AS AmountKzt, reason AS Reason, status AS Status,
                request_timestamp AS RequestTimestamp, nonce AS Nonce,
                action_code AS ActionCode, response_code AS ResponseCode,
                rrn AS Rrn, int_ref AS IntRef, bank_message AS BankMessage,
                created_by AS CreatedBy, created_at AS CreatedAt,
                updated_at AS UpdatedAt, completed_at AS CompletedAt
            FROM payment_refunds
            WHERE order_id = @orderId
            ORDER BY created_at DESC", new { orderId }).AsList();
    }

    /// <summary>
    /// Обновляет статус и банковские реквизиты операции возврата.
    /// </summary>
    public bool UpdateStatus(
        string refundId,
        string status,
        string? actionCode = null,
        string? responseCode = null,
        string? rrn = null,
        string? intRef = null,
        string? bankMessage = null,
        string? completedAt = null)
    {
        if (!PaymentRefundStatuses.IsValid(status))
            throw new ArgumentException($"Недопустимый статус возврата '{status}'.", nameof(status));

        using var conn = GetConn();
        var updatedAt = DateTime.UtcNow.ToString("o");

        int rows = conn.Execute(@"
            UPDATE payment_refunds
            SET
                status = @status,
                updated_at = @updatedAt,
                action_code = COALESCE(@actionCode, action_code),
                response_code = COALESCE(@responseCode, response_code),
                rrn = COALESCE(@rrn, rrn),
                int_ref = COALESCE(@intRef, int_ref),
                bank_message = COALESCE(@bankMessage, bank_message),
                completed_at = COALESCE(@completedAt, completed_at)
            WHERE id = @refundId", new
        {
            refundId,
            status,
            updatedAt,
            actionCode,
            responseCode,
            rrn,
            intRef,
            bankMessage,
            completedAt
        });

        return rows > 0;
    }

    /// <summary>
    /// Атомарно и идемпотентно завершает возврат платежа в единой транзакции SQLite:
    /// - проверяет существование и соответствие refund, orderId и payment;
    /// - переводит payment_refunds из pending в succeeded (или подтверждает существующий succeeded);
    /// - переводит исходный payment из paid в refunded;
    /// - если у сессии есть ДРУГОЙ платёж в статусе paid, сохраняет оплаченный доступ сессии и лида;
    /// - если других paid-платежей нет, отзывает доступ (paid = 0, payment_method = 'refunded');
    /// - фиксирует банковские реквизиты (action_code, response_code, rrn, int_ref, completed_at).
    /// </summary>
    public CompleteRefundTransactionResult CompleteRefundTransaction(
        string refundId,
        string orderId,
        string? actionCode,
        string? responseCode,
        string? rrn = null,
        string? intRef = null,
        string? bankMessage = null,
        string? completedAt = null)
    {
        if (string.IsNullOrWhiteSpace(refundId) || string.IsNullOrWhiteSpace(orderId))
        {
            return CompleteRefundTransactionResult.Fail("invalid_parameters", "Не указан идентификатор возврата или номер заказа.");
        }

        using var conn = GetConn();
        using var tx = conn.BeginTransaction();

        string timestamp = completedAt ?? DateTime.UtcNow.ToString("o");

        // 1. Проверяем существование и соответствие операции возврата
        var refund = conn.QuerySingleOrDefault<PaymentRefund>(
            "SELECT id AS Id, order_id AS OrderId, payment_id AS PaymentId, status AS Status FROM payment_refunds WHERE id = @refundId",
            new { refundId },
            tx);

        if (refund == null)
        {
            return CompleteRefundTransactionResult.Fail("refund_not_found", $"Запись возврата {refundId} не найдена в базе данных.");
        }

        if (!string.Equals(refund.OrderId, orderId, StringComparison.OrdinalIgnoreCase))
        {
            return CompleteRefundTransactionResult.Fail("order_id_mismatch", $"Заказ возврата {refund.OrderId} не соответствует запрошенному заказу {orderId}.");
        }

        if (refund.Status != PaymentRefundStatuses.Pending && refund.Status != PaymentRefundStatuses.Succeeded)
        {
            return CompleteRefundTransactionResult.Fail("invalid_refund_status", $"Возврат находится в статусе '{refund.Status}' и не может быть завершён.");
        }

        // 2. Проверяем существование и соответствие платежа
        var payment = conn.QuerySingleOrDefault<Payment>(
            "SELECT id AS Id, order_id AS OrderId, session_id AS SessionId, status AS Status FROM payments WHERE order_id = @orderId",
            new { orderId },
            tx);

        if (payment == null)
        {
            return CompleteRefundTransactionResult.Fail("payment_not_found", $"Исходный платёж для заказа {orderId} не найден.");
        }

        if (!string.Equals(refund.PaymentId, payment.Id, StringComparison.Ordinal))
        {
            return CompleteRefundTransactionResult.Fail("payment_id_mismatch", $"Идентификатор платежа {payment.Id} не соответствует операции возврата {refund.PaymentId}.");
        }

        if (payment.Status != PaymentStatuses.Paid && payment.Status != PaymentStatuses.Refunded)
        {
            return CompleteRefundTransactionResult.Fail("invalid_payment_status", $"Платёж находится в статусе '{payment.Status}' и не может быть переведён в refunded.");
        }

        // 3. Обновляем payment_refunds до succeeded
        if (refund.Status != PaymentRefundStatuses.Succeeded)
        {
            int refundRows = conn.Execute(@"
                UPDATE payment_refunds
                SET
                    status = @status,
                    action_code = COALESCE(@actionCode, action_code),
                    response_code = COALESCE(@responseCode, response_code),
                    rrn = COALESCE(@rrn, rrn),
                    int_ref = COALESCE(@intRef, int_ref),
                    bank_message = COALESCE(@bankMessage, bank_message),
                    completed_at = COALESCE(completed_at, @timestamp),
                    updated_at = @timestamp
                WHERE id = @refundId AND status = 'pending'",
                new
                {
                    refundId,
                    status = PaymentRefundStatuses.Succeeded,
                    actionCode,
                    responseCode,
                    rrn,
                    intRef,
                    bankMessage,
                    timestamp
                },
                tx);

            if (refundRows == 0)
            {
                return CompleteRefundTransactionResult.Fail("refund_update_conflict", "Не удалось обновить запись возврата: конфликт состояния строки.");
            }
        }

        // 4. Обновляем статус исходного платежа на refunded
        int paymentRows = conn.Execute(@"
            UPDATE payments
            SET
                status = @status,
                action_code = COALESCE(@actionCode, action_code),
                response_code = COALESCE(@responseCode, response_code),
                notification_received_at = COALESCE(notification_received_at, @timestamp),
                updated_at = @timestamp
            WHERE order_id = @orderId AND id = @paymentId AND status IN ('paid', 'refunded')",
            new
            {
                orderId,
                paymentId = payment.Id,
                status = PaymentStatuses.Refunded,
                actionCode,
                responseCode,
                timestamp
            },
            tx);

        if (paymentRows == 0)
        {
            return CompleteRefundTransactionResult.Fail("payment_update_conflict", "Не удалось обновить статус платежа на refunded: конфликт состояния строки.");
        }

        // 5. Проверяем наличие ДРУГОГО успешного paid-платежа для той же сессии
        var remainingPaid = conn.QueryFirstOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt
            FROM payments
            WHERE session_id = @sessionId AND status = 'paid' AND id != @paymentId
            ORDER BY paid_at DESC, updated_at DESC
            LIMIT 1",
            new { sessionId = payment.SessionId, paymentId = payment.Id },
            tx);

        if (remainingPaid != null)
        {
            // Если есть другой paid-платёж, доступ сессии и лида сохраняется
            conn.Execute(@"
                UPDATE sessions
                SET paid = 1,
                    paid_at = @paidAt,
                    payment_amount = @amountKzt,
                    payment_method = @provider,
                    updated_at = @timestamp
                WHERE id = @sessionId;

                UPDATE leads
                SET paid = 1,
                    paid_at = @paidAt,
                    payment_amount = @amountKzt,
                    payment_method = @provider
                WHERE session_id = @sessionId;
            ", new
            {
                sessionId = payment.SessionId,
                paidAt = remainingPaid.PaidAt,
                amountKzt = remainingPaid.AmountKzt,
                provider = remainingPaid.Provider,
                timestamp
            }, tx);
        }
        else
        {
            // Других оплаченных платежей нет — отзываем доступ
            conn.Execute(@"
                UPDATE sessions
                SET
                    paid = 0,
                    payment_method = 'refunded',
                    updated_at = @timestamp
                WHERE id = @sessionId;

                UPDATE leads
                SET
                    paid = 0,
                    payment_method = 'refunded'
                WHERE session_id = @sessionId;
            ", new { sessionId = payment.SessionId, timestamp }, tx);
        }

        tx.Commit();
        return CompleteRefundTransactionResult.Ok();
    }
}

/// <summary>
/// Результат выполнения атомарной транзакции завершения возврата.
/// Поддерживает неявное приведение к bool для обратной совместимости.
/// </summary>
public sealed class CompleteRefundTransactionResult
{
    public bool Success { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }

    public static CompleteRefundTransactionResult Ok() => new() { Success = true };

    public static CompleteRefundTransactionResult Fail(string errorCode, string errorMessage) =>
        new() { Success = false, ErrorCode = errorCode, ErrorMessage = errorMessage };

    public static implicit operator bool(CompleteRefundTransactionResult? result) => result?.Success == true;
}
