using System.Security.Cryptography;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Register Services & Repositories
builder.Services.AddSingleton<DbInitializer>();
builder.Services.AddSingleton<SessionRepository>();
builder.Services.AddSingleton<LeadRepository>();
builder.Services.AddSingleton<ScoringEngine>();
builder.Services.AddSingleton<TypstPdfService>();

var app = builder.Build();

// Init Database
var dbInit = app.Services.GetRequiredService<DbInitializer>();
dbInit.Initialize();

if (args.Contains("--generate-worst-case"))
{
    var scoringEngine = app.Services.GetRequiredService<ScoringEngine>();
    var pdfService = app.Services.GetRequiredService<TypstPdfService>();

    var answers = new Dictionary<string, object>
    {
        ["f_count"] = "two",
        ["f_equity_fixed"] = "none",
        ["f_roles"] = "no",
        ["f_agreement"] = "no",
        ["f_fulltime"] = "few",
        ["f_leaver"] = "none",
        ["f_vesting"] = "no",
        ["f_decisions"] = "no",
        ["f_deadlock"] = "no",
        ["f_transfer"] = "no",
        ["c_inc"] = "yes",
        ["c_jur"] = "kz",
        ["c_match"] = "partial",
        ["c_promises"] = "yes_verbal",
        ["c_captable"] = "no",
        ["c_approvals"] = "no",
        ["ip_creators"] = "mixed",
        ["ip_contracts"] = "none",
        ["ip_transfer"] = "no",
        ["ip_founder_assign"] = "no",
        ["ip_control"] = "external",
        ["ip_domain"] = "external",
        ["ip_oss"] = "yes",
        ["ip_oss_check"] = "no",
        ["ip_registered"] = new string[] { "none" },
        ["t_size"] = "s6_15",
        ["t_has"] = "yes",
        ["t_contracts"] = "none",
        ["t_provisions"] = "no",
        ["t_foreign"] = "yes",
        ["t_core"] = "yes",
        ["p_users"] = "yes",
        ["p_revenue"] = "yes",
        ["p_model"] = "both",
        ["p_terms"] = "no",
        ["p_acceptance"] = "no",
        ["p_payments"] = "recurring",
        ["p_ugc"] = "yes",
        ["p_ugc_terms"] = "no",
        ["d_pd"] = "yes",
        ["d_categories"] = new string[] { "contact", "behavior", "payment", "location", "sensitive" },
        ["d_pp"] = "no",
        ["d_pp_match"] = "no",
        ["d_services"] = new string[] { "openai", "anthropic", "google", "aws", "firebase", "analytics", "crm", "apis" },
        ["d_ai"] = "yes",
        ["d_ai_data"] = "yes",
        ["d_ai_informed"] = "no",
        ["d_ai_sensitive"] = "yes",
        ["d_geo"] = "yes",
        ["k_b2b"] = "no",
        ["k_terms"] = "not_sure",
        ["k_dependency"] = "yes",
        ["i_funding"] = "yes",
        ["i_instruments"] = new string[] { "informal" },
        ["i_captable_reflects"] = "no",
        ["i_round"] = "m3",
        ["i_dataroom"] = "no",
        ["i_dd"] = "no"
    };

    var scoreResult = scoringEngine.ComputeResult(answers);

    Console.WriteLine($"FENIX_SCORE:{scoreResult.Overall}");
    Console.WriteLine($"TOTAL_RISKS:{scoreResult.Risks.Count}");
    Console.WriteLine($"CRITICAL_RISKS:{scoreResult.CriticalCount}");
    Console.WriteLine($"HIGH_RISKS:{scoreResult.HighCount}");
    Console.WriteLine($"MEDIUM_RISKS:{scoreResult.MediumCount}");

    foreach (var sec in scoreResult.Sections)
    {
        var secRisks = scoreResult.Risks.Count(r => r.SectionId == sec.SectionId);
        Console.WriteLine($"DOMAIN_STAT:{sec.Title}|{(sec.Score.HasValue ? sec.Score.Value : 0)}|{secRisks}");
    }

    var typstContent = pdfService.BuildTypstMarkup(scoreResult, "Phoenix Test Startup");
    var typPath = Path.Combine(Directory.GetCurrentDirectory(), "fenix-worst-case.typ");
    File.WriteAllText(typPath, typstContent, System.Text.Encoding.UTF8);
    Console.WriteLine($"TYP_GENERATED:{typPath}");

    var pdfBytes = pdfService.GeneratePdfAsync(scoreResult, "Phoenix Test Startup").GetAwaiter().GetResult();
    if (pdfBytes != null)
    {
        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "fenix-worst-case-report.pdf");
        File.WriteAllBytes(outputPath, pdfBytes);
        Console.WriteLine($"PDF_GENERATED:{outputPath}");
    }
    else
    {
        Console.WriteLine("PDF_GENERATION_FAILED");
    }

    return;
}

