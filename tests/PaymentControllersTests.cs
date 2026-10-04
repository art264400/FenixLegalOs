using System.Text.Json;
using Dapper;
using FenixLegalOs.Controllers;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Options;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class PaymentControllersTests : IDisposable
{
    private readonly string _databasePath;
    private readonly SessionRepository _sessions;
    private readonly PaymentRepository _paymentRepo;
    private readonly SettingsRepository _settings;
    private readonly UserRepository _userRepo;
    private readonly PaymentService _paymentService;
    private readonly PaymentsController _payments;

    public PaymentControllersTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_fenix_payments_{Guid.NewGuid():N}.db");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FENIX_DB_PATH"] = _databasePath
            })
            .Build();

        var database = new DbInitializer(configuration);
        database.Initialize();
        _sessions = new SessionRepository(database);
        _paymentRepo = new PaymentRepository(database);
        _settings = new SettingsRepository(database);
        _userRepo = new UserRepository(database);
        var gateway = new BccPaymentGateway(configuration);
        _paymentService = new PaymentService(_sessions, _settings, _paymentRepo, gateway, _userRepo);
        _payments = new PaymentsController(_paymentService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };
    }

    [Fact(DisplayName = "Payment environment is read explicitly from configuration")]
    public async Task PaymentEnvironment_ReadFromConfiguration()
    {
        var defaultPayment = new Payment();
        Assert.Equal("test", defaultPayment.Environment);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test"
            })
            .Build();
        var gateway = new BccPaymentGateway(config);
        var paymentService = new PaymentService(_sessions, _settings, _paymentRepo, gateway, _userRepo);
        var payments = new PaymentsController(paymentService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string sessionId = CreateCompletedSession();
        var action = await payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var unavailable = Assert.IsType<ObjectResult>(action);
        string json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("\"environment\":\"test\"", json);
    }

    [Fact(DisplayName = "Payment start fails closed until BCC is configured")]
    public async Task StartPayment_WhenBccIsNotConfigured_Returns503WithoutUnlockingReport()
    {
        string sessionId = CreateCompletedSession();

        var action = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "consultation",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });

        var unavailable = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        Assert.False(_sessions.GetSession(sessionId)!.Paid);

        string json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("payment_gateway_not_configured", json);
        Assert.Contains("90990", json);
    }

    [Fact(DisplayName = "Payment amount cannot be supplied by the browser")]
    public async Task StartPayment_UsesOnlyKnownTariffs()
    {
        string sessionId = CreateCompletedSession();

        var action = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "free",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.IsType<BadRequestObjectResult>(action);
        Assert.False(_sessions.GetSession(sessionId)!.Paid);
    }

    [Fact(DisplayName = "Payment status reflects stored session state")]
    public void GetPaymentStatus_ReturnsCurrentPaidState()
    {
        string sessionId = CreateCompletedSession();

        var unpaid = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        Assert.Contains("not_started", JsonSerializer.Serialize(unpaid.Value));

        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        var paid = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string paidJson = JsonSerializer.Serialize(paid.Value);
        Assert.Contains("\"paid\":true", paidJson);
        Assert.Contains("\"tariff\":null", paidJson);
    }

    [Fact(DisplayName = "Unconfigured BCC notification cannot mutate payment state")]
    public async Task Notify_WhenNotConfigured_Returns503()
    {
        var service = new BccNotificationService(_paymentRepo);
        var controller = new BccCallbacksController(service);

        var action = await controller.Notify();

        var unavailable = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
    }

    [Fact(DisplayName = "BCC browser return only redirects to results")]
    public void ReturnToMerchant_DoesNotConfirmPayment()
    {
        var service = new BccNotificationService(_paymentRepo);
        var controller = new BccCallbacksController(service);

        var action = Assert.IsType<RedirectResult>(controller.ReturnToMerchant());

        Assert.Equal("/#/results", action.Url);
    }


    [Fact(DisplayName = "Payment repository can insert, retrieve and order payments")]
    public void PaymentRepository_CanInsertAndRetrievePayments()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = "ORD-TEST-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            Currency = "KZT",
            Environment = "test",
            TerminalId = "TID123",
            Status = PaymentStatuses.Created,
            Nonce = "abc-123",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };

        _paymentRepo.Create(payment);

        var retrieved = _paymentRepo.GetByOrderId("ORD-TEST-001");
        Assert.NotNull(retrieved);
        Assert.Equal(payment.Id, retrieved.Id);
        Assert.Equal(payment.SessionId, retrieved.SessionId);
        Assert.Equal(49990, retrieved.AmountKzt);
        Assert.Equal(PaymentStatuses.Created, retrieved.Status);

        var bySession = _paymentRepo.GetBySessionId(sessionId);
        Assert.Single(bySession);
        Assert.Equal("ORD-TEST-001", bySession[0].OrderId);

        var statusResult = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string statusJson = JsonSerializer.Serialize(statusResult.Value);
        Assert.Contains("ORD-TEST-001", statusJson);
        Assert.Contains("created", statusJson);
    }

    [Fact(DisplayName = "Payment repository updates status and bank notification fields")]
    public void PaymentRepository_CanUpdateStatusAndBankDetails()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-TEST-002",
            Tariff = "consultation",
            AmountKzt = 90990,
            Provider = "bcc",
            TerminalId = "TID123",
            Nonce = "nonce-2",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);

        string now = DateTime.UtcNow.ToString("o");
        bool updated = _paymentRepo.UpdateStatus(
            orderId: "ORD-TEST-002",
            status: PaymentStatuses.Paid,
            rrn: "123456789012",
            approvalCode: "APP001",
            responseCode: "00",
            paidAt: now,
            notificationReceivedAt: now);

        Assert.True(updated);

        var updatedPayment = _paymentRepo.GetByOrderId("ORD-TEST-002");
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatuses.Paid, updatedPayment.Status);
        Assert.Equal("123456789012", updatedPayment.Rrn);
        Assert.Equal("APP001", updatedPayment.ApprovalCode);
        Assert.Equal("00", updatedPayment.ResponseCode);
        Assert.Equal(now, updatedPayment.PaidAt);
    }

    [Fact(DisplayName = "Duplicate order_id is rejected by database constraint")]
    public void PaymentRepository_DuplicateOrderIdThrowsException()
    {
        string sessionId = CreateCompletedSession();
        var p1 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-DUP-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID123",
            Nonce = "nonce-a",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(p1);

        var p2 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-DUP-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID123",
            Nonce = "nonce-b",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };

        Assert.Throws<SqliteException>(() => _paymentRepo.Create(p2));
    }

    [Fact(DisplayName = "Payment cannot be created for non-existent session_id (foreign key enforced)")]
    public void PaymentRepository_NonExistentSessionId_ThrowsForeignKeyException()
    {
        string nonExistentSessionId = Guid.NewGuid().ToString();
        var payment = new Payment
        {
            SessionId = nonExistentSessionId,
            OrderId = "ORD-FK-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID123",
            Nonce = "nonce-fk",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };

        var ex = Assert.Throws<SqliteException>(() => _paymentRepo.Create(payment));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "Session cannot be deleted while payments exist (ON DELETE RESTRICT)")]
    public void PaymentRepository_DeleteSessionWithPayment_IsBlockedByRestrict()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-FK-RESTRICT-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID123",
            Nonce = "nonce-restrict",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);

        using var conn = new SqliteConnection($"Data Source={_databasePath};Foreign Keys=True;");
        conn.Open();
        var ex = Assert.Throws<SqliteException>(() =>
            Dapper.SqlMapper.Execute(conn, "DELETE FROM sessions WHERE id = @sessionId", new { sessionId }));
        Assert.Contains("FOREIGN KEY", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "When an earlier attempt was paid, status and details always match the successful payment, not a newer pending attempt")]
    public void GetPaymentStatus_WhenOlderAttemptPaidAndNewerPending_ReturnsSuccessfulPaymentDetails()
    {
        string sessionId = CreateCompletedSession();

        // 1. Успешно оплаченная попытка (тариф report, 49990)
        var paidPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-SUCCESS-001",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-1",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-10).ToString("o")
        };
        _paymentRepo.Create(paidPayment);
        _paymentRepo.UpdateStatus("ORD-SUCCESS-001", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddMinutes(-9).ToString("o"));

        // 2. Более поздняя незавершённая попытка (например, pending на тариф consultation, 90990)
        var pendingPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-NEWER-PENDING-002",
            Tariff = "consultation",
            AmountKzt = 90990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-2",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(pendingPayment);
        _paymentRepo.UpdateStatus("ORD-NEWER-PENDING-002", PaymentStatuses.Pending);

        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        var result = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string json = JsonSerializer.Serialize(result.Value);

        // Данные должны быть строго согласованы с успешной оплатой
        Assert.Contains("\"orderId\":\"ORD-SUCCESS-001\"", json);
        Assert.Contains("\"status\":\"paid\"", json);
        Assert.Contains("\"paid\":true", json);
        Assert.Contains("\"amountKzt\":49990", json);
        Assert.Contains("\"tariff\":\"report\"", json);
        Assert.DoesNotContain("ORD-NEWER-PENDING-002", json);
        Assert.DoesNotContain("90990", json);
    }

    [Fact(DisplayName = "UpdateStatus rejects arbitrary/invalid status string")]
    public void UpdateStatus_InvalidStatusString_ThrowsArgumentException()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-STATUS-INVALID-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);

        Assert.Throws<ArgumentException>(() => _paymentRepo.UpdateStatus("ORD-STATUS-INVALID-01", "malicious_status"));
    }

    [Fact(DisplayName = "UpdateStatus protects paid status from being overwritten by delayed pending or failed notification")]
    public void UpdateStatus_WhenAlreadyPaid_PreventsDowngradeToPendingOrFailed()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-PROTECT-PAID-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-5).ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus("ORD-PROTECT-PAID-01", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddMinutes(-4).ToString("o"));

        string checkTimestamp = DateTime.UtcNow.ToString("o");

        // Попытка перевести уже оплаченный платёж в 'pending'
        bool pendingAttempt = _paymentRepo.UpdateStatus("ORD-PROTECT-PAID-01", PaymentStatuses.Pending, lastStatusCheckAt: checkTimestamp);
        Assert.False(pendingAttempt);

        // Попытка перевести уже оплаченный платёж в 'failed'
        bool failedAttempt = _paymentRepo.UpdateStatus("ORD-PROTECT-PAID-01", PaymentStatuses.Failed, lastStatusCheckAt: checkTimestamp);
        Assert.False(failedAttempt);

        // Статус должен остаться строго 'paid', а метка проверки обновлена
        var stored = _paymentRepo.GetByOrderId("ORD-PROTECT-PAID-01");
        Assert.NotNull(stored);
        Assert.Equal(PaymentStatuses.Paid, stored.Status);
        Assert.Equal(checkTimestamp, stored.LastStatusCheckAt);

        // Переход в 'refunded' при этом разрешён
        bool refundAttempt = _paymentRepo.UpdateStatus("ORD-PROTECT-PAID-01", PaymentStatuses.Refunded);
        Assert.True(refundAttempt);

        var refunded = _paymentRepo.GetByOrderId("ORD-PROTECT-PAID-01");
        Assert.NotNull(refunded);
        Assert.Equal(PaymentStatuses.Refunded, refunded.Status);
    }

    [Fact(DisplayName = "Duplicate/idempotent paid notification preserves original paid_at timestamp")]
    public void UpdateStatus_DuplicatePaidNotification_PreservesOriginalPaidAt()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-IDEMPOTENT-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-1",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);

        string originalPaidAt = DateTime.UtcNow.AddMinutes(-30).ToString("o");

        // 1. Первая успешная нотификация
        bool firstPaid = _paymentRepo.UpdateStatus(
            orderId: "ORD-IDEMPOTENT-01",
            status: PaymentStatuses.Paid,
            paidAt: originalPaidAt,
            rrn: "111111111111");
        Assert.True(firstPaid);

        var firstCheck = _paymentRepo.GetByOrderId("ORD-IDEMPOTENT-01");
        Assert.NotNull(firstCheck);
        Assert.Equal(originalPaidAt, firstCheck.PaidAt);

        // 2. Вторая (повторная) нотификация без передачи paidAt
        bool secondPaid = _paymentRepo.UpdateStatus(
            orderId: "ORD-IDEMPOTENT-01",
            status: PaymentStatuses.Paid,
            rrn: "111111111111",
            notificationReceivedAt: DateTime.UtcNow.ToString("o"));
        Assert.True(secondPaid);

        // Первоначальный paid_at обязан сохраниться без изменений
        var secondCheck = _paymentRepo.GetByOrderId("ORD-IDEMPOTENT-01");
        Assert.NotNull(secondCheck);
        Assert.Equal(originalPaidAt, secondCheck.PaidAt);

        // 3. Третья попытка, куда вызывающий код передал новую дату оплаты
        bool thirdPaid = _paymentRepo.UpdateStatus(
            orderId: "ORD-IDEMPOTENT-01",
            status: PaymentStatuses.Paid,
            paidAt: DateTime.UtcNow.AddYears(1).ToString("o"),
            rrn: "111111111111");
        Assert.True(thirdPaid);

        var thirdCheck = _paymentRepo.GetByOrderId("ORD-IDEMPOTENT-01");
        Assert.NotNull(thirdCheck);
        Assert.Equal(originalPaidAt, thirdCheck.PaidAt);
    }

    [Fact(DisplayName = "When payment is refunded, GetPaymentStatus returns status=refunded and paid=false even if session was previously paid")]
    public void GetPaymentStatus_WhenPaymentRefunded_ReturnsRefundedStatusAndPaidFalse()
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-REFUND-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-1",
            RequestTimestamp = DateTime.UtcNow.AddHours(-1).ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus("ORD-REFUND-01", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddHours(-1).ToString("o"));
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        // Платёж переведён в refunded
        bool refunded = _paymentRepo.UpdateStatus("ORD-REFUND-01", PaymentStatuses.Refunded);
        Assert.True(refunded);

        var actionResult = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string json = JsonSerializer.Serialize(actionResult.Value);

        Assert.Contains("\"status\":\"refunded\"", json);
        Assert.Contains("\"paid\":false", json);
        Assert.Contains("\"orderId\":\"ORD-REFUND-01\"", json);

        // Сессия также должна быть отозвана (Option A)
        var updatedSession = _sessions.GetSession(sessionId);
        Assert.NotNull(updatedSession);
        Assert.False(updatedSession.Paid);
        Assert.Equal("refunded", updatedSession.PaymentMethod);
    }

    [Fact(DisplayName = "Refund status is preserved even if followed by a newer failed or pending attempt")]
    public void GetPaymentStatus_WhenRefundedFollowedByNewerFailedAttempt_ReturnsRefundedStatusAndPaidFalse()
    {
        string sessionId = CreateCompletedSession();

        // 1. Первая попытка была оплачена и затем возвращена
        var firstPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-REFUND-PREV-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-1",
            RequestTimestamp = DateTime.UtcNow.AddHours(-2).ToString("o")
        };
        _paymentRepo.Create(firstPayment);
        _paymentRepo.UpdateStatus("ORD-REFUND-PREV-01", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddHours(-2).ToString("o"));
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        bool refunded = _paymentRepo.UpdateStatus("ORD-REFUND-PREV-01", PaymentStatuses.Refunded);
        Assert.True(refunded);

        // 2. Более новая попытка создана, но завершилась неудачно (failed)
        var newerAttempt = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-FAILED-LATER-02",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-2",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-5).ToString("o")
        };
        _paymentRepo.Create(newerAttempt);
        _paymentRepo.UpdateStatus("ORD-FAILED-LATER-02", PaymentStatuses.Failed);

        // 3. Запрос статуса не должен потерять refunded или ошибочно вернуть paid/failed
        var actionResult = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string json = JsonSerializer.Serialize(actionResult.Value);

        Assert.Contains("\"status\":\"refunded\"", json);
        Assert.Contains("\"paid\":false", json);
        Assert.Contains("\"orderId\":\"ORD-REFUND-PREV-01\"", json);
    }

    [Fact(DisplayName = "StartPayment rejects payment if session was refunded")]
    public async Task StartPayment_WhenSessionRefunded_ReturnsConflictPaymentRefunded()
    {
        string sessionId = CreateCompletedSession();

        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-REFUNDED-SESSION",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-ref",
            RequestTimestamp = DateTime.UtcNow.AddHours(-1).ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus("ORD-REFUNDED-SESSION", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddHours(-1).ToString("o"));
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");
        _paymentRepo.UpdateStatus("ORD-REFUNDED-SESSION", PaymentStatuses.Refunded);

        var result = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        string json = JsonSerializer.Serialize(conflict.Value);

        Assert.Contains("payment_refunded", json);
    }

    [Fact(DisplayName = "StartPayment returns existing attempt if status is created or pending")]
    public async Task StartPayment_WhenAttemptCreatedOrPending_ReturnsExistingAttempt()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string sessionId = CreateCompletedSession();

        var existingPending = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-PENDING-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-pend",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(existingPending);
        _paymentRepo.UpdateStatus("ORD-PENDING-01", PaymentStatuses.Pending);

        var result = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var ok = Assert.IsType<OkObjectResult>(result);
        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(ok.Value, jsonOptions);

        Assert.Contains("ORD-PENDING-01", json);
        Assert.Contains("Используется существующая активная попытка оплаты.", json);
        Assert.Contains("\"tariff\":\"report\"", json);
        Assert.Contains("\"amountKzt\":49990", json);
        Assert.Contains("\"currency\":\"KZT\"", json);
        Assert.Contains("\"provider\":\"bcc\"", json);
        Assert.Contains("\"environment\":\"test\"", json);
        Assert.Contains("\"status\":\"pending\"", json);
        Assert.Contains("actionUrl", json);
        Assert.Contains("formFields", json);

        // Служебные и банковские поля НЕ должны утекать клиенту
        Assert.DoesNotContain("TID1", json);
        Assert.DoesNotContain("nonce-pend", json);
        Assert.DoesNotContain("RequestTimestamp", json);
        Assert.DoesNotContain("Rrn", json);
        Assert.DoesNotContain("IntRef", json);
        Assert.DoesNotContain("ApprovalCode", json);
    }

    [Fact(DisplayName = "StartPayment allows new attempt when latest attempt is failed or cancelled")]
    public async Task StartPayment_WhenAttemptFailedOrCancelled_AllowsNewAttempt()
    {
        string sessionId = CreateCompletedSession();

        var failed = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-FAILED-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-fail",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-10).ToString("o")
        };
        _paymentRepo.Create(failed);
        _paymentRepo.UpdateStatus("ORD-FAILED-01", PaymentStatuses.Failed);

        // Попытка разрешена (но возвращает 503 gateway unconfigured, пока BCC не сконфигурирован)
        var result = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var unavailable = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        string json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("payment_gateway_not_configured", json);
    }

    [Fact(DisplayName = "When session is paid and newer attempt is failed, status and all attributes strictly match successful payment")]
    public void GetPaymentStatus_WhenPaidSessionHasNewerFailedAttempt_AttributesBelongStrictlyToSuccessfulPayment()
    {
        string sessionId = CreateCompletedSession();

        // 1. Первая попытка оплачена (тариф report, 49990 KZT, заказ ORD-PAID-OK)
        var paidPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-PAID-OK",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-ok",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-30).ToString("o")
        };
        _paymentRepo.Create(paidPayment);
        _paymentRepo.UpdateStatus("ORD-PAID-OK", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddMinutes(-29).ToString("o"));
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        // 2. Вторая попытка осталась неудачной (например failed на консультацию 90990 KZT)
        var failedPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-FAILED-LATER",
            Tariff = "consultation",
            AmountKzt = 90990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-failed",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-5).ToString("o")
        };
        _paymentRepo.Create(failedPayment);
        _paymentRepo.UpdateStatus("ORD-FAILED-LATER", PaymentStatuses.Failed);

        // 3. Проверяем GetPaymentStatus
        var result = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string json = JsonSerializer.Serialize(result.Value);

        // Статус и абсолютно все реквизиты должны принадлежать фактически успешному платежу
        Assert.Contains("\"status\":\"paid\"", json);
        Assert.Contains("\"paid\":true", json);
        Assert.Contains("\"orderId\":\"ORD-PAID-OK\"", json);
        Assert.Contains("\"amountKzt\":49990", json);
        Assert.Contains("\"tariff\":\"report\"", json);
        Assert.DoesNotContain("ORD-FAILED-LATER", json);
        Assert.DoesNotContain("90990", json);
        Assert.DoesNotContain("consultation", json);
    }

    [Fact(DisplayName = "StartPayment rejects with payment_in_progress when active attempt has different tariff")]
    public async Task StartPayment_WhenActiveAttemptHasDifferentTariff_ReturnsConflictPaymentInProgress()
    {
        string sessionId = CreateCompletedSession();

        // Активная попытка на report
        var existing = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-ACTIVE-REPORT",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-act",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(existing);
        _paymentRepo.UpdateStatus("ORD-ACTIVE-REPORT", PaymentStatuses.Pending);

        // Пользователь запрашивает consultation
        var result = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "consultation",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var jsonOptions = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        string json = JsonSerializer.Serialize(conflict.Value, jsonOptions);

        Assert.Contains("payment_in_progress", json);
        Assert.Contains("ORD-ACTIVE-REPORT", json);
        Assert.Contains("report", json);
    }

    [Fact(DisplayName = "Refunding one payment does not revoke session if another paid payment still exists")]
    public void UpdateStatus_WhenRefundingOneOfTwoPaidPayments_SessionRemainsPaid()
    {
        string sessionId = CreateCompletedSession();

        // Первый оплаченный платёж
        var p1 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-DUAL-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-d1",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-20).ToString("o")
        };
        _paymentRepo.Create(p1);
        _paymentRepo.UpdateStatus("ORD-DUAL-01", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddMinutes(-19).ToString("o"));

        // Второй оплаченный платёж (например, параллельный)
        var p2 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-DUAL-02",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-d2",
            RequestTimestamp = DateTime.UtcNow.AddMinutes(-15).ToString("o")
        };
        _paymentRepo.Create(p2);
        _paymentRepo.UpdateStatus("ORD-DUAL-02", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.AddMinutes(-14).ToString("o"));
        _sessions.MarkSessionPaid(sessionId, 49990, "bcc");

        // Делаем возврат по первому платежу
        bool refunded = _paymentRepo.UpdateStatus("ORD-DUAL-01", PaymentStatuses.Refunded);
        Assert.True(refunded);

        // Сессия обязана остаться оплаченной, так как ORD-DUAL-02 всё ещё paid
        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.True(session.Paid);
        Assert.NotEqual("refunded", session.PaymentMethod);

        // GetPaymentStatus тоже возвращает статус paid на основе оставшегося успешного платежа
        var statusResult = Assert.IsType<OkObjectResult>(_payments.GetPaymentStatus(sessionId));
        string statusJson = JsonSerializer.Serialize(statusResult.Value);
        Assert.Contains("\"status\":\"paid\"", statusJson);
        Assert.Contains("\"paid\":true", statusJson);
        Assert.Contains("\"orderId\":\"ORD-DUAL-02\"", statusJson);
    }

    [Theory(DisplayName = "PaymentRepository.Create validates amount, tariff, environment and initial status")]
    [InlineData(0, "report", "test", "created", "KZT", "bcc")]
    [InlineData(-100, "report", "test", "created", "KZT", "bcc")]
    [InlineData(49990, "invalid_tariff", "test", "created", "KZT", "bcc")]
    [InlineData(49990, "report", "invalid_env", "created", "KZT", "bcc")]
    [InlineData(49990, "report", "test", "refunded", "KZT", "bcc")]
    [InlineData(49990, "report", "test", "pending", "KZT", "bcc")] // начальный статус только created
    [InlineData(49990, "report", "test", "created", "USD", "bcc")] // не KZT
    [InlineData(49990, "report", "test", "created", "KZT", "")] // пустой Provider
    public void PaymentRepository_Create_ValidatesInvalidFields(
        int amount, string tariff, string env, string status, string currency, string provider)
    {
        string sessionId = CreateCompletedSession();
        var payment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-VAL-" + Guid.NewGuid().ToString("N"),
            Tariff = tariff,
            AmountKzt = amount,
            Environment = env,
            Status = status,
            Currency = currency,
            Provider = provider
        };

        Assert.Throws<ArgumentException>(() => _paymentRepo.Create(payment));
    }

    [Fact(DisplayName = "Payment repository allows nullable BCC-specific fields and stores provider_metadata for other gateways")]
    public void PaymentRepository_SupportsNonBccProvidersWithMetadataAndNullTerminal()
    {
        string sessionId = CreateCompletedSession();
        var customPayment = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-STRIPE-001",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "stripe",
            Environment = "test",
            TerminalId = null,
            Nonce = null,
            RequestTimestamp = null,
            ProviderMetadata = "{\"client_secret\":\"cs_test_123\"}",
            Status = PaymentStatuses.Created
        };

        _paymentRepo.Create(customPayment);

        var retrieved = _paymentRepo.GetByOrderId("ORD-STRIPE-001");
        Assert.NotNull(retrieved);
        Assert.Equal("stripe", retrieved.Provider);
        Assert.Null(retrieved.TerminalId);
        Assert.Null(retrieved.Nonce);
        Assert.Null(retrieved.RequestTimestamp);
        Assert.Equal("{\"client_secret\":\"cs_test_123\"}", retrieved.ProviderMetadata);
    }

    [Theory(DisplayName = "Incomplete BCC configuration sets IsConfigured to false")]
    [InlineData("BCC_ENVIRONMENT")]
    [InlineData("BCC_TERMINAL_ID")]
    [InlineData("BCC_GATEWAY_URL")]
    [InlineData("BCC_NOTIFY_URL")]
    [InlineData("BCC_RETURN_URL")]
    [InlineData("BCC_MERCHANT_ID")]
    [InlineData("BCC_MERCHANT_NAME")]
    [InlineData("BCC_MAC_KEY")]
    public void BccPaymentGateway_IncompleteConfiguration_IsConfiguredIsFalse(string missingKey)
    {
        var dict = new Dictionary<string, string?>
        {
            ["BCC_ENVIRONMENT"] = "test",
            ["BCC_TERMINAL_ID"] = "TID999",
            ["BCC_GATEWAY_URL"] = "https://test-epay.bcc.kz/pay",
            ["BCC_NOTIFY_URL"] = "https://example.com/api/payments/bcc/notify",
            ["BCC_RETURN_URL"] = "https://example.com/api/payments/bcc/return",
            ["BCC_MERCHANT_ID"] = "00000001",
            ["BCC_MERCHANT_NAME"] = "FENIX LEGAL OS",
            ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
        };
        dict.Remove(missingKey);

        var config = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
        var gateway = new BccPaymentGateway(config);
        Assert.False(gateway.IsConfigured);
    }

    [Fact(DisplayName = "BCC payment gateway rejects invalid HEX key in IsConfigured and SignMac")]
    public void BccPaymentGateway_InvalidHexKey_IsConfiguredIsFalseAndSignMacThrows()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "TID999",
                ["BCC_GATEWAY_URL"] = "https://test-epay.bcc.kz/pay",
                ["BCC_NOTIFY_URL"] = "https://example.com/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://example.com/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "FENIX LEGAL OS",
                ["BCC_MAC_KEY"] = "NOT_A_VALID_HEX_KEY_123"
            })
            .Build();

        var gateway = new BccPaymentGateway(config);
        Assert.False(gateway.IsConfigured);

        var ex = Assert.Throws<ArgumentException>(() => gateway.SignMac("6350.00"));
        Assert.Contains("HEX", ex.Message);
    }

    [Fact(DisplayName = "BCC logs only a safe MAC key fingerprint in test environment")]
    public void BccPaymentGateway_TestEnvironment_LogsMacKeyFingerprintWithoutKey()
    {
        const string macKey = "6BB0AC02E47BDF73D98FEB777F3B5294";
        var logger = new ListLogger<BccPaymentGateway>();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888881",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://example.com/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://example.com/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "FENIX LEGAL OS",
                ["BCC_MAC_KEY"] = macKey
            })
            .Build();

        _ = new BccPaymentGateway(config, logger);

        var entry = Assert.Single(logger.Entries.Where(e =>
            e.EventId == PaymentEvents.BccTestConfigurationLoaded));
        byte[] keyBytes = Convert.FromHexString(macKey);
        string expectedFingerprint = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(keyBytes))[..16];

        Assert.Equal(expectedFingerprint, entry.Properties["MacKeyFingerprint"]?.ToString());
        Assert.DoesNotContain(macKey, entry.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(macKey, entry.Properties.Values.Select(v => v?.ToString() ?? ""));
    }

    [Fact(DisplayName = "CreatePaymentAsync returns error if gateway is not configured")]
    public async Task BccPaymentGateway_NotConfigured_ReturnsPaymentGatewayNotConfiguredError()
    {
        var gateway = new BccPaymentGateway(new ConfigurationBuilder().Build());
        Assert.False(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.1",
            Phone = "+77001234567",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.False(result.Success);
        Assert.Equal("payment_gateway_not_configured", result.ErrorCode);
    }

    [Fact(DisplayName = "CreatePaymentAsync returns error if ClientIp is missing")]
    public async Task BccPaymentGateway_MissingClientIp_ReturnsValidationError()
    {
        var gateway = CreateFullyConfiguredGateway();
        Assert.True(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = null,
            Phone = "+77001234567",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.False(result.Success);
        Assert.Equal("missing_client_ip", result.ErrorCode);
    }

    [Fact(DisplayName = "CreatePaymentAsync returns error if Phone is missing")]
    public async Task BccPaymentGateway_MissingPhone_ReturnsValidationError()
    {
        var gateway = CreateFullyConfiguredGateway();
        Assert.True(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.1",
            Phone = "",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.False(result.Success);
        Assert.Equal("phone_required", result.ErrorCode);
    }

    [Fact(DisplayName = "BCC payment gateway returns form fields, NOTIFY_URL, non-empty P_SIGN, MERCH_RN_ID, CLIENT_IP and M_INFO")]
    public async Task BccPaymentGateway_CreatePaymentAsync_ReturnsCheckoutDescriptorWithFormFieldsAndPSign()
    {
        var gateway = CreateFullyConfiguredGateway();
        Assert.True(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.100",
            Phone = "+77001234567",
            BillingAddress = "г. Астана, ул. Абая, 10",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.True(result.Success);
        Assert.Equal("https://test-epay.bcc.kz/pay", result.PaymentUrl);
        Assert.Equal("https://test-epay.bcc.kz/pay", result.ActionUrl);
        Assert.Equal("POST", result.Method);
        Assert.Equal("form_post", result.CheckoutType);
        Assert.NotNull(result.FormFields);
        Assert.Equal("TID999", result.FormFields["TERMINAL"]);
        Assert.Equal("49990.00", result.FormFields["AMOUNT"]);
        Assert.Equal("398", result.FormFields["CURRENCY"]);
        Assert.Equal("1", result.FormFields["TRTYPE"]);
        Assert.Equal("0", result.FormFields["MERCH_GMT"]);
        Assert.Equal("https://example.com/api/payments/bcc/notify", result.FormFields["NOTIFY_URL"]);
        Assert.Equal("https://example.com/api/payments/bcc/return", result.FormFields["BACKREF"]);
        Assert.Equal("192.168.1.100", result.FormFields["CLIENT_IP"]);
        Assert.True(result.FormFields.ContainsKey("M_INFO"));
        Assert.True(result.FormFields.ContainsKey("ORDER"));
        Assert.True(result.FormFields.ContainsKey("NONCE"));
        Assert.True(result.FormFields.ContainsKey("TIMESTAMP"));
        Assert.False(string.IsNullOrWhiteSpace(result.FormFields["P_SIGN"]));

        // MERCH_RN_ID: ровно 16 буквенно-цифровых символов
        Assert.True(result.FormFields.ContainsKey("MERCH_RN_ID"));
        string merchRnId = result.FormFields["MERCH_RN_ID"];
        Assert.Equal(16, merchRnId.Length);
        Assert.Matches("^[A-Za-z0-9]{16}$", merchRnId);
        Assert.Equal(merchRnId, result.MerchRnId);
    }

    [Fact(DisplayName = "CreatePaymentAsync returns error if BillingAddress is missing")]
    public async Task BccPaymentGateway_MissingBillingAddress_ReturnsValidationError()
    {
        var gateway = CreateFullyConfiguredGateway();
        Assert.True(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.1",
            Phone = "+77001234567",
            BillingAddress = "   ",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.False(result.Success);
        Assert.Equal("billing_address_required", result.ErrorCode);
    }

    [Fact(DisplayName = "CreatePaymentAsync returns error if BillingAddress is longer than 50 chars")]
    public async Task BccPaymentGateway_BillingAddressTooLong_ReturnsValidationError()
    {
        var gateway = CreateFullyConfiguredGateway();
        Assert.True(gateway.IsConfigured);

        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.1",
            Phone = "+77001234567",
            BillingAddress = new string('A', 51),
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.False(result.Success);
        Assert.Equal("billing_address_too_long", result.ErrorCode);
    }

    [Fact(DisplayName = "MERCH_RN_ID does not affect P_SIGN calculation for TRTYPE=1")]
    public async Task BccPaymentGateway_MerchRnId_DoesNotAffectPSignCalculation()
    {
        var gateway = CreateFullyConfiguredGateway();
        var result = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            ClientIp = "192.168.1.100",
            Phone = "+77001234567",
            BillingAddress = "г. Астана, ул. Абая, 10",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.True(result.Success);

        // Строка источника собирается строго из 9 полей TRTYPE=1:
        // AMOUNT, CURRENCY, ORDER, MERCHANT, TERMINAL, MERCH_GMT, TIMESTAMP, TRTYPE, NONCE
        string macData = BccPaymentGateway.BuildMacDataString(
            amount: result.FormFields["AMOUNT"],
            currency: result.FormFields["CURRENCY"],
            order: result.FormFields["ORDER"],
            merchant: result.FormFields["MERCHANT"],
            terminal: result.FormFields["TERMINAL"],
            merchGmt: result.FormFields["MERCH_GMT"],
            timestamp: result.FormFields["TIMESTAMP"],
            trType: result.FormFields["TRTYPE"],
            nonce: result.FormFields["NONCE"]);

        string expectedPSign = gateway.SignMac(macData);
        Assert.Equal(expectedPSign, result.FormFields["P_SIGN"]);
        Assert.DoesNotContain(result.FormFields["MERCH_RN_ID"], macData);
    }

    [Fact(DisplayName = "BCC MAC calculation strictly matches official BCC documentation test vector for TRTYPE=1")]
    public void BccPaymentGateway_SignMac_MatchesBccDocumentationTestVector()
    {
        // Официальный тестовый вектор из документации BCC e-Commerce WEBVIEW (раздел МАКИРОВАНИЕ (MAC)):
        // Key: 6BB0AC02E47BDF73D98FEB777F3B5294
        // Поля: AMOUNT=350.00, CURRENCY=398, ORDER=3558714461568, MERCHANT=merchantname,
        //       TERMINAL=88888881, MERCH_GMT=0, TIMESTAMP=20200224073921, TRTYPE=1, NONCE=F2B2DD7E603A7AAF5E1BC35DEE1F6C9A
        // Строка: 6350.00339813355871446156812merchantname888888811014202002240739211132F2B2DD7E603A7AAF5E1BC35DEE1F6C9A
        // Ожидаемый P_SIGN: 9B1C58714CFF6E4BCC6E97B4D503275838F4ED68
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888881",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://example.com/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://example.com/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "merchantname",
                ["BCC_MERCHANT_NAME"] = "TEST MERCHANT",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
            })
            .Build();

        var gateway = new BccPaymentGateway(config);
        string macData = BccPaymentGateway.BuildMacDataString(
            amount: "350.00",
            currency: "398",
            order: "3558714461568",
            merchant: "merchantname",
            terminal: "88888881",
            merchGmt: "0",
            timestamp: "20200224073921",
            trType: "1",
            nonce: "F2B2DD7E603A7AAF5E1BC35DEE1F6C9A");

        string pSign = gateway.SignMac(macData);

        Assert.Equal("9B1C58714CFF6E4BCC6E97B4D503275838F4ED68", pSign);
    }

    [Fact(DisplayName = "Phone validation and normalization tests for KZ numbers")]
    public void PhoneHelper_ValidationAndNormalizationTests()
    {
        // 1. +7 700 123 45 67 нормализуется в +77001234567
        Assert.True(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("+7 700 123 45 67", out string n1));
        Assert.Equal("+77001234567", n1);

        // 2. 87001234567 нормализуется в +77001234567
        Assert.True(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("87001234567", out string n2));
        Assert.Equal("+77001234567", n2);

        // 3. номер со скобками и дефисами нормализуется
        Assert.True(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("8 (700) 123-45-67", out string n3));
        Assert.Equal("+77001234567", n3);

        // 4. пустое значение отклоняется
        Assert.False(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("", out _));
        Assert.False(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("   ", out _));

        // 5. короткий номер отклоняется
        Assert.False(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("+770012345", out _));

        // 6. российский +7 999... отклоняется
        Assert.False(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("+79991234567", out _));

        // 7. строка с буквами отклоняется
        Assert.False(FenixLegalOs.Infrastructure.PhoneHelper.TryNormalizePhone("+7 700 123 45 6a", out _));

        // 8. cc получается "7"
        // 9. subscriber получается "7001234567"
        Assert.True(FenixLegalOs.Infrastructure.PhoneHelper.TryExtractMInfoPhone("+7 700 123 45 67", out string cc, out string subscriber));
        Assert.Equal("7", cc);
        Assert.Equal("7001234567", subscriber);
    }

    [Fact(DisplayName = "Registration tests: validation, normalization, and sensitive data exclusion")]
    public void AuthController_RegistrationAndProfileTests()
    {
        var auth = new AuthController(
            _userRepo,
            _sessions,
            new LeadRepository(new DbInitializer(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FENIX_DB_PATH"] = _databasePath }).Build())))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        // Регистрация без телефона возвращает 400
        var resNoPhone = auth.Register(new AuthController.RegisterDto("no_phone@test.com", "pass123", "Name", "Co", "CEO", null, null, true));
        var badNoPhone = Assert.IsType<BadRequestObjectResult>(resNoPhone);
        Assert.Contains("invalid_phone", JsonSerializer.Serialize(badNoPhone.Value));

        // Некорректный телефон возвращает 400
        var resBadPhone = auth.Register(new AuthController.RegisterDto("bad_phone@test.com", "pass123", "Name", "Co", "CEO", "+79991234567", null, true));
        var badPhone = Assert.IsType<BadRequestObjectResult>(resBadPhone);
        Assert.Contains("invalid_phone", JsonSerializer.Serialize(badPhone.Value));

        // Телефон сохраняется нормализованным
        var resOk = auth.Register(new AuthController.RegisterDto("valid_phone@test.com", "pass123", "Name", "Co", "CEO", "8 (700) 123-45-67", null, true));
        var okReg = Assert.IsType<OkObjectResult>(resOk);
        string regJson = JsonSerializer.Serialize(okReg.Value);
        using var regDoc = JsonDocument.Parse(regJson);
        var regUser = regDoc.RootElement.GetProperty("user");
        Assert.Equal("+77001234567", regUser.GetProperty("phone").GetString());
        Assert.False(regUser.TryGetProperty("passwordHash", out _));
        Assert.False(regUser.TryGetProperty("salt", out _));

        // GetUserById возвращает телефон
        var user = _userRepo.GetUserByEmail("valid_phone@test.com");
        Assert.NotNull(user);
        Assert.Equal("+77001234567", user.Phone);

        var byId = _userRepo.GetUserById(user.Id);
        Assert.NotNull(byId);
        Assert.Equal("+77001234567", byId.Phone);

        // GetUserByToken возвращает телефон
        string token = _userRepo.CreateSessionToken(user.Id);
        var byToken = _userRepo.GetUserByToken(token);
        Assert.NotNull(byToken);
        Assert.Equal("+77001234567", byToken.Phone);

        // Login возвращает phone, не возвращая хеши
        var loginRes = auth.Login(new AuthController.LoginDto("valid_phone@test.com", "pass123", null));
        var okLogin = Assert.IsType<OkObjectResult>(loginRes);
        string loginJson = JsonSerializer.Serialize(okLogin.Value);
        using var loginDoc = JsonDocument.Parse(loginJson);
        var loginUser = loginDoc.RootElement.GetProperty("user");
        Assert.Equal("+77001234567", loginUser.GetProperty("phone").GetString());
        Assert.False(loginUser.TryGetProperty("passwordHash", out _));
        Assert.False(loginUser.TryGetProperty("salt", out _));
    }

    [Fact(DisplayName = "Payment tests: phone is read from users.phone, M_INFO structure, and IP address handling")]
    public async Task PaymentService_PhoneAndMInfo_ValidationAndStructure()
    {
        // Сессия пользователя без телефона
        string sessionNoPhone = _sessions.CreateSession();
        _sessions.CompleteSession(sessionNoPhone, "{}", new ScoreResult { Overall = 50 });
        var userWithoutPhone = _userRepo.CreateUser("nophone@example.com", "pass123", "No Phone", "Co", "CEO", phone: null);
        _userRepo.AttachUserToSession(sessionNoPhone, userWithoutPhone.Id);

        var resNoPhone = await _payments.StartPayment(sessionNoPhone, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var badNoPhone = Assert.IsType<BadRequestObjectResult>(resNoPhone);
        Assert.Contains("phone_required", JsonSerializer.Serialize(badNoPhone.Value));

        // Сессия с настроенным шлюзом и телефоном
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.195") }
                }
            }
        };

        string sessionId = CreateCompletedSession();
        var startRes = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 900,
            BrowserScreenWidth = 1440,
            BillingAddress = "   г. Астана, ул. Абая, д. 10, кв. 5   "
        });

        var okStart = Assert.IsType<OkObjectResult>(startRes);
        string startJson = JsonSerializer.Serialize(okStart.Value);
        Assert.Contains("actionUrl", startJson);
        Assert.Contains("formFields", startJson);

        // Проверяем созданную запись в payments
        var createdPayment = _paymentRepo.GetBySessionId(sessionId).First();
        Assert.Equal(PaymentStatuses.Created, createdPayment.Status);
        Assert.NotNull(createdPayment.ProviderMetadata);
        Assert.Contains("merch_rn_id", createdPayment.ProviderMetadata);

        // M_INFO является корректным Base64
        using var initDoc = JsonDocument.Parse(startJson);
        string mInfoBase64 = initDoc.RootElement.GetProperty("formFields").GetProperty("M_INFO").GetString()!;
        byte[] mInfoBytes = Convert.FromBase64String(mInfoBase64);

        // Декодированный M_INFO разбираем через JsonDocument (чтобы корректно обрабатывать экранирование \uXXXX)
        using var doc = JsonDocument.Parse(mInfoBytes);
        var root = doc.RootElement;

        Assert.Equal("900", root.GetProperty("browserScreenHeight").GetString());
        Assert.Equal("1440", root.GetProperty("browserScreenWidth").GetString());
        Assert.True(root.TryGetProperty("mobilePhone", out var mobilePhone));
        Assert.False(root.TryGetProperty("phone", out _));
        Assert.Equal("7", mobilePhone.GetProperty("cc").GetString());
        Assert.Equal("7001234567", mobilePhone.GetProperty("subscriber").GetString());
        // Пробелы по краям удалены (Trim)
        Assert.Equal("г. Астана, ул. Абая, д. 10, кв. 5", root.GetProperty("billAddrLine1").GetString());

        // До подтверждения оплаты PDF остаётся закрытым
        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid);
    }

    [Fact(DisplayName = "Billing address: empty address is rejected with billing_address_required and no payment created in DB")]
    public async Task StartPayment_EmptyBillingAddress_ReturnsBadRequestAndDoesNotCreatePayment()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.195") }
                }
            }
        };

        string sessionId = CreateCompletedSession();

        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        // Пустой адрес и адрес из одних пробелов
        foreach (var emptyAddr in new[] { null, "", "   " })
        {
            var res = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
            {
                Tariff = "report",
                BrowserScreenHeight = 1080,
                BrowserScreenWidth = 1920,
                BillingAddress = emptyAddr
            });

            var bad = Assert.IsType<BadRequestObjectResult>(res);
            string json = JsonSerializer.Serialize(bad.Value, jsonOptions);
            Assert.Contains("billing_address_required", json);
            Assert.Contains("Для перехода к оплате необходимо указать адрес плательщика.", json);
        }

        // Платёж в БД не создаётся
        var payments = _paymentRepo.GetBySessionId(sessionId);
        Assert.Empty(payments);
    }

    [Fact(DisplayName = "Billing address: address longer than 50 chars is rejected with billing_address_too_long and no payment created in DB")]
    public async Task StartPayment_BillingAddressTooLong_ReturnsBadRequestAndDoesNotCreatePayment()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.195") }
                }
            }
        };

        string sessionId = CreateCompletedSession();
        string tooLongAddress = new string('А', 51); // 51 символ

        var res = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = tooLongAddress
        });

        var bad = Assert.IsType<BadRequestObjectResult>(res);
        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(bad.Value, jsonOptions);
        Assert.Contains("billing_address_too_long", json);
        Assert.Contains("Адрес плательщика не должен превышать 50 символов.", json);

        // Платёж в БД не создаётся
        var payments = _paymentRepo.GetBySessionId(sessionId);
        Assert.Empty(payments);
    }

    [Fact(DisplayName = "Phone security: phone is strictly retrieved from users.phone and client cannot spoof it")]
    public async Task StartPayment_PhoneStrictlyFromDatabase_ClientCannotSpoof()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.195") }
                }
            }
        };

        // Пользователь в БД имеет телефон +77001234567
        string sessionId = CreateCompletedSession();

        var res = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });

        var ok = Assert.IsType<OkObjectResult>(res);
        string json = JsonSerializer.Serialize(ok.Value);
        using var doc = JsonDocument.Parse(json);
        string mInfoB64 = doc.RootElement.GetProperty("formFields").GetProperty("M_INFO").GetString()!;
        using var mInfoDoc = JsonDocument.Parse(Convert.FromBase64String(mInfoB64));
        var mobilePhone = mInfoDoc.RootElement.GetProperty("mobilePhone");

        // Телефон взят строго из базы данных пользователя
        Assert.Equal("7", mobilePhone.GetProperty("cc").GetString());
        Assert.Equal("7001234567", mobilePhone.GetProperty("subscriber").GetString());
    }

    [Fact(DisplayName = "Configuration address is removed: gateway IsConfigured does not depend on configuration address")]
    public async Task BccPaymentGateway_ConfigurationAddress_IsNoLongerUsed()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888888",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "http://localhost:5050/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "http://localhost:5050/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "6156812",
                ["BCC_MERCHANT_NAME"] = "merchantname",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
                // BCC_BILL_ADDR_LINE1 отсутствует
            })
            .Build();

        var gateway = new BccPaymentGateway(config);
        // Шлюз сконфигурирован без статического адреса в конфигурации
        Assert.True(gateway.IsConfigured);

        // Статический адрес из конфигурации не переопределяет адрес из запроса
        var init = await gateway.CreatePaymentAsync(new PaymentGatewayInitRequest
        {
            SessionId = "sess-1",
            Tariff = "report",
            AmountKzt = 49990,
            ClientIp = "127.0.0.1",
            Phone = "+77001234567",
            BillingAddress = "г. Караганда, пр. Бухар Жырау, 1",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920
        });

        Assert.True(init.Success);
        string mInfoB64 = init.FormFields["M_INFO"];
        using var mInfoDoc = JsonDocument.Parse(Convert.FromBase64String(mInfoB64));
        Assert.Equal("г. Караганда, пр. Бухар Жырау, 1", mInfoDoc.RootElement.GetProperty("billAddrLine1").GetString());
    }

    [Fact(DisplayName = "Client IP is read strictly from RemoteIpAddress and not directly from header")]
    public async Task StartPayment_ClientIpReadFromRemoteIpAddress_NotDirectlyFromHeader()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.195");
        httpContext.Request.Headers["X-Forwarded-For"] = "198.51.100.42"; // Сырой заголовок не должен использоваться напрямую

        var payments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };

        string sessionId = CreateCompletedSession();
        var result = await payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var jsonOptions = new JsonSerializerOptions
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        string json = JsonSerializer.Serialize(ok.Value, jsonOptions);
        using var doc = JsonDocument.Parse(json);
        var formFields = doc.RootElement.GetProperty("formFields");
        // IP должен строго браться из RemoteIpAddress ("203.0.113.195"), а не напрямую из X-Forwarded-For ("198.51.100.42")
        Assert.Equal("203.0.113.195", formFields.GetProperty("CLIENT_IP").GetString());
    }

    [Fact(DisplayName = "Two concurrent starts do not create two active records in DB")]
    public async Task StartPayment_ConcurrentStarts_DoNotCreateTwoActiveRecords()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string sessionId = CreateCompletedSession();

        // Запуск двух параллельных запросов создания платежа
        var task1 = fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var task2 = fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });

        var results = await Task.WhenAll(task1, task2);

        // В БД должна быть ровно одна активная запись
        var activePayments = _paymentRepo.GetBySessionId(sessionId)
            .Where(p => p.Status == PaymentStatuses.Created || p.Status == PaymentStatuses.Pending)
            .ToList();
        Assert.Single(activePayments);

        // Никаких необработанных исключений и 500 ошибок
        Assert.All(results, res =>
        {
            Assert.True(res is OkObjectResult || res is ConflictObjectResult);
            Assert.False(res is ObjectResult obj && obj.StatusCode == 500);
        });
    }

    [Fact(DisplayName = "Partial unique index uq_payments_active_session rejects second active payment at SQLite level")]
    public void Db_PartialUniqueIndex_RejectsSecondActivePaymentForSameSession()
    {
        string sessionId = CreateCompletedSession();

        var payment1 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-ACTIVE-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment1);

        var payment2 = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-ACTIVE-02",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            Status = PaymentStatuses.Created
        };

        // Попытка создать вторую активную запись для этой же сессии должна нарушить частичный уникальный индекс
        var ex = Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _paymentRepo.Create(payment2));
        Assert.Equal(19, ex.SqliteErrorCode); // SQLITE_CONSTRAINT
    }

    [Fact(DisplayName = "Reopening active payment preserves original ORDER, tariff and amount")]
    public async Task StartPayment_ReopeningActivePayment_PreservesOriginalOrderTariffAndAmount()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string sessionId = CreateCompletedSession();

        // 1. Первый старт
        var firstResult = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var ok1 = Assert.IsType<OkObjectResult>(firstResult);
        string json1 = JsonSerializer.Serialize(ok1.Value);
        using var doc1 = JsonDocument.Parse(json1);
        string initialOrderId = doc1.RootElement.GetProperty("orderId").GetString()!;
        int initialAmount = doc1.RootElement.GetProperty("amountKzt").GetInt32();
        string initialTariff = doc1.RootElement.GetProperty("tariff").GetString()!;

        // 2. Повторный старт для той же сессии
        var secondResult = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var ok2 = Assert.IsType<OkObjectResult>(secondResult);
        string json2 = JsonSerializer.Serialize(ok2.Value);
        using var doc2 = JsonDocument.Parse(json2);

        Assert.Equal(initialOrderId, doc2.RootElement.GetProperty("orderId").GetString());
        Assert.Equal(initialAmount, doc2.RootElement.GetProperty("amountKzt").GetInt32());
        Assert.Equal(initialTariff, doc2.RootElement.GetProperty("tariff").GetString());
        Assert.Equal(initialOrderId, doc2.RootElement.GetProperty("formFields").GetProperty("ORDER").GetString());
        Assert.Equal($"{initialAmount}.00", doc2.RootElement.GetProperty("formFields").GetProperty("AMOUNT").GetString());
    }

    [Fact(DisplayName = "Reopening active payment atomically updates MERCH_RN_ID, NONCE and TIMESTAMP in DB matching form")]
    public async Task StartPayment_ReopeningActivePayment_UpdatesDbMatchingFormFields()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string sessionId = CreateCompletedSession();
        var initialRes = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var okInitial = Assert.IsType<OkObjectResult>(initialRes);
        string initialJson = JsonSerializer.Serialize(okInitial.Value);
        using var initDoc = JsonDocument.Parse(initialJson);
        string orderId = initDoc.RootElement.GetProperty("orderId").GetString()!;

        // Повторный старт через небольшую задержку
        await Task.Delay(20);
        var reopenRes = await fullyPayments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var okReopen = Assert.IsType<OkObjectResult>(reopenRes);
        string reopenJson = JsonSerializer.Serialize(okReopen.Value);
        using var reopenDoc = JsonDocument.Parse(reopenJson);
        var formFields = reopenDoc.RootElement.GetProperty("formFields");
        string newNonce = formFields.GetProperty("NONCE").GetString()!;
        string newTimestamp = formFields.GetProperty("TIMESTAMP").GetString()!;
        string newMerchRnId = formFields.GetProperty("MERCH_RN_ID").GetString()!;

        // Читаем запись из БД
        var stored = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(stored);
        Assert.Equal(newNonce, stored.Nonce);
        Assert.Equal(newTimestamp, stored.RequestTimestamp);
        Assert.NotNull(stored.ProviderMetadata);
        Assert.Contains(newMerchRnId, stored.ProviderMetadata);
    }

    [Fact(DisplayName = "Paid or refunded payment cannot be reopened")]
    public async Task StartPayment_WhenAttemptPaidOrRefunded_CannotReopen()
    {
        var fullyGateway = CreateFullyConfiguredGateway();
        var fullyService = new PaymentService(_sessions, _settings, _paymentRepo, fullyGateway, _userRepo);
        var fullyPayments = new PaymentsController(fullyService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        // 1. Оплаченный платёж
        string sessionPaid = CreateCompletedSession();
        var pPaid = new Payment
        {
            SessionId = sessionPaid,
            OrderId = "ORD-TEST-PAID",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(pPaid);
        _paymentRepo.UpdateStatus("ORD-TEST-PAID", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.ToString("o"));
        _sessions.MarkSessionPaid(sessionPaid, 49990, "bcc");

        var resPaid = await fullyPayments.StartPayment(sessionPaid, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var conflictPaid = Assert.IsType<ConflictObjectResult>(resPaid);
        Assert.Contains("already_paid", JsonSerializer.Serialize(conflictPaid.Value));

        // 2. Возвращённый платёж
        string sessionRef = CreateCompletedSession();
        var pRef = new Payment
        {
            SessionId = sessionRef,
            OrderId = "ORD-TEST-REF",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(pRef);
        _paymentRepo.UpdateStatus("ORD-TEST-REF", PaymentStatuses.Paid, paidAt: DateTime.UtcNow.ToString("o"));
        _sessions.MarkSessionPaid(sessionRef, 49990, "bcc");
        _paymentRepo.UpdateStatus("ORD-TEST-REF", PaymentStatuses.Refunded);

        var resRef = await fullyPayments.StartPayment(sessionRef, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });
        var conflictRef = Assert.IsType<ConflictObjectResult>(resRef);
        Assert.Contains("payment_refunded", JsonSerializer.Serialize(conflictRef.Value));
    }

    [Fact(DisplayName = "StartPayment when attempt created or pending and gateway not configured returns 503")]
    public async Task StartPayment_WhenAttemptCreatedOrPendingAndGatewayNotConfigured_Returns503()
    {
        string sessionId = CreateCompletedSession();
        var existingPending = new Payment
        {
            SessionId = sessionId,
            OrderId = "ORD-UNCONF-01",
            Tariff = "report",
            AmountKzt = 49990,
            Provider = "bcc",
            TerminalId = "TID1",
            Status = PaymentStatuses.Created,
            Nonce = "nonce-unconf",
            RequestTimestamp = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(existingPending);

        var result = await _payments.StartPayment(sessionId, new StartPaymentRequest
        {
            Tariff = "report",
            BrowserScreenHeight = 1080,
            BrowserScreenWidth = 1920,
            BillingAddress = "г. Астана, ул. Абая, 10"
        });

        var unavailable = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        string json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("payment_gateway_not_configured", json);
    }

    [Fact(DisplayName = "DbInitializer throws diagnostic exception when duplicate active payments exist and does not alter records")]
    public void DbInitializer_WhenDuplicateActivePaymentsExist_ThrowsDiagnosticExceptionWithoutModifyingData()
    {
        string testDbPath = Path.Combine(Path.GetTempPath(), $"test_dup_diag_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["FENIX_DB_PATH"] = testDbPath })
            .Build();

        var db = new DbInitializer(config);
        db.Initialize();

        // Временно удаляем частичный уникальный индекс, чтобы смоделировать базу данных с историческими дубликатами
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            conn.Execute("DROP INDEX IF EXISTS uq_payments_active_session;");

            // Создаём сессию и вставляем две активные записи для одной сессии
            conn.Execute("INSERT INTO sessions (id, created_at, updated_at) VALUES ('sess-dup-01', datetime('now'), datetime('now'));");
            conn.Execute(@"
                INSERT INTO payments (id, session_id, order_id, tariff, amount_kzt, currency, provider, environment, status, created_at, updated_at)
                VALUES ('p1', 'sess-dup-01', 'ORD-D1', 'report', 49990, 'KZT', 'bcc', 'test', 'created', datetime('now'), datetime('now')),
                       ('p2', 'sess-dup-01', 'ORD-D2', 'report', 49990, 'KZT', 'bcc', 'test', 'pending', datetime('now'), datetime('now'));
            ");
        }

        // Повторный запуск инициализации БД обязан обнаружить дубликаты и выбросить диагностическое исключение
        var ex = Assert.Throws<InvalidOperationException>(() => db.Initialize());
        Assert.Contains("uq_payments_active_session", ex.Message);
        Assert.Contains("sess-dup-01", ex.Message);

        // Финансовые записи не должны быть автоматически изменены или удалены
        using (var conn = new SqliteConnection(db.ConnectionString))
        {
            conn.Open();
            int count = conn.ExecuteScalar<int>("SELECT COUNT(*) FROM payments WHERE session_id = 'sess-dup-01';");
            Assert.Equal(2, count);
        }

        SqliteConnection.ClearAllPools();
        foreach (string suffix in new[] { "", "-wal", "-shm" })
        {
            string path = testDbPath + suffix;
            if (File.Exists(path)) File.Delete(path);
        }
    }

    // =========================================================================
    // Тесты безопасной и идемпотентной обработки уведомлений BCC (POST /api/payments/bcc/notify)
    // =========================================================================

    [Fact(DisplayName = "BCC Notify: успешное уведомление переводит платёж в paid и активирует сессию и лид")]
    public async Task Notify_SuccessfulPurchase_TransitionsToPaidAndUnlocksSessionAndLead()
    {
        string sessionId = CreateCompletedSession();
        using (var conn = new SqliteConnection($"Data Source={_databasePath}"))
        {
            conn.Open();
            conn.Execute("INSERT INTO leads (id, session_id, type, name, email, paid, created_at) VALUES ('lead-1', @sessionId, 'report', 'Lead User', 'lead@example.com', 0, datetime('now'))", new { sessionId });
        }


        string orderId = "ORD-BCC-SUCCESS-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        var form = CreateBccNotifyForm(
            orderId: orderId,
            amount: "49990.00",
            currency: "398",
            terminal: "TID999",
            trType: "1",
            action: "0",
            rc: "00",
            rrn: "123456789012",
            intRef: "INT99887766",
            approval: "APP999",
            madvCode: "0",
            text: "Transaction approved");

        var actionResult = await controller.Notify(form);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        // Проверяем обновление платежа в БД
        var updatedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatuses.Paid, updatedPayment.Status);
        Assert.Equal("123456789012", updatedPayment.Rrn);
        Assert.Equal("INT99887766", updatedPayment.IntRef);
        Assert.Equal("APP999", updatedPayment.ApprovalCode);
        Assert.Equal("0", updatedPayment.ActionCode);
        Assert.Equal("00", updatedPayment.ResponseCode);
        Assert.Equal("0", updatedPayment.MerchantAdviceCode);
        Assert.Equal("Transaction approved", updatedPayment.BankMessage);
        Assert.NotNull(updatedPayment.PaidAt);
        Assert.NotNull(updatedPayment.NotificationReceivedAt);

        // Проверяем активацию сессии
        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.True(session.Paid);
        Assert.NotNull(session.PaidAt);

        // Проверяем активацию лида
        using (var conn = new SqliteConnection($"Data Source={_databasePath}"))
        {
            conn.Open();
            int leadPaid = conn.ExecuteScalar<int>("SELECT paid FROM leads WHERE session_id = @sessionId", new { sessionId });
            Assert.Equal(1, leadPaid);
        }
    }

    [Fact(DisplayName = "BCC Notify: отказ банка переводит активный платёж в failed и не активирует сессию")]
    public async Task Notify_BankRejection_TransitionsToFailedAndDoesNotUnlockSession()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-DECLINE-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        var form = CreateBccNotifyForm(
            orderId: orderId,
            action: "1",
            rc: "05",
            text: "Declined by issuer");

        var actionResult = await controller.Notify(form);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        var updatedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatuses.Failed, updatedPayment.Status);
        Assert.Equal("1", updatedPayment.ActionCode);
        Assert.Equal("05", updatedPayment.ResponseCode);
        Assert.Equal("Declined by issuer", updatedPayment.BankMessage);
        Assert.Null(updatedPayment.PaidAt);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid);
    }

    [Fact(DisplayName = "BCC Notify: повторное одинаковое уведомление идемпотентно и не меняет paid_at")]
    public async Task Notify_DuplicateSuccess_IsIdempotentAndPreservesOriginalPaidAt()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-DUP-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");

        // Первое успешное уведомление
        var firstResult = await controller.Notify(form);
        Assert.IsType<OkObjectResult>(firstResult);

        var paymentAfterFirst = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfterFirst);
        string? originalPaidAt = paymentAfterFirst.PaidAt;
        Assert.NotNull(originalPaidAt);

        // Второе идентичное уведомление
        var secondResult = await controller.Notify(form);
        Assert.IsType<OkObjectResult>(secondResult);

        var paymentAfterSecond = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfterSecond);
        Assert.Equal(PaymentStatuses.Paid, paymentAfterSecond.Status);
        Assert.Equal(originalPaidAt, paymentAfterSecond.PaidAt);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.True(session.Paid);
    }

    [Fact(DisplayName = "BCC Notify: неверный Basic Auth возвращает 401 и WWW-Authenticate и не меняет платёж")]
    public async Task Notify_InvalidBasicAuth_Returns401WithWwwAuthenticateAndLeavesPaymentUntouched()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-AUTH-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        // Передаём неверный пароль
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "wrong_password");

        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");

        var actionResult = await controller.Notify(form);
        var unauthorized = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
        Assert.Equal("Basic realm=\"BCC Notify\"", controller.Response.Headers.WWWAuthenticate.ToString());

        var unchangedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(unchangedPayment);
        Assert.Equal(PaymentStatuses.Created, unchangedPayment.Status);
        Assert.Null(unchangedPayment.PaidAt);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid);
    }

    [Fact(DisplayName = "BCC Notify: неизвестный ORDER возвращает 400 и ничего не обновляет")]
    public async Task Notify_UnknownOrder_Returns400AndLeavesDatabaseUntouched()
    {
        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        var form = CreateBccNotifyForm(orderId: "NON_EXISTENT_ORDER_9999", action: "0", rc: "00");

        var actionResult = await controller.Notify(form);
        var badRequest = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);

        string json = JsonSerializer.Serialize(badRequest.Value);
        Assert.Contains("order_not_found", json);
    }

    [Fact(DisplayName = "BCC Notify: несовпадение суммы, валюты, терминала или TRTYPE возвращает 400")]
    public async Task Notify_MismatchOfAmountCurrencyTerminalOrTrType_Returns400WithoutModifications()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-MISMATCH-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        // 1. Несовпадение суммы
        var badAmountForm = CreateBccNotifyForm(orderId: orderId, amount: "10000.00");
        var res1 = Assert.IsType<ObjectResult>(await controller.Notify(badAmountForm));
        Assert.Equal(StatusCodes.Status400BadRequest, res1.StatusCode);
        Assert.Contains("amount_mismatch", JsonSerializer.Serialize(res1.Value));

        // 2. Несовпадение валюты (например 840 = USD)
        var badCurrencyForm = CreateBccNotifyForm(orderId: orderId, currency: "840");
        var res2 = Assert.IsType<ObjectResult>(await controller.Notify(badCurrencyForm));
        Assert.Equal(StatusCodes.Status400BadRequest, res2.StatusCode);
        Assert.Contains("currency_mismatch", JsonSerializer.Serialize(res2.Value));

        // 3. Несовпадение терминала
        var badTerminalForm = CreateBccNotifyForm(orderId: orderId, terminal: "OTHER_TID");
        var res3 = Assert.IsType<ObjectResult>(await controller.Notify(badTerminalForm));
        Assert.Equal(StatusCodes.Status400BadRequest, res3.StatusCode);
        Assert.Contains("terminal_mismatch", JsonSerializer.Serialize(res3.Value));

        // 4. Несовпадение TRTYPE (например 99 вместо 1)
        var badTrTypeForm = CreateBccNotifyForm(orderId: orderId, trType: "99");
        var res4 = Assert.IsType<ObjectResult>(await controller.Notify(badTrTypeForm));
        Assert.Equal(StatusCodes.Status400BadRequest, res4.StatusCode);
        Assert.Contains("invalid_trtype", JsonSerializer.Serialize(res4.Value));

        // Статус платежа в БД не должен измениться
        var checkPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(checkPayment);
        Assert.Equal(PaymentStatuses.Created, checkPayment.Status);
    }

    [Fact(DisplayName = "BCC Notify: запоздалое уведомление не должно понизить paid или refunded")]
    public async Task Notify_LateNotificationAfterPaidOrRefunded_DoesNotDowngradeStatus()
    {
        string sessionId = CreateCompletedSession();
        string orderIdPaid = "ORD-BCC-ALREADY-PAID";
        var paymentPaid = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderIdPaid,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(paymentPaid);
        _paymentRepo.UpdateStatus(orderIdPaid, PaymentStatuses.Paid, paidAt: DateTime.UtcNow.ToString("o"));

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        // Отправляем запоздалый отказ банка (ACTION=1, RC=05)
        var lateDecline = CreateBccNotifyForm(orderId: orderIdPaid, action: "1", rc: "05", text: "Late decline");
        var res1 = Assert.IsType<OkObjectResult>(await controller.Notify(lateDecline));
        Assert.Equal(StatusCodes.Status200OK, res1.StatusCode);

        // Платёж остаётся paid
        var checkPaid = _paymentRepo.GetByOrderId(orderIdPaid);
        Assert.NotNull(checkPaid);
        Assert.Equal(PaymentStatuses.Paid, checkPaid.Status);
        var sessionPaid = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionPaid);
        Assert.True(sessionPaid.Paid);

        // Теперь моделируем платёж в статусе refunded для отдельной сессии
        string refundedSessionId = CreateCompletedSession();
        string orderIdRefunded = "ORD-BCC-ALREADY-REFUNDED";
        var paymentRefunded = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = refundedSessionId,
            OrderId = orderIdRefunded,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(paymentRefunded);
        _paymentRepo.UpdateStatus(orderIdRefunded, PaymentStatuses.Paid);
        _paymentRepo.UpdateStatus(orderIdRefunded, PaymentStatuses.Refunded);

        // Отправляем уведомление об успехе (ACTION=0, RC=00)
        var lateSuccess = CreateBccNotifyForm(orderId: orderIdRefunded, action: "0", rc: "00");
        var res2 = Assert.IsType<OkObjectResult>(await controller.Notify(lateSuccess));
        Assert.Equal(StatusCodes.Status200OK, res2.StatusCode);

        // Платёж остаётся refunded
        var checkRefunded = _paymentRepo.GetByOrderId(orderIdRefunded);
        Assert.NotNull(checkRefunded);
        Assert.Equal(PaymentStatuses.Refunded, checkRefunded.Status);
        var sessionRefunded = _sessions.GetSession(refundedSessionId);
        Assert.NotNull(sessionRefunded);
        Assert.False(sessionRefunded.Paid);

    }

    [Fact(DisplayName = "BCC Notify: callback без настроек авторизации возвращает 503 и не меняет платёж")]
    public async Task Notify_WhenAuthNotConfigured_Returns503WithoutDatabaseModifications()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-NO-AUTH-CONFIG";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        // Сервис без NotifyUsername и NotifyPassword
        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "TID999",
            NotifyUsername = "",
            NotifyPassword = ""
        };
        var service = new BccNotificationService(_paymentRepo, options: options);
        var controller = new BccCallbacksController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");
        var actionResult = await controller.Notify(form);
        var unavailable = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        Assert.Contains("bcc_notifications_not_configured", JsonSerializer.Serialize(unavailable.Value));

        // Платёж остаётся created
        var checkPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(checkPayment);
        Assert.Equal(PaymentStatuses.Created, checkPayment.Status);
    }

    [Fact(DisplayName = "BCC Notify: test + флаг true + без Basic Auth → уведомление принимается")]
    public async Task Notify_TestEnvironment_WithFlagTrue_WithoutBasicAuth_AcceptsNotification()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-TEST-NOAUTH-001";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        // Тестовое окружение с allowUnauthenticated = true
        var (controller, _) = CreateBccCallbacksController(environment: "test", allowUnauthenticated: true);
        // Заголовок Authorization намеренно не передаётся

        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");
        var actionResult = await controller.Notify(form);
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        // Платёж успешно переходит в paid
        var updated = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updated);
        Assert.Equal(PaymentStatuses.Paid, updated.Status);
        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.True(session.Paid);
    }

    [Fact(DisplayName = "BCC Notify: Authorization заголовок и секреты никогда не попадают в логи")]
    public async Task Notify_AuthorizationHeader_IsNeverLoggedInAnyEnvironment()
    {
        const string authorization = "Basic dGVzdF91c2VyOnRlc3RfcGFzc3dvcmQ=";
        var logger = new ListLogger<BccNotificationService>();
        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "TID999"
        };
        var service = new BccNotificationService(_paymentRepo, options: options, logger: logger);
        var emptyForm = new FormCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());

        await service.ProcessNotificationAsync(authorization, emptyForm, "trace-auth-1");
        await service.ProcessNotificationAsync(authorization, emptyForm, "trace-auth-2");

        // Ни одно событие не должно содержать сырой заголовок авторизации или пароль
        Assert.DoesNotContain(logger.Entries, e =>
            e.Message.Contains(authorization) ||
            (e.Properties != null && e.Properties.Values.Any(v => v?.ToString()?.Contains(authorization) == true)));
    }

    [Fact(DisplayName = "BCC Notify: production без Basic Auth возвращает 401 (при наличии настроек) или 503 (при отсутствии)")]
    public async Task Notify_Production_WithoutBasicAuth_Returns401Or503()
    {
        string sessionId = CreateCompletedSession();
        string orderId1 = "ORD-BCC-PROD-NOAUTH-401";
        var payment1 = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId1,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "production",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment1);

        // 1. Production с настроенными учетными данными, но без заголовка Authorization -> 401
        var (controllerConfigured, _) = CreateBccCallbacksController(
            username: "prod_user",
            password: "prod_password",
            environment: "production",
            allowUnauthenticated: false);

        var form1 = CreateBccNotifyForm(orderId: orderId1, action: "0", rc: "00");
        var res1 = Assert.IsType<ObjectResult>(await controllerConfigured.Notify(form1));
        Assert.Equal(StatusCodes.Status401Unauthorized, res1.StatusCode);
        Assert.Equal("Basic realm=\"BCC Notify\"", controllerConfigured.Response.Headers.WWWAuthenticate.ToString());

        // 2. Production без настроенных учетных данных -> 503
        string sessionId2 = CreateCompletedSession();
        string orderId2 = "ORD-BCC-PROD-NOAUTH-503";
        var payment2 = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId2,
            OrderId = orderId2,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "production",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment2);

        var (controllerUnconfigured, _) = CreateBccCallbacksController(
            username: "",
            password: "",
            environment: "production",
            allowUnauthenticated: false);

        var form2 = CreateBccNotifyForm(orderId: orderId2, action: "0", rc: "00");
        var res2 = Assert.IsType<ObjectResult>(await controllerUnconfigured.Notify(form2));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, res2.StatusCode);
        Assert.Contains("bcc_notifications_not_configured", JsonSerializer.Serialize(res2.Value));
    }

    [Fact(DisplayName = "BCC Notify: production никогда не принимает unauthenticated callback, даже если флаг true")]
    public async Task Notify_Production_NeverAcceptsUnauthenticatedCallback_EvenIfFlagTrue()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-PROD-FLAG-TRUE";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "production",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        // Флаг allowUnauthenticated выставлен в true, но environment = "production"
        var (controller, _) = CreateBccCallbacksController(
            username: "prod_user",
            password: "prod_password",
            environment: "production",
            allowUnauthenticated: true);

        // Без Basic Auth
        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");
        var res = Assert.IsType<ObjectResult>(await controller.Notify(form));
        Assert.Equal(StatusCodes.Status401Unauthorized, res.StatusCode);

        // Платёж остался created
        var check = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(check);
        Assert.Equal(PaymentStatuses.Created, check.Status);
    }

    [Fact(DisplayName = "BCC Notify: test + флаг false не принимает callback без защиты")]
    public async Task Notify_TestEnvironment_WithFlagFalse_RejectsUnauthenticatedCallback()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-TEST-FLAG-FALSE";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        // Тестовое окружение с allowUnauthenticated = false
        var (controller, _) = CreateBccCallbacksController(
            username: "test_user",
            password: "test_password",
            environment: "test",
            allowUnauthenticated: false);

        // Без Basic Auth
        var form = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "00");
        var res = Assert.IsType<ObjectResult>(await controller.Notify(form));
        Assert.Equal(StatusCodes.Status401Unauthorized, res.StatusCode);

        var check = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(check);
        Assert.Equal(PaymentStatuses.Created, check.Status);
    }

    [Fact(DisplayName = "BCC Notify: дробное несовпадение суммы отклоняется точно без округления")]
    public async Task Notify_FractionalAmountMismatch_Rejected()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-EXACT-AMOUNT";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        // 1. 49990.01 отклоняется
        var form01 = CreateBccNotifyForm(orderId: orderId, amount: "49990.01");
        var res01 = Assert.IsType<ObjectResult>(await controller.Notify(form01));
        Assert.Equal(StatusCodes.Status400BadRequest, res01.StatusCode);
        Assert.Contains("amount_mismatch", JsonSerializer.Serialize(res01.Value));

        // 2. 49990.49 отклоняется (не должно округляться в 49990)
        var form49 = CreateBccNotifyForm(orderId: orderId, amount: "49990.49");
        var res49 = Assert.IsType<ObjectResult>(await controller.Notify(form49));
        Assert.Equal(StatusCodes.Status400BadRequest, res49.StatusCode);
        Assert.Contains("amount_mismatch", JsonSerializer.Serialize(res49.Value));

        // Статус платежа не изменился
        var check = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(check);
        Assert.Equal(PaymentStatuses.Created, check.Status);

        // 3. 49990.00 успешно принимается
        var form00 = CreateBccNotifyForm(orderId: orderId, amount: "49990.00");
        var res00 = Assert.IsType<OkObjectResult>(await controller.Notify(form00));
        Assert.Equal(StatusCodes.Status200OK, res00.StatusCode);
        var checkPaid = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(checkPaid);
        Assert.Equal(PaymentStatuses.Paid, checkPaid.Status);
    }

    [Fact(DisplayName = "BCC Notify: отсутствующий ACTION или RC возвращает 400 и не меняет статус")]
    public async Task Notify_MissingActionOrRc_Returns400AndDoesNotChangeStatus()
    {
        string sessionId = CreateCompletedSession();
        string orderId = "ORD-BCC-MISSING-ACTION-RC";
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(payment);

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        // 1. ACTION пустой
        var noActionForm = CreateBccNotifyForm(orderId: orderId, action: "", rc: "00");
        var resAction = Assert.IsType<ObjectResult>(await controller.Notify(noActionForm));
        Assert.Equal(StatusCodes.Status400BadRequest, resAction.StatusCode);
        Assert.Contains("missing_action", JsonSerializer.Serialize(resAction.Value));

        // 2. RC пустой
        var noRcForm = CreateBccNotifyForm(orderId: orderId, action: "0", rc: "");
        var resRc = Assert.IsType<ObjectResult>(await controller.Notify(noRcForm));
        Assert.Equal(StatusCodes.Status400BadRequest, resRc.StatusCode);
        Assert.Contains("missing_rc", JsonSerializer.Serialize(resRc.Value));

        // Статус в БД остаётся created
        var check = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(check);
        Assert.Equal(PaymentStatuses.Created, check.Status);
    }

    [Fact(DisplayName = "BCC Notify: ответ содержит фактический статус из БД после защищённого paid и refunded")]
    public async Task Notify_ResponseContainsActualDatabaseStatus_AfterProtectedPaidOrRefunded()
    {
        string sessionId = CreateCompletedSession();

        // 1. Проверяем paid платёж: при запоздалом отказе в ответе должен быть "paid", а не "failed"
        string orderIdPaid = "ORD-BCC-ACTUAL-PAID";
        var paymentPaid = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderIdPaid,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(paymentPaid);
        _paymentRepo.UpdateStatus(orderIdPaid, PaymentStatuses.Paid, paidAt: DateTime.UtcNow.ToString("o"));

        var (controller, _) = CreateBccCallbacksController();
        controller.ControllerContext.HttpContext.Request.Headers.Authorization = CreateBasicAuthHeader("bcc_user", "bcc_secret_password");

        var lateDecline = CreateBccNotifyForm(orderId: orderIdPaid, action: "1", rc: "05", text: "Late decline");
        var res1 = Assert.IsType<OkObjectResult>(await controller.Notify(lateDecline));
        string json1 = JsonSerializer.Serialize(res1.Value);
        Assert.Contains("\"paymentStatus\":\"paid\"", json1);

        // 2. Проверяем refunded платёж: при запоздалом одобрении в ответе должен быть "refunded", а не "paid"
        string refundedSessionId = CreateCompletedSession();
        string orderIdRefunded = "ORD-BCC-ACTUAL-REFUNDED";
        var paymentRefunded = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = refundedSessionId,
            OrderId = orderIdRefunded,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "TID999",
            Status = PaymentStatuses.Created
        };
        _paymentRepo.Create(paymentRefunded);
        _paymentRepo.UpdateStatus(orderIdRefunded, PaymentStatuses.Paid);
        _paymentRepo.UpdateStatus(orderIdRefunded, PaymentStatuses.Refunded);

        var lateSuccess = CreateBccNotifyForm(orderId: orderIdRefunded, action: "0", rc: "00");
        var res2 = Assert.IsType<OkObjectResult>(await controller.Notify(lateSuccess));
        string json2 = JsonSerializer.Serialize(res2.Value);
        Assert.Contains("\"paymentStatus\":\"refunded\"", json2);
    }

    private (BccCallbacksController Controller, BccNotificationService Service) CreateBccCallbacksController(
        string username = "bcc_user",
        string password = "bcc_secret_password",
        string terminalId = "TID999",
        string environment = "test",
        bool allowUnauthenticated = false)
    {
        var options = new BccPaymentOptions
        {
            Environment = environment,
            TerminalId = terminalId,
            GatewayUrl = "https://test-epay.bcc.kz/pay",
            NotifyUrl = "https://example.com/api/payments/bcc/notify",
            ReturnUrl = "https://example.com/api/payments/bcc/return",
            MerchantId = "00000001",
            MerchantName = "FENIX LEGAL OS",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            NotifyUsername = username,
            NotifyPassword = password,
            AllowUnauthenticatedTestNotifications = allowUnauthenticated
        };
        var service = new BccNotificationService(_paymentRepo, options: options);
        var controller = new BccCallbacksController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        return (controller, service);
    }


    private static string CreateBasicAuthHeader(string username, string password)
    {
        string raw = $"{username}:{password}";
        return "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw));
    }

    private static IFormCollection CreateBccNotifyForm(
        string orderId,
        string amount = "49990.00",
        string currency = "398",
        string terminal = "TID999",
        string trType = "1",
        string action = "0",
        string rc = "00",
        string rrn = "123456789012",
        string intRef = "INT12345678",
        string approval = "APP123",
        string madvCode = "0",
        string text = "Transaction approved")
    {
        var dict = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = orderId,
            ["AMOUNT"] = amount,
            ["CURRENCY"] = currency,
            ["TERMINAL"] = terminal,
            ["TRTYPE"] = trType,
            ["ACTION"] = action,
            ["RC"] = rc,
            ["RRN"] = rrn,
            ["INT_REF"] = intRef,
            ["APPROVAL"] = approval,
            ["MADV_CODE"] = madvCode,
            ["TEXT"] = text
        };
        return new FormCollection(dict);
    }

    private static BccPaymentGateway CreateFullyConfiguredGateway()

    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "TID999",
                ["BCC_GATEWAY_URL"] = "https://test-epay.bcc.kz/pay",
                ["BCC_NOTIFY_URL"] = "https://example.com/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://example.com/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "FENIX LEGAL OS",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
            })
            .Build();

        return new BccPaymentGateway(config);
    }

    private string CreateCompletedSession()
    {
        string sessionId = _sessions.CreateSession();
        _sessions.CompleteSession(sessionId, "{}", new ScoreResult { Overall = 50 });
        var user = _userRepo.CreateUser(
            email: $"test_{Guid.NewGuid():N}@example.com",
            password: "password123",
            name: "Test User",
            company: "Test Co",
            position: "CEO",
            phone: "+77001234567");
        _userRepo.AttachUserToSession(sessionId, user.Id);
        return sessionId;
    }

    [Fact(DisplayName = "PaymentService logs Information with OrderId on start")]
    public async Task PaymentService_LogsInformationWithOrderId_OnStart()
    {
        var logger = new ListLogger<PaymentService>();
        var configuredGateway = CreateFullyConfiguredGateway();
        var serviceWithLogger = new PaymentService(_sessions, _settings, _paymentRepo, configuredGateway, _userRepo, logger);

        string sessionId = CreateCompletedSession();
        var result = await serviceWithLogger.StartAsync(
            sessionId: sessionId,
            requestedTariff: "report",
            browserScreenHeight: 1080,
            browserScreenWidth: 1920,
            clientIp: "127.0.0.1",
            billingAddress: "г. Астана, ул. Абая, д. 10");

        Assert.Equal(200, result.StatusCode);
        var createdLogs = logger.Entries.Where(e => e.EventId == PaymentEvents.PaymentCreated).ToList();
        Assert.NotEmpty(createdLogs);
        var createdLog = createdLogs.First();
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, createdLog.LogLevel);

        // Проверяем, что OrderId присутствует в логах
        string json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        string orderId = doc.RootElement.GetProperty("orderId").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(orderId));
        Assert.Contains(orderId, createdLog.Message);
    }

    [Fact(DisplayName = "BccNotificationService logs ACTION and RC on successful callback")]
    public async Task BccNotificationService_LogsActionAndRc_OnSuccess()
    {
        var logger = new ListLogger<BccNotificationService>();
        var notifyService = new BccNotificationService(_paymentRepo, null, new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "88888881",
            AllowUnauthenticatedTestNotifications = true
        }, logger);

        string sessionId = CreateCompletedSession();
        string orderId = $"ORDER_{Guid.NewGuid():N}";
        _paymentRepo.Create(new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "88888881",
            Status = PaymentStatuses.Created
        });

        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = orderId,
            ["AMOUNT"] = "49990.00",
            ["CURRENCY"] = "398",
            ["TERMINAL"] = "88888881",
            ["TRTYPE"] = "1",
            ["ACTION"] = "0",
            ["RC"] = "00",
            ["RRN"] = "123456789012",
            ["INT_REF"] = "INT999"
        });

        var res = await notifyService.ProcessNotificationAsync(null, form);
        Assert.Equal(200, res.StatusCode);

        var actionRcLogs = logger.Entries.Where(e => e.Message.Contains("Банковский результат принят")).ToList();
        Assert.NotEmpty(actionRcLogs);
        var log = actionRcLogs.First();
        Assert.Contains("ACTION 0", log.Message);
        Assert.Contains("RC 00", log.Message);
        Assert.Contains(orderId, log.Message);
    }

    [Fact(DisplayName = "BccNotificationService logs Warning on invalid callback")]
    public async Task BccNotificationService_LogsWarning_OnInvalidCallback()
    {
        var logger = new ListLogger<BccNotificationService>();
        var notifyService = new BccNotificationService(_paymentRepo, null, new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "88888881",
            AllowUnauthenticatedTestNotifications = true
        }, logger);

        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = "NON_EXISTING_ORDER_999",
            ["AMOUNT"] = "49990.00",
            ["CURRENCY"] = "398",
            ["TERMINAL"] = "88888881",
            ["TRTYPE"] = "1",
            ["ACTION"] = "0",
            ["RC"] = "00"
        });

        var res = await notifyService.ProcessNotificationAsync(null, form);
        Assert.Equal(400, res.StatusCode);

        var warnings = logger.Entries.Where(e => e.LogLevel == Microsoft.Extensions.Logging.LogLevel.Warning).ToList();
        Assert.NotEmpty(warnings);
        Assert.Contains(warnings, w => w.Message.Contains("не найден"));
    }

    [Fact(DisplayName = "BccNotificationService logs separate Warning when callback received unauthenticated in test environment")]
    public async Task BccNotificationService_LogsSeparateWarning_WhenUnauthenticatedInTest()
    {
        var logger = new ListLogger<BccNotificationService>();
        var notifyService = new BccNotificationService(_paymentRepo, null, new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "88888881",
            AllowUnauthenticatedTestNotifications = true
        }, logger);

        string sessionId = CreateCompletedSession();
        string orderId = $"ORDER_{Guid.NewGuid():N}";
        _paymentRepo.Create(new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "88888881",
            Status = PaymentStatuses.Created
        });

        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = orderId,
            ["AMOUNT"] = "49990.00",
            ["CURRENCY"] = "398",
            ["TERMINAL"] = "88888881",
            ["TRTYPE"] = "1",
            ["ACTION"] = "0",
            ["RC"] = "00"
        });

        await notifyService.ProcessNotificationAsync(null, form);

        var testUnauthWarnings = logger.Entries.Where(e =>
            e.LogLevel == Microsoft.Extensions.Logging.LogLevel.Warning &&
            e.EventId == PaymentEvents.BccUnauthenticatedTestCallbackAccepted &&
            e.Message.Contains("без авторизации")).ToList();

        Assert.NotEmpty(testUnauthWarnings);
    }

    [Fact(DisplayName = "BccNotificationService logs forbidden status transition or protected status")]
    public async Task BccNotificationService_LogsForbiddenStatusTransition()
    {
        var logger = new ListLogger<BccNotificationService>();
        var notifyService = new BccNotificationService(_paymentRepo, null, new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "88888881",
            AllowUnauthenticatedTestNotifications = true
        }, logger);

        string sessionId = CreateCompletedSession();
        string orderId = $"ORDER_{Guid.NewGuid():N}";
        _paymentRepo.Create(new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            AmountKzt = 49990,
            Currency = "KZT",
            Provider = "bcc",
            Environment = "test",
            TerminalId = "88888881",
            Status = PaymentStatuses.Created
        });
        _paymentRepo.UpdateStatus(orderId, PaymentStatuses.Paid, paidAt: DateTime.UtcNow.ToString("o"));

        // Запоздалый неуспешный коллбэк (ACTION=1)
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = orderId,
            ["AMOUNT"] = "49990.00",
            ["CURRENCY"] = "398",
            ["TERMINAL"] = "88888881",
            ["TRTYPE"] = "1",
            ["ACTION"] = "1",
            ["RC"] = "05"
        });

        var res = await notifyService.ProcessNotificationAsync(null, form);
        Assert.Equal(200, res.StatusCode);

        // Проверяем, что залогировано предупреждение о том, что переход был отклонён и статус не изменился
        var statusWarnings = logger.Entries.Where(e =>
            e.LogLevel == Microsoft.Extensions.Logging.LogLevel.Warning &&
            (e.Message.Contains("был отклонён") || e.Message.Contains("остался paid"))).ToList();

        Assert.NotEmpty(statusWarnings);
    }

    [Fact(DisplayName = "Logging never leaks sensitive data in messages, structured properties, or exceptions")]
    public async Task Logging_NeverLeaks_SensitiveData()
    {
        var pLogger = new ListLogger<PaymentService>();
        var gLogger = new ListLogger<BccPaymentGateway>();
        var nLogger = new ListLogger<BccNotificationService>();
        var cLogger = new ListLogger<BccCallbacksController>();

        string secretMacKey = "6BB0AC02E47BDF73D98FEB777F3B5294";
        string secretPassword = "SuperSecretPassword123!";
        string secretUsername = "notifyUser";
        string secretPhone = "+77019998877";
        string secretAddress = "г. Алматы, ул. Секретная, д. 77, кв. 99";

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888881",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenixsls.org:443/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenixsls.org:443/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "TOO MERCHANT",
                ["BCC_MAC_KEY"] = secretMacKey,
                ["BCC_NOTIFY_USERNAME"] = secretUsername,
                ["BCC_NOTIFY_PASSWORD"] = secretPassword,
                ["BCC_ALLOW_UNAUTHENTICATED_TEST_NOTIFICATIONS"] = "false"
            })
            .Build();

        var gateway = new BccPaymentGateway(config, gLogger);
        var paymentService = new PaymentService(_sessions, _settings, _paymentRepo, gateway, _userRepo, pLogger);

        string sessionId = _sessions.CreateSession();
        _sessions.CompleteSession(sessionId, "{}", new ScoreResult { Overall = 50 });
        var user = _userRepo.CreateUser(
            email: $"test_{Guid.NewGuid():N}@example.com",
            password: "password123",
            name: "Secret User",
            company: "Secret Co",
            position: "CEO",
            phone: secretPhone);
        _userRepo.AttachUserToSession(sessionId, user.Id);

        // Запуск платежа
        var startResult = await paymentService.StartAsync(
            sessionId: sessionId,
            requestedTariff: "report",
            browserScreenHeight: 1080,
            browserScreenWidth: 1920,
            clientIp: "127.0.0.1",
            billingAddress: secretAddress);

        Assert.Equal(200, startResult.StatusCode);

        string pSign = "";
        string mInfo = "";
        if (startResult.Value is not null)
        {
            var formFieldsProp = startResult.Value.GetType().GetProperty("formFields");
            if (formFieldsProp?.GetValue(startResult.Value) is Dictionary<string, string> dict)
            {
                dict.TryGetValue("P_SIGN", out pSign!);
                dict.TryGetValue("M_INFO", out mInfo!);
            }
        }
        Assert.False(string.IsNullOrWhiteSpace(pSign));
        Assert.False(string.IsNullOrWhiteSpace(mInfo));

        // Вызов нотификации через контроллер
        var notifyService = new BccNotificationService(_paymentRepo, config, null, nLogger);
        var controller = new BccCallbacksController(notifyService, cLogger)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "trace-test-123",
                    Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1") }
                }
            }
        };

        string authHeader = "Basic " + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{secretUsername}:{secretPassword}"));
        controller.HttpContext.Request.Headers["Authorization"] = authHeader;

        string startJson = JsonSerializer.Serialize(startResult.Value);
        using var startDoc = JsonDocument.Parse(startJson);
        string orderId = startDoc.RootElement.GetProperty("orderId").GetString()!;
        var form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["ORDER"] = orderId,
            ["AMOUNT"] = "49990.00",
            ["CURRENCY"] = "398",
            ["TERMINAL"] = "88888881",
            ["TRTYPE"] = "1",
            ["ACTION"] = "0",
            ["RC"] = "00",
            ["RRN"] = "123456789012",
            ["INT_REF"] = "INT999",
            ["TEXT"] = "BANK TRANSACTION APPROVED TEXT WITH SENSITIVE DATA"
        });

        await controller.Notify(form);

        // Проверяем все логи всех компонентов
        var allEntries = pLogger.Entries
            .Concat(gLogger.Entries)
            .Concat(nLogger.Entries)
            .Concat(cLogger.Entries)
            .ToList();

        var sensitiveStrings = new[]
        {
            secretMacKey,
            pSign,
            secretPassword,
            authHeader,
            secretPhone,
            "7019998877",
            secretAddress,
            "Секретная",
            mInfo,
            "BANK TRANSACTION APPROVED TEXT WITH SENSITIVE DATA"
        };

        foreach (var entry in allEntries)
        {
            string msg = entry.Message;
            string exStr = entry.Exception?.ToString() ?? "";

            foreach (var secret in sensitiveStrings)
            {
                // Проверка отформатированного сообщения
                Assert.DoesNotContain(secret, msg, StringComparison.OrdinalIgnoreCase);

                // Проверка Exception.ToString()
                if (!string.IsNullOrEmpty(exStr))
                {
                    Assert.DoesNotContain(secret, exStr, StringComparison.OrdinalIgnoreCase);
                }

                // Проверка всех структурированных свойств из state
                foreach (var kvp in entry.Properties)
                {
                    string propKey = kvp.Key;
                    string propVal = kvp.Value?.ToString() ?? "";
                    Assert.DoesNotContain(secret, propKey, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain(secret, propVal, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
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

public sealed class LogEntry
{
    public Microsoft.Extensions.Logging.LogLevel LogLevel { get; init; }
    public Microsoft.Extensions.Logging.EventId EventId { get; init; }
    public string Message { get; init; } = "";
    public Exception? Exception { get; init; }
    public IReadOnlyDictionary<string, object?> Properties { get; init; } = new Dictionary<string, object?>();
}

public sealed class ListLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
{
    private readonly List<LogEntry> _entries = new();
    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return NullScope.Instance;
    }

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

    public void Log<TState>(
        Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        string message = formatter(state, exception);
        var properties = new Dictionary<string, object?>();

        if (state is IEnumerable<KeyValuePair<string, object?>> stateProperties)
        {
            foreach (var kvp in stateProperties)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }
        else if (state is IReadOnlyList<KeyValuePair<string, object?>> readOnlyList)
        {
            foreach (var kvp in readOnlyList)
            {
                properties[kvp.Key] = kvp.Value;
            }
        }

        _entries.Add(new LogEntry
        {
            LogLevel = logLevel,
            EventId = eventId,
            Message = message,
            Exception = exception,
            Properties = properties
        });
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
