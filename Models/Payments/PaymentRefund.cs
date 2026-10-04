using System;

namespace FenixLegalOs.Models.Payments;

/// <summary>
/// Сущность операции возврата платежа в таблице payment_refunds.
/// </summary>
public sealed class PaymentRefund
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string PaymentId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string Provider { get; set; } = "";
    public string Environment { get; set; } = "test";
    public int AmountKzt { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = PaymentRefundStatuses.Pending;
    public string? RequestTimestamp { get; set; }
    public string? Nonce { get; set; }
    public string? ActionCode { get; set; }
    public string? ResponseCode { get; set; }
    public string? Rrn { get; set; }
    public string? IntRef { get; set; }
    public string? BankMessage { get; set; }
    public string? CreatedBy { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string? CompletedAt { get; set; }
}
