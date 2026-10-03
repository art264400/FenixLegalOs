using FenixLegalOs.Infrastructure;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Payments;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

/// <summary>
/// HTTP-контроллер платежей: обработка запросов, роутинг и коды ответов.
/// Вся бизнес-логика вынесена в PaymentService.
/// </summary>
[ApiController]
[Route("api/payments")]
public sealed class PaymentsController(PaymentService paymentService) : ControllerBase
{
    [HttpPost("{sessionId}/start")]
    [RequireSessionAccess(RouteParamName = "sessionId")]
    public async System.Threading.Tasks.Task<IActionResult> StartPayment(string sessionId, [FromBody] StartPaymentRequest? request)
    {
        var cachedSession = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession;
        string? clientIp = HttpContext?.Connection?.RemoteIpAddress?.ToString();

        int height = request?.BrowserScreenHeight ?? 0;
        int width = request?.BrowserScreenWidth ?? 0;

        var result = await paymentService.StartAsync(
            sessionId,
            request?.Tariff,
            height,
            width,
            clientIp,
            cachedSession,
            HttpContext?.RequestAborted ?? default);

        return ToActionResult(result);
    }

    [HttpGet("{sessionId}/status")]
    [RequireSessionAccess(RouteParamName = "sessionId")]
    public IActionResult GetPaymentStatus(string sessionId)
    {
        var cachedSession = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession;
        var result = paymentService.GetStatus(sessionId, cachedSession);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(PaymentServiceResult result)
    {
        return result.StatusCode switch
        {
            200 => Ok(result.Value),
            400 => BadRequest(result.Value),
            404 => NotFound(result.Value),
            409 => Conflict(result.Value),
            _ => StatusCode(result.StatusCode, result.Value)
        };
    }
}
