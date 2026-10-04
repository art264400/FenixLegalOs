using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FenixLegalOs.Controllers;

/// <summary>
/// HTTP-контроллер для входящих запросов от шлюза BCC и браузера пользователя.
/// Обрабатывает серверные уведомления (POST /api/payments/bcc/notify) и возвраты браузера (return).
/// Контроллер тонкий: вся логика валидации и изменения состояния вынесена в BccNotificationService.
/// </summary>
[ApiController]
[Route("api/payments/bcc")]
public sealed class BccCallbacksController : ControllerBase
{
    private readonly BccNotificationService _notificationService;
    private readonly ILogger<BccCallbacksController> _logger;

    public BccCallbacksController(
        BccNotificationService notificationService,
        ILogger<BccCallbacksController>? logger = null)
    {
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? NullLogger<BccCallbacksController>.Instance;
    }

    [HttpPost("notify")]
    public async Task<IActionResult> Notify([FromForm] IFormCollection? form = null)
    {
        string httpMethod = Request?.Method ?? "POST";
        string contentType = Request?.ContentType ?? "";
        string traceId = HttpContext?.TraceIdentifier ?? "";
        bool authHeaderPresent = !string.IsNullOrWhiteSpace(Request?.Headers?.Authorization.ToString());

        _logger.LogInformation(
            PaymentEvents.BccNotifyRequestReceived,
            "Получен запрос BCC Notify: метод {HttpMethod}, Content-Type {ContentType}, TraceIdentifier {TraceIdentifier}, AuthorizationPresent: {AuthorizationPresent}",
            httpMethod,
            contentType,
            traceId,
            authHeaderPresent);

        var authHeader = Request?.Headers?.Authorization.ToString();
        IFormCollection? formCollection = form;
        if (formCollection == null || formCollection.Count == 0)
        {
            try
            {
                if (Request?.HasFormContentType == true)
                {
                    formCollection = await Request.ReadFormAsync();
                }
                else if (Request?.Form != null)
                {
                    formCollection = Request.Form;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    PaymentEvents.BccControllerWarning,
                    ex,
                    "Невозможно прочитать тело формы уведомления BCC, TraceIdentifier {TraceIdentifier}",
                    traceId);

                // Если Form не была передана в теле Request, используем переданный параметр или пустую коллекцию
                formCollection = form ?? new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
            }
        }

        BccNotificationResult result;
        try
        {
            result = await _notificationService.ProcessNotificationAsync(authHeader, formCollection, traceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                PaymentEvents.BccControllerError,
                ex,
                "Неожиданная ошибка обработки уведомления BCC, TraceIdentifier {TraceIdentifier}",
                traceId);
            throw;
        }

        _logger.LogInformation(
            PaymentEvents.BccNotifyRequestProcessed,
            "Запрос BCC Notify обработан: HTTP status {HttpStatus}, TraceIdentifier {TraceIdentifier}",
            result.StatusCode,
            traceId);

        if (result.StatusCode == StatusCodes.Status401Unauthorized && !string.IsNullOrWhiteSpace(result.WwwAuthenticateHeader))
        {
            if (Response?.Headers != null)
            {
                Response.Headers.WWWAuthenticate = result.WwwAuthenticateHeader;
            }
        }

        if (result.StatusCode == StatusCodes.Status200OK)
        {
            return Ok(result.Value);
        }

        return StatusCode(result.StatusCode, result.Value);
    }

    [HttpGet("return")]
    [HttpPost("return")]
    public IActionResult ReturnToMerchant()
    {
        string httpMethod = Request?.Method ?? "GET";
        string traceId = HttpContext?.TraceIdentifier ?? "";
        const string redirectPath = "/#/results";

        _logger.LogInformation(
            PaymentEvents.BccReturnReceived,
            "Браузер вернулся из BCC: метод {HttpMethod}, TraceIdentifier {TraceIdentifier}, целевой путь {RedirectPath}",
            httpMethod,
            traceId,
            redirectPath);

        // Этот маршрут только возвращает браузер в приложение и намеренно
        // не отмечает диагностику как оплаченную.
        return Redirect(redirectPath);
    }
}
