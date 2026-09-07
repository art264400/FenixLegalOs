using System;
using System.Threading.Tasks;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace FenixLegalOs.Infrastructure;

/// <summary>
/// Specifies declarative session access requirements for controller actions.
/// Enforces mandatory dependencies (fail-closed), terms acceptance, ownership, and optional payment.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class RequireSessionAccessAttribute : Attribute, IAsyncActionFilter
{
    public bool RequirePayment { get; set; }
    public bool DisallowCompleted { get; set; }
    public string RouteParamName { get; set; } = "id";

    public RequireSessionAccessAttribute(bool requirePayment = false, bool disallowCompleted = false)
    {
        RequirePayment = requirePayment;
        DisallowCompleted = disallowCompleted;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        
        // P1 Fix: Fail-closed. Mandatory dependencies MUST be resolved.
        var sessionsRepo = httpContext.RequestServices?.GetService<SessionRepository>();
        var usersRepo = httpContext.RequestServices?.GetService<UserRepository>();

        if (sessionsRepo == null || usersRepo == null)
        {
            context.Result = new ObjectResult(new
            {
                error = "security_configuration_error",
                message = "Ошибка конфигурации сервиса безопасности: отсутствуют обязательные репозитории доступа."
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
            return;
        }

        // Extract session id from route data
        if (!context.RouteData.Values.TryGetValue(RouteParamName, out var idObj) || idObj == null)
        {
            context.Result = new BadRequestObjectResult(new { error = "missing_session_id" });
            return;
        }

        string sessionId = idObj.ToString()!;
        var session = sessionsRepo.GetSession(sessionId);
        if (session == null)
        {
            context.Result = new NotFoundObjectResult(new { error = "not_found" });
            return;
        }

        // Resolve authenticated user from HttpOnly cookie or Authorization Bearer header
        UserAccount? authUser = null;
        string? token = null;

        if (httpContext.Request.Cookies.TryGetValue("fenix_user_token", out var cookieToken) && !string.IsNullOrEmpty(cookieToken))
        {
            token = cookieToken;
        }
        else if (httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerStr = authHeader.ToString();
            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                token = headerStr.Substring("Bearer ".Length).Trim();
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            authUser = usersRepo.GetUserByToken(token);
        }

        // 1. Mandatory Terms check
        if (!session.TermsAccepted)
        {
            context.Result = new ObjectResult(new
            {
                error = "terms_required",
                message = "Необходимо подтвердить согласие с Пользовательским соглашением и Политикой конфиденциальности."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // 2. Session ownership check
        if (!string.IsNullOrEmpty(session.UserId))
        {
            if (authUser == null || authUser.Id != session.UserId)
            {
                context.Result = new ObjectResult(new
                {
                    error = "forbidden_session_owner",
                    message = "Доступ запрещен: анкета принадлежит другому пользователю или срок авторизации истёк."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }
        }

        // 3. Completed session immutability check
        if (DisallowCompleted && !string.IsNullOrEmpty(session.CompletedAt))
        {
            context.Result = new ObjectResult(new
            {
                error = "session_already_completed",
                message = "Диагностика по этой анкете уже завершена. Ответы и результат зафиксированы и не могут быть изменены."
            })
            {
                StatusCode = StatusCodes.Status409Conflict
            };
            return;
        }

        // 4. Payment requirement (if specified)
        if (RequirePayment && !session.Paid)
        {
            context.Result = new ObjectResult(new
            {
                error = "payment_required",
                message = "Действие доступно только после оплаты полного отчёта."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        // Stash resolved session and auth user in HttpContext.Items for action use
        httpContext.Items["DiagnosticSession"] = session;
        if (authUser != null)
        {
            httpContext.Items["AuthUser"] = authUser;
        }

        await next();
    }
}
