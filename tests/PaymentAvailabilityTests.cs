using System.Text.Json;
using FenixLegalOs.Controllers;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class PaymentAvailabilityTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"fenix_payment_availability_{Guid.NewGuid():N}.db");
    private readonly SessionRepository _sessions;
    private readonly PaymentRepository _payments;
    private readonly SettingsRepository _settings;
    private readonly UserRepository _users;

    public PaymentAvailabilityTests()
    {
        var db = new DbInitializer(Config(new Dictionary<string, string?> { ["FENIX_DB_PATH"] = _databasePath }));
        db.Initialize();
        _sessions = new SessionRepository(db);
        _payments = new PaymentRepository(db);
        _settings = new SettingsRepository(db);
        _users = new UserRepository(db);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData(" FALSE ")]
    [InlineData("invalid")]
    [InlineData("1")]
    public async Task DisabledOrMissingSwitch_BlocksConfiguredGatewayWithoutMutation(string? flag)
    {
        string sessionId = CompletedSession();
        var before = _sessions.GetSession(sessionId)!;
        var gateway = new SpyPaymentGateway();
        var service = Service(gateway, flag, omitConfiguration: flag == null);
        var controller = new PaymentsController(service) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

        // A stale or manually crafted request must not reach validation or the bank while disabled.
        var response = Assert.IsType<ObjectResult>(await controller.StartPayment(sessionId, null));

        Assert.Equal(503, response.StatusCode);
        using var payload = Json(response.Value);
        Assert.Equal("payment_disabled", payload.RootElement.GetProperty("error").GetString());
        Assert.Contains("недоступна", payload.RootElement.GetProperty("message").GetString());
        Assert.False(service.PaymentsEnabled);
        Assert.Equal(0, gateway.CreateCalls);
        Assert.Empty(_payments.GetBySessionId(sessionId));
        var after = _sessions.GetSession(sessionId)!;
        Assert.False(after.Paid);
        Assert.Equal(before.AnswersJson, after.AnswersJson);
        Assert.Equal(before.ResultJson, after.ResultJson);
        Assert.Equal(before.CompletedAt, after.CompletedAt);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
    }

    [Theory]
    [InlineData("true")]
    [InlineData(" TRUE ")]
    public async Task ExplicitTrue_PreservesNormalPaymentCreation(string flag)
    {
        string sessionId = CompletedSession();
        var gateway = new SpyPaymentGateway();
        var service = Service(gateway, flag);

        var response = await Start(service, sessionId);

        Assert.Equal(200, response.StatusCode);
        Assert.True(service.PaymentsEnabled);
        Assert.Equal(1, gateway.CreateCalls);
        var payment = Assert.Single(_payments.GetBySessionId(sessionId));
        Assert.Equal(49990, payment.AmountKzt);
        Assert.Equal(PaymentStatuses.Created, payment.Status);
        Assert.False(_sessions.GetSession(sessionId)!.Paid);
    }

    [Theory]
    [InlineData(PaymentStatuses.Created)]
    [InlineData(PaymentStatuses.Pending)]
    public async Task DisabledSwitch_DoesNotReopenOrMutateActiveAttempt(string status)
    {
        string sessionId = CompletedSession();
        var before = ActivePayment(sessionId, "ORD-DISABLED-ACTIVE", status);
        var gateway = new SpyPaymentGateway();
        var service = Service(gateway, "false");

        Assert.Equal(503, (await Start(service, sessionId)).StatusCode);

        Assert.Equal(0, gateway.CreateCalls);
        var after = Assert.Single(_payments.GetBySessionId(sessionId));
        Assert.Equal(before.OrderId, after.OrderId);
        Assert.Equal(before.Status, after.Status);
        Assert.Equal(before.Nonce, after.Nonce);
        Assert.Equal(before.RequestTimestamp, after.RequestTimestamp);
        Assert.Equal(before.MerchRnId, after.MerchRnId);
        Assert.Equal(before.ProviderMetadata, after.ProviderMetadata);
        Assert.Equal(before.UpdatedAt, after.UpdatedAt);
        using var statusPayload = Json(service.GetStatus(sessionId).Value);
        Assert.Equal(status, statusPayload.RootElement.GetProperty("status").GetString());
        Assert.False(statusPayload.RootElement.GetProperty("paid").GetBoolean());
    }

    [Fact]
    public async Task DisabledSwitch_PreservesPaidSessionAndSavedReport()
    {
        string sessionId = CompletedSession();
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");
        var savedPdf = new byte[] { 37, 80, 68, 70, 45, 49, 46, 55 };
        Assert.True(_sessions.SavePdf(sessionId, savedPdf));
        var gateway = new SpyPaymentGateway();
        var service = Service(gateway, "false");

        var response = await Start(service, sessionId);

        Assert.Equal(409, response.StatusCode);
        using var error = Json(response.Value);
        Assert.Equal("already_paid", error.RootElement.GetProperty("error").GetString());
        using var status = Json(service.GetStatus(sessionId).Value);
        Assert.True(status.RootElement.GetProperty("paid").GetBoolean());
        Assert.Equal(PaymentStatuses.Paid, status.RootElement.GetProperty("status").GetString());
        Assert.Equal(savedPdf, _sessions.GetPdf(sessionId));
        Assert.Equal(0, gateway.CreateCalls);
    }

    [Fact]
    public async Task DisabledSwitch_ExistingPendingPaymentCanStillBeConfirmedByBank()
    {
        string sessionId = CompletedSession();
        const string orderId = "ORD-DISABLED-CALLBACK";
        ActivePayment(sessionId, orderId, PaymentStatuses.Pending);
        var gateway = new SpyPaymentGateway();
        var service = Service(gateway, "false");
        Assert.Equal(503, (await Start(service, sessionId)).StatusCode);
        var options = new BccPaymentOptions
        {
            Environment = "test", TerminalId = "TID999", GatewayUrl = "https://example.com/pay",
            NotifyUrl = "https://example.com/api/payments/bcc/notify", ReturnUrl = "https://example.com/api/payments/bcc/return",
            MerchantId = "test-merchant", MerchantName = "Fenix tests", MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            NotifyUsername = "test_callback_user", NotifyPassword = "test_callback_password"
        };
        var callback = new BccCallbacksController(new BccNotificationService(_payments, options: options))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        callback.HttpContext.Request.Headers.Authorization = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("test_callback_user:test_callback_password"));
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["ORDER"] = orderId, ["AMOUNT"] = "49990.00", ["CURRENCY"] = "398", ["TERMINAL"] = "TID999",
            ["TRTYPE"] = "1", ["ACTION"] = "0", ["RC"] = "00", ["RRN"] = "123456789012", ["INT_REF"] = "INT_TEST"
        });

        Assert.IsType<OkObjectResult>(await callback.Notify(form));

        Assert.Equal(PaymentStatuses.Paid, _payments.GetByOrderId(orderId)!.Status);
        Assert.True(_sessions.GetSession(sessionId)!.Paid);
        using var status = Json(service.GetStatus(sessionId).Value);
        Assert.True(status.RootElement.GetProperty("paid").GetBoolean());
        Assert.Equal(0, gateway.CreateCalls);
    }

    [Theory]
    [InlineData("true", true, true)]
    [InlineData("true", false, false)]
    [InlineData("false", true, false)]
    [InlineData(null, true, false)]
    [InlineData("invalid", true, false)]
    public void PublicPricing_PreservesPricesAndExposesOnlyEffectiveAvailability(string? flag, bool configured, bool expected)
    {
        _settings.UpdatePricing(35000, 42000, 79900);
        var gateway = new SpyPaymentGateway(configured);
        var service = Service(gateway, flag);
        var controller = new SessionsController(null!, null!, null!, null!, null!, _settings, null!, paymentService: service);

        using var payload = Json(Assert.IsType<OkObjectResult>(controller.GetPricing()).Value);
        var data = payload.RootElement;
        Assert.Equal(35000, data.GetProperty("priceKzt").GetInt32());
        Assert.Equal(42000, data.GetProperty("oldPriceKzt").GetInt32());
        Assert.Equal(79900, data.GetProperty("consultationPriceKzt").GetInt32());
        Assert.Equal("₸", data.GetProperty("currency").GetString());
        Assert.Equal(17, data.GetProperty("discountPercent").GetInt32());
        Assert.Equal(expected, data.GetProperty("paymentsEnabled").GetBoolean());
        Assert.Equal(expected, service.PaymentsEnabled);
        Assert.Equal(new[] { "consultationPriceKzt", "currency", "discountPercent", "oldPriceKzt", "paymentsEnabled", "priceKzt" }, data.EnumerateObject().Select(p => p.Name).OrderBy(n => n).ToArray());
        Assert.Equal(0, gateway.CreateCalls);
        var cache = Assert.IsType<ResponseCacheAttribute>(Attribute.GetCustomAttribute(typeof(SessionsController).GetMethod(nameof(SessionsController.GetPricing))!, typeof(ResponseCacheAttribute)));
        Assert.True(cache.NoStore);
    }

    [Fact]
    public void PublicPricing_WithoutPaymentService_FailsClosed()
    {
        var controller = new SessionsController(null!, null!, null!, null!, null!, _settings, null!);
        using var payload = Json(Assert.IsType<OkObjectResult>(controller.GetPricing()).Value);
        Assert.False(payload.RootElement.GetProperty("paymentsEnabled").GetBoolean());
    }

    private PaymentService Service(SpyPaymentGateway gateway, string? flag, bool omitConfiguration = false) => new(
        _sessions, _settings, _payments, gateway, _users, configuration: omitConfiguration ? null : Config(new Dictionary<string, string?> { ["PAYMENTS_ENABLED"] = flag }));

    private static Task<PaymentServiceResult> Start(PaymentService service, string sessionId) =>
        service.StartAsync(sessionId, "report", 1080, 1920, "127.0.0.1", "Test billing address");

    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static JsonDocument Json(object? value) => JsonDocument.Parse(JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

    private string CompletedSession()
    {
        string sessionId = _sessions.CreateSession();
        Assert.True(_sessions.CompleteSession(sessionId, "{}", new ScoreResult { Overall = 50 }));
        var user = _users.CreateUser($"test_{Guid.NewGuid():N}@example.com", "password123", "Test User", "Test Co", "CEO", "+77001234567");
        _users.AttachUserToSession(sessionId, user.Id);
        return sessionId;
    }

    private Payment ActivePayment(string sessionId, string orderId, string status)
    {
        _payments.Create(new Payment
        {
            SessionId = sessionId, OrderId = orderId, Tariff = "report", AmountKzt = 49990, Currency = "KZT",
            Provider = "bcc", Environment = "test", TerminalId = "TID999", Status = PaymentStatuses.Created,
            Nonce = "original-nonce", RequestTimestamp = "20261013000000", MerchRnId = "original-reference",
            ProviderMetadata = "{\"source\":\"original\"}"
        });
        if (status == PaymentStatuses.Pending) _payments.UpdateStatus(orderId, PaymentStatuses.Pending);
        return _payments.GetByOrderId(orderId)!;
    }

    private sealed class SpyPaymentGateway(bool configured = true) : IPaymentGateway
    {
        public int CreateCalls { get; private set; }
        public string Provider => "bcc";
        public string Environment => "test";
        public string TerminalId => "TID999";
        public bool IsConfigured => configured;

        public Task<PaymentGatewayInitResult> CreatePaymentAsync(PaymentGatewayInitRequest request, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            var orderId = request.OrderId ?? "SPY-" + Guid.NewGuid().ToString("N");
            return Task.FromResult(new PaymentGatewayInitResult
            {
                Success = true, OrderId = orderId, PaymentUrl = "https://example.com/pay", TerminalId = TerminalId,
                Nonce = "new-nonce", RequestTimestamp = "20261013010101", MerchRnId = "new-reference",
                FormFields = new Dictionary<string, string> { ["ORDER"] = orderId }
            });
        }

        public Task<PaymentGatewayCheckResult> CheckStatusAsync(string orderId, CancellationToken cancellationToken = default, string tranTrType = "14") => throw new InvalidOperationException("Unexpected bank status call.");
        public Task<PaymentGatewayRefundResult> RefundAsync(PaymentGatewayRefundRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected bank refund call.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = _databasePath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