// Static Files Configuration (serving wwwroot directory)
var staticPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (Directory.Exists(staticPath))
{
    app.UseFileServer(new FileServerOptions
    {
        FileProvider = new PhysicalFileProvider(staticPath),
        RequestPath = "",
        EnableDefaultFiles = true
    });
}
else
{
    app.UseStaticFiles();
}

const string adminTokenCookieName = "fenix_admin";
var adminTokens = new HashSet<string>();
var adminPassword = Environment.GetEnvironmentVariable("FENIX_ADMIN_PASSWORD") ?? "fenix2026";

bool IsAdmin(HttpContext ctx)
{
    if (ctx.Request.Cookies.TryGetValue(adminTokenCookieName, out var token) && !string.IsNullOrEmpty(token))
    {
        return adminTokens.Contains(token);
    }
    return false;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

app.MapGet("/api/questionnaire", () => Results.Ok(new
{
    sections = DataBank.Sections,
    questions = DataBank.Questions.Where(q => q.Enabled),
    versions = new
    {
        questionBank = DataBank.QuestionBankVersion,
        scoringEngine = DataBank.ScoringEngineVersion,
        riskLibrary = DataBank.RiskLibraryVersion
    }
}));

app.MapPost("/api/sessions", (SessionRepository sessions, LeadRepository leads) =>
{
    var id = sessions.CreateSession();
    leads.RecordEvent("diagnostic_started", id, null);
    return Results.Ok(new { id });
});

app.MapPut("/api/sessions/{id}/answers", (string id, JsonElement body, SessionRepository sessions) =>
{
    if (!body.TryGetProperty("answers", out var answersProp))
        return Results.BadRequest(new { error = "invalid_answers" });

    var answersJson = answersProp.GetRawText();
    string? lastSectionId = body.TryGetProperty("lastSectionId", out var secProp) ? secProp.GetString() : null;

    bool ok = sessions.SaveAnswers(id, answersJson, lastSectionId);
    return ok ? Results.Ok(new { ok = true }) : Results.NotFound(new { error = "not_found" });
});

app.MapGet("/api/sessions/{id}/answers", (string id, SessionRepository sessions) =>
{
    var session = sessions.GetSession(id);
    if (session == null) return Results.NotFound(new { error = "not_found" });

    var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson) ?? new();
    return Results.Ok(new { answers = answersDict, lastSectionId = session.LastSectionId });
});

app.MapPost("/api/sessions/{id}/complete", (string id, JsonElement body, SessionRepository sessions, LeadRepository leads, ScoringEngine engine) =>
{
    var session = sessions.GetSession(id);
    if (session == null) return Results.NotFound(new { error = "not_found" });

    string answersJson = body.TryGetProperty("answers", out var aProp) ? aProp.GetRawText() : session.AnswersJson;
    var answersDict = JsonSerializer.Deserialize<Dictionary<string, object>>(answersJson) ?? new();

    var result = engine.ComputeResult(answersDict);
    sessions.CompleteSession(id, answersJson, result);
    leads.RecordEvent("diagnostic_completed", id, new { overall = result.Overall, critical = result.CriticalCount });

    return Results.Ok(new { result });
});

