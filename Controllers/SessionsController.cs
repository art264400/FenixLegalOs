using System.Text.Json;
using FenixLegalOs.Infrastructure;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

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
    private readonly UserRepository? _users;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Task<byte[]?>> _pdfGenerationTasks = new();

    public SessionsController(
        SessionRepository sessions,
        LeadRepository leads,
        ScoringEngine scoringEngine,
        TypstPdfService pdfService,
        AiReportService aiReportService,
        SettingsRepository settings,
        QuestionRepository questionRepo,
        UserRepository? users = null)
    {
        _sessions = sessions;
        _leads = leads;
        _scoringEngine = scoringEngine;
        _pdfService = pdfService;
        _aiReportService = aiReportService;
        _settings = settings;
        _questionRepo = questionRepo;
        _users = users;
    }

    private UserAccount? GetAuthenticatedUser()
    {
        if (_users == null) return null;

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

    [HttpGet("pricing")]
    public IActionResult GetPricing()
    {
        return Ok(_settings.GetPricing());
    }

    [HttpPost]
    public IActionResult CreateSession()
    {
        var id = _sessions.CreateSession();
        var authUser = GetAuthenticatedUser();
        if (authUser != null && _users != null)
        {
            _users.AttachUserToSession(id, authUser.Id);
        }
        _leads.RecordEvent("diagnostic_started", id, null);
        return Ok(new { id });
    }

    [HttpPut("{id}/answers")]
    [RequireSessionAccess(disallowCompleted: true)]
    public IActionResult SaveAnswers(string id, [FromBody] JsonElement body)
    {
        var session = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession ?? _sessions.GetSession(id);
        if (session != null && !string.IsNullOrEmpty(session.CompletedAt))
        {
            return Conflict(new { error = "session_already_completed", message = "Диагностика по этой анкете уже завершена. Ответы и результат зафиксированы и не могут быть изменены." });
        }

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
        string? answeredQuestionId = body.TryGetProperty("answeredQuestionId", out var aqProp) ? aqProp.GetString() : null;

        bool ok = _sessions.SaveAnswers(id, answersJson, lastSectionId);
        if (!ok)
        {
            var curSession = _sessions.GetSession(id);
            if (curSession != null && !string.IsNullOrEmpty(curSession.CompletedAt))
            {
                return Conflict(new { error = "session_already_completed", message = "Диагностика по этой анкете уже завершена. Ответы и результат зафиксированы и не могут быть изменены." });
            }
            return NotFound(new { error = "not_found" });
        }

        // Architecture A: Return authoritative navigation state alongside save acknowledgement.
        var navigation = _scoringEngine.GetNavigationState(answersDict, currentQuestionId, answeredQuestionId);
        return Ok(new { accepted = true, navigation });
    }

    [HttpGet("{id}/answers")]
    [RequireSessionAccess]
    public IActionResult GetAnswers(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null) return NotFound(new { error = "not_found" });

        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
        return Ok(new { answers = answersDict, lastSectionId = session.LastSectionId });
    }

    [HttpPost("{id}/complete")]
    [RequireSessionAccess(disallowCompleted: true)]
    public IActionResult CompleteSession(string id, [FromBody] JsonElement body)
    {
        var session = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession ?? _sessions.GetSession(id);
        if (session == null) return NotFound(new { error = "not_found" });
        if (!string.IsNullOrEmpty(session.CompletedAt))
        {
            return Conflict(new { error = "session_already_completed", message = "Диагностика по этой анкете уже завершена. Ответы и результат зафиксированы и не могут быть изменены." });
        }

        string answersJson = body.TryGetProperty("answers", out var aProp) ? aProp.GetRawText() : session.AnswersJson;
        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(answersJson) ?? new();

        var validationResult = FenixLegalOs.Scoring.Validation.AnswerValidator.Validate(answersDict, _questionRepo.GetQuestions());
        if (!validationResult.IsValid)
        {
            return BadRequest(new { error = "validation_failed", details = validationResult.Errors });
        }

        var result = _scoringEngine.ComputeResult(answersDict);
        bool completed = _sessions.CompleteSession(id, answersJson, result);
        if (!completed)
        {
            var cur = _sessions.GetSession(id);
            if (cur != null && !string.IsNullOrEmpty(cur.CompletedAt))
            {
                return Conflict(new { error = "session_already_completed", message = "Диагностика по этой анкете уже завершена. Ответы и результат зафиксированы и не могут быть изменены." });
            }
        }
        _leads.RecordEvent("diagnostic_completed", id, new { overall = result.Overall, critical = result.CriticalCount });

        return Ok(new { result });
    }

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
    [RequireSessionAccess]
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
                    result.Risks[i].Finding = "Детальный разбор доступен в полном платном отчете";
                    result.Risks[i].WhyItMatters = "Информация скрыта в бесплатной демо-версии";
                    result.Risks[i].Recommendation = "Разблокируйте отчёт и дорожную карту для просмотра рекомендаций юриста";
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
    [RequireSessionAccess(requirePayment: true)]
    public async Task<IActionResult> DownloadPdf(string id)
    {
        var session = HttpContext?.Items["DiagnosticSession"] as DiagnosticSession ?? _sessions.GetSession(id);
        if (session == null || string.IsNullOrEmpty(session.ResultJson))
            return NotFound(new { error = "not_found" });

        // 1. Check in-memory cache
        var cache = HttpContext?.RequestServices?.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        string cacheKey = $"pdf_report_{id}";
        if (cache != null && cache.TryGetValue(cacheKey, out byte[]? cachedBytes) && cachedBytes != null && cachedBytes.Length > 0)
        {
            return File(cachedBytes, "application/pdf", $"Fenix_SLS_Report_{id}.pdf");
        }

        // 2. Check if already persisted in database
        if (session.PdfBytes != null && session.PdfBytes.Length > 0)
        {
            cache?.Set(cacheKey, session.PdfBytes, TimeSpan.FromHours(2));
            return File(session.PdfBytes, "application/pdf", $"Fenix_SLS_Report_{id}.pdf");
        }

        var existingPdf = _sessions.GetPdf(id);
        if (existingPdf != null && existingPdf.Length > 0)
        {
            cache?.Set(cacheKey, existingPdf, TimeSpan.FromHours(2));
            return File(existingPdf, "application/pdf", $"Fenix_SLS_Report_{id}.pdf");
        }

        // 3. Resolve result and context: prioritize stored immutable diagnostic result!
        var answersDict = !string.IsNullOrEmpty(session.AnswersJson)
            ? JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new()
            : new();

        ScoreResult? result = null;
        if (!string.IsNullOrEmpty(session.ResultJson))
        {
            try
            {
                result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
            }
            catch { /* fallback */ }
        }

        if (result == null && answersDict.Count > 0)
        {
            result = _scoringEngine.ComputeResult(answersDict);
        }

        if (result == null) return NotFound(new { error = "invalid_result" });

        var facts = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answersDict);
        var lead = _leads.FindLeadsBySession(id).FirstOrDefault();
        string? leadCompany = lead?.Company as string;
        string companyName = !string.IsNullOrWhiteSpace(leadCompany) ? leadCompany : "Стартап";

        // 4. Coordinate concurrent PDF generation per session to avoid duplicate runs and race writes
        Task<byte[]?> generationTask;
        bool isInitiator = false;

        lock (_pdfGenerationTasks)
        {
            if (_pdfGenerationTasks.TryGetValue(id, out var inFlightTask))
            {
                generationTask = inFlightTask;
            }
            else
            {
                isInitiator = true;
                generationTask = Task.Run(async () =>
                {
                    // Double check database in case another thread/process completed right before
                    var persisted = _sessions.GetPdf(id);
                    if (persisted != null && persisted.Length > 0)
                    {
                        return persisted;
                    }

                    var generated = await _pdfService.GeneratePdfAsync(result, facts, id, companyName);
                    if (generated != null && generated.Length > 0)
                    {
                        _sessions.SavePdf(id, generated);
                    }
                    return generated;
                });
                _pdfGenerationTasks[id] = generationTask;
            }
        }

        byte[]? pdfBytes;
        try
        {
            pdfBytes = await generationTask;
        }
        finally
        {
            if (isInitiator)
            {
                lock (_pdfGenerationTasks)
                {
                    _pdfGenerationTasks.TryRemove(id, out _);
                }
            }
        }

        if (pdfBytes == null || pdfBytes.Length == 0)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "generation_failed",
                message = "Не удалось сформировать PDF-документ. Пожалуйста, повторите попытку позже."
            });
        }

        cache?.Set(cacheKey, pdfBytes, TimeSpan.FromHours(2));
        return File(pdfBytes, "application/pdf", $"Fenix_SLS_Report_{id}.pdf");
    }

    [HttpPost("{id}/pay")]
    [RequireSessionAccess]
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
    [RequireSessionAccess(requirePayment: true)]
    public async Task<IActionResult> GenerateAiSummary(string id)
    {
        var session = _sessions.GetSession(id);
        if (session == null || string.IsNullOrEmpty(session.ResultJson))
            return NotFound(new { error = "session_not_found" });

        var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
        if (result == null) return BadRequest(new { error = "invalid_result" });

        var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
        var facts = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answersDict);

        var reportCtx = FenixLegalOs.Scoring.Report.ReportEngine.AssembleReportContext(result, facts, id, "Стартап");
        var narratives = await _aiReportService.GenerateReportNarrativesAsync(reportCtx);

        return Ok(new { summary = narratives.ExecutiveConclusion, narratives });
    }
}
