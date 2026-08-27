using System.Security.Cryptography;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly LeadRepository _leads;
    private readonly QuestionRepository _questionRepo;
    private readonly ScoringEngine _scoringEngine;
    private readonly AiReportService _aiReportService;
    private const string AdminTokenCookieName = "fenix_admin";
    private static readonly HashSet<string> AdminTokens = new();
    private static readonly string AdminPassword = Environment.GetEnvironmentVariable("FENIX_ADMIN_PASSWORD") ?? "fenix2026";

    public AdminController(
        LeadRepository leads,
        QuestionRepository questionRepo,
        ScoringEngine scoringEngine,
        AiReportService aiReportService)
    {
        _leads = leads;
        _questionRepo = questionRepo;
        _scoringEngine = scoringEngine;
        _aiReportService = aiReportService;
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
        var versions = _questionRepo.GetVersions();
        return Ok(new
        {
            version = versions.GetValueOrDefault("question_bank", DataBank.QuestionBankVersion),
            sections = _questionRepo.GetSections(enabledOnly: false),
            questions = _questionRepo.GetQuestions(enabledOnly: false)
        });
    }

    [HttpGet("risk-library")]
    public IActionResult GetRiskLibrary()
    {
        if (!IsAdmin()) return Unauthorized();
        var versions = _questionRepo.GetVersions();
        return Ok(new
        {
            version = versions.GetValueOrDefault("risk_library", DataBank.RiskLibraryVersion),
            risks = _questionRepo.GetRisks()
        });
    }

    [HttpGet("testbench/presets")]
    public IActionResult GetTestBenchPresets()
    {
        if (!IsAdmin()) return Unauthorized();
        var presets = new[]
        {
            new
            {
                id = "preset_deadlock",
                title = "Критический Deadlock 50/50 и нет IP",
                description = "2 сооснователя, равные доли 50/50, совместные решения без механизма выхода из тупика, юрлицо не зарегистрировано, код у фрилансеров.",
                badge = "🔴 Высокий риск",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C03"] = "dispute",
                    ["FND-C04"] = "none",
                    ["FND-01"] = "active_conflict",
                    ["FND-02"] = "dispute",
                    ["FND-03"] = "stopped",
                    ["FND-04"] = "dispute",
                    ["FND-05"] = "not_discussed",
                    ["FND-06"] = "none",
                    ["FND-07"] = "none",
                    ["COR-C01"] = "none"
                }
            },
            new
            {
                id = "preset_solo_ai",
                title = "Solo-Founder B2B SaaS с AI",
                description = "1 основатель 100%, ТОО зарегистрировано в Казахстане, B2B-клиенты, интеграция с LLM, оформлены основные отношения.",
                badge = "🟠 1 Компания",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz",
                    ["COR-01"] = "match",
                    ["COR-02"] = "complete",
                    ["COR-03"] = "none",
                    ["COR-04"] = "none",
                    ["COR-05"] = "systematic",
                    ["COR-06"] = "clear_limits",
                    ["COR-07"] = "aligned",
                    ["COR-08"] = "organized",
                    ["COR-T01"] = "none"
                }
            },
            new
            {
                id = "preset_seed_ready",
                title = "Группа компаний (МФЦА Холдинг + ТОО в РК)",
                description = "3 основателя с вестингом, холдинг в МФЦА (AIFC), операционная компания в РК для работы с клиентами и приема выручки, четкая структура.",
                badge = "🟢 Группа компаний",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "3",
                    ["FND-C03"] = "none",
                    ["FND-C04"] = "signed",
                    ["FND-01"] = "none",
                    ["FND-02"] = "written",
                    ["FND-03"] = "full",
                    ["FND-04"] = "registered",
                    ["FND-05"] = "vesting",
                    ["FND-05A"] = "yes",
                    ["FND-06"] = "written",
                    ["FND-07"] = "mechanism",
                    ["FND-08"] = "written",
                    ["COR-C01"] = "multiple",
                    ["COR-C02A"] = "aifc",
                    ["COR-C02B"] = "2",
                    ["COR-C02C"] = JsonSerializer.SerializeToElement(new[]
                    {
                        new { jurisdiction = "kz", roles = new[] { "clients", "payments" } }
                    }),
                    ["COR-01"] = "match",
                    ["COR-02"] = "complete",
                    ["COR-03"] = "none",
                    ["COR-04"] = "none",
                    ["COR-05"] = "systematic",
                    ["COR-06"] = "clear_limits",
                    ["COR-07_GROUP"] = "aligned",
                    ["COR-08"] = "organized",
                    ["COR-T01"] = "none"
                }
            }
        };

        // Also fetch up to 10 latest real user sessions for testing live data
        var liveLeads = _leads.ListLeads().Take(10).Select(l => new
        {
            id = "session_" + l.Id,
            title = "Сессия: " + (string.IsNullOrEmpty(l.Company) ? (l.Name ?? l.Id) : l.Company),
            description = $"Создан: {l.CreatedAt}, Heat: {l.HeatLabel}, Контакт: {l.Email ?? l.Messenger ?? "нет"}",
            badge = "👤 Реальный пользователь",
            answers = l.SessionAnswers != null ? JsonSerializer.Deserialize<Dictionary<string, object>>((string)l.SessionAnswers) : new()
        }).ToList();

        return Ok(new { presets, liveSessions = liveLeads });
    }

    [HttpPost("testbench/simulate")]
    public IActionResult SimulateTest([FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        var answers = new Dictionary<string, object>();
        if (body.TryGetProperty("answers", out var aProp) && aProp.ValueKind == JsonValueKind.Object)
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, object>>(aProp.GetRawText()) ?? new();
        }

        var result = _scoringEngine.ComputeResult(answers);

        // Format questions and chosen answers grouped by sections for human-friendly display
        var allQuestions = _questionRepo.GetQuestions(enabledOnly: false);
        var allSections = _questionRepo.GetSections(enabledOnly: false);

        var structuredAnswers = allSections.Select(sec =>
        {
            var secQuestions = allQuestions.Where(q => q.SectionId == sec.Id).Select(q =>
            {
                answers.TryGetValue(q.Id, out var val);
                string answerText = "—";
                if (val != null)
                {
                    var opt = q.Options?.FirstOrDefault(o => o.Id.Equals(val.ToString(), StringComparison.OrdinalIgnoreCase));
                    answerText = opt != null ? opt.Label : val.ToString()!;
                }

                return new
                {
                    id = q.Id,
                    question = q.Question,
                    rawVal = val?.ToString(),
                    answerText = answerText,
                    answered = val != null
                };
            }).Where(q => q.answered).ToList();

            return new
            {
                sectionId = sec.Id,
                sectionTitle = sec.Title,
                questions = secQuestions
            };
        }).Where(s => s.questions.Count > 0).ToList();

        return Ok(new
        {
            result,
            structuredAnswers,
            totalAnswered = answers.Count
        });
    }

    [HttpPost("testbench/generate-ai")]
    public async Task<IActionResult> GenerateAiTest([FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        ScoreResult? result = null;
        Dictionary<string, object>? answers = null;

        if (body.TryGetProperty("result", out var rProp) && rProp.ValueKind == JsonValueKind.Object)
        {
            result = JsonSerializer.Deserialize<ScoreResult>(rProp.GetRawText());
        }
        if (body.TryGetProperty("answers", out var aProp) && aProp.ValueKind == JsonValueKind.Object)
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, object>>(aProp.GetRawText());
        }

        if (result == null) return BadRequest(new { error = "missing_result" });

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var memo = await _aiReportService.GenerateExecutiveSummaryAsync(answers ?? new(), result);
        sw.Stop();

        return Ok(new
        {
            memo,
            durationMs = sw.ElapsedMilliseconds,
            model = "gpt-4o-mini"
        });
    }
}