app.MapGet("/api/sessions/{id}/result", (string id, SessionRepository sessions, LeadRepository leads) =>
{
    var session = sessions.GetSession(id);
    if (session == null || string.IsNullOrEmpty(session.ResultJson))
        return Results.NotFound(new { error = "not_found" });

    var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
    bool unlocked = leads.FindLeadsBySession(id).Any();
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

    return Results.Ok(new
    {
        result,
        unlocked,
        paid = session.Paid,
        paidAt = session.PaidAt,
        paymentAmount = session.PaymentAmount,
        paymentMethod = session.PaymentMethod
    });
});

app.MapGet("/api/sessions/{id}/pdf", async (string id, SessionRepository sessions, TypstPdfService typstPdf) =>
{
    var session = sessions.GetSession(id);
    if (session == null || string.IsNullOrEmpty(session.ResultJson))
        return Results.NotFound(new { error = "not_found" });

    var result = JsonSerializer.Deserialize<ScoreResult>(session.ResultJson);
    if (result == null) return Results.NotFound(new { error = "invalid_result" });

    var pdfBytes = await typstPdf.GeneratePdfAsync(result);
    if (pdfBytes == null) return Results.Problem("PDF generation failed");

    return Results.File(pdfBytes, "application/pdf", $"Fenix_Legal_Score_Report_{id}.pdf");
});

app.MapPost("/api/sessions/{id}/pay", (string id, JsonElement body, SessionRepository sessions, LeadRepository leads) =>
{
    var session = sessions.GetSession(id);
    if (session == null) return Results.NotFound(new { error = "session_not_found" });

    int amount = body.TryGetProperty("amount", out var amProp) ? amProp.GetInt32() : 9900;
    string method = body.TryGetProperty("method", out var mProp) ? mProp.GetString() ?? "kaspi_qr" : "kaspi_qr";

    sessions.MarkSessionPaid(id, amount, method);
    leads.RecordEvent("payment_completed", id, new { amount, method });
    leads.AuditLog("system", "session_paid", $"{id} ({amount} KZT via {method})");

    return Results.Ok(new { ok = true, paid = true, amount, method });
});

app.MapPost("/api/leads", (JsonElement body, SessionRepository sessions, LeadRepository leads) =>
{
    string sessionId = body.TryGetProperty("sessionId", out var sProp) ? sProp.GetString() ?? "" : "";
    var session = sessions.GetSession(sessionId);
    if (session == null) return Results.NotFound(new { error = "session_not_found" });

    string type = body.TryGetProperty("type", out var tProp) && tProp.GetString() == "consultation" ? "consultation" : "report_gate";
    string name = body.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
    string email = body.TryGetProperty("email", out var eProp) ? eProp.GetString() ?? "" : "";

    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
        return Results.BadRequest(new { error = "invalid_input" });

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

    string leadId = leads.CreateLead(lead);
    leads.RecordEvent(type == "consultation" ? "consultation_requested" : "lead_captured", sessionId, new { leadId });

    return Results.Ok(new { ok = true, leadId });
});

app.MapPost("/api/events", (JsonElement body, LeadRepository leads) =>
{
    string name = body.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
    string? sessionId = body.TryGetProperty("sessionId", out var sProp) ? sProp.GetString() : null;
    leads.RecordEvent(name, sessionId, body);
    return Results.Ok(new { ok = true });
});

// ---------------------------------------------------------------------------
// Admin API
// ---------------------------------------------------------------------------

app.MapPost("/api/admin/login", (HttpContext ctx, JsonElement body, LeadRepository leads) =>
{
    string pwd = body.TryGetProperty("password", out var pProp) ? pProp.GetString() ?? "" : "";
    if (pwd != adminPassword) return Results.Unauthorized();

    string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(18));
    adminTokens.Add(token);
    leads.AuditLog("admin", "login", null);

    ctx.Response.Headers.Append("Set-Cookie", $"fenix_admin={token}; HttpOnly; Path=/; SameSite=Strict; Max-Age=86400");
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/admin/overview", (HttpContext ctx, LeadRepository leads) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    return Results.Ok(leads.GetOverviewStats());
});

