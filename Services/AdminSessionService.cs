using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace FenixLegalOs.Services;

/// <summary>
/// Единый сервис аутентификации и сессий администратора (админки Fenix Legal OS).
/// Управляет валидацией пароля, выпуском и проверкой токенов через cookie fenix_admin.
/// </summary>
public sealed class AdminSessionService
{
    public const string CookieName = "fenix_admin";
    private const int CookieLifetimeSeconds = 86400; // 24 часа

    private readonly string? _configuredPassword;

    // Потокобезопасная коллекция активных сессионных токенов со временем истечения (UTC)
    public static readonly ConcurrentDictionary<string, DateTime> ActiveTokens = new();

    public AdminSessionService(IConfiguration? configuration = null)
    {
        _configuredPassword = configuration?["FENIX_ADMIN_PASSWORD"] 
            ?? Environment.GetEnvironmentVariable("FENIX_ADMIN_PASSWORD");
    }

    /// <summary>
    /// Проверяет, настроен ли пароль администратора в переменных окружения.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_configuredPassword);

    /// <summary>
    /// Проверяет, авторизован ли текущий HTTP-запрос как администратор через cookie fenix_admin.
    /// Проверяет время истечения токена и удаляет просроченные токены.
    /// </summary>
    public bool IsAdmin(HttpContext? httpContext)
    {
        if (httpContext == null) return false;

        if (httpContext.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            if (ActiveTokens.TryGetValue(token, out var expiresAt))
            {
                if (DateTime.UtcNow < expiresAt)
                {
                    return true;
                }

                // Токен просрочен — удаляем его
                ActiveTokens.TryRemove(token, out _);
            }
        }

        return false;
    }

    /// <summary>
    /// Выполняет аутентификацию по паролю и при успехе сохраняет сессионную cookie.
    /// </summary>
    public (bool Success, string? ErrorCode, string? ErrorMessage) Login(string? password, HttpContext httpContext)
    {
        if (!IsConfigured)
        {
            return (false, "admin_not_configured", "Вход в панель администратора не настроен: отсутствует переменная FENIX_ADMIN_PASSWORD.");
        }

        if (string.IsNullOrEmpty(password) || password != _configuredPassword)
        {
            return (false, "invalid_credentials", "Неверный пароль администратора.");
        }

        // Очистка ранее истекших токенов при новом входе
        CleanupExpiredTokens();

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        DateTime expiresAt = DateTime.UtcNow.AddSeconds(CookieLifetimeSeconds);
        ActiveTokens[token] = expiresAt;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            MaxAge = TimeSpan.FromSeconds(CookieLifetimeSeconds),
            Secure = httpContext.Request.IsHttps
        };

        httpContext.Response.Cookies.Append(CookieName, token, cookieOptions);
        return (true, null, null);
    }

    /// <summary>
    /// Сбрасывает активную сессию администратора и немедленно инвалидирует токен.
    /// </summary>
    public void Logout(HttpContext httpContext)
    {
        if (httpContext.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrWhiteSpace(token))
        {
            ActiveTokens.TryRemove(token, out _);
        }

        httpContext.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Path = "/",
            Secure = httpContext.Request.IsHttps
        });
    }

    /// <summary>
    /// Удаляет просроченные токены из памяти.
    /// </summary>
    private static void CleanupExpiredTokens()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in ActiveTokens)
        {
            if (kvp.Value <= now)
            {
                ActiveTokens.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>
    /// Метод для тестовых целей: добавление токена напрямую с произвольным сроком действия.
    /// </summary>
    public static void AddTestToken(string token, TimeSpan? lifetime = null)
    {
        ActiveTokens[token] = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromSeconds(CookieLifetimeSeconds));
    }
}
