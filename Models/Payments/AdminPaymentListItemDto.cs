namespace FenixLegalOs.Models.Payments;

/// <summary>
/// DTO элемента списка платежей для административной панели Fenix Legal OS.
/// Содержит агрегированные данные о заказе, клиенте и операциях возврата.
/// </summary>
public sealed class AdminPaymentListItemDto
{
    public string Id { get; init; } = "";
    public string OrderId { get; init; } = "";
    public string SessionId { get; init; } = "";
    public string ClientName { get; init; } = "";
    public string ClientEmail { get; init; } = "";
    public string ClientPhone { get; init; } = "";
    public string Tariff { get; init; } = "";
    public int AmountKzt { get; init; }
    public string Currency { get; init; } = "KZT";
    public string Provider { get; init; } = "";
    public string Environment { get; init; } = "";
    public string Status { get; init; } = "";
    public string? Rrn { get; init; }
    public string? IntRef { get; init; }
    public string? MerchRnId { get; init; }
    public string CreatedAt { get; init; } = "";
    public string? PaidAt { get; init; }

    // Данные связанного возврата
    public string? RefundId { get; init; }
    public string? RefundStatus { get; init; }
    public string? RefundReason { get; init; }
    public int? RefundAmountKzt { get; init; }
    public string? RefundCreatedAt { get; init; }
    public string? RefundCompletedAt { get; init; }

    /// <summary>
    /// Признак наличия возврата (хотя бы одной записи в таблице payment_refunds).
    /// </summary>
    public bool HasRefund => !string.IsNullOrEmpty(RefundId);

    /// <summary>
    /// Признак возможности инициирования возврата в админке:
    /// - платёж имеет статус paid;
    /// - окружение платежа совпадает с окружением текущего платёжного шлюза;
    /// - отсутствует возврат со статусом pending или succeeded.
    /// Вычисляется при получении списка в контексте текущего окружения шлюза.
    /// </summary>
    public bool CanRefund { get; set; }
}