app.MapGet("/api/admin/leads", (HttpContext ctx, LeadRepository leads) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    var list = leads.ListLeads().Select(l =>
    {
        ScoreResult? result = l.SessionResult != null ? JsonSerializer.Deserialize<ScoreResult>((string)l.SessionResult) : null;
        return new
        {
            id = l.Id, name = l.Name, company = l.Company, email = l.Email,
            messenger = l.Messenger, type = l.Type, interest = l.Interest,
            heatScore = l.HeatScore, heatLabel = l.HeatLabel, status = l.Status,
            paid = Convert.ToBoolean(l.Paid), paidAt = l.PaidAt,
            paymentAmount = l.PaymentAmount, paymentMethod = l.PaymentMethod,
            createdAt = l.CreatedAt,
            overall = result?.Overall,
            criticalCount = result?.CriticalCount,
            topRisk = result?.Risks?.FirstOrDefault()?.Title
        };
    });
    return Results.Ok(new { leads = list });
});

app.MapGet("/api/admin/leads/{id}", (HttpContext ctx, string id, LeadRepository leads) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    var lead = leads.GetLead(id);
    if (lead == null) return Results.NotFound(new { error = "not_found" });

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

    return Results.Ok(new
    {
        lead = new
        {
            id = lead.Id, name = lead.Name, company = lead.Company, website = lead.Website,
            email = lead.Email, messenger = lead.Messenger, type = lead.Type,
            interest = lead.Interest, sourceRiskCode = lead.SourceRiskCode,
            heatScore = lead.HeatScore, heatLabel = lead.HeatLabel,
            status = lead.Status, paid = Convert.ToBoolean(lead.Paid), paidAt = lead.PaidAt,
            paymentAmount = lead.PaymentAmount, paymentMethod = lead.PaymentMethod,
            createdAt = lead.CreatedAt
        },
        result,
        answers = answerRows,
        fundraisingTimeline = answers != null && answers.TryGetValue("i_round", out var r) ? r?.ToString() : null,
        notes = leads.GetLeadNotes(id)
    });
});

app.MapPost("/api/admin/leads/{id}/status", (HttpContext ctx, string id, JsonElement body, LeadRepository leads) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    string status = body.TryGetProperty("status", out var sProp) ? sProp.GetString() ?? "" : "";
    bool ok = leads.UpdateStatus(id, status);
    if (ok) leads.AuditLog("admin", "lead_status", $"{id} → {status}");
    return ok ? Results.Ok(new { ok = true }) : Results.NotFound(new { error = "not_found" });
});

app.MapPost("/api/admin/leads/{id}/notes", (HttpContext ctx, string id, JsonElement body, LeadRepository leads) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    string note = body.TryGetProperty("note", out var nProp) ? nProp.GetString() ?? "" : "";
    if (string.IsNullOrWhiteSpace(note)) return Results.BadRequest(new { error = "empty_note" });
    leads.AddNote(id, note);
    leads.AuditLog("admin", "lead_note", id);
    return Results.Ok(new { ok = true });
});

app.MapGet("/api/admin/question-bank", (HttpContext ctx) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    return Results.Ok(new { version = DataBank.QuestionBankVersion, sections = DataBank.Sections, questions = DataBank.Questions });
});

app.MapGet("/api/admin/risk-library", (HttpContext ctx) =>
{
    if (!IsAdmin(ctx)) return Results.Unauthorized();
    return Results.Ok(new { version = DataBank.RiskLibraryVersion, risks = DataBank.Risks });
});

app.MapGet("/admin", (HttpContext ctx) =>
{
    var adminHtml = Path.Combine(staticPath, "admin.html");
    return File.Exists(adminHtml) ? Results.File(adminHtml, "text/html") : Results.NotFound();
});

var portStr = Environment.GetEnvironmentVariable("PORT") ?? "5050";
var url = $"http://0.0.0.0:{portStr}";

Console.WriteLine($"Fenix Legal OS (.NET C# + Dapper) -> http://localhost:{portStr}");
Console.WriteLine($"Admin -> http://localhost:{portStr}/admin");

app.Run(url);
