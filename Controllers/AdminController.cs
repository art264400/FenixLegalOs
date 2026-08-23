using System.Security.Cryptography;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly LeadRepository _leads;
    private const string AdminTokenCookieName = "fenix_admin";
    private static readonly HashSet<string> AdminTokens = new();
    private static readonly string AdminPassword = Environment.GetEnvironmentVariable("FENIX_ADMIN_PASSWORD") ?? "fenix2026";

    public AdminController(LeadRepository leads)
    {
        _leads = leads;
    }

    private bool IsAdmin()
    {
        if (Request.Cookies.TryGetValue(AdminTokenCookieName, out var token) && !string.IsNullOrEmpty(token))
        {
            return AdminTokens.Contains(token);
        }
        return false;
    }

    [HttpPost("login")]
    public IActionResult Login([FromBody] JsonElement body)
    {
        string pwd = body.TryGetProperty("password", out var pProp) ? pProp.GetString() ?? "" : "";
        if (pwd != AdminPassword) return Unauthorized();

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
        AdminTokens.Add(token);
        _leads.AuditLog("admin", "login", null);

        Response.Headers.Append("Set-Cookie", $"{AdminTokenCookieName}={token}; HttpOnly; Path=/; SameSite=Strict; Max-Age=86400");
        return Ok(new { ok = true });
    }

    [HttpGet("overview")]
    public IActionResult GetOverview()
    {
        if (!IsAdmin()) return Unauthorized();
        return Ok(_leads.GetOverviewStats());
    }

    [HttpGet("leads")]
    public IActionResult GetLeads()
    {
        if (!IsAdmin()) return Unauthorized();
        var list = _leads.ListLeads().Select(l =>
        {
            ScoreResult? result = l.SessionResult != null ? JsonSerializer.Deserialize<ScoreResult>((string)l.SessionResult) : null;
            return new
            {
                id = l.Id,
                name = l.Name,
                company = l.Company,
                email = l.Email,
                messenger = l.Messenger,
                type = l.Type,
                interest = l.Interest,
                heatScore = l.HeatScore,
                heatLabel = l.HeatLabel,
                status = l.Status,
                paid = Convert.ToBoolean(l.Paid),
                paidAt = l.PaidAt,
                paymentAmount = l.PaymentAmount,
                paymentMethod = l.PaymentMethod,
                createdAt = l.CreatedAt,
                overall = result?.Overall,
                criticalCount = result?.CriticalCount,
                topRisk = result?.Risks?.FirstOrDefault()?.Title
            };
        });
        return Ok(new { leads = list });
    }

    [HttpGet("leads/{id}")]
    public IActionResult GetLeadById(string id)
    {
        if (!IsAdmin()) return Unauthorized();
        var lead = _leads.GetLead(id);
        if (lead == null) return NotFound(new { error = "not_found" });

        var answers = lead.SessionAnswers != null ? JsonSerializer.Deserialize<Dictionary<string, object>>((string)lead.SessionAnswers) : new();
        var result = lead.SessionResult != null ? JsonSerializer.Deserialize<ScoreResult>((string)lead.SessionResult) : null;

        var answerRows = DataBank.Questions
            .Where(q => answers != null && answers.ContainsKey(q.Id))
            .Select(q => new
            {
                sectionId = q.SectionId,
                question = q.Question,
                answer = answers![q.Id]?.ToString()
            });

        return Ok(new
        {
            lead = new
            {
                id = lead.Id,
                name = lead.Name,
                company = lead.Company,
                website = lead.Website,
                email = lead.Email,
                messenger = lead.Messenger,
                type = lead.Type,
                interest = lead.Interest,
                sourceRiskCode = lead.SourceRiskCode,
                heatScore = lead.HeatScore,
                heatLabel = lead.HeatLabel,
                status = lead.Status,
                paid = Convert.ToBoolean(lead.Paid),
                paidAt = lead.PaidAt,
                paymentAmount = lead.PaymentAmount,
                paymentMethod = lead.PaymentMethod,
                createdAt = lead.CreatedAt
            },
            result,
            answers = answerRows,
            fundraisingTimeline = answers != null && answers.TryGetValue("INVEST-01", out var r) ? r?.ToString() : null,
            notes = _leads.GetLeadNotes(id)
        });
    }

    [HttpPost("leads/{id}/status")]
    public IActionResult UpdateLeadStatus(string id, [FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        string status = body.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
        bool ok = _leads.UpdateStatus(id, status);
        if (ok) _leads.AuditLog("admin", "lead_status", $"{id} → {status}");
        return ok ? Ok(new { ok = true }) : NotFound(new { error = "not_found" });
    }

    [HttpPost("leads/{id}/notes")]
    public IActionResult AddLeadNote(string id, [FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        string note = body.TryGetProperty("note", out var nProp) ? nProp.GetString() ?? "" : "";
        if (string.IsNullOrWhiteSpace(note)) return BadRequest(new { error = "empty_note" });
        _leads.AddNote(id, note);
        _leads.AuditLog("admin", "lead_note", id);
        return Ok(new { ok = true });
    }

    [HttpGet("question-bank")]
    public IActionResult GetQuestionBank()
    {
        if (!IsAdmin()) return Unauthorized();
        return Ok(new { version = DataBank.QuestionBankVersion, sections = DataBank.Sections, questions = DataBank.Questions });
    }

    [HttpGet("risk-library")]
    public IActionResult GetRiskLibrary()
    {
        if (!IsAdmin()) return Unauthorized();
        return Ok(new { version = DataBank.RiskLibraryVersion, risks = DataBank.Risks });
    }
}
