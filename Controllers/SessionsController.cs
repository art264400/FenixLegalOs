using System.Text.Json;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly SessionRepository _sessions;
    private readonly LeadRepository _leads;
    private readonly ScoringEngine _scoringEngine;
    private readonly TypstPdfService _pdfService;
    private readonly AiReportService _aiReportService;
    private readonly SettingsRepository _settings;
    private readonly QuestionRepository _questionRepo;

    public SessionsController(
        SessionRepository sessions,
        LeadRepository leads,
        ScoringEngine scoringEngine,
        TypstPdfService pdfService,
        AiReportService aiReportService,
        SettingsRepository settings,
        QuestionRepository questionRepo)
    {
        _sessions = sessions;
        _leads = leads;
        _scoringEngine = scoringEngine;
        _pdfService = pdfService;
        _aiReportService = aiReportService;
        _settings = settings;
        _questionRepo = questionRepo;
    }

    [HttpGet("pricing")]
    public IActionResult GetPricing()
    {
        return Ok(_settings.GetPricing());
    }

    [HttpPost]
    public IActionResult CreateSession()
    {
        var id = _sessions.CreateSession();
        _leads.RecordEvent("diagnostic_started", id, null);
        return Ok(new { id });
    }

    [HttpPut("{id}/answers")]
    public IActionResult SaveAnswers(string id, [FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("answers", out var answersProp))
            return BadRequest(new { error = "invalid_answers" });

        var answersJson = answersProp.GetRawText();
        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(answersJson) ?? new();

        var validationResult = FenixLegalOs.Scoring.Validation.AnswerValidator.Validate(answersDict, _questionRepo.GetQuestions());
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = "validation_failed", details = validationResult.Errors });
        }

        string? lastSectionId = body.TryGetProperty("lastSectionId", out var secProp) ? secProp.GetString() : null;
        string? currentQuestionId = body.TryGetProperty("currentQuestionId", out var cqProp) ? cqProp.GetString() : null;

        bool ok = _sessions.SaveAnswers(id, answersJson, lastSectionId);
        if (!ok) return NotFound(new { error = "not_found" });

        // Architecture A: Return authoritative navigation state alongside save acknowledgement.
        var navigation = _scoringEngine.GetNavigationState(answersDict, currentQuestionId);
        return Ok(new { accepted = true, navigation });
    }

    [HttpGet("{id}/answers")]
    public IActionResult GetAnswers(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null) return NotFound(new { error = "not_found" });

        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
        return Ok(new { answers = answersDict, lastSectionId = session.LastSectionId });
    }

    [HttpPost("{id}/complete")]
    public IActionResult CompleteSession(string id, [FromBody] JsonElement body)
    {
        var session = _sessions.GetSession(id);
        if (session == null) return NotFound(new { error = "not_found" });

        string answersJson = body.TryGetProperty("answers", out var aProp) ? aProp.GetRawText() : session.AnswersJson;
        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(answersJson) ?? new();

        var validationResult = FenixLegalOs.Scoring.Validation.AnswerValidator.Validate(answersDict, _questionRepo.GetQuestions());
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = "validation_failed", details = validationResult.Errors });
        }

        var result = _scoringEngine.ComputeResult(answersDict);
        _sessions.CompleteSession(id, answersJson, result);
        _leads.RecordEvent("diagnostic_completed", id, new { overall = result.Overall, critical = result.CriticalCount });

        return Ok(new { result });
    }

    /// <summary>
    /// Architecture A вЂ” Server-Driven Routing:
    /// Accepts draft answers, returns the authoritative list of visible question IDs.
    /// Frontend uses this to navigate without any local ShowIf/fact evaluation.
    /// Adding a new module requires ZERO changes to the frontend.
    /// </summary>
    [HttpPost("{id}/navigate")]
    public IActionResult Navigate(string id, [FromBody] JsonElement body)
    {
        Dictionary<string, object> answers = new();
        string? currentQuestionId = null;

        if (body.TryGetProperty("answers", out var answersProp))
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, object>>(answersProp.GetRawText()) ?? new();
        }
        if (body.TryGetProperty("currentQuestionId", out var cqProp) && cqProp.ValueKind == JsonValueKind.String)
        {
            currentQuestionId = cqProp.GetString();
        }

        var nav = _scoringEngine.GetNavigationState(answers, currentQuestionId);
        return Ok(nav);
    }

    [HttpGet("{id}/result")]
    public IActionResult GetResult(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null || string.IsNullOrEmpty(session.ResultJson))
            return NotFound(new { error = "not_found" });

        var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
        bool unlocked = _leads.FindLeadsBySession(id).Any();
        bool paid = session.Paid;

        if (!paid && result != null)
        {
            for (int i = 0; i < result.Risks.Count; i++)
            {
                if (i >= 2)
                {
                    result.Risks[i].Finding = "Р”РµС‚Р°Р»СЊРЅС‹Р№ СЂР°Р·Р±РѕСЂ РґРѕСЃС‚СѓРїРµРЅ РІ РїРѕР»РЅРѕРј РїР»Р°С‚РЅРѕРј РѕС‚С‡РµС‚Рµ";
                    result.Risks[i].WhyItMatters = "РРЅС„РѕСЂРјР°С†РёСЏ СЃРєСЂС‹С‚Р° РІ Р±РµСЃРїР»Р°С‚РЅРѕР№ РґРµРјРѕ-РІРµСЂСЃРёРё";
                    result.Risks[i].Recommendation = "Р Р°Р·Р±Р»РѕРєРёСЂСѓР№С‚Рµ РѕС‚С‡С‘С‚ Рё РґРѕСЂРѕР¶РЅСѓСЋ РєР°СЂС‚Сѓ РґР»СЏ РїСЂРѕСЃРјРѕС‚СЂР° СЂРµРєРѕРјРµРЅРґР°С†РёР№ СЋСЂРёСЃС‚Р°";
                }
            }
        }

        return Ok(new
        {
            result,
            unlocked,
            paid = session.Paid,
            paidAt = session.PaidAt,
            paymentAmount = session.PaymentAmount,
            paymentMethod = session.PaymentMethod
        });
    }

    [HttpGet("{id}/pdf")]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null || string.IsNullOrEmpty(session.ResultJson))
            return NotFound(new { error = "not_found" });

        var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
        if (result == null) return NotFound(new { error = "invalid_result" });

        string? aiSummary = null;
        try
        {
            var answersDict = !string.IsNullOrEmpty(session.AnswersJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new()
                : new();
            aiSummary = await _aiReportService.GenerateExecutiveSummaryAsync(answersDict, result);
        }
        catch (Exception ex)
        {
            Console.WriteLine("[PDF AI Summary Error] " + ex.Message);
        }

        var pdfBytes = await _pdfService.GeneratePdfAsync(result, aiSummary);
        if (pdfBytes == null) return Problem("PDF generation failed");

        return File(pdfBytes, "application/pdf", $"Fenix_Legal_Score_Report_{id}.pdf");
    }

    [HttpPost("{id}/pay")]
    public IActionResult ProcessPayment(string id, [FromBody] JsonElement body)
    {
        var session = _sessions.GetSession(id);
        if (session == null) return NotFound(new { error = "session_not_found" });

        int amount = body.TryGetProperty("amount", out var amProp) ? amProp.GetInt32() : 19999;
        string method = body.TryGetProperty("method", out var mProp) ? mProp.GetString() ?? "kaspi_pay" : "kaspi_pay";

        _sessions.MarkSessionPaid(id, amount, method);
        _leads.RecordEvent("payment_completed", id, new { amount, method });
        _leads.AuditLog("system", "session_paid", $"{id} ({amount} KZT via {method})");

        return Ok(new { ok = true, paid = true, amount, method });
    }

    [HttpPost("{id}/ai-summary")]
    public async Task<IActionResult> GenerateAiSummary(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null || string.IsNullOrEmpty(session.ResultJson))
            return NotFound(new { error = "session_not_found" });

        if (!session.Paid)
        {
            return Ok(new { summary = "рџ”’ **AI-Р·Р°РєР»СЋС‡РµРЅРёРµ РґРѕСЃС‚СѓРїРЅРѕ РїРѕСЃР»Рµ РѕРїР»Р°С‚С‹**\n\nР Р°Р·Р±Р»РѕРєРёСЂСѓР№С‚Рµ РїРѕР»РЅС‹Р№ РѕС‚С‡С‘С‚ Fenix Legal OS, С‡С‚РѕР±С‹ РїРѕР»СѓС‡РёС‚СЊ РїРµСЂСЃРѕРЅР°Р»СЊРЅС‹Р№ СЋСЂРёРґРёС‡РµСЃРєРёР№ РјРµРјРѕСЂР°РЅРґСѓРј РІРµРЅС‡СѓСЂРЅРѕРіРѕ СЋСЂРёСЃС‚Р° Рё РїРѕС€Р°РіРѕРІС‹Р№ Action Plan." });
        }

        var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
        if (result == null) return BadRequest(new { error = "invalid_result" });

        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
        var summary = await _aiReportService.GenerateExecutiveSummaryAsync(answersDict, result);

        return Ok(new { summary });
    }
}

