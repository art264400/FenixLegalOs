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
        Response?.Headers.Append("Set-Cookie", $"fenix_user_token={token}; HttpOnly; Path=/; SameSite=Lax; Max-Age=2592000");

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
        Response?.Headers.Append("Set-Cookie", $"fenix_user_token={token}; HttpOnly; Path=/; SameSite=Lax; Max-Age=2592000");

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
}
