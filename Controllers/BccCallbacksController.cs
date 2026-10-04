using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

/// <summary>
/// HTTP-контроллер для входящих запросов от шлюза BCC и браузера пользователя.
/// Обрабатывает серверные уведомления (POST /api/payments/bcc/notify) и возвраты браузера (return).
/// Контроллер тонкий: вся логика валидации и изменения состояния вынесена в BccNotificationService.
/// </summary>
[ApiController]
[Route("api/payments/bcc")]
public sealed class BccCallbacksController(BccNotificationService notificationService) : ControllerBase
{
    [HttpPost("notify")]
    public async Task<IActionResult> Notify([FromForm] IFormCollection? form = null)
    {
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
            catch
            {
                // Если Form не была передана в теле Request, используем переданный параметр или пустую коллекцию
                formCollection = form ?? new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>());
            }
        }

        var result = await notificationService.ProcessNotificationAsync(authHeader, formCollection);

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
        // Этот маршрут только возвращает браузер в приложение и намеренно
        // не отмечает диагностику как оплаченную.
        return Redirect("/#/results");
    }
}
