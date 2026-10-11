using System.Security.Cryptography;
using System.Text;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/admin")]
public sealed class AdminReportRevisionsController(
    AdminSessionService admin, ReportRevisionService editor, ReportRevisionRepository revisions,
    SessionRepository sessions, LeadRepository leads) : ControllerBase
{
    private string Actor()
    {
        var token = Request.Cookies[AdminSessionService.CookieName] ?? "";
        return "admin:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))[..12];
    }

    private IActionResult Run(Func<object> action)
    {
        if (!admin.IsAdmin(HttpContext)) return Unauthorized(new { error = "unauthorized" });
        try { return Ok(action()); }
        catch (ReportRevisionException ex) { return StatusCode(ex.Status, new { error = ex.Code, message = ex.Message, details = ex.Details, missingQuestionIds = ex.Code == "incomplete_answers" ? ex.Details : null }); }
    }

    [HttpGet("leads/{leadId}/answer-editor")]
    public IActionResult GetByLead(string leadId) => Run(() =>
    {
        var lead = leads.GetLead(leadId);
        if (lead == null) throw new ReportRevisionException("not_found", "Отчёт не найден.", 404);
        return editor.GetEditor((string)lead.SessionId);
    });

    [HttpGet("sessions/{sessionId}/answer-editor")]
    public IActionResult Get(string sessionId) => Run(() => editor.GetEditor(sessionId));

    [HttpPost("sessions/{sessionId}/answer-editor/preview")]
    public IActionResult Preview(string sessionId, [FromBody] AnswerEditorRequest request) => Run(() => editor.Preview(sessionId, request));

    [HttpPut("sessions/{sessionId}/answer-editor")]
    public IActionResult Save(string sessionId, [FromBody] AnswerEditorRequest request) => Run(() => editor.Save(sessionId, request, Actor()));

    [HttpPost("sessions/{sessionId}/answer-revisions/{revisionId}/recalculate")]
    public async Task<IActionResult> Recalculate(string sessionId, string revisionId, [FromBody] RevisionVersionRequest request)
    {
        if (!admin.IsAdmin(HttpContext)) return Unauthorized(new { error = "unauthorized" });
        try { return Ok(await editor.RecalculateAsync(sessionId, revisionId, request.Version)); }
        catch (ReportRevisionException ex) { return StatusCode(ex.Status, new { error = ex.Code, message = ex.Message, details = ex.Details, missingQuestionIds = ex.Code == "incomplete_answers" ? ex.Details : null }); }
    }

    [HttpPost("sessions/{sessionId}/answer-revisions/{revisionId}/publish")]
    public IActionResult Publish(string sessionId, string revisionId, [FromBody] RevisionVersionRequest request)
        => Run(() => editor.Publish(sessionId, revisionId, request.Version, Actor()));

    [HttpPost("sessions/{sessionId}/answer-revisions/{revisionId}/discard")]
    public IActionResult Discard(string sessionId, string revisionId, [FromBody] RevisionVersionRequest request)
        => Run(() => editor.Discard(sessionId, revisionId, request.Version, Actor()));

    [HttpGet("sessions/{sessionId}/answer-revisions/{revisionId}/pdf")]
    public IActionResult Pdf(string sessionId, string revisionId)
    {
        if (!admin.IsAdmin(HttpContext)) return Unauthorized(new { error = "unauthorized" });
        var bytes = revisionId == "original" ? sessions.GetOriginalSession(sessionId)?.PdfBytes : revisions.Get(sessionId, revisionId)?.PdfBytes;
        if (bytes is not { Length: > 0 }) return NotFound(new { error = "pdf_not_ready", message = "PDF этой редакции ещё не сформирован." });
        return File(bytes, "application/pdf", $"Fenix_SLS_Report_{sessionId}_{revisionId}.pdf");
    }
}
