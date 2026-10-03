using System;
using System.Collections.Generic;

namespace FenixLegalOs.Models.Payments;

public static class PaymentStatuses
{
    public const string Created = "created";
    public const string Pending = "pending";
    public const string Paid = "paid";
    public const string Failed = "failed";
    public const string Cancelled = "cancelled";
    public const string Refunded = "refunded";
    public const string Expired = "expired";
    public const string Unknown = "unknown";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        Created,
        Pending,
        Paid,
        Failed,
        Cancelled,
        Refunded,
        Expired,
        Unknown
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status);

    public static bool CanTransition(string fromStatus, string toStatus)
    {
        if (!IsValid(fromStatus) || !IsValid(toStatus))
            return false;

        string from = fromStatus.ToLowerInvariant();
        string to = toStatus.ToLowerInvariant();

        if (from == to)
            return true; // Идемпотентный повтор

        return from switch
        {
            Created => to is Pending or Paid or Failed or Cancelled or Expired,
            Pending => to is Paid or Failed or Cancelled or Expired,
            Paid => to is Refunded, // Оплаченный статус нельзя вернуть в pending, failed, cancelled и т.д.
            Failed => to is Paid, // Допоздний приход нотификации об успешном списании
            Cancelled => to is Paid,
            Expired => to is Paid,
            Refunded => false, // Возврат — финальное состояние
            Unknown => true,
            _ => false
        };
    }
}
