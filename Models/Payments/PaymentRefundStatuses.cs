namespace FenixLegalOs.Models.Payments;

/// <summary>
/// Статусы жизненного цикла операции возврата платежа (payment_refunds).
/// </summary>
public static class PaymentRefundStatuses
{
    public const string Pending = "pending";
    public const string Succeeded = "succeeded";
    public const string Failed = "failed";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Succeeded,
        Failed
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);
}
