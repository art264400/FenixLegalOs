namespace FenixLegalOs.Models.Payments;

public sealed class CheckoutDescriptorDto
{
    public string Type { get; init; } = "form_post"; // form_post, redirect, qr
    public string ActionUrl { get; init; } = "";
    public string Method { get; init; } = "POST";
    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>();
}

public sealed class ActivePaymentDto
{
    public string OrderId { get; init; } = "";
    public string Tariff { get; init; } = "report";
    public int AmountKzt { get; init; }
    public string Currency { get; init; } = "KZT";
    public string Status { get; init; } = PaymentStatuses.Created;
    public string Provider { get; init; } = "";
    public string Environment { get; init; } = "test";
    public CheckoutDescriptorDto? Checkout { get; init; }
}

public sealed class PaymentStatusResponse
{
    public string Provider { get; init; } = "";
    public string Environment { get; init; } = "test";
    public string SessionId { get; init; } = "";
    public string? OrderId { get; init; }
    public bool Paid { get; init; }
    public string Status { get; init; } = "not_started";
    public string? PaidAt { get; init; }
    public int? AmountKzt { get; init; }
    public string? Tariff { get; init; }
    public string? Method { get; init; }
}

public sealed class PaymentServiceResult
{
    public int StatusCode { get; init; }
    public object? Value { get; init; }

    public static PaymentServiceResult Ok(object value) =>
        new() { StatusCode = 200, Value = value };

    public static PaymentServiceResult BadRequest(string error, string message) =>
        new() { StatusCode = 400, Value = new { error, message } };

    public static PaymentServiceResult NotFound(string error) =>
        new() { StatusCode = 404, Value = new { error } };

    public static PaymentServiceResult Conflict(object value) =>
        new() { StatusCode = 409, Value = value };

    public static PaymentServiceResult ServiceUnavailable(object value) =>
        new() { StatusCode = 503, Value = value };
}
