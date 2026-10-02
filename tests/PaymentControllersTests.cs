using System.Text.Json;
using FenixLegalOs.Controllers;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
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
        _payments = new PaymentsController(_sessions, new SettingsRepository(database));
    }

    [Fact(DisplayName = "Payment start fails closed until BCC is configured")]
    public void StartPayment_WhenBccIsNotConfigured_Returns503WithoutUnlockingReport()
    {
        string sessionId = CreateCompletedSession();

        var action = _payments.StartPayment(sessionId, new StartPaymentRequest { Tariff = "consultation" });

        var unavailable = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        Assert.False(_sessions.GetSession(sessionId)!.Paid);

        string json = JsonSerializer.Serialize(unavailable.Value);
        Assert.Contains("payment_gateway_not_configured", json);
        Assert.Contains("90990", json);
    }

    [Fact(DisplayName = "Payment amount cannot be supplied by the browser")]
    public void StartPayment_UsesOnlyKnownTariffs()
    {
        string sessionId = CreateCompletedSession();

        var action = _payments.StartPayment(sessionId, new StartPaymentRequest { Tariff = "free" });

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
        Assert.Contains("\"paid\":true", JsonSerializer.Serialize(paid.Value));
    }

    [Fact(DisplayName = "Unconfigured BCC notification cannot mutate payment state")]
    public void Notify_WhenNotConfigured_Returns503()
    {
        var controller = new BccCallbacksController();

        var action = controller.Notify();

        var unavailable = Assert.IsType<ObjectResult>(action);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
    }

    [Fact(DisplayName = "BCC browser return only redirects to results")]
    public void ReturnToMerchant_DoesNotConfirmPayment()
    {
        var controller = new BccCallbacksController();

        var action = Assert.IsType<RedirectResult>(controller.ReturnToMerchant());

        Assert.Equal("/#/results", action.Url);
    }

    private string CreateCompletedSession()
    {
        string sessionId = _sessions.CreateSession();
        _sessions.CompleteSession(sessionId, "{}", new ScoreResult { Overall = 50 });
        return sessionId;
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
