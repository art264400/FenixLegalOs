using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api")]
public class LeadsController : ControllerBase
{
    private readonly SessionRepository _sessions;
    private readonly LeadRepository _leads;

    public LeadsController(SessionRepository sessions, LeadRepository leads)
    {
        _sessions = sessions;
        _leads = leads;
    }

    [HttpPost("leads")]
    public IActionResult CreateLead([FromBody] JsonElement body)
    {
        string sessionId = body.TryGetProperty("sessionId", out var sProp) ? sProp.GetString() ?? "" : "";
        var session = _sessions.GetSession(sessionId);
        if (session == null) return NotFound(new { error = "session_not_found" });

        string type = body.TryGetProperty("type", out var tProp) && tProp.GetString() == "consultation" ? "consultation" : "report_gate";
        string name = body.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
        string email = body.TryGetProperty("email", out var eProp) ? eProp.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "invalid_input" });

        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
        ScoreResult? scoreResult = !string.IsNullOrEmpty(session.ResultJson) ? JsonSerializer.Deserialize<ScoreResult>(session.ResultJson) : null;

        var heat = LeadHeatEngine.Compute(answersDict, scoreResult, type == "consultation");

        var lead = new Lead
        {
            SessionId = sessionId,
            Type = type,
            Name = name,
            Email = email,
            Company = body.TryGetProperty("company", out var cProp) ? cProp.GetString() : null,
            Website = body.TryGetProperty("website", out var wProp) ? wProp.GetString() : null,
            Messenger = body.TryGetProperty("messenger", out var mProp) ? mProp.GetString() : null,
            Interest = body.TryGetProperty("interest", out var iProp) ? iProp.GetString() : null,
            SourceRiskCode = body.TryGetProperty("sourceRiskCode", out var rProp) ? rProp.GetString() : null,
            HeatScore = heat.Score,
            HeatLabel = heat.Label,
            Paid = session.Paid,
            PaidAt = session.PaidAt,
            PaymentAmount = session.PaymentAmount,
            PaymentMethod = session.PaymentMethod
        };

        string leadId = _leads.CreateLead(lead);
        _leads.RecordEvent(type == "consultation" ? "consultation_requested" : "lead_captured", sessionId, new { leadId });

        return Ok(new { ok = true, leadId });
    }

    [HttpPost("events")]
    public IActionResult RecordEvent([FromBody] JsonElement body)
    {
        string name = body.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
        string? sessionId = body.TryGetProperty("sessionId", out var sProp) ? sProp.GetString() : null;
        _leads.RecordEvent(name, sessionId, body);
        return Ok(new { ok = true });
    }
}
