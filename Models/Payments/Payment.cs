using System;

namespace FenixLegalOs.Models.Payments;

public sealed class Payment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SessionId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string Tariff { get; set; } = "report";
    public int AmountKzt { get; set; }
    public string Currency { get; set; } = "KZT";
    public string Provider { get; set; } = "";
    public string Environment { get; set; } = "test";
    public string? TerminalId { get; set; }
    public string Status { get; set; } = PaymentStatuses.Created;
    public string? Nonce { get; set; }
    public string? RequestTimestamp { get; set; }
    public string? ProviderMetadata { get; set; }
    public string? Rrn { get; set; }
    public string? IntRef { get; set; }
    public string? ApprovalCode { get; set; }
    public string? ActionCode { get; set; }
    public string? ResponseCode { get; set; }
    public string? MerchantAdviceCode { get; set; }
    public string? BankMessage { get; set; }
    public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string UpdatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    public string? PaidAt { get; set; }
    public string? NotificationReceivedAt { get; set; }
    public string? LastStatusCheckAt { get; set; }
}
