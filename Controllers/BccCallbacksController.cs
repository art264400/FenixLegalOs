using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

/// <summary>
/// Методы, вызываемые банком BCC и браузером пользователя после банковского сценария.
/// Возврат браузера не подтверждает оплату: статус можно менять только после проверенного
/// уведомления банка или аутентифицированного запроса статуса.
/// </summary>
[ApiController]
[Route("api/payments/bcc")]
public sealed class BccCallbacksController : ControllerBase
{
    [HttpPost("notify")]
    public IActionResult Notify()
    {
        // До реализации Basic Auth, проверки полей и идемпотентной обработки платежей
        // уведомление банка не должно изменять состояние системы.
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            error = "bcc_notifications_not_configured",
            message = "Приём уведомлений BCC ещё не настроен."
        });
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
