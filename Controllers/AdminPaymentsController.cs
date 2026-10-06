using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FenixLegalOs.Models.Payments;
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
    private readonly PaymentRefundRepository? _refundRepo;
    private readonly IPaymentGateway? _gateway;

    public AdminPaymentsController(
        PaymentRepository paymentRepo,
        PaymentRefundService refundService,
        AdminSessionService sessionService,
        PaymentRefundRepository? refundRepo = null,
        IPaymentGateway? gateway = null)
    {
        _paymentRepo = paymentRepo;
        _refundService = refundService;
        _sessionService = sessionService;
        _refundRepo = refundRepo;
        _gateway = gateway;
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

        var payments = _paymentRepo.GetAdminPaymentsList(
            status,
            limit,
            _refundService.GatewayEnvironment,
            _gateway?.Provider,
            _gateway?.TerminalId);
        return Ok(payments);
    }

    [HttpGet("{orderId}")]
    public IActionResult GetPayment(string orderId)
    {
        if (!IsAdmin())
            return Unauthorized(new { error = "unauthorized", message = "Доступ разрешён только авторизованному администратору." });

        var payment = _paymentRepo.GetByOrderId(orderId);
        if (payment == null)
            return NotFound(new { error = "payment_not_found", message = "Платёж с указанным ORDER не найден." });

        return Ok(new
        {
            payment,
            refund = _refundRepo?.GetLatestByOrderId(orderId),
            orderUrl = $"/admin?tab=payments&order={System.Uri.EscapeDataString(orderId)}"
        });
    }

    [HttpPost("{orderId}/status")]
    public async Task<IActionResult> CheckPaymentStatus(string orderId, CancellationToken cancellationToken)
    {
        if (!IsAdmin())
            return Unauthorized(new { error = "unauthorized", message = "Доступ разрешён только авторизованному администратору." });
        if (_gateway == null)
            return StatusCode(503, new { error = "gateway_unavailable", message = "Платёжный шлюз временно недоступен." });

        var payment = _paymentRepo.GetByOrderId(orderId);
        if (payment == null)
            return NotFound(new { error = "payment_not_found", message = "Платёж с указанным ORDER не найден." });

        if (!_gateway.IsConfigured ||
            !string.Equals(payment.Provider, _gateway.Provider, System.StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payment.Environment, _gateway.Environment, System.StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payment.TerminalId, _gateway.TerminalId, System.StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new
            {
                error = "gateway_context_mismatch",
                message = "Платёж не относится к текущим провайдеру, окружению или терминалу BCC. Проверка статуса запрещена."
            });
        }

        var result = await _gateway.CheckStatusAsync(
            orderId,
            cancellationToken,
            tranTrType: "1");

        if (result.IsFinal && string.IsNullOrWhiteSpace(result.ErrorCode))
        {
            _paymentRepo.UpdateStatus(
                orderId,
                result.Success ? PaymentStatuses.Paid : PaymentStatuses.Failed,
                rrn: result.Rrn,
                intRef: result.IntRef,
                approvalCode: result.ApprovalCode,
                actionCode: result.ActionCode,
                responseCode: result.ResponseCode,
                bankMessage: result.BankMessage,
                lastStatusCheckAt: System.DateTime.UtcNow.ToString("o"));
        }

        return Ok(new
        {
            orderId,
            tranTrType = "1",
            result.Success,
            result.IsFinal,
            result.Status,
            result.ActionCode,
            result.ResponseCode,
            result.Rrn,
            result.IntRef,
            result.ApprovalCode,
            result.BankMessage
        });
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
