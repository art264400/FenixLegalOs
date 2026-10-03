using System;
using System.Collections.Generic;
using Dapper;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using Microsoft.Data.Sqlite;

namespace FenixLegalOs.Repositories;

public class PaymentRepository
{
    private readonly DbInitializer _db;

    public PaymentRepository(DbInitializer db)
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

    public void Create(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);

        if (string.IsNullOrWhiteSpace(payment.SessionId))
            throw new ArgumentException("SessionId is required.", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.OrderId))
            throw new ArgumentException("OrderId is required.", nameof(payment));

        if (payment.AmountKzt <= 0)
            throw new ArgumentException("AmountKzt must be greater than zero.", nameof(payment));

        string tariff = payment.Tariff?.Trim().ToLowerInvariant() ?? "";
        if (tariff is not ("report" or "consultation"))
            throw new ArgumentException($"Invalid tariff '{payment.Tariff}'. Allowed tariffs are 'report', 'consultation'.", nameof(payment));

        string environment = payment.Environment?.Trim().ToLowerInvariant() ?? "";
        if (environment is not ("test" or "production"))
            throw new ArgumentException($"Invalid environment '{payment.Environment}'. Allowed environments are 'test', 'production'.", nameof(payment));

        string status = payment.Status?.Trim().ToLowerInvariant() ?? "";
        if (status != PaymentStatuses.Created)
        {
            throw new ArgumentException(
                "Новый платёж должен создаваться только со статусом created.",
                nameof(payment));
        }

        if (!string.Equals(payment.Currency?.Trim(), "KZT", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Поддерживается только KZT.", nameof(payment));

        if (string.IsNullOrWhiteSpace(payment.Provider))
            throw new ArgumentException("Provider обязателен.", nameof(payment));

        // Сохраняем нормализованные значения
        payment.Tariff = tariff;
        payment.Environment = environment;
        payment.Status = status;
        payment.Currency = payment.Currency?.Trim().ToUpperInvariant() ?? "KZT";
        payment.Provider = payment.Provider.Trim().ToLowerInvariant();

        using var conn = GetConn();
        conn.Execute(@"
            INSERT INTO payments (
                id, session_id, order_id, tariff, amount_kzt, currency,
                provider, environment, terminal_id, status, nonce,
                request_timestamp, provider_metadata, rrn, int_ref, approval_code,
                action_code, response_code, merchant_advice_code,
                bank_message, created_at, updated_at, paid_at,
                notification_received_at, last_status_check_at
            ) VALUES (
                @Id, @SessionId, @OrderId, @Tariff, @AmountKzt, @Currency,
                @Provider, @Environment, @TerminalId, @Status, @Nonce,
                @RequestTimestamp, @ProviderMetadata, @Rrn, @IntRef, @ApprovalCode,
                @ActionCode, @ResponseCode, @MerchantAdviceCode,
                @BankMessage, @CreatedAt, @UpdatedAt, @PaidAt,
                @NotificationReceivedAt, @LastStatusCheckAt
            )", payment);
    }

    public Payment? GetById(string id)
    {
        using var conn = GetConn();
        return conn.QuerySingleOrDefault<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE id = @id", new { id });
    }

    public Payment? GetByOrderId(string orderId)
    {
        using var conn = GetConn();
        return conn.QuerySingleOrDefault<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE order_id = @orderId", new { orderId });
    }

    public List<Payment> GetBySessionId(string sessionId)
    {
        using var conn = GetConn();
        return conn.Query<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE session_id = @sessionId
            ORDER BY created_at DESC", new { sessionId }).AsList();
    }

    public Payment? GetLatestBySessionId(string sessionId)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE session_id = @sessionId
            ORDER BY created_at DESC
            LIMIT 1", new { sessionId });
    }

    public Payment? GetSuccessfulPaymentBySessionId(string sessionId)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE session_id = @sessionId AND status = 'paid'
            ORDER BY paid_at DESC, updated_at DESC
            LIMIT 1", new { sessionId });
    }

    public Payment? GetLatestRefundedPaymentBySessionId(string sessionId)
    {
        using var conn = GetConn();
        return conn.QueryFirstOrDefault<Payment>(@"
            SELECT 
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE session_id = @sessionId AND status = 'refunded'
            ORDER BY updated_at DESC
            LIMIT 1", new { sessionId });
    }

    public bool UpdateStatus(
        string orderId,
        string status,
        string? rrn = null,
        string? intRef = null,
        string? approvalCode = null,
        string? actionCode = null,
        string? responseCode = null,
        string? merchantAdviceCode = null,
        string? bankMessage = null,
        string? paidAt = null,
        string? notificationReceivedAt = null,
        string? lastStatusCheckAt = null)
    {
        if (!PaymentStatuses.IsValid(status))
            throw new ArgumentException($"Invalid payment status '{status}'.", nameof(status));

        using var conn = GetConn();
        using var tx = conn.BeginTransaction();

        // 1. Читаем текущий статус платежа
        var current = conn.QuerySingleOrDefault<Payment>(
            "SELECT status AS Status, paid_at AS PaidAt FROM payments WHERE order_id = @orderId",
            new { orderId },
            tx);

        if (current == null)
            return false;

        var updatedAt = DateTime.UtcNow.ToString("o");

        // 2. Валидация допустимости перехода состояния
        if (!PaymentStatuses.CanTransition(current.Status, status))
        {
            // Если переход запрещён (например, запоздавший ответ банка с failed/pending для уже оплаченного paid),
            // мы сохраняем аудит обращения (last_status_check_at), но НЕ перезаписываем защищённый статус.
            if (!string.IsNullOrWhiteSpace(lastStatusCheckAt) || !string.IsNullOrWhiteSpace(notificationReceivedAt))
            {
                conn.Execute(@"
                    UPDATE payments
                    SET 
                        last_status_check_at = COALESCE(@lastStatusCheckAt, last_status_check_at),
                        notification_received_at = COALESCE(@notificationReceivedAt, notification_received_at),
                        updated_at = @updatedAt
                    WHERE order_id = @orderId",
                    new { orderId, lastStatusCheckAt, notificationReceivedAt, updatedAt },
                    tx);
                tx.Commit();
            }
            return false;
        }

        // 3. Защита даты оплаты:
        // Если платёж УЖЕ оплачен (current.Status == Paid), повторная нотификация (paid -> paid)
        // ни при каких обстоятельствах не должна менять дату оплаты.
        if (current.Status == PaymentStatuses.Paid)
        {
            paidAt = null;
        }
        else if (status == PaymentStatuses.Paid && string.IsNullOrWhiteSpace(paidAt))
        {
            // Автоматически проставляем paidAt только при ПЕРВОМ переходе в paid
            paidAt = updatedAt;
        }

        int rows = conn.Execute(@"
            UPDATE payments
            SET 
                status = @status,
                updated_at = @updatedAt,
                rrn = COALESCE(@rrn, rrn),
                int_ref = COALESCE(@intRef, int_ref),
                approval_code = COALESCE(@approvalCode, approval_code),
                action_code = COALESCE(@actionCode, action_code),
                response_code = COALESCE(@responseCode, response_code),
                merchant_advice_code = COALESCE(@merchantAdviceCode, merchant_advice_code),
                bank_message = COALESCE(@bankMessage, bank_message),
                paid_at = COALESCE(paid_at, @paidAt),
                notification_received_at = COALESCE(@notificationReceivedAt, notification_received_at),
                last_status_check_at = COALESCE(@lastStatusCheckAt, last_status_check_at)
            WHERE order_id = @orderId", new
        {
            orderId,
            status,
            updatedAt,
            rrn,
            intRef,
            approvalCode,
            actionCode,
            responseCode,
            merchantAdviceCode,
            bankMessage,
            paidAt,
            notificationReceivedAt,
            lastStatusCheckAt
        }, tx);

        if (status == PaymentStatuses.Refunded)
        {
            // Находим другой действующий платёж со статусом 'paid' для этой же сессии
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
                    created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                    notification_received_at AS NotificationReceivedAt,
                    last_status_check_at AS LastStatusCheckAt
                FROM payments
                WHERE session_id = (
                    SELECT session_id
                    FROM payments
                    WHERE order_id = @orderId
                )
                AND status = 'paid'
                ORDER BY paid_at DESC, updated_at DESC
                LIMIT 1", new { orderId }, tx);

            if (remainingPaid != null)
            {
                // Если есть другой успешный платёж, обновляем sessions и leads его данными
                conn.Execute(@"
                    UPDATE sessions
                    SET paid = 1,
                        paid_at = @paidAt,
                        payment_amount = @amountKzt,
                        payment_method = @provider,
                        updated_at = @updatedAt
                    WHERE id = @sessionId;

                    UPDATE leads
                    SET paid = 1,
                        paid_at = @paidAt,
                        payment_amount = @amountKzt,
                        payment_method = @provider
                    WHERE session_id = @sessionId;
                ", new
                {
                    sessionId = remainingPaid.SessionId,
                    paidAt = remainingPaid.PaidAt,
                    amountKzt = remainingPaid.AmountKzt,
                    provider = remainingPaid.Provider,
                    updatedAt
                }, tx);
            }
            else
            {
                // Если другого paid нет — отзываем доступ
                conn.Execute(@"
                    UPDATE sessions
                    SET paid = 0, payment_method = 'refunded', updated_at = @updatedAt
                    WHERE id = (SELECT session_id FROM payments WHERE order_id = @orderId);

                    UPDATE leads
                    SET paid = 0, payment_method = 'refunded'
                    WHERE session_id = (SELECT session_id FROM payments WHERE order_id = @orderId);
                ", new { orderId, updatedAt }, tx);
            }
        }
        else if (status == PaymentStatuses.Paid)
        {
            // При подтверждении оплаты активируем сессию
            conn.Execute(@"
                UPDATE sessions
                SET paid = 1, paid_at = COALESCE(paid_at, @paidAt),
                    payment_amount = (SELECT amount_kzt FROM payments WHERE order_id = @orderId),
                    payment_method = (SELECT provider FROM payments WHERE order_id = @orderId),
                    updated_at = @updatedAt
                WHERE id = (SELECT session_id FROM payments WHERE order_id = @orderId);

                UPDATE leads
                SET paid = 1, paid_at = COALESCE(paid_at, @paidAt),
                    payment_amount = (SELECT amount_kzt FROM payments WHERE order_id = @orderId),
                    payment_method = (SELECT provider FROM payments WHERE order_id = @orderId)
                WHERE session_id = (SELECT session_id FROM payments WHERE order_id = @orderId);
            ", new { orderId, paidAt, updatedAt }, tx);
        }

        tx.Commit();
        return rows > 0;
    }
}
