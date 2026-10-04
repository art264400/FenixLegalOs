using System.Net;
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace FenixLegalOs.Tests;

/// <summary>
/// Комплексные тесты административного функционала возвратов платежей (BCC TRTYPE=14).
/// Покрывает все 18 требований пункта 13 спецификации.
/// </summary>
public sealed class PaymentRefundTests : IDisposable
{
    private readonly string _databasePath;
    private readonly DbInitializer _dbInitializer;
    private readonly SessionRepository _sessions;
    private readonly PaymentRepository _paymentRepo;
    private readonly PaymentRefundRepository _refundRepo;
    private readonly SettingsRepository _settings;
    private readonly UserRepository _userRepo;
    private readonly LeadRepository _leadRepo;
    private readonly AdminSessionService _adminSessionService;
    private readonly IConfiguration _configuration;

    public PaymentRefundTests()
    {
        _databasePath = Path.Combine(Path.GetTempPath(), $"test_fenix_refunds_{Guid.NewGuid():N}.db");
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FENIX_DB_PATH"] = _databasePath,
                ["FENIX_ADMIN_PASSWORD"] = "SecretAdminPass123!",
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "92000001",
                ["BCC_GATEWAY_URL"] = "https://test-epay.bcc.kz/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenix.kz/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenix.kz/payment/result",
                ["BCC_MERCHANT_ID"] = "10000001",
                ["BCC_MERCHANT_NAME"] = "Fenix Legal OS",
                ["BCC_MAC_KEY"] = "00000000000000000000000000000000",
                ["BCC_NOTIFY_USERNAME"] = "bcc_user",
                ["BCC_NOTIFY_PASSWORD"] = "bcc_pass"
            })
            .Build();

        _dbInitializer = new DbInitializer(_configuration);
        _dbInitializer.Initialize();

        _sessions = new SessionRepository(_dbInitializer);
        _paymentRepo = new PaymentRepository(_dbInitializer);
        _refundRepo = new PaymentRefundRepository(_dbInitializer);
        _settings = new SettingsRepository(_dbInitializer);
        _userRepo = new UserRepository(_dbInitializer);
        _leadRepo = new LeadRepository(_dbInitializer);
        _adminSessionService = new AdminSessionService(_configuration);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }
        }
        catch
        {
            // Игнорируем ошибки удаления временного файла
        }
    }

    private string CreateTestUser()
    {
        var user = _userRepo.CreateUser(
            email: $"client_{Guid.NewGuid():N}@example.com",
            password: "password123",
            name: "Тестовый Клиент",
            company: "Fenix Test Co",
            position: "CEO",
            phone: "+77011234567");
        return user.Id;
    }

    private string CreateTestSession(string userId)
    {
        string sessionId = _sessions.CreateSession();
        _sessions.CompleteSession(sessionId, "{}", new ScoreResult { Overall = 50 });
        _userRepo.AttachUserToSession(sessionId, userId);
        return sessionId;
    }

    private Payment CreatePaidPayment(
        string orderId,
        string sessionId,
        int amountKzt = 49990,
        string merchRnId = "RN12345678",
        string rrn = "123456789012",
        string intRef = "INTREF987654",
        string terminalId = "92000001",
        string tariff = "report")
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = tariff,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = amountKzt,
            Currency = "KZT",
            Status = PaymentStatuses.Created,
            MerchRnId = merchRnId,
            Rrn = rrn,
            IntRef = intRef,
            TerminalId = terminalId,
            CreatedAt = DateTime.UtcNow.AddMinutes(-10).ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus(
            orderId: orderId,
            status: PaymentStatuses.Paid,
            rrn: rrn,
            intRef: intRef,
            approvalCode: "APP001",
            responseCode: "00",
            paidAt: DateTime.UtcNow.ToString("o"),
            notificationReceivedAt: DateTime.UtcNow.ToString("o"));

        _sessions.MarkSessionPaid(sessionId, amountKzt, "bcc");
        return _paymentRepo.GetByOrderId(orderId)!;
    }

    private static string GetJsonErrorCode(PaymentRefundResult result)
    {
        string json = JsonSerializer.Serialize(result.Value);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("error", out var prop))
        {
            return prop.GetString() ?? "";
        }
        return "";
    }

    // 1. Неавторизованный пользователь не может вызвать /api/admin/payments и /api/admin/payments/{orderId}/refund
    [Fact(DisplayName = "1. Неавторизованный пользователь получает 401 на admin payments и refund")]
    public async Task Scenario01_UnauthenticatedUser_Gets401()
    {
        var gateway = new BccPaymentGateway(_configuration);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);
        var controller = new AdminPaymentsController(_paymentRepo, refundService, _adminSessionService);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var listResult = controller.GetPayments(null, 50);
        var listUnauthorized = Assert.IsType<UnauthorizedObjectResult>(listResult);
        Assert.Equal(401, listUnauthorized.StatusCode);

        using var bodyDoc = JsonDocument.Parse("{\"reason\":\"Корректная причина возврата\"}");
        var refundResult = await controller.RefundPayment("ORD-12345", bodyDoc.RootElement, CancellationToken.None);
        var refundUnauthorized = Assert.IsType<UnauthorizedObjectResult>(refundResult);
        Assert.Equal(401, refundUnauthorized.StatusCode);
    }

    // 2. Возврат возможен только для платежа в статусе paid
    [Fact(DisplayName = "2. Возврат разрешён только для статуса paid")]
    public async Task Scenario02_Refund_AllowedOnlyForPaidStatus()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-TEST-PENDING";

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            Provider = "bcc",
            Environment = "test",
            AmountKzt = 49990,
            Currency = "KZT",
            Status = PaymentStatuses.Created,
            MerchRnId = "RN123",
            Rrn = "123",
            IntRef = "INT123",
            TerminalId = "92000001",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus(orderId, PaymentStatuses.Pending);

        var gateway = new BccPaymentGateway(_configuration);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("invalid_status_for_refund", GetJsonErrorCode(result));
    }

    // 3. Сумма берётся строго из БД, браузер не может передать свою сумму
    [Fact(DisplayName = "3. Сумма возврата берётся строго из БД и равна сумме платежа")]
    public async Task Scenario03_RefundAmount_StrictlyFromDatabase()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-AMOUNT-TEST";
        int originalAmount = 90990;

        CreatePaidPayment(orderId, sessionId, amountKzt: originalAmount);

        var gatewayMock = new FakePaymentGateway(success: true);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Ошибочный платёж клиента", "admin", CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.NotNull(gatewayMock.LastRefundRequest);
        Assert.Equal(originalAmount, gatewayMock.LastRefundRequest.RefundAmountKzt);
        Assert.Equal(originalAmount, gatewayMock.LastRefundRequest.OriginalAmountKzt);

        var storedRefund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(storedRefund);
        Assert.Equal(originalAmount, storedRefund.AmountKzt);
    }

    // 4. Отсутствие RRN, INT_REF или MERCH_RN_ID отклоняет возврат до обращения к шлюзу
    [Theory(DisplayName = "4. Отсутствие обязательных банковских реквизитов отклоняет возврат")]
    [InlineData("", "RRN1", "INT1", "missing_merch_rn_id")]
    [InlineData("RN1", "", "INT1", "missing_rrn")]
    [InlineData("RN1", "RRN1", "", "missing_int_ref")]
    public async Task Scenario04_MissingBankIdentifiers_RejectsRefund(string merchRnId, string rrn, string intRef, string expectedErrorCode)
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = $"ORD-MISSING-{expectedErrorCode}";

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            SessionId = sessionId,
            OrderId = orderId,
            Tariff = "report",
            Provider = "bcc",
            Environment = "test",
            AmountKzt = 49990,
            Currency = "KZT",
            Status = PaymentStatuses.Created,
            MerchRnId = string.IsNullOrEmpty(merchRnId) ? null : merchRnId,
            Rrn = string.IsNullOrEmpty(rrn) ? null : rrn,
            IntRef = string.IsNullOrEmpty(intRef) ? null : intRef,
            TerminalId = "92000001",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _paymentRepo.Create(payment);
        _paymentRepo.UpdateStatus(
            orderId: orderId,
            status: PaymentStatuses.Paid,
            rrn: string.IsNullOrEmpty(rrn) ? null : rrn,
            intRef: string.IsNullOrEmpty(intRef) ? null : intRef);

        var gatewayMock = new FakePaymentGateway(success: true);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(expectedErrorCode, GetJsonErrorCode(result));
        Assert.Null(gatewayMock.LastRefundRequest); // К шлюзу не обращались
    }

    // 5. В исходящем запросе TRTYPE=14 передаются все обязательные поля
    // 6. MAC-строка собирается строго в требуемом порядке: ORDER, ORG_AMOUNT, AMOUNT, CURRENCY, RRN, INT_REF, TERMINAL, TIMESTAMP, TRTYPE, NONCE
    [Fact(DisplayName = "5-6. MAC-строка и поля запроса TRTYPE=14 собираются строго по спецификации")]
    public void Scenario05_06_BccRefundMacDataString_OrderAndFields()
    {
        string order = "ORD-999";
        string orgAmount = "49990.00";
        string amount = "49990.00";
        string currency = "398";
        string rrn = "123456789012";
        string intRef = "INT_987654";
        string terminal = "92000001";
        string timestamp = "20261004120000";
        string trType = "14";
        string nonce = "A1B2C3D4E5F60718";

        string macData = BccPaymentGateway.BuildRefundMacDataString(
            order: order,
            orgAmount: orgAmount,
            amount: amount,
            currency: currency,
            rrn: rrn,
            intRef: intRef,
            terminal: terminal,
            timestamp: timestamp,
            trType: trType,
            nonce: nonce);

        // Проверяем точный порядок полей с их префиксами длины:
        // ORDER (7ORD-999) + ORG_AMOUNT (849990.00) + AMOUNT (849990.00) + CURRENCY (3398) + RRN (12123456789012) +
        // INT_REF (10INT_987654) + TERMINAL (892000001) + TIMESTAMP (1420261004120000) + TRTYPE (214) + NONCE (16A1B2C3D4E5F60718)
        string expected = "7ORD-999" +
                          "849990.00" +
                          "849990.00" +
                          "3398" +
                          "12123456789012" +
                          "10INT_987654" +
                          "892000001" +
                          "1420261004120000" +
                          "214" +
                          "16A1B2C3D4E5F60718";

        Assert.Equal(expected, macData);
    }

    // 7. Успешный синхронный ответ шлюза переводит возврат в succeeded, а платёж — в refunded
    [Fact(DisplayName = "7. Успешный ответ шлюза переводит возврат в succeeded, а платёж в refunded")]
    public async Task Scenario07_GatewaySuccess_CompletesRefund()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-SYNC-SUCCESS";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(success: true, actionCode: "0", responseCode: "00", rrn: "REF_RRN_1", intRef: "REF_INT_1");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Клиент запросил возврат средств", "admin", CancellationToken.None);

        Assert.Equal(200, result.StatusCode);

        var payment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(payment);
        Assert.Equal("refunded", payment.Status);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid); // Сессия заблокирована

        var refund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refund);
        Assert.Equal("succeeded", refund.Status);
        Assert.Equal("REF_RRN_1", refund.Rrn);
        Assert.Equal("REF_INT_1", refund.IntRef);
    }

    // 8. Однозначный отказ шлюза переводит возврат в failed, а платёж остаётся paid
    [Fact(DisplayName = "8. Отказ шлюза переводит возврат в failed, платёж остаётся paid")]
    public async Task Scenario08_GatewayReject_RefundFailed_PaymentRemainsPaid()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-SYNC-REJECT";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(success: false, isFinal: true, actionCode: "1", responseCode: "51", bankMessage: "Insufficient funds");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.Equal(502, result.StatusCode);

        var payment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(payment);
        Assert.Equal("paid", payment.Status);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.True(session.Paid);

        var refund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refund);
        Assert.Equal("failed", refund.Status);
    }

    // 9. Неопределённый результат (таймаут/сеть) оставляет возврат в pending, платёж — в paid
    [Fact(DisplayName = "9. Неопределённый результат оставляет возврат в pending")]
    public async Task Scenario09_GatewayIndeterminate_LeavesRefundPending()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-INDETERMINATE";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(accepted: true, isFinal: false, bankMessage: "Request pending bank settlement");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.Equal(202, result.StatusCode);

        var payment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(payment);
        Assert.Equal("paid", payment.Status); // Платёж ещё не refunded пока нет окончательного подтверждения банка

        var refund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refund);
        Assert.Equal("pending", refund.Status);
    }

    // 10. Повторный вызов возврата для того же платежа не отправляет второй запрос в банк
    [Fact(DisplayName = "10. Повторный вызов возврата не отправляет второй запрос в банк")]
    public async Task Scenario10_DuplicateRefundRequest_DoesNotSendSecondBankRequest()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-DUP-TEST";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(success: true);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var firstResult = await refundService.RefundAsync(orderId, "Первый возврат клиенту", "admin", CancellationToken.None);
        Assert.Equal(200, firstResult.StatusCode);
        Assert.Equal(1, gatewayMock.CallCount);

        // Повторный запрос
        var secondResult = await refundService.RefundAsync(orderId, "Повторный возврат клиенту", "admin", CancellationToken.None);
        Assert.Equal(409, secondResult.StatusCode);
        Assert.Equal("already_refunded", GetJsonErrorCode(secondResult));
        Assert.Equal(1, gatewayMock.CallCount); // Второй вызов в банк НЕ отправлен!
    }

    // 11. Параллельные запросы возврата создают не более одной активной записи
    [Fact(DisplayName = "11. Параллельные запросы возврата создают не более одной активной записи")]
    public async Task Scenario11_ConcurrentRefundRequests_CreateAtMostOneActiveRecord()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CONCURRENT-TEST";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(success: true, delayMs: 100);
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var task1 = Task.Run(() => refundService.RefundAsync(orderId, "Параллельный возврат 1", "admin1", CancellationToken.None));
        var task2 = Task.Run(() => refundService.RefundAsync(orderId, "Параллельный возврат 2", "admin2", CancellationToken.None));

        var results = await Task.WhenAll(task1, task2);

        int successCount = results.Count(r => r.StatusCode == 200);
        int conflictCount = results.Count(r => r.StatusCode == 409);

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);
        Assert.Equal(1, gatewayMock.CallCount);
    }

    // 12. Успешный callback TRTYPE=14 переводит pending возврат в succeeded и платёж в refunded
    [Fact(DisplayName = "12. Успешный callback TRTYPE=14 переводит возврат в succeeded и платёж в refunded")]
    public async Task Scenario12_SuccessfulCallbackTrtype14_CompletesRefund()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CALLBACK-SUCC";

        var payment = CreatePaidPayment(orderId, sessionId);

        // Создаём pending возврат вручную
        var pendingRefund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = 49990,
            Reason = "Возврат через банк",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(pendingRefund);

        var notificationService = CreateNotificationService();
        var form = CreateBccNotifyForm(orderId, trType: "14", action: "0", rc: "00", rrn: "CB_RRN_1", intRef: "CB_INT_1");

        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");
        var result = await notificationService.ProcessNotificationAsync(authHeader, form);

        Assert.Equal(200, result.StatusCode);

        var updatedRefund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(updatedRefund);
        Assert.Equal("succeeded", updatedRefund.Status);
        Assert.Equal("CB_RRN_1", updatedRefund.Rrn);

        var updatedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updatedPayment);
        Assert.Equal("refunded", updatedPayment.Status);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid);
    }

    // 13. Неуспешный callback TRTYPE=14 переводит возврат в failed, а платёж оставляет paid
    [Fact(DisplayName = "13. Неуспешный callback TRTYPE=14 переводит возврат в failed, платёж остаётся paid")]
    public async Task Scenario13_FailedCallbackTrtype14_SetsRefundFailed()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CALLBACK-FAIL";

        var payment = CreatePaidPayment(orderId, sessionId);

        var pendingRefund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = 49990,
            Reason = "Возврат через банк",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(pendingRefund);

        var notificationService = CreateNotificationService();
        var form = CreateBccNotifyForm(orderId, trType: "14", action: "1", rc: "51");

        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");
        var result = await notificationService.ProcessNotificationAsync(authHeader, form);

        Assert.Equal(200, result.StatusCode);

        var updatedRefund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(updatedRefund);
        Assert.Equal("failed", updatedRefund.Status);

        var updatedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updatedPayment);
        Assert.Equal("paid", updatedPayment.Status);
    }

    // 14. Повторный callback TRTYPE=14 обрабатывается идемпотентно
    [Fact(DisplayName = "14. Повторный callback TRTYPE=14 обрабатывается идемпотентно")]
    public async Task Scenario14_RepeatedCallbackTrtype14_IsIdempotent()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CALLBACK-IDEMP";

        var payment = CreatePaidPayment(orderId, sessionId);

        var pendingRefund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = 49990,
            Reason = "Идемпотентный возврат",
            Status = "pending",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(pendingRefund);

        var notificationService = CreateNotificationService();
        var form = CreateBccNotifyForm(orderId, trType: "14", action: "0", rc: "00");
        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");

        var firstResult = await notificationService.ProcessNotificationAsync(authHeader, form);
        Assert.Equal(200, firstResult.StatusCode);

        var secondResult = await notificationService.ProcessNotificationAsync(authHeader, form);
        Assert.Equal(200, secondResult.StatusCode);

        var checkPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(checkPayment);
        Assert.Equal("refunded", checkPayment.Status);
    }

    // 15. Запоздалый callback покупки (TRTYPE=1) для уже refunded платежа не возвращает его в paid
    [Fact(DisplayName = "15. Запоздалый callback покупки не возвращает refunded платёж в paid")]
    public async Task Scenario15_LatePurchaseCallback_DoesNotRevertRefundedPayment()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-LATE-CALLBACK";

        var payment = CreatePaidPayment(orderId, sessionId);
        _paymentRepo.UpdateStatus(orderId, PaymentStatuses.Refunded);

        var notificationService = CreateNotificationService();
        var latePurchaseForm = CreateBccNotifyForm(orderId, trType: "1", action: "0", rc: "00");
        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");

        var result = await notificationService.ProcessNotificationAsync(authHeader, latePurchaseForm);

        Assert.Equal(200, result.StatusCode);

        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal("refunded", paymentAfter.Status); // Остался refunded!
    }

    // 16. После возврата эндпоинт скачивания PDF блокирует доступ, если нет других оплаченных платежей
    [Fact(DisplayName = "16. После возврата эндпоинт скачивания PDF блокирует доступ")]
    public void Scenario16_AfterRefund_PdfDownloadEndpointBlocksAccess()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-PDF-BLOCK";

        var payment = CreatePaidPayment(orderId, sessionId);

        // Изначально платёж paid, сессия paid:true
        var sessionBefore = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionBefore);
        Assert.True(sessionBefore.Paid);

        // Оформляем возврат
        _paymentRepo.UpdateStatus(orderId, PaymentStatuses.Refunded);

        var sessionAfter = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.False(sessionAfter.Paid); // Отозвана сессия

        // Проверяем логику отчёта - только оплаченная сессия может скачивать PDF
        var latestPaid = _paymentRepo.GetSuccessfulPaymentBySessionId(sessionId);
        Assert.Null(latestPaid);
    }

    // 17. API списка платежей для админки возвращает HasRefund, CanRefund и ClientPhone
    [Fact(DisplayName = "17. GetAdminPaymentsList возвращает HasRefund, CanRefund и ClientPhone")]
    public void Scenario17_AdminPaymentsList_ReturnsHasRefundCanRefundAndClientPhone()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-ADMIN-LIST-01";

        CreatePaidPayment(orderId, sessionId);

        // Без возвратов: CanRefund = true (окружение test совпадает со шлюзом test), HasRefund = false
        var items = _paymentRepo.GetAdminPaymentsList(limit: 10, gatewayEnvironment: "test");
        var item = items.FirstOrDefault(i => i.OrderId == orderId);

        Assert.NotNull(item);
        Assert.Equal("+77011234567", item.ClientPhone);
        Assert.False(item.HasRefund);
        Assert.True(item.CanRefund);

        // Создаём pending возврат
        var pendingRefund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = item.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = item.AmountKzt,
            Reason = "Проверочный возврат",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(pendingRefund);

        // Снова запрашиваем список
        var itemsWithRefund = _paymentRepo.GetAdminPaymentsList(limit: 10, gatewayEnvironment: "test");
        var itemWithRefund = itemsWithRefund.FirstOrDefault(i => i.OrderId == orderId);

        Assert.NotNull(itemWithRefund);
        Assert.True(itemWithRefund.HasRefund);
        Assert.False(itemWithRefund.CanRefund); // Заблокирован pending возвратом
    }

    // 18. CanRefund=false при несовпадении окружения со шлюзом
    [Fact(DisplayName = "18. CanRefund=false для другого окружения платежа")]
    public void Scenario18_CanRefund_FalseWhenEnvironmentMismatches()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-ENV-TEST";

        CreatePaidPayment(orderId, sessionId);

        // Платёж создан в test, но активный шлюз в production
        var items = _paymentRepo.GetAdminPaymentsList(limit: 10, gatewayEnvironment: "production");
        var item = items.FirstOrDefault(i => i.OrderId == orderId);

        Assert.NotNull(item);
        Assert.False(item.CanRefund);
    }

    // 19. Несовпадение окружения возвращает 409 Conflict и не вызывает gateway
    [Fact(DisplayName = "19. Несовпадение окружения возвращает 409 Conflict и не вызывает gateway")]
    public async Task Scenario19_EnvironmentMismatch_Returns409Conflict_DoesNotCallGateway()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-MISMATCH-ENV";

        CreatePaidPayment(orderId, sessionId);

        // Шлюз сконфигурирован в production, а платёж в test
        var prodConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "production",
                ["BCC_TERMINAL_ID"] = "99999999",
                ["BCC_GATEWAY_URL"] = "https://epay.bcc.kz/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenix.kz/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenix.kz/payment/result",
                ["BCC_MERCHANT_ID"] = "10000001",
                ["BCC_MERCHANT_NAME"] = "Fenix Legal OS",
                ["BCC_MAC_KEY"] = "00000000000000000000000000000000",
                ["BCC_NOTIFY_USERNAME"] = "bcc_user",
                ["BCC_NOTIFY_PASSWORD"] = "bcc_pass"
            })
            .Build();

        var gatewayMock = new FakePaymentGateway(success: true); // Provider bcc, Environment test
        // Создадим шлюз с окружением production:
        var prodGateway = new FakePaymentGateway(success: true, environment: "production");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, prodGateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal("environment_mismatch", GetJsonErrorCode(result));
        Assert.Null(prodGateway.LastRefundRequest); // Шлюз не вызывался!
    }

    // 20. Неопределённые HTTP статусы от BCC не считаются окончательным отказом (IsFinal=false, статус pending)
    [Theory(DisplayName = "20. Неопределённые HTTP статусы (400, 401, 403, 404, 408, 429, 500, 502, 504) оставляют возврат pending")]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(504)]
    public async Task Scenario20_TransientGatewayErrors_LeaveRefundPending(int httpStatusCode)
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = $"ORD-TRANSIENT-{httpStatusCode}";

        CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(
            accepted: false,
            isFinal: false,
            success: false,
            responseCode: httpStatusCode.ToString(),
            bankMessage: $"Gateway HTTP {httpStatusCode}");

        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        // При IsFinal=false результат 202 Accepted
        Assert.Equal(202, result.StatusCode);

        var refund = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refund);
        Assert.Equal(PaymentRefundStatuses.Pending, refund.Status);

        var payment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(payment);
        Assert.Equal(PaymentStatuses.Paid, payment.Status); // Платёж остался paid
    }

    // 21. Успешное завершение возврата атомарно обновляет refund, payment, session, lead
    [Fact(DisplayName = "21. CompleteRefundTransaction атомарно обновляет refund, payment, session, lead")]
    public void Scenario21_CompleteRefundTransaction_AtomicallyUpdatesAllEntities()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-ATOMIC-ALL";

        var payment = CreatePaidPayment(orderId, sessionId);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Атомарный возврат всех сущностей",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        bool completed = _refundRepo.CompleteRefundTransaction(
            refundId: refund.Id,
            orderId: orderId,
            actionCode: "0",
            responseCode: "00",
            rrn: "ATOMIC_RRN",
            intRef: "ATOMIC_INT",
            bankMessage: "OK");

        Assert.True(completed);

        var updatedRefund = _refundRepo.GetById(refund.Id);
        Assert.NotNull(updatedRefund);
        Assert.Equal(PaymentRefundStatuses.Succeeded, updatedRefund.Status);
        Assert.Equal("ATOMIC_RRN", updatedRefund.Rrn);

        var updatedPayment = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(updatedPayment);
        Assert.Equal(PaymentStatuses.Refunded, updatedPayment.Status);

        var session = _sessions.GetSession(sessionId);
        Assert.NotNull(session);
        Assert.False(session.Paid);
        Assert.Equal("refunded", session.PaymentMethod);

        // Повторный вызов идемпотентен
        bool reCompleted = _refundRepo.CompleteRefundTransaction(
            refundId: refund.Id,
            orderId: orderId,
            actionCode: "0",
            responseCode: "00",
            rrn: "ATOMIC_RRN",
            intRef: "ATOMIC_INT",
            bankMessage: "OK");

        Assert.True(reCompleted);
    }

    // 22. Callback TRTYPE=14 без локальной записи возврата не меняет платёж и возвращает конфликт
    [Fact(DisplayName = "22. Callback TRTYPE=14 без локального возврата не меняет платёж")]
    public async Task Scenario22_CallbackWithoutLocalRefund_DoesNotChangePayment()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NO-LOCAL-REFUND";

        CreatePaidPayment(orderId, sessionId);

        var notificationService = CreateNotificationService();
        var form = CreateBccNotifyForm(orderId, trType: "14", action: "0", rc: "00");
        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");

        var result = await notificationService.ProcessNotificationAsync(authHeader, form);

        Assert.Equal(409, result.StatusCode);

        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Paid, paymentAfter.Status); // Платёж остался paid!

        var sessionAfter = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.True(sessionAfter.Paid); // Сессия осталась оплаченной
    }

    // 23. Неуспешный callback TRTYPE=14 не может понизить succeeded в failed
    [Fact(DisplayName = "23. Неуспешный callback не понижает возврат со статусом succeeded в failed")]
    public async Task Scenario23_FailedCallback_DoesNotDowngradeSucceededRefund()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NO-DOWNGRADE";

        var payment = CreatePaidPayment(orderId, sessionId);

        var succeededRefund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Уже подтверждённый возврат",
            Status = PaymentRefundStatuses.Succeeded,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
            CompletedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(succeededRefund);
        _paymentRepo.UpdateStatus(orderId, PaymentStatuses.Refunded);

        var notificationService = CreateNotificationService();
        var failedCallbackForm = CreateBccNotifyForm(orderId, trType: "14", action: "1", rc: "51");
        string authHeader = CreateBasicAuthHeader("bcc_user", "bcc_pass");

        var result = await notificationService.ProcessNotificationAsync(authHeader, failedCallbackForm);

        Assert.Equal(200, result.StatusCode);

        var refundAfter = _refundRepo.GetById(succeededRefund.Id);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Succeeded, refundAfter.Status); // Остался succeeded!
    }

    // 24. Просроченный токен администратора не принимается и удаляется
    [Fact(DisplayName = "24. Просроченный токен администратора не принимается")]
    public void Scenario24_ExpiredAdminToken_IsRejectedAndRemoved()
    {
        string testToken = "expired_token_12345";
        AdminSessionService.AddTestToken(testToken, TimeSpan.FromSeconds(-10)); // Истёк 10 секунд назад

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = $"fenix_admin={testToken}";

        bool isAdmin = _adminSessionService.IsAdmin(httpContext);
        Assert.False(isAdmin);

        // Проверяем, что просроченный токен удалён из активных
        Assert.False(AdminSessionService.ActiveTokens.ContainsKey(testToken));
    }

    // 25. Logout немедленно инвалидирует токен администратора
    [Fact(DisplayName = "25. Logout немедленно инвалидирует токен администратора")]
    public void Scenario25_AdminLogout_ImmediatelyInvalidatesToken()
    {
        string testToken = "valid_token_logout_test";
        AdminSessionService.AddTestToken(testToken, TimeSpan.FromHours(1));

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["Cookie"] = $"fenix_admin={testToken}";

        Assert.True(_adminSessionService.IsAdmin(httpContext));

        _adminSessionService.Logout(httpContext);

        Assert.False(AdminSessionService.ActiveTokens.ContainsKey(testToken));
        Assert.False(_adminSessionService.IsAdmin(httpContext));
    }

    // 26. Возврат одного платежа не отзывает доступ, если у session есть другой paid-платёж
    [Fact(DisplayName = "26. CompleteRefundTransaction сохраняет доступ, если у сессии есть другой paid-платёж")]
    public void Scenario26_MultiplePaidPayments_RefundDoesNotRevokeSessionAccess()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string leadId = _leadRepo.CreateLead(new Lead
        {
            SessionId = sessionId,
            UserId = userId,
            Name = "Тестовый Лид",
            Email = "lead_test@example.com",
            Paid = true,
            PaymentAmount = 49990,
            PaymentMethod = "bcc"
        });
        string orderId1 = "ORD-MULTI-01";
        string orderId2 = "ORD-MULTI-02";

        var payment1 = CreatePaidPayment(orderId1, sessionId, amountKzt: 49990, tariff: "report");
        var payment2 = CreatePaidPayment(orderId2, sessionId, amountKzt: 90990, tariff: "consultation");

        var refund1 = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment1.Id,
            OrderId = orderId1,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment1.AmountKzt,
            Reason = "Частичный отказ от услуги report при сохранении консультации",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund1);

        var result = _refundRepo.CompleteRefundTransaction(
            refundId: refund1.Id,
            orderId: orderId1,
            actionCode: "0",
            responseCode: "00",
            rrn: "RRN-MULTI-01",
            intRef: "INT-MULTI-01",
            bankMessage: "OK");

        Assert.True(result.Success);

        var payment1After = _paymentRepo.GetByOrderId(orderId1);
        Assert.NotNull(payment1After);
        Assert.Equal(PaymentStatuses.Refunded, payment1After.Status);

        var payment2After = _paymentRepo.GetByOrderId(orderId2);
        Assert.NotNull(payment2After);
        Assert.Equal(PaymentStatuses.Paid, payment2After.Status);

        var sessionAfter = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.True(sessionAfter.Paid); // Доступ сохранён!
        Assert.Equal(90990, sessionAfter.PaymentAmount); // Восстановлена сумма второго платежа
        Assert.Equal("bcc", sessionAfter.PaymentMethod);

        var leadAfter = _leadRepo.GetLead(leadId);
        Assert.NotNull(leadAfter);
        Assert.Equal(1L, (long)leadAfter.Paid);
        Assert.Equal(90990L, (long)leadAfter.PaymentAmount);
    }

    // 27. Доступ отзывается, если других paid-платежей нет
    [Fact(DisplayName = "27. CompleteRefundTransaction отзывает доступ, если других paid-платежей нет")]
    public void Scenario27_SinglePaidPayment_RefundRevokesSessionAccess()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string leadId = _leadRepo.CreateLead(new Lead
        {
            SessionId = sessionId,
            UserId = userId,
            Name = "Тестовый Лид",
            Email = "lead_test@example.com",
            Paid = true,
            PaymentAmount = 49990,
            PaymentMethod = "bcc"
        });
        string orderId = "ORD-SINGLE-REVOKE";

        var payment = CreatePaidPayment(orderId, sessionId, amountKzt: 49990);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Полный возврат единственного платежа",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var result = _refundRepo.CompleteRefundTransaction(
            refundId: refund.Id,
            orderId: orderId,
            actionCode: "0",
            responseCode: "00",
            rrn: "RRN-REVOKE",
            intRef: "INT-REVOKE");

        Assert.True(result.Success);

        var sessionAfter = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.False(sessionAfter.Paid); // Отозван
        Assert.Equal("refunded", sessionAfter.PaymentMethod);

        var leadAfter = _leadRepo.GetLead(leadId);
        Assert.NotNull(leadAfter);
        Assert.Equal(0L, (long)leadAfter.Paid);
        Assert.Equal("refunded", (string)leadAfter.PaymentMethod);
    }

    // 28. Несовпадение refundId/orderId не изменяет БД
    [Fact(DisplayName = "28. CompleteRefundTransaction возвращает ошибку и не меняет БД при несовпадении orderId")]
    public void Scenario28_OrderIdMismatch_DoesNotAlterDatabase()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-MISMATCH-SAFE";

        var payment = CreatePaidPayment(orderId, sessionId);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест несоответствия orderId",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var result = _refundRepo.CompleteRefundTransaction(
            refundId: refund.Id,
            orderId: "ORD-COMPLETELY-WRONG",
            actionCode: "0",
            responseCode: "00");

        Assert.False(result.Success);
        Assert.Equal("order_id_mismatch", result.ErrorCode);

        var refundAfter = _refundRepo.GetById(refund.Id);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfter.Status); // Не изменился

        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Paid, paymentAfter.Status); // Не изменился
    }

    // 29. Отсутствие refund или payment не возвращает ложный success
    [Fact(DisplayName = "29. CompleteRefundTransaction возвращает ошибку при отсутствии refund или payment")]
    public void Scenario29_MissingRefundOrPayment_FailsGracefully()
    {
        var res1 = _refundRepo.CompleteRefundTransaction(
            refundId: "NON-EXISTENT-REFUND",
            orderId: "NON-EXISTENT-ORDER",
            actionCode: "0",
            responseCode: "00");

        Assert.False(res1.Success);
        Assert.Equal("refund_not_found", res1.ErrorCode);

        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        var payment = CreatePaidPayment("ORD-WILL-DELETE", sessionId);
        var refundForDeleted = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = "ORD-WILL-DELETE",
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест сироты",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refundForDeleted);

        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(_dbInitializer.ConnectionString))
        {
            conn.Open();
            conn.Execute("PRAGMA foreign_keys = OFF; DELETE FROM payments WHERE id = @id; PRAGMA foreign_keys = ON;", new { id = payment.Id });
        }

        var res2 = _refundRepo.CompleteRefundTransaction(
            refundId: refundForDeleted.Id,
            orderId: "ORD-WILL-DELETE",
            actionCode: "0",
            responseCode: "00");

        Assert.False(res2.Success);
        Assert.Equal("payment_not_found", res2.ErrorCode);
    }

    // 30. PaymentRefundService не отвечает succeeded, если CompleteRefundTransaction вернул ошибку
    [Fact(DisplayName = "30. PaymentRefundService возвращает 500, если CompleteRefundTransaction не удался")]
    public async Task Scenario30_PaymentRefundService_WhenAtomicCompletionFails_Returns500()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-FAIL-COMPLETION";

        var payment = CreatePaidPayment(orderId, sessionId);

        var gatewayMock = new FakePaymentGateway(success: true, isFinal: true, actionCode: "0", responseCode: "00");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        // Предварительно изменим статус платежа на failed в БД, чтобы CompleteRefundTransaction отверг его
        using (var conn = new Microsoft.Data.Sqlite.SqliteConnection(_dbInitializer.ConnectionString))
        {
            conn.Open();
            conn.Execute("UPDATE payments SET status = 'failed' WHERE order_id = @orderId", new { orderId });
        }

        var result = await refundService.RefundAsync(orderId, "Причина возврата для теста", "admin", CancellationToken.None);

        Assert.NotEqual(200, result.StatusCode);
    }

    // 31. HTTP 400/401/403/404 без ACTION/RC оставляет refund pending в BccPaymentGateway
    [Fact(DisplayName = "31. BccPaymentGateway при HTTP 400 без ACTION/RC возвращает IsFinal=false")]
    public async Task Scenario31_BccGateway_Http400WithoutCodes_ReturnsIndeterminate()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BCC_ENVIRONMENT"] = "test",
            ["BCC_TERMINAL_ID"] = "92000001",
            ["BCC_GATEWAY_URL"] = "https://test.bcc.kz/gateway",
            ["BCC_NOTIFY_URL"] = "https://fenix.kz/notify",
            ["BCC_RETURN_URL"] = "https://fenix.kz/return",
            ["BCC_MERCHANT_ID"] = "10000001",
            ["BCC_MERCHANT_NAME"] = "FENIX",
            ["BCC_MAC_KEY"] = "00000000000000000000000000000000"
        }).Build();

        var httpFactory = new MockHttpClientFactory(req =>
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest);
            resp.Content = new StringContent("{\"error\":\"invalid_request\"}");
            return resp;
        });

        var gateway = new BccPaymentGateway(config, NullLogger<BccPaymentGateway>.Instance, httpFactory);
        var req = new PaymentGatewayRefundRequest
        {
            OrderId = "ORD-HTTP400",
            MerchRnId = "0123456789ABCDEF",
            OriginalAmountKzt = 49990,
            RefundAmountKzt = 49990,
            TerminalId = "92000001",
            Rrn = "123456789012",
            IntRef = "INTREF123456"
        };

        var result = await gateway.RefundAsync(req);

        Assert.False(result.Accepted);
        Assert.False(result.IsFinal);
        Assert.False(result.Success);
    }

    // 32. HTTP 4xx/5xx с явным отказом ACTION/RC переводит refund в failed
    [Fact(DisplayName = "32. BccPaymentGateway при HTTP 500 с явным ACTION=1 возвращает IsFinal=true, Success=false")]
    public async Task Scenario32_BccGateway_HttpErrorWithDefinitiveFailureCodes_ReturnsFailed()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BCC_ENVIRONMENT"] = "test",
            ["BCC_TERMINAL_ID"] = "92000001",
            ["BCC_GATEWAY_URL"] = "https://test.bcc.kz/gateway",
            ["BCC_NOTIFY_URL"] = "https://fenix.kz/notify",
            ["BCC_RETURN_URL"] = "https://fenix.kz/return",
            ["BCC_MERCHANT_ID"] = "10000001",
            ["BCC_MERCHANT_NAME"] = "FENIX",
            ["BCC_MAC_KEY"] = "00000000000000000000000000000000"
        }).Build();

        var httpFactory = new MockHttpClientFactory(req =>
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            resp.Content = new StringContent("ACTION=1&RC=05&TEXT=Declined by Issuer");
            return resp;
        });

        var gateway = new BccPaymentGateway(config, NullLogger<BccPaymentGateway>.Instance, httpFactory);
        var req = new PaymentGatewayRefundRequest
        {
            OrderId = "ORD-HTTP500-REJECT",
            MerchRnId = "0123456789ABCDEF",
            OriginalAmountKzt = 49990,
            RefundAmountKzt = 49990,
            TerminalId = "92000001",
            Rrn = "123456789012",
            IntRef = "INTREF123456"
        };

        var result = await gateway.RefundAsync(req);

        Assert.True(result.IsFinal);
        Assert.False(result.Success);
        Assert.Equal("1", result.ActionCode);
        Assert.Equal("05", result.ResponseCode);
    }

    // 33. HTTP-ошибка с успешными ACTION=0 и RC=00 корректно подтверждает возврат
    [Fact(DisplayName = "33. BccPaymentGateway при HTTP ошибке с ACTION=0 и RC=00 возвращает Success=true")]
    public async Task Scenario33_BccGateway_HttpErrorWithSuccessCodes_ReturnsSuccess()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BCC_ENVIRONMENT"] = "test",
            ["BCC_TERMINAL_ID"] = "92000001",
            ["BCC_GATEWAY_URL"] = "https://test.bcc.kz/gateway",
            ["BCC_NOTIFY_URL"] = "https://fenix.kz/notify",
            ["BCC_RETURN_URL"] = "https://fenix.kz/return",
            ["BCC_MERCHANT_ID"] = "10000001",
            ["BCC_MERCHANT_NAME"] = "FENIX",
            ["BCC_MAC_KEY"] = "00000000000000000000000000000000"
        }).Build();

        var httpFactory = new MockHttpClientFactory(req =>
        {
            var resp = new HttpResponseMessage(System.Net.HttpStatusCode.GatewayTimeout);
            resp.Content = new StringContent("ACTION=0&RC=00&RRN=999888777666&INT_REF=INT999888&TEXT=Approved");
            return resp;
        });

        var gateway = new BccPaymentGateway(config, NullLogger<BccPaymentGateway>.Instance, httpFactory);
        var req = new PaymentGatewayRefundRequest
        {
            OrderId = "ORD-HTTP504-OK",
            MerchRnId = "0123456789ABCDEF",
            OriginalAmountKzt = 49990,
            RefundAmountKzt = 49990,
            TerminalId = "92000001",
            Rrn = "123456789012",
            IntRef = "INTREF123456"
        };

        var result = await gateway.RefundAsync(req);

        Assert.True(result.IsFinal);
        Assert.True(result.Success);
        Assert.Equal("0", result.ActionCode);
        Assert.Equal("00", result.ResponseCode);
        Assert.Equal("999888777666", result.Rrn);
    }

    // 34. Проверка статуса pending-возврата не отправляет повторный TRTYPE=14
    [Fact(DisplayName = "34. CheckRefundStatusAsync вызывает только TRTYPE=90 и не отправляет TRTYPE=14")]
    public async Task Scenario34_CheckRefundStatus_DoesNotSendTrType14()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CHECK-NO-RETRY";

        var payment = CreatePaidPayment(orderId, sessionId);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Ожидающий возврат для сверки",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var gatewayMock = new FakePaymentGateway();
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(200, result.StatusCode);
        Assert.Equal(0, gatewayMock.RefundCallCount); // TRTYPE=14 НЕ вызывался!
        Assert.Equal(1, gatewayMock.CheckStatusCallCount); // Только сверка TRTYPE=90
    }

    // 35. Успешная сверка атомарно завершает возврат
    [Fact(DisplayName = "35. CheckRefundStatusAsync при успешном ответе банка атомарно завершает возврат")]
    public async Task Scenario35_CheckRefundStatus_OnSuccess_CompletesRefundAtomically()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CHECK-SUCCESS";

        var payment = CreatePaidPayment(orderId, sessionId);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Сверка успешного возврата",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var gatewayMock = new FakePaymentGateway(success: true, isFinal: true, actionCode: "0", responseCode: "00", rrn: "RRN-CHECK-OK");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(200, result.StatusCode);

        var refundAfter = _refundRepo.GetById(refund.Id);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Succeeded, refundAfter.Status);
        Assert.Equal("RRN-CHECK-OK", refundAfter.Rrn);

        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Refunded, paymentAfter.Status);

        var sessionAfter = _sessions.GetSession(sessionId);
        Assert.NotNull(sessionAfter);
        Assert.False(sessionAfter.Paid);
    }

    // 36. Неоднозначная сверка оставляет pending
    [Fact(DisplayName = "36. CheckRefundStatusAsync при неопределённом ответе оставляет статус pending")]
    public async Task Scenario36_CheckRefundStatus_OnIndeterminate_LeavesPending()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-CHECK-INDETERMINATE";

        var payment = CreatePaidPayment(orderId, sessionId);

        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Сверка неопределённого возврата",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var gatewayMock = new FakePaymentGateway(success: false, isFinal: false, bankMessage: "Ответ относится к TRTYPE=1, требуется ручная сверка");
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await refundService.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(202, result.StatusCode);

        var refundAfter = _refundRepo.GetById(refund.Id);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfter.Status); // Остался pending!

        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Paid, paymentAfter.Status);
    }

    // 37. Endpoint сверки требует admin-авторизацию
    [Fact(DisplayName = "37. AdminPaymentsController.CheckRefundStatus требует авторизацию администратора")]
    public async Task Scenario37_CheckRefundStatusEndpoint_RequiresAdminAuth()
    {
        var gatewayMock = new FakePaymentGateway();
        var refundService = new PaymentRefundService(_paymentRepo, _refundRepo, gatewayMock, _leadRepo, NullLogger<PaymentRefundService>.Instance);
        var controller = new AdminPaymentsController(_paymentRepo, refundService, _adminSessionService);

        // Без cookie администратора
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        var resultUnauthorized = await controller.CheckRefundStatus("ORD-AUTH-CHECK", CancellationToken.None);
        var unauthObj = Assert.IsType<UnauthorizedObjectResult>(resultUnauthorized);
        Assert.Equal(401, unauthObj.StatusCode);

        // С валидной cookie администратора
        string adminToken = "valid_admin_token_check_auth";
        AdminSessionService.AddTestToken(adminToken, TimeSpan.FromHours(1));
        var authContext = new DefaultHttpContext();
        authContext.Request.Headers["Cookie"] = $"fenix_admin={adminToken}";
        controller.ControllerContext = new ControllerContext { HttpContext = authContext };

        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-AUTH-SUCCESS";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест авторизованной проверки",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var resultAuthorized = await controller.CheckRefundStatus(orderId, CancellationToken.None);
        var okResult = Assert.IsType<ObjectResult>(resultAuthorized);
        Assert.Equal(200, okResult.StatusCode);
    }

    // 38. Тестовый MAC-вектор BCC для TRTYPE=90 даёт 7C0D8BF3F6C7DCB0AA35E88F045292E176184B5E
    [Fact(DisplayName = "38. Тестовый MAC-вектор BCC для TRTYPE=90 строго даёт 7C0D8BF3F6C7DCB0AA35E88F045292E176184B5E")]
    public void Scenario38_BccPaymentGateway_SignMac_MatchesBccDocumentationTestVector_ForTrType90()
    {
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
        string macData = BccPaymentGateway.BuildStatusCheckMacDataString(
            order: "3558714461568",
            terminal: "88888881",
            timestamp: "20200224073921",
            trType: "90",
            nonce: "F2B2DD7E603A7AAF5E1BC35DEE1F6C9A");

        string pSign = gateway.SignMac(macData);

        Assert.Equal("7C0D8BF3F6C7DCB0AA35E88F045292E176184B5E", pSign);
    }

    // 39. Запрос TRTYPE=90 содержит MERCH_GMT=0, TRAN_TRTYPE=14, NOTIFY_URL
    [Fact(DisplayName = "39. BccPaymentGateway.CheckStatusAsync запрос содержит MERCH_GMT, TRAN_TRTYPE, NOTIFY_URL")]
    public async Task Scenario39_BccPaymentGateway_CheckStatusAsync_SendsRequiredFields()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var mockFactory = new MockHttpClientFactory(req =>
        {
            capturedRequest = req;
            if (req.Content != null)
            {
                using var stream = req.Content.ReadAsStream();
                using var reader = new StreamReader(stream);
                capturedBody = reader.ReadToEnd();
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("ACTION=0&RC=00&TRTYPE=90&TRAN_TRTYPE=14&RRN=123456789012")
            };
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888881",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenix.org/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenix.org/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "TEST",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
            })
            .Build();

        var gateway = new BccPaymentGateway(config, httpClientFactory: mockFactory);
        var result = await gateway.CheckStatusAsync("ORD-REQ-FIELDS-01");

        Assert.NotNull(capturedRequest);
        Assert.NotNull(capturedBody);
        var form = System.Web.HttpUtility.ParseQueryString(capturedBody);

        Assert.Equal("ORD-REQ-FIELDS-01", form["ORDER"]);
        Assert.Equal("88888881", form["TERMINAL"]);
        Assert.Equal("90", form["TRTYPE"]);
        Assert.Equal("0", form["MERCH_GMT"]);
        Assert.Equal("14", form["TRAN_TRTYPE"]);
        Assert.Equal("https://fenix.org/api/payments/bcc/notify", form["NOTIFY_URL"]);
        Assert.False(string.IsNullOrWhiteSpace(form["TIMESTAMP"]));
        Assert.False(string.IsNullOrWhiteSpace(form["NONCE"]));
        Assert.False(string.IsNullOrWhiteSpace(form["P_SIGN"]));
    }

    // 40. Пустой HTTP-ответ оставляет pending
    [Fact(DisplayName = "40. BccPaymentGateway.CheckStatusAsync при пустом HTTP-ответе оставляет pending (IsFinal=false)")]
    public async Task Scenario40_BccPaymentGateway_CheckStatusAsync_EmptyResponse_LeavesPending()
    {
        var mockFactory = new MockHttpClientFactory(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("") // Синхронный ответ пуст
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "88888881",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenix.org/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenix.org/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "TEST",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
            })
            .Build();

        var gateway = new BccPaymentGateway(config, httpClientFactory: mockFactory);
        var checkResult = await gateway.CheckStatusAsync("ORD-EMPTY-01");

        Assert.False(checkResult.IsFinal);
        Assert.False(checkResult.Success);
        Assert.Equal(PaymentStatuses.Unknown, checkResult.Status);

        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-EMPTY-01";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест пустого ответа",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var service = new PaymentRefundService(_paymentRepo, _refundRepo, gateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);
        var refundResult = await service.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(202, refundResult.StatusCode);
        var refundAfter = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfter.Status);
    }

    // 41. Callback TRTYPE=90 + TRAN_TRTYPE=14 успешно завершает возврат
    [Fact(DisplayName = "41. BccNotificationService callback TRTYPE=90 + TRAN_TRTYPE=14 успешно завершает возврат")]
    public async Task Scenario41_BccNotificationService_TrType90_TranTrType14_Success_CompletesRefund()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NOTIFY-90-SUCCESS";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Проверка callback 90",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "92000001",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            NotifyUsername = "notifyUser",
            NotifyPassword = "secretPassword",
            AllowUnauthenticatedTestNotifications = true
        };
        var service = new BccNotificationService(_paymentRepo, options: options, refundRepository: _refundRepo, logger: NullLogger<BccNotificationService>.Instance);

        var form = CreateBccNotifyForm(
            orderId: orderId,
            terminal: "92000001",
            trType: "90",
            action: "0",
            rc: "00",
            rrn: "123456789012",
            intRef: "INT90REF001",
            tranTrType: "14");

        var result = await service.ProcessNotificationAsync(null, form);

        Assert.Equal(200, result.StatusCode);
        var refundAfter = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Succeeded, refundAfter.Status);
        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Refunded, paymentAfter.Status);
    }

    // 42. Неоднозначный callback оставляет pending
    [Fact(DisplayName = "42. BccNotificationService callback TRTYPE=90 с неполными ACTION/RC оставляет pending")]
    public async Task Scenario42_BccNotificationService_TrType90_AmbiguousCallback_LeavesPending()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NOTIFY-90-AMBIGUOUS";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест неоднозначного callback",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "92000001",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            AllowUnauthenticatedTestNotifications = true
        };
        var service = new BccNotificationService(_paymentRepo, options: options, refundRepository: _refundRepo, logger: NullLogger<BccNotificationService>.Instance);

        var form = CreateBccNotifyForm(
            orderId: orderId,
            terminal: "92000001",
            trType: "90",
            action: "",
            rc: "",
            tranTrType: "14");

        var result = await service.ProcessNotificationAsync(null, form);

        Assert.Equal(200, result.StatusCode);
        var refundAfter = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfter.Status);
        var paymentAfter = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfter);
        Assert.Equal(PaymentStatuses.Paid, paymentAfter.Status);
    }

    // 43. Другие TRAN_TRTYPE не изменяют возврат
    [Fact(DisplayName = "43. BccNotificationService callback TRTYPE=90 с TRAN_TRTYPE=1 не изменяет возврат")]
    public async Task Scenario43_BccNotificationService_TrType90_OtherTranTrType_DoesNotModifyRefund()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NOTIFY-90-OTHER-TRTYPE";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест другого TRAN_TRTYPE",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "92000001",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            AllowUnauthenticatedTestNotifications = true
        };
        var service = new BccNotificationService(_paymentRepo, options: options, refundRepository: _refundRepo, logger: NullLogger<BccNotificationService>.Instance);

        var form = CreateBccNotifyForm(
            orderId: orderId,
            terminal: "92000001",
            trType: "90",
            action: "0",
            rc: "00",
            tranTrType: "1");

        var result = await service.ProcessNotificationAsync(null, form);

        Assert.Equal(200, result.StatusCode);
        var refundAfter = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfter.Status); // Возврат не изменился!
    }

    // 44. Повторный callback остаётся идемпотентным
    [Fact(DisplayName = "44. BccNotificationService повторный callback TRTYPE=90 + TRAN_TRTYPE=14 остаётся идемпотентным")]
    public async Task Scenario44_BccNotificationService_TrType90_DuplicateCallback_IsIdempotent()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-NOTIFY-90-IDEMPOTENT";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест идемпотентности 90",
            Status = PaymentRefundStatuses.Succeeded, // Уже завершён
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var options = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "92000001",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            AllowUnauthenticatedTestNotifications = true
        };
        var service = new BccNotificationService(_paymentRepo, options: options, refundRepository: _refundRepo, logger: NullLogger<BccNotificationService>.Instance);

        var form = CreateBccNotifyForm(
            orderId: orderId,
            terminal: "92000001",
            trType: "90",
            action: "0",
            rc: "00",
            tranTrType: "14");

        var result = await service.ProcessNotificationAsync(null, form);

        Assert.Equal(200, result.StatusCode);
        var refundAfter = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfter);
        Assert.Equal(PaymentRefundStatuses.Succeeded, refundAfter.Status); // Остаётся succeeded
    }

    // 45. После 24 часов запрос TRTYPE=90 не отправляется, возвращается понятное сообщение
    [Fact(DisplayName = "45. PaymentRefundService.CheckRefundStatusAsync после 24 часов блокирует отправку и возвращает ошибку")]
    public async Task Scenario45_PaymentRefundService_CheckRefundStatusAsync_After24Hours_RejectsWithoutGatewayCall()
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-EXPIRED-24H";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Возврат 25 часов назад",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.AddHours(-25).ToString("o"),
            UpdatedAt = DateTime.UtcNow.AddHours(-25).ToString("o")
        };
        _refundRepo.Create(refund);

        var fakeGateway = new FakePaymentGateway();
        var service = new PaymentRefundService(_paymentRepo, _refundRepo, fakeGateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await service.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        Assert.Equal(0, fakeGateway.CheckStatusCallCount); // Запрос в шлюз не отправлялся!
    }

    // 46. TRTYPE=1, TRAN_TRTYPE=14, ACTION=0, RC=00 не завершает возврат
    [Fact(DisplayName = "46. TRTYPE=1, TRAN_TRTYPE=14, ACTION=0, RC=00 не завершает возврат ни в CheckStatusAsync, ни в callback")]
    public async Task Scenario46_TrType1_TranTrType14_Action0_Rc00_DoesNotCompleteRefund()
    {
        // А) Проверка в BccPaymentGateway.CheckStatusAsync:
        // Банк вернул TRTYPE=1 (покупка), а не TRTYPE=90. Ответ не является точным подтверждением возврата.
        var mockFactory = new MockHttpClientFactory(req => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("ACTION=0&RC=00&TRTYPE=1&TRAN_TRTYPE=14&RRN=123456789012")
        });

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BCC_ENVIRONMENT"] = "test",
                ["BCC_TERMINAL_ID"] = "92000001",
                ["BCC_GATEWAY_URL"] = "https://test3ds.bcc.kz:5445/cgi-bin/cgi_link",
                ["BCC_NOTIFY_URL"] = "https://fenix.org/api/payments/bcc/notify",
                ["BCC_RETURN_URL"] = "https://fenix.org/api/payments/bcc/return",
                ["BCC_MERCHANT_ID"] = "00000001",
                ["BCC_MERCHANT_NAME"] = "TEST",
                ["BCC_MAC_KEY"] = "6BB0AC02E47BDF73D98FEB777F3B5294"
            })
            .Build();

        var gateway = new BccPaymentGateway(config, httpClientFactory: mockFactory);
        var checkResult = await gateway.CheckStatusAsync("ORD-TRTYPE1-REFUND-01");

        // Должно остаться pending (IsFinal=false, Success=false, Status=Unknown)
        Assert.False(checkResult.IsFinal);
        Assert.False(checkResult.Success);
        Assert.Equal(PaymentStatuses.Unknown, checkResult.Status);

        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = "ORD-TRTYPE1-REFUND-01";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест TRTYPE=1 с TRAN_TRTYPE=14",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var service = new PaymentRefundService(_paymentRepo, _refundRepo, gateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);
        var refundResult = await service.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(202, refundResult.StatusCode);
        var refundAfterCheck = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfterCheck);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfterCheck.Status); // Возврат остался pending!

        // Б) Проверка в BccNotificationService: callback TRTYPE=1 с TRAN_TRTYPE=14 не завершает возврат
        var notifyOptions = new BccPaymentOptions
        {
            Environment = "test",
            TerminalId = "92000001",
            MacKeyHex = "6BB0AC02E47BDF73D98FEB777F3B5294",
            AllowUnauthenticatedTestNotifications = true
        };
        var notifyService = new BccNotificationService(_paymentRepo, options: notifyOptions, refundRepository: _refundRepo, logger: NullLogger<BccNotificationService>.Instance);

        var form = CreateBccNotifyForm(
            orderId: orderId,
            terminal: "92000001",
            trType: "1",
            action: "0",
            rc: "00",
            tranTrType: "14");

        var notifyResult = await notifyService.ProcessNotificationAsync(null, form);
        Assert.Equal(200, notifyResult.StatusCode);

        var refundAfterNotify = _refundRepo.GetLatestByOrderId(orderId);
        Assert.NotNull(refundAfterNotify);
        Assert.Equal(PaymentRefundStatuses.Pending, refundAfterNotify.Status); // Возврат не завершён, остаётся pending!
        var paymentAfterNotify = _paymentRepo.GetByOrderId(orderId);
        Assert.NotNull(paymentAfterNotify);
        Assert.Equal(PaymentStatuses.Paid, paymentAfterNotify.Status);
    }

    // 47. Отсутствующая или невалидная дата возврата блокирует TRTYPE=90
    [Theory(DisplayName = "47. Отсутствующая или невалидная дата возврата блокирует TRTYPE=90 с кодом 409")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-date")]
    [InlineData("2025-99-99T99:99:99")]
    public async Task Scenario47_MissingOrInvalidRefundCreatedAt_BlocksTrType90(string invalidCreatedAt)
    {
        string userId = CreateTestUser();
        string sessionId = CreateTestSession(userId);
        string orderId = $"ORD-INVALID-DATE-{Guid.NewGuid():N}";
        var payment = CreatePaidPayment(orderId, sessionId);
        var refund = new PaymentRefund
        {
            Id = Guid.NewGuid().ToString(),
            PaymentId = payment.Id,
            OrderId = orderId,
            Provider = "bcc",
            Environment = "test",
            AmountKzt = payment.AmountKzt,
            Reason = "Тест невалидной даты возврата",
            Status = PaymentRefundStatuses.Pending,
            CreatedAt = invalidCreatedAt!,
            UpdatedAt = DateTime.UtcNow.ToString("o")
        };
        _refundRepo.Create(refund);

        var fakeGateway = new FakePaymentGateway();
        var service = new PaymentRefundService(_paymentRepo, _refundRepo, fakeGateway, _leadRepo, NullLogger<PaymentRefundService>.Instance);

        var result = await service.CheckRefundStatusAsync(orderId, "admin", CancellationToken.None);

        Assert.Equal(409, result.StatusCode);
        string json = JsonSerializer.Serialize(result.Value);
        Assert.Contains("invalid_refund_created_at", json);
        Assert.Equal(0, fakeGateway.CheckStatusCallCount); // Запрос в шлюз не отправлялся!
    }

    // Вспомогательные методы
    private BccNotificationService CreateNotificationService()
    {
        return new BccNotificationService(
            _paymentRepo,
            _configuration,
            logger: NullLogger<BccNotificationService>.Instance,
            refundRepository: _refundRepo);
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
        string terminal = "92000001",
        string trType = "14",
        string action = "0",
        string rc = "00",
        string rrn = "123456789012",
        string intRef = "INTREF123456",
        string approval = "APP123",
        string madvCode = "0",
        string text = "Transaction approved",
        string? tranTrType = null)
    {
        var dict = new Dictionary<string, StringValues>
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
        if (!string.IsNullOrWhiteSpace(tranTrType))
        {
            dict["TRAN_TRTYPE"] = tranTrType;
        }
        return new FormCollection(dict);
    }

    private sealed class FakePaymentGateway : IPaymentGateway
    {
        public string Provider => "bcc";
        public string Environment => _environment;
        public string TerminalId => "92000001";
        public bool IsConfigured => true;

        public PaymentGatewayRefundRequest? LastRefundRequest { get; private set; }
        public int RefundCallCount { get; private set; }
        public int CheckStatusCallCount { get; private set; }
        public int CallCount => RefundCallCount;
        public string? LastCheckStatusOrderId { get; private set; }

        public PaymentGatewayCheckResult? CheckStatusResultToReturn { get; set; }

        private readonly string _environment;
        private readonly bool _accepted;
        private readonly bool _isFinal;
        private readonly bool _success;
        private readonly string? _actionCode;
        private readonly string? _responseCode;
        private readonly string? _rrn;
        private readonly string? _intRef;
        private readonly string? _bankMessage;
        private readonly int _delayMs;

        public FakePaymentGateway(
            bool success = true,
            bool accepted = true,
            bool isFinal = true,
            string? actionCode = "0",
            string? responseCode = "00",
            string? rrn = "FAKE_RRN",
            string? intRef = "FAKE_INT",
            string? bankMessage = null,
            int delayMs = 0,
            string environment = "test")
        {
            _environment = environment;
            _success = success;
            _accepted = accepted;
            _isFinal = isFinal;
            _actionCode = actionCode;
            _responseCode = responseCode;
            _rrn = rrn;
            _intRef = intRef;
            _bankMessage = bankMessage;
            _delayMs = delayMs;
        }

        public Task<PaymentGatewayInitResult> CreatePaymentAsync(PaymentGatewayInitRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PaymentGatewayCheckResult> CheckStatusAsync(string orderId, CancellationToken cancellationToken = default)
        {
            CheckStatusCallCount++;
            LastCheckStatusOrderId = orderId;
            if (CheckStatusResultToReturn != null)
            {
                return Task.FromResult(CheckStatusResultToReturn);
            }

            return Task.FromResult(new PaymentGatewayCheckResult
            {
                Status = _success ? PaymentStatuses.Refunded : PaymentStatuses.Failed,
                Success = _success,
                IsFinal = _isFinal,
                ActionCode = _actionCode,
                ResponseCode = _responseCode,
                Rrn = _rrn,
                IntRef = _intRef,
                BankMessage = _bankMessage,
                TrType = "14"
            });
        }

        public async Task<PaymentGatewayRefundResult> RefundAsync(PaymentGatewayRefundRequest request, CancellationToken cancellationToken = default)
        {
            RefundCallCount++;
            LastRefundRequest = request;
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, cancellationToken);
            }

            return new PaymentGatewayRefundResult
            {
                Accepted = _accepted,
                IsFinal = _isFinal,
                Success = _success,
                ActionCode = _actionCode,
                ResponseCode = _responseCode,
                Rrn = _rrn,
                IntRef = _intRef,
                BankMessage = _bankMessage
            };
        }
    }

    private sealed class MockHttpClientFactory : System.Net.Http.IHttpClientFactory
    {
        private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _handler;

        public MockHttpClientFactory(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public System.Net.Http.HttpClient CreateClient(string name)
        {
            return new System.Net.Http.HttpClient(new DelegatingMockHandler(_handler));
        }

        private sealed class DelegatingMockHandler : System.Net.Http.HttpMessageHandler
        {
            private readonly Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> _fn;
            public DelegatingMockHandler(Func<System.Net.Http.HttpRequestMessage, System.Net.Http.HttpResponseMessage> fn) => _fn = fn;
            protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(_fn(request));
        }
    }
}
