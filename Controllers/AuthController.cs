using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserRepository _users;
    private readonly SessionRepository _sessions;
    private readonly LeadRepository _leads;

    public AuthController(UserRepository users, SessionRepository sessions, LeadRepository leads)
    {
        _users = users;
        _sessions = sessions;
        _leads = leads;
    }

    public record RegisterDto(
        string Email,
        string Password,
        string Name,
        string Company,
        string Position,
        string? Messenger,
        string? SessionId,
        bool TermsAccepted
    );

    public record LoginDto(
        string Email,
        string Password,
        string? SessionId
    );

    [HttpPost("register")]
    public IActionResult Register([FromBody] RegisterDto dto)
    {
        if (!dto.TermsAccepted)
        {
            return BadRequest(new { error = "terms_required", message = "Необходимо подтвердить согласие с Пользовательским соглашением и Политикой конфиденциальности." });
        }

        if (string.IsNullOrWhiteSpace(dto.Email) ||
            string.IsNullOrWhiteSpace(dto.Password) ||
            string.IsNullOrWhiteSpace(dto.Name) ||
            string.IsNullOrWhiteSpace(dto.Company) ||
            string.IsNullOrWhiteSpace(dto.Position))
        {
            return BadRequest(new { error = "missing_fields", message = "Пожалуйста, заполните все обязательные поля (ФИО, Email, пароль, должность, компания)." });
        }

        if (dto.Password.Length < 6)
        {
            return BadRequest(new { error = "weak_password", message = "Пароль должен содержать как минимум 6 символов." });
        }

        var existing = _users.GetUserByEmail(dto.Email);
        if (existing != null)
        {
            return Conflict(new { error = "email_exists", message = "Аккаунт с таким Email уже зарегистрирован. Пожалуйста, выполните вход." });
        }

        var user = _users.CreateUser(
            dto.Email,
            dto.Password,
            dto.Name,
            dto.Company,
            dto.Position,
            dto.Messenger
        );

        string sessionId = dto.SessionId ?? "";
        var existingSession = string.IsNullOrEmpty(sessionId) ? null : _sessions.GetSession(sessionId);
        if (existingSession == null || (!string.IsNullOrEmpty(existingSession.UserId) && existingSession.UserId != user.Id))
        {
            sessionId = _sessions.CreateSession();
        }

        _users.AttachUserToSession(sessionId, user.Id);

        // Record registration lead
        var lead = new Lead
        {
            SessionId = sessionId,
            Type = "registration",
            Name = user.Name,
            Email = user.Email,
            Company = user.Company,
            Position = user.Position,
            Messenger = user.Messenger,
            UserId = user.Id,
            TermsAccepted = true,
            TermsAcceptedAt = user.TermsAcceptedAt,
            HeatScore = 30,
            HeatLabel = "warm"
        };
        _leads.CreateLead(lead);
        _leads.RecordEvent("user_registered", sessionId, new { userId = user.Id, email = user.Email });

        string token = _users.CreateSessionToken(user.Id);
        bool isHttps = Request.IsHttps || Request.Headers["X-Forwarded-Proto"] == "https";
        string secureFlag = isHttps ? "; Secure" : "";
        Response?.Headers.Append("Set-Cookie", $"fenix_user_token={token}; HttpOnly; Path=/; SameSite=Lax{secureFlag}; Max-Age=2592000");

        return Ok(new
        {
            ok = true,
            sessionId,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                company = user.Company,
                position = user.Position,
                messenger = user.Messenger,
                termsAccepted = user.TermsAccepted
            }
        });
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
        {
            return BadRequest(new { error = "missing_fields", message = "Введите email и пароль." });
        }

        var user = _users.VerifyLogin(dto.Email, dto.Password);
        if (user == null)
        {
            return Unauthorized(new { error = "invalid_credentials", message = "Неверный email или пароль." });
        }

        string sessionId = dto.SessionId ?? "";
        var existingSession = string.IsNullOrEmpty(sessionId) ? null : _sessions.GetSession(sessionId);
        if (existingSession == null || (!string.IsNullOrEmpty(existingSession.UserId) && existingSession.UserId != user.Id))
        {
            sessionId = _sessions.CreateSession();
        }

        _users.AttachUserToSession(sessionId, user.Id);
        _leads.RecordEvent("user_logged_in", sessionId, new { userId = user.Id, email = user.Email });

        string token = _users.CreateSessionToken(user.Id);
        bool isHttps = Request.IsHttps || Request.Headers["X-Forwarded-Proto"] == "https";
        string secureFlag = isHttps ? "; Secure" : "";
        Response?.Headers.Append("Set-Cookie", $"fenix_user_token={token}; HttpOnly; Path=/; SameSite=Lax{secureFlag}; Max-Age=2592000");

        return Ok(new
        {
            ok = true,
            sessionId,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                company = user.Company,
                position = user.Position,
                messenger = user.Messenger,
                termsAccepted = user.TermsAccepted
            }
        });
    }

    private UserAccount? GetAuthenticatedUser()
    {
        try
        {
            var req = HttpContext?.Request;
            if (req == null) return null;

            string? token = null;
            if (req.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var headerStr = authHeader.ToString();
                if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = headerStr.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token) && req.Cookies.TryGetValue("fenix_user_token", out var cookieToken))
            {
                token = cookieToken;
            }

            if (string.IsNullOrEmpty(token)) return null;

            return _users.GetUserByToken(token);
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("me")]
    public IActionResult GetMe()
    {
        var user = GetAuthenticatedUser();
        if (user == null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не авторизован." });
        }

        return Ok(new
        {
            ok = true,
            user = new
            {
                id = user.Id,
                email = user.Email,
                name = user.Name,
                company = user.Company,
                position = user.Position,
                messenger = user.Messenger,
                termsAccepted = user.TermsAccepted
            }
        });
    }

    [HttpGet("me/reports")]
    public IActionResult GetMyReports()
    {
        var user = GetAuthenticatedUser();
        if (user == null)
        {
            return Unauthorized(new { error = "unauthorized", message = "Пользователь не авторизован." });
        }

        var reports = _users.GetUserSessions(user.Id);
        return Ok(new
        {
            ok = true,
            reports
        });
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        string? token = null;
        var req = HttpContext?.Request;
        if (req != null)
        {
            if (req.Headers.TryGetValue("Authorization", out var authHeader))
            {
                var headerStr = authHeader.ToString();
                if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = headerStr.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token) && req.Cookies.TryGetValue("fenix_user_token", out var cookieToken))
            {
                token = cookieToken;
            }
        }

        if (!string.IsNullOrEmpty(token))
        {
            _users.RevokeSessionToken(token);
        }

        bool isHttps = Request.IsHttps || Request.Headers["X-Forwarded-Proto"] == "https";
        string secureFlag = isHttps ? "; Secure" : "";
        Response?.Headers.Append("Set-Cookie", $"fenix_user_token=; HttpOnly; Path=/; SameSite=Lax{secureFlag}; Max-Age=0; Expires=Thu, 01 Jan 1970 00:00:00 GMT");
        return Ok(new { ok = true });
    }
}



