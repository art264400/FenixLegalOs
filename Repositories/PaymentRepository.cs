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

        // Извлечение merch_rn_id с fallback на provider_metadata
        if (string.IsNullOrWhiteSpace(payment.MerchRnId) && !string.IsNullOrWhiteSpace(payment.ProviderMetadata))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(payment.ProviderMetadata);
                if (doc.RootElement.TryGetProperty("merch_rn_id", out var mrProp))
                {
                    payment.MerchRnId = mrProp.GetString();
                }
            }
            catch { }
        }

        using var conn = GetConn();
        conn.Execute(@"
            INSERT INTO payments (
                id, session_id, order_id, tariff, amount_kzt, currency,
                provider, environment, terminal_id, status, nonce,
                request_timestamp, merch_rn_id, provider_metadata, rrn, int_ref, approval_code,
                action_code, response_code, merchant_advice_code,
                bank_message, created_at, updated_at, paid_at,
                notification_received_at, last_status_check_at
            ) VALUES (
                @Id, @SessionId, @OrderId, @Tariff, @AmountKzt, @Currency,
                @Provider, @Environment, @TerminalId, @Status, @Nonce,
                @RequestTimestamp, @MerchRnId, @ProviderMetadata, @Rrn, @IntRef, @ApprovalCode,
                @ActionCode, @ResponseCode, @MerchantAdviceCode,
                @BankMessage, @CreatedAt, @UpdatedAt, @PaidAt,
                @NotificationReceivedAt, @LastStatusCheckAt
            )", payment);
    }

    private static Payment PopulateFallback(Payment? p)
    {
        if (p != null && string.IsNullOrWhiteSpace(p.MerchRnId) && !string.IsNullOrWhiteSpace(p.ProviderMetadata))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(p.ProviderMetadata);
                if (doc.RootElement.TryGetProperty("merch_rn_id", out var mrProp))
                {
                    p.MerchRnId = mrProp.GetString();
                }
            }
            catch { }
        }
        return p!;
    }

    public Payment? GetById(string id)
    {
        using var conn = GetConn();
        var p = conn.QuerySingleOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE id = @id", new { id });
        return p != null ? PopulateFallback(p) : null;
    }

    public Payment? GetByOrderId(string orderId)
    {
        using var conn = GetConn();
        var p = conn.QuerySingleOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE order_id = @orderId", new { orderId });
        return p != null ? PopulateFallback(p) : null;
    }

    public List<Payment> GetBySessionId(string sessionId)
    {
        using var conn = GetConn();
        var list = conn.Query<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
                rrn AS Rrn, int_ref AS IntRef, approval_code AS ApprovalCode,
                action_code AS ActionCode, response_code AS ResponseCode,
                merchant_advice_code AS MerchantAdviceCode, bank_message AS BankMessage,
                created_at AS CreatedAt, updated_at AS UpdatedAt, paid_at AS PaidAt,
                notification_received_at AS NotificationReceivedAt,
                last_status_check_at AS LastStatusCheckAt
            FROM payments
            WHERE session_id = @sessionId
            ORDER BY created_at DESC", new { sessionId }).AsList();
        foreach (var item in list) PopulateFallback(item);
        return list;
    }

    public Payment? GetLatestBySessionId(string sessionId)
    {
        using var conn = GetConn();
        var p = conn.QueryFirstOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
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
        return p != null ? PopulateFallback(p) : null;
    }

    public Payment? GetSuccessfulPaymentBySessionId(string sessionId)
    {
        using var conn = GetConn();
        var p = conn.QueryFirstOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
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
        return p != null ? PopulateFallback(p) : null;
    }

    public Payment? GetLatestRefundedPaymentBySessionId(string sessionId)
    {
        using var conn = GetConn();
        var p = conn.QueryFirstOrDefault<Payment>(@"
            SELECT
                id AS Id, session_id AS SessionId, order_id AS OrderId,
                tariff AS Tariff, amount_kzt AS AmountKzt, currency AS Currency,
                provider AS Provider, environment AS Environment, terminal_id AS TerminalId,
                status AS Status, nonce AS Nonce, request_timestamp AS RequestTimestamp,
                merch_rn_id AS MerchRnId, provider_metadata AS ProviderMetadata,
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
        return p != null ? PopulateFallback(p) : null;
    }

    /// <summary>
    /// Атомарно обновляет параметры активной попытки (nonce, request_timestamp, provider_metadata, merch_rn_id)
    /// при повторном открытии платёжной формы только для записей в статусе created или pending.
    /// </summary>
    public bool UpdateAttempt(string orderId, string nonce, string requestTimestamp, string? providerMetadata = null, string? merchRnId = null)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            return false;

        if (string.IsNullOrWhiteSpace(merchRnId) && !string.IsNullOrWhiteSpace(providerMetadata))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(providerMetadata);
                if (doc.RootElement.TryGetProperty("merch_rn_id", out var mrProp))
                {
                    merchRnId = mrProp.GetString();
                }
            }
            catch { }
        }

        using var conn = GetConn();
        int rows = conn.Execute(@"
            UPDATE payments
            SET
                nonce = @nonce,
                request_timestamp = @requestTimestamp,
                merch_rn_id = COALESCE(@merchRnId, merch_rn_id),
                provider_metadata = COALESCE(@providerMetadata, provider_metadata),
                updated_at = @updatedAt
            WHERE order_id = @orderId AND status IN ('created', 'pending')", new
        {
            orderId,
            nonce,
            requestTimestamp,
            merchRnId,
            providerMetadata,
            updatedAt = DateTime.UtcNow.ToString("o")
        });
        return rows > 0;
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

    /// <summary>
    /// Возвращает типизированный список платежей для админки с данными клиента и последнего возврата.
    /// CanRefund вычисляется на основе статуса paid, отсутствия активного возврата и совпадения окружения с gatewayEnvironment.
    /// </summary>
    public List<AdminPaymentListItemDto> GetAdminPaymentsList(
        string? statusFilter = null,
        int limit = 100,
        string? gatewayEnvironment = null,
        string? gatewayProvider = null,
        string? gatewayTerminalId = null)
    {
        using var conn = GetConn();
        int safeLimit = Math.Clamp(limit, 1, 500);

        string sql = @"
            SELECT
                p.id AS Id,
                p.order_id AS OrderId,
                p.session_id AS SessionId,
                COALESCE(l.name, u.name, 'Гость') AS ClientName,
                COALESCE(l.email, u.email, '') AS ClientEmail,
                COALESCE(u.phone, l.messenger, '') AS ClientPhone,
                p.tariff AS Tariff,
                p.amount_kzt AS AmountKzt,
                p.currency AS Currency,
                p.provider AS Provider,
                p.environment AS Environment,
                p.terminal_id AS TerminalId,
                p.status AS Status,
                p.rrn AS Rrn,
                p.int_ref AS IntRef,
                p.merch_rn_id AS MerchRnId,
                p.created_at AS CreatedAt,
                p.paid_at AS PaidAt,
                pr.id AS RefundId,
                pr.status AS RefundStatus,
                pr.reason AS RefundReason,
                pr.amount_kzt AS RefundAmountKzt,
                pr.created_at AS RefundCreatedAt,
                pr.completed_at AS RefundCompletedAt
            FROM payments p
            LEFT JOIN sessions s ON s.id = p.session_id
            LEFT JOIN users u ON u.id = s.user_id
            LEFT JOIN leads l ON l.session_id = p.session_id
            LEFT JOIN payment_refunds pr ON pr.id = (
                SELECT pr_inner.id
                FROM payment_refunds pr_inner
                WHERE pr_inner.payment_id = p.id
                ORDER BY pr_inner.created_at DESC
                LIMIT 1
            )
            WHERE (@statusFilter IS NULL OR p.status = @statusFilter)
            ORDER BY p.created_at DESC
            LIMIT @safeLimit";

        var items = conn.Query<AdminPaymentListItemDto>(sql, new { statusFilter, safeLimit }).AsList();

        string currentGatewayEnv = gatewayEnvironment?.Trim().ToLowerInvariant() ?? "";
        string currentGatewayProvider = gatewayProvider?.Trim().ToLowerInvariant() ?? "";
        string currentGatewayTerminal = gatewayTerminalId?.Trim() ?? "";

        foreach (var item in items)
        {
            bool isPaid = string.Equals(item.Status, PaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase);
            bool envMatches = string.IsNullOrEmpty(currentGatewayEnv) ||
                              string.Equals(item.Environment?.Trim(), currentGatewayEnv, StringComparison.OrdinalIgnoreCase);
            bool providerMatches = !string.IsNullOrEmpty(currentGatewayProvider) &&
                                   string.Equals(item.Provider?.Trim(), currentGatewayProvider, StringComparison.OrdinalIgnoreCase);
            bool terminalMatches = !string.IsNullOrEmpty(currentGatewayTerminal) &&
                                   string.Equals(item.TerminalId?.Trim(), currentGatewayTerminal, StringComparison.OrdinalIgnoreCase);

            bool hasBlockingRefund = string.Equals(item.RefundStatus, PaymentRefundStatuses.Pending, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(item.RefundStatus, PaymentRefundStatuses.Succeeded, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(item.RefundStatus, PaymentRefundStatuses.ReconciliationRequired, StringComparison.OrdinalIgnoreCase);

            item.CanRefund = isPaid && envMatches && providerMatches && terminalMatches && !hasBlockingRefund;
            item.CanCheckStatus = envMatches && providerMatches && terminalMatches;
        }

        return items;
    }
}
