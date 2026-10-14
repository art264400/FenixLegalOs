using System.Reflection;
using System.Text.Json;
using Dapper;
using FenixLegalOs.Controllers;
using FenixLegalOs.Infrastructure;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class PaymentStatusRecoveryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"fenix_recovery_{Guid.NewGuid():N}.db");
    private readonly SessionRepository _sessions;
    private readonly PaymentRepository _payments;
    private readonly UserRepository _users;
    private readonly PaymentService _service;
    private readonly Gateway _gateway = new();
    private readonly string _sessionId;

    public PaymentStatusRecoveryTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _path,
            // Recovery must work even when starting new payments is disabled.
            ["PAYMENTS_ENABLED"] = "false"
        }).Build();
        var db = new DbInitializer(config);
        db.Initialize();
        _sessions = new SessionRepository(db);
        _payments = new PaymentRepository(db);
        _users = new UserRepository(db);
        _service = new PaymentService(_sessions, new SettingsRepository(db), _payments, _gateway, _users, configuration: config);
        _sessionId = _sessions.CreateSession();
        _sessions.CompleteSession(_sessionId, "{}", new ScoreResult { Overall = 50 });
        new LeadRepository(db).CreateLead(new Lead { SessionId = _sessionId, Type = "report", Name = "Test User" });
    }

    [Theory]
    [InlineData(PaymentStatuses.Created)]
    [InlineData(PaymentStatuses.Pending)]
    public async Task Success_RecoversActivePurchaseAndAccessAtomically(string status)
    {
        var payment = AddPayment(status);
        var result = await _service.CheckActiveStatusAsync(_sessionId);

        Assert.True(Paid(result));
        Assert.False(_service.PaymentsEnabled);
        Assert.Equal((payment.OrderId, "1"), Assert.Single(_gateway.Calls));
        var stored = _payments.GetByOrderId(payment.OrderId)!;
        Assert.Equal(PaymentStatuses.Paid, stored.Status);
        Assert.Equal("RRN-test", stored.Rrn);
        Assert.Equal("REF-test", stored.IntRef);
        Assert.Equal("AUTH-test", stored.ApprovalCode);
        Assert.Equal("00", stored.ResponseCode);
        Assert.NotNull(stored.PaidAt);
        Assert.NotNull(stored.LastStatusCheckAt);
        Assert.True(_sessions.GetSession(_sessionId)!.Paid);
        using var conn = Connection();
        Assert.Equal(1, conn.QuerySingle<int>("SELECT paid FROM leads WHERE session_id = @id", new { id = _sessionId }));
    }

    [Theory]
    [InlineData(PaymentStatuses.Paid)]
    [InlineData(PaymentStatuses.Refunded)]
    [InlineData(PaymentStatuses.Failed)]
    [InlineData(PaymentStatuses.Cancelled)]
    [InlineData(PaymentStatuses.Expired)]
    public async Task TerminalPayment_NeverContactsBank(string status)
    {
        AddPayment(status);
        await _service.CheckActiveStatusAsync(_sessionId);
        Assert.Empty(_gateway.Calls);
    }

    [Fact]
    public async Task MissingPaymentOrSession_NeverContactsBank()
    {
        Assert.False(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Equal(404, (await _service.CheckActiveStatusAsync("missing")).StatusCode);
        Assert.Empty(_gateway.Calls);
    }

    [Theory]
    [InlineData(PaymentStatuses.Paid)]
    [InlineData(PaymentStatuses.Refunded)]
    public async Task OlderPaidOrRefundedPayment_BlocksNewerActiveCheck(string status)
    {
        AddPayment(status, createdAt: DateTime.UtcNow.AddMinutes(-5));
        AddPayment(PaymentStatuses.Pending);
        await _service.CheckActiveStatusAsync(_sessionId);
        Assert.Empty(_gateway.Calls);
    }

    [Fact]
    public async Task LegacyPaidSession_BlocksActiveCheck()
    {
        AddPayment(PaymentStatuses.Pending);
        using (var conn = Connection()) conn.Execute("UPDATE sessions SET paid = 1 WHERE id = @id", new { id = _sessionId });
        Assert.True(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Empty(_gateway.Calls);
    }

    [Theory]
    [InlineData("provider")]
    [InlineData("environment")]
    [InlineData("terminal")]
    [InlineData("configuration")]
    public async Task WrongGatewayContext_NeverContactsBank(string mismatch)
    {
        var payment = AddPayment();
        if (mismatch == "configuration") _gateway.IsConfigured = false;
        else
        {
            using var conn = Connection();
            string column = mismatch == "terminal" ? "terminal_id" : mismatch;
            conn.Execute($"UPDATE payments SET {column} = @value WHERE order_id = @orderId",
                new { value = mismatch == "environment" ? "production" : "other", orderId = payment.OrderId });
        }
        await _service.CheckActiveStatusAsync(_sessionId);
        Assert.Empty(_gateway.Calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("context")]
    [InlineData("wrong_type")]
    [InlineData("exception")]
    public async Task IndeterminateReply_PreservesPendingAndCooldown(string reply)
    {
        var payment = AddPayment(PaymentStatuses.Pending);
        _gateway.Respond = () => reply switch
        {
            "context" => Task.FromResult(new PaymentGatewayCheckResult { ErrorCode = "response_context_mismatch", IsFinal = true, Success = true, TrType = "1", Status = PaymentStatuses.Paid }),
            "wrong_type" => Task.FromResult(new PaymentGatewayCheckResult { IsFinal = true, Success = true, TrType = "14", Status = PaymentStatuses.Refunded }),
            "exception" => throw new HttpRequestException("offline"),
            _ => Task.FromResult(new PaymentGatewayCheckResult())
        };

        Assert.False(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.False(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Single(_gateway.Calls);
        Assert.Equal(PaymentStatuses.Pending, _payments.GetByOrderId(payment.OrderId)!.Status);
        Assert.False(_sessions.GetSession(_sessionId)!.Paid);
    }

    [Fact]
    public async Task DefinitiveDecline_DoesNotGrantAccess()
    {
        var payment = AddPayment();
        _gateway.Respond = () => Task.FromResult(new PaymentGatewayCheckResult
        {
            IsFinal = true, TrType = "1", Status = PaymentStatuses.Failed,
            ActionCode = "2", ResponseCode = "61", BankMessage = "Limit exceeded"
        });
        Assert.False(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Equal(PaymentStatuses.Failed, _payments.GetByOrderId(payment.OrderId)!.Status);
        Assert.False(_sessions.GetSession(_sessionId)!.Paid);
    }

    [Fact]
    public async Task ParallelRequests_OnlyOneBankCallThenRetryAfterCooldown()
    {
        var payment = AddPayment();
        var pending = new TaskCompletionSource<PaymentGatewayCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _gateway.Respond = () => pending.Task;
        var first = _service.CheckActiveStatusAsync(_sessionId);
        Assert.False(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Single(_gateway.Calls);
        pending.SetResult(new PaymentGatewayCheckResult());
        await first;

        using (var conn = Connection()) conn.Execute("UPDATE payments SET last_status_check_at = @time WHERE order_id = @orderId",
            new { time = DateTime.UtcNow.AddSeconds(-31).ToString("o"), orderId = payment.OrderId });
        _gateway.Respond = () => Task.FromResult(Success());
        Assert.True(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Equal(2, _gateway.Calls.Count);
    }

    [Theory]
    [InlineData("paid_callback")]
    [InlineData("refund_callback")]
    [InlineData("reopened")]
    [InlineData("newer_check")]
    public async Task SupersededCheck_DoesNotOverwriteConcurrentChanges(string change)
    {
        var payment = AddPayment(PaymentStatuses.Pending);
        _gateway.Respond = () =>
        {
            if (change is "paid_callback" or "refund_callback")
            {
                _payments.UpdateStatus(payment.OrderId, PaymentStatuses.Paid, bankMessage: "callback", paidAt: "20261014120000");
                if (change == "refund_callback") _payments.UpdateStatus(payment.OrderId, PaymentStatuses.Refunded);
            }
            else if (change == "reopened") _payments.UpdateAttempt(payment.OrderId, "new-nonce", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
            else
            {
                using var conn = Connection();
                conn.Execute("UPDATE payments SET last_status_check_at = @time WHERE order_id = @orderId",
                    new { time = DateTime.UtcNow.AddSeconds(1).ToString("o"), orderId = payment.OrderId });
            }
            return Task.FromResult(change == "paid_callback"
                ? new PaymentGatewayCheckResult { IsFinal = true, TrType = "1", Status = PaymentStatuses.Failed }
                : Success());
        };
        var result = await _service.CheckActiveStatusAsync(_sessionId);
        var stored = _payments.GetByOrderId(payment.OrderId)!;
        if (change == "paid_callback")
        {
            Assert.True(Paid(result));
            Assert.Equal("callback", stored.BankMessage);
            Assert.Equal("20261014120000", stored.PaidAt);
        }
        else
        {
            Assert.False(Paid(result));
            Assert.Equal(change == "refund_callback" ? PaymentStatuses.Refunded : PaymentStatuses.Pending, stored.Status);
        }
    }

    [Theory]
    [InlineData("old")]
    [InlineData("invalid")]
    [InlineData("future")]
    public async Task OutsideBankLookupWindow_SkipsCheckWithoutExpiringPayment(string timestamp)
    {
        var payment = AddPayment(requestTimestamp: timestamp switch
        {
            "old" => DateTime.UtcNow.AddHours(-25).ToString("yyyyMMddHHmmss"),
            "future" => DateTime.UtcNow.AddHours(1).ToString("yyyyMMddHHmmss"),
            _ => "bad"
        });
        await _service.CheckActiveStatusAsync(_sessionId);
        Assert.Empty(_gateway.Calls);
        Assert.Equal(PaymentStatuses.Created, _payments.GetByOrderId(payment.OrderId)!.Status);
    }

    [Fact]
    public async Task ReopenedRecentAttempt_CanBeCheckedEvenIfOrderWasCreatedEarlier()
    {
        AddPayment(createdAt: DateTime.UtcNow.AddDays(-2));
        Assert.True(Paid(await _service.CheckActiveStatusAsync(_sessionId)));
        Assert.Single(_gateway.Calls);
    }

    [Fact]
    public async Task OlderActiveAttempt_IsNotCheckedInsteadOfLatestFailedPayment()
    {
        AddPayment(PaymentStatuses.Pending, createdAt: DateTime.UtcNow.AddMinutes(-5));
        AddNewerFailedHistory();
        await _service.CheckActiveStatusAsync(_sessionId);
        Assert.Empty(_gateway.Calls);
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("reopened")]
    [InlineData("latest")]
    public void Claim_RevalidatesSnapshotBeforeCallingBank(string change)
    {
        var snapshot = AddPayment();
        if (change == "callback") _payments.UpdateStatus(snapshot.OrderId, PaymentStatuses.Paid);
        else if (change == "reopened") _payments.UpdateAttempt(snapshot.OrderId, "new-nonce", DateTime.UtcNow.ToString("yyyyMMddHHmmss"));
        else AddNewerFailedHistory();
        var now = DateTime.UtcNow;
        Assert.False(_payments.TryClaimActiveStatusCheck(snapshot, now, now.AddSeconds(-30)));
    }

    [Fact]
    public async Task Controller_CheckEndpointEnforcesSessionOwnership()
    {
        AddPayment();
        var owner = _users.CreateUser($"owner-{Guid.NewGuid():N}@example.com", "password123", "Test User", "Test Co", "CEO", "+77001234567");
        _users.AttachUserToSession(_sessionId, owner.Id);
        using var services = new ServiceCollection().AddSingleton(_sessions).AddSingleton(_users).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        var controller = new PaymentsController(_service) { ControllerContext = new ControllerContext { HttpContext = http } };
        var filter = typeof(PaymentsController).GetMethod(nameof(PaymentsController.CheckActivePaymentStatus))!
            .GetCustomAttribute<RequireSessionAccessAttribute>()!;
        var route = new RouteData();
        route.Values[filter.RouteParamName] = _sessionId;
        var context = new ActionExecutingContext(new ActionContext(http, route, new ActionDescriptor()),
            new List<IFilterMetadata>(), new Dictionary<string, object?>(), controller);
        await filter.OnActionExecutionAsync(context, async () =>
        {
            await controller.CheckActivePaymentStatus(_sessionId);
            return new ActionExecutedContext(context, new List<IFilterMetadata>(), controller);
        });
        Assert.Equal(403, Assert.IsType<ObjectResult>(context.Result).StatusCode);
        Assert.Empty(_gateway.Calls);
    }

    // Seed legacy history without violating the unique-active-payment constraint.
    private void AddNewerFailedHistory()
    {
        var active = _payments.GetLatestBySessionId(_sessionId)!;
        _payments.UpdateStatus(active.OrderId, PaymentStatuses.Failed);
        AddPayment(PaymentStatuses.Failed, createdAt: DateTime.UtcNow.AddSeconds(1));
        using var conn = Connection();
        conn.Execute("UPDATE payments SET status = @status WHERE order_id = @orderId",
            new { status = active.Status, orderId = active.OrderId });
    }

    private Payment AddPayment(string status = PaymentStatuses.Created, DateTime? createdAt = null, string? requestTimestamp = null)
    {
        var payment = new Payment
        {
            SessionId = _sessionId, OrderId = Guid.NewGuid().ToString("N"), AmountKzt = 49990,
            Provider = "bcc", Environment = "test", TerminalId = "88888881", Status = PaymentStatuses.Created,
            Nonce = Guid.NewGuid().ToString("N"), RequestTimestamp = requestTimestamp ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
            CreatedAt = (createdAt ?? DateTime.UtcNow).ToString("o")
        };
        _payments.Create(payment);
        if (status == PaymentStatuses.Refunded) _payments.UpdateStatus(payment.OrderId, PaymentStatuses.Paid);
        if (status != PaymentStatuses.Created) Assert.True(_payments.UpdateStatus(payment.OrderId, status));
        return _payments.GetByOrderId(payment.OrderId)!;
    }

    private static bool Paid(PaymentServiceResult result)
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value));
        return json.RootElement.GetProperty("paid").GetBoolean();
    }

    private SqliteConnection Connection()
    {
        var conn = new SqliteConnection($"Data Source={_path}");
        conn.Open();
        return conn;
    }

    private static PaymentGatewayCheckResult Success() => new()
    {
        IsFinal = true, Success = true, TrType = "1", Status = PaymentStatuses.Paid,
        ActionCode = "0", ResponseCode = "00", Rrn = "RRN-test", IntRef = "REF-test", ApprovalCode = "AUTH-test"
    };

    private sealed class Gateway : IPaymentGateway
    {
        public string Provider => "bcc";
        public string Environment => "test";
        public string TerminalId => "88888881";
        public bool IsConfigured { get; set; } = true;
        public List<(string OrderId, string Type)> Calls { get; } = new();
        public Func<Task<PaymentGatewayCheckResult>> Respond { get; set; } = () => Task.FromResult(Success());
        public Task<PaymentGatewayCheckResult> CheckStatusAsync(string orderId, CancellationToken cancellationToken = default, string tranTrType = "14")
        {
            Calls.Add((orderId, tranTrType));
            return Respond();
        }
        public Task<PaymentGatewayInitResult> CreatePaymentAsync(PaymentGatewayInitRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Recovery must not create a purchase.");
        public Task<PaymentGatewayRefundResult> RefundAsync(PaymentGatewayRefundRequest request, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Recovery must not refund a purchase.");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" }) if (File.Exists(_path + suffix)) File.Delete(_path + suffix);
    }
}
