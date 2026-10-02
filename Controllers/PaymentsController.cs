using FenixLegalOs.Infrastructure;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

/// <summary>
/// Пользовательские методы оплаты. Формирование подписи и обмен данными с банком
/// будут переданы платёжному сервису BCC после реализации банковской интеграции.
/// </summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly SessionRepository _sessions;
    private readonly SettingsRepository _settings;

    public PaymentsController(SessionRepository sessions, SettingsRepository settings)
    {
        _sessions = sessions;
        _settings = settings;
    }

    [HttpPost("{sessionId}/start")]
    [RequireSessionAccess(RouteParamName = "sessionId")]
    public IActionResult StartPayment(string sessionId, [FromBody] StartPaymentRequest? request)
    {
        var session = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession
            ?? _sessions.GetSession(sessionId);

        if (session == null)
            return NotFound(new { error = "session_not_found" });

        if (string.IsNullOrWhiteSpace(session.CompletedAt) || string.IsNullOrWhiteSpace(session.ResultJson))
        {
            return Conflict(new
            {
                error = "diagnostic_not_completed",
                message = "Оплата доступна только после завершения диагностики."
            });
        }

        if (session.Paid)
        {
            return Conflict(new
            {
                error = "already_paid",
                message = "Полный отчёт по этой диагностике уже оплачен."
            });
        }

        string tariff = request?.Tariff?.Trim().ToLowerInvariant() ?? "";
        if (tariff is not ("report" or "consultation"))
        {
            return BadRequest(new
            {
                error = "invalid_tariff",
                message = "Неизвестный тариф оплаты."
            });
        }

        var pricing = _settings.GetPricing();
        int amountKzt = tariff == "consultation"
            ? pricing.ConsultationPriceKzt
            : pricing.PriceKzt;

        // До подключения подписи запросов и хранения платежей BCC всегда отказываем в операции.
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            error = "payment_gateway_not_configured",
            message = "Платёжный шлюз BCC ещё не настроен.",
            provider = "bcc",
            sessionId,
            tariff,
            amountKzt,
            currency = "KZT"
        });
    }

    [HttpGet("{sessionId}/status")]
    [RequireSessionAccess(RouteParamName = "sessionId")]
    public IActionResult GetPaymentStatus(string sessionId)
    {
        var session = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession
            ?? _sessions.GetSession(sessionId);

        if (session == null)
            return NotFound(new { error = "session_not_found" });

        return Ok(new
        {
            provider = "bcc",
            sessionId,
            paid = session.Paid,
            status = session.Paid ? "paid" : "not_started",
            paidAt = session.PaidAt,
            amountKzt = session.PaymentAmount,
            method = session.PaymentMethod
        });
    }
}
