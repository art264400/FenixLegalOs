using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

/// <summary>
/// Административный контроллер для управления платежами и возвратами.
/// Защищён авторизацией через AdminSessionService (cookie fenix_admin).
/// </summary>
[ApiController]
[Route("api/admin/payments")]
public sealed class AdminPaymentsController : ControllerBase
{
    private readonly PaymentRepository _paymentRepo;
    private readonly PaymentRefundService _refundService;
    private readonly AdminSessionService _sessionService;

    public AdminPaymentsController(
        PaymentRepository paymentRepo,
        PaymentRefundService refundService,
        AdminSessionService sessionService)
    {
        _paymentRepo = paymentRepo;
        _refundService = refundService;
        _sessionService = sessionService;
    }

    private bool IsAdmin() => _sessionService.IsAdmin(HttpContext);

    /// <summary>
    /// Возвращает список платежей с возможностью фильтрации по статусу и лимиту.
    /// </summary>
    [HttpGet]
    public IActionResult GetPayments([FromQuery] string? status = null, [FromQuery] int limit = 100)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new
            {
                error = "unauthorized",
                message = "Доступ разрешён только авторизованному администратору."
            });
        }

        var payments = _paymentRepo.GetAdminPaymentsList(status, limit, _refundService.GatewayEnvironment);
        return Ok(payments);
    }

    /// <summary>
    /// Инициирует полный возврат успешного платежа по номеру заказа (orderId).
    /// </summary>
    [HttpPost("{orderId}/refund")]
    public async Task<IActionResult> RefundPayment(
        string orderId,
        [FromBody] JsonElement body,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new
            {
                error = "unauthorized",
                message = "Доступ разрешён только авторизованному администратору."
            });
        }

        string reason = body.TryGetProperty("reason", out var rProp) ? rProp.GetString() ?? "" : "";

        var result = await _refundService.RefundAsync(
            orderId: orderId,
            reason: reason,
            createdBy: "admin",
            cancellationToken: cancellationToken);

        return StatusCode(result.StatusCode, result.Value);
    }

    /// <summary>
    /// Выполняет сверку статуса зависшего pending-возврата через запрос TRTYPE=90 в шлюз.
    /// Доступен только авторизованному администратору.
    /// </summary>
    [HttpPost("{orderId}/refund/status")]
    public async Task<IActionResult> CheckRefundStatus(
        string orderId,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin())
        {
            return Unauthorized(new
            {
                error = "unauthorized",
                message = "Доступ разрешён только авторизованному администратору."
            });
        }

        var result = await _refundService.CheckRefundStatusAsync(
            orderId: orderId,
            createdBy: "admin",
            cancellationToken: cancellationToken);

        return StatusCode(result.StatusCode, result.Value);
    }
}
