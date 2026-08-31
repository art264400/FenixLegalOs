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
    private readonly RiskRepository _riskRepo;
    private readonly ScoringEngine _scoringEngine;
    private readonly AiReportService _aiReportService;
    private readonly SettingsRepository _settings;
    private readonly TypstPdfService _pdfService;
    private const string AdminTokenCookieName = "fenix_admin";
    private static readonly HashSet<string> AdminTokens = new();
    private static readonly string AdminPassword = Environment.GetEnvironmentVariable("FENIX_ADMIN_PASSWORD") ?? "fenix2026";

    public AdminController(
        LeadRepository leads,
        QuestionRepository questionRepo,
        RiskRepository riskRepo,
        ScoringEngine scoringEngine,
        AiReportService aiReportService,
        SettingsRepository settings,
        TypstPdfService pdfService)
    {
        _leads = leads;
        _questionRepo = questionRepo;
        _riskRepo = riskRepo;
        _scoringEngine = scoringEngine;
        _aiReportService = aiReportService;
        _settings = settings;
        _pdfService = pdfService;
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

        Response?.Headers.Append("Set-Cookie", $"{AdminTokenCookieName}={token}; HttpOnly; Path=/; SameSite=Strict; Max-Age=86400");
        return Ok(new { ok = true });
    }

    [HttpGet("overview")]
    public IActionResult GetOverview()
    {
        if (!IsAdmin()) return Unauthorized();
        return Ok(_leads.GetOverviewStats());
    }

    [HttpGet("settings/pricing")]
    public IActionResult GetPricingSettings()
    {
        if (!IsAdmin()) return Unauthorized();
        var pricing = _settings.GetPricing();
        var contacts = _settings.GetContacts();
        return Ok(new
        {
            pricing.PriceKzt,
            pricing.OldPriceKzt,
            pricing.ConsultationPriceKzt,
            pricing.Currency,
            pricing.DiscountPercent,
            contacts.Telegram,
            contacts.Website,
            contacts.Phone,
            contacts.Email
        });
    }

    [HttpPost("settings/pricing")]
    public IActionResult UpdatePricingSettings([FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        int price = body.TryGetProperty("priceKzt", out var pProp) ? pProp.GetInt32() : 49990;
        int oldPrice = body.TryGetProperty("oldPriceKzt", out var oProp) ? oProp.GetInt32() : price;
        int consultationPrice = body.TryGetProperty("consultationPriceKzt", out var cProp) ? cProp.GetInt32() : 79900;

        _settings.UpdatePricing(price, oldPrice, consultationPrice);

        string telegram = body.TryGetProperty("telegram", out var tgProp) ? tgProp.GetString() ?? "@fenixlaw" : "@fenixlaw";
        string website = body.TryGetProperty("website", out var webProp) ? webProp.GetString() ?? "www.fenixlaw.org" : "www.fenixlaw.org";
        string phone = body.TryGetProperty("phone", out var phProp) ? phProp.GetString() ?? "+7-700-559-1377" : "+7-700-559-1377";
        string email = body.TryGetProperty("email", out var emProp) ? emProp.GetString() ?? "team@fenixlaw.org" : "team@fenixlaw.org";

        _settings.UpdateContacts(telegram, website, phone, email);

        _leads.AuditLog("admin", "pricing_and_contacts_updated", $"Price: {price} KZT, Consultation: {consultationPrice} KZT, TG: {telegram}, Phone: {phone}");
        
        var pricing = _settings.GetPricing();
        var contacts = _settings.GetContacts();
        return Ok(new
        {
            ok = true,
            pricing.PriceKzt,
            pricing.OldPriceKzt,
            pricing.ConsultationPriceKzt,
            pricing.Currency,
            contacts.Telegram,
            contacts.Website,
            contacts.Phone,
            contacts.Email
        });
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
                title = "Критический Deadlock 50/50 и спор по долям",
                description = "Два основателя 50/50 без механизма разрешения тупиковых ситуаций, устные договоренности, активные разногласия.",
                badge = "🔴 Deadlock 50/50",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 }),
                    ["FND-C03"] = "none",
                    ["FND-C04"] = "none",
                    ["FND-01"] = "none",
                    ["FND-02"] = "clear_oral",
                    ["FND-03"] = "stopped",
                    ["FND-04"] = "verbal",
                    ["FND-05"] = "not_discussed",
                    ["FND-05A"] = "none",
                    ["FND-06"] = "none",
                    ["FND-06A"] = "broad_unanimity",
                    ["FND-07"] = "none",
                    ["FND-08"] = "none",
                    ["FND-09"] = "none",
                    ["FND-10"] = "none",
                    ["FND-11"] = "aligned",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz",
                    ["COR-01"] = "match",
                    ["COR-02"] = "fragmented",
                    ["COR-03"] = "none",
                    ["COR-04"] = "none",
                    ["IP-01"] = "ready",
                    ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design" }),
                    ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders" }),
                    ["IP-04"] = "some",
                    ["IP-05"] = "agreed",
                    ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
                    ["PROD-01"] = "first",
                    ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
                    ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["DATA-01"] = "yes",
                    ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact" }),
                    ["AI-01"] = "yes",
                    ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
                    ["INVEST-01"] = "none"
                }
            },
            new
            {
                id = "preset_solo_ai",
                title = "Solo-Founder B2C SaaS без юрлица с AI",
                description = "1 основатель 100%, активные продажи через карты физлиц, сбор персональных данных и интеграция с OpenAI API.",
                badge = "🟠 Solo Pre-Entity",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "solo",
                    ["COR-C01"] = "none",
                    ["IP-01"] = "ready",
                    ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design", "app" }),
                    ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders" }),
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
                    ["PROD-01"] = "regular",
                    ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "consumers" }),
                    ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "app", "website" }),
                    ["PROD-04"] = "template",
                    ["PROD-05"] = "template_unchecked",
                    ["PROD-06"] = "mostly",
                    ["PROD-10"] = "subscription",
                    ["DATA-01"] = "yes",
                    ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact", "account", "payment" }),
                    ["DATA-03"] = "no",
                    ["DATA-04"] = JsonSerializer.SerializeToElement(new[] { "user" }),
                    ["DATA-05"] = "none",
                    ["DATA-06"] = "preparing",
                    ["AI-01"] = "yes",
                    ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
                    ["INVEST-01"] = "none"
                }
            },
            new
            {
                id = "preset_seed_ready",
                title = "Группа компаний (МФЦА Холдинг + ТОО в РК, Вестинг)",
                description = "3 основателя с вестингом, холдинг в МФЦА (AIFC), операционная компания в РК, оформленный Cap Table, полная защита IP.",
                badge = "🟢 МФЦА + ТОО (Зрелая)",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "3",
                    ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 30, ["founder_3"] = 20 }),
                    ["FND-C03"] = "none",
                    ["FND-C04"] = "signed",
                    ["FND-01"] = "none",
                    ["FND-02"] = "written",
                    ["FND-03"] = "aligned",
                    ["FND-04"] = "registered",
                    ["FND-05"] = "vesting",
                    ["FND-05A"] = "defined",
                    ["FND-06"] = "written",
                    ["FND-06A"] = "different_thresholds",
                    ["FND-07"] = "full",
                    ["FND-08"] = "full",
                    ["FND-09"] = "documented",
                    ["FND-10"] = "none",
                    ["FND-11"] = "aligned",
                    ["COR-C01"] = "multiple",
                    ["COR-C02A"] = "aifc",
                    ["COR-C02B"] = "2",
                    ["COR-C02C"] = JsonSerializer.SerializeToElement(new[]
                    {
                        new Dictionary<string, object> { ["index"] = 2, ["jurisdiction"] = "kz", ["roles"] = new[] { "clients", "payments" } }
                    }),
                    ["COR-01"] = "match",
                    ["COR-02"] = "complete",
                    ["COR-03"] = "documented_included",
                    ["COR-04"] = "complete",
                    ["COR-04A"] = "yes",
                    ["COR-05"] = "systematic",
                    ["COR-06"] = "clear_limits",
                    ["COR-07_GROUP"] = "aligned",
                    ["COR-08"] = "organized",
                    ["COR-T01"] = "none",
                    ["IP-01"] = "ready",
                    ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "design", "brand" }),
                    ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "founders", "employees" }),
                    ["IP-04"] = "all",
                    ["IP-05"] = "assigned",
                    ["IP-06"] = "all",
                    ["IP-10"] = "no",
                    ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "employees" }),
                    ["TEAM-02"] = "6_10",
                    ["TEAM-03"] = "all",
                    ["TEAM-06"] = "clear",
                    ["PROD-01"] = "regular",
                    ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
                    ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
                    ["PROD-04"] = "current",
                    ["PROD-05"] = "yes",
                    ["PROD-06"] = "clear",
                    ["PROD-10"] = "subscription",
                    ["PROD-14"] = "self_service",
                    ["DATA-01"] = "yes",
                    ["DATA-02"] = JsonSerializer.SerializeToElement(new[] { "contact", "account" }),
                    ["DATA-03"] = "yes",
                    ["DATA-04"] = JsonSerializer.SerializeToElement(new[] { "company" }),
                    ["DATA-05"] = "yes",
                    ["DATA-06"] = "organized",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "clients" }),
                    ["CONTRACT-02"] = "written",
                    ["CONTRACT-03"] = "standard_rules",
                    ["CONTRACT-05"] = "balanced",
                    ["CONTRACT-06"] = "standard_contract",
                    ["CONTRACT-07"] = "always_controlled",
                    ["CONTRACT-08"] = "none",
                    ["INVEST-01"] = "searching",
                    ["INVEST-02"] = "yes",
                    ["INVEST-03"] = "modeled",
                    ["INVEST-04"] = "prepared",
                    ["INVEST-05"] = "documented",
                    ["INVEST-06"] = "yes",
                    ["INVEST-07"] = "verified",
                    ["INVEST-08"] = "prepared",
                    ["INVEST-09"] = "understood",
                    ["INVEST-10"] = "lawyer",
                    ["INVEST-11"] = "full"
                }
            },
            new
            {
                id = "preset_ip_dispute",
                title = "Критический IP-разрыв (Подрядчики без договоров)",
                description = "Код написан внешними фрилансерами без актов отчуждения прав, с ушедшим разработчиком возник спор, риск срыва раунда.",
                badge = "🔴 IP Blocker",
                answers = new Dictionary<string, object>
                {
                    ["FND-C01"] = "2",
                    ["FND-C02"] = JsonSerializer.SerializeToElement(new Dictionary<string, object> { ["founder_1"] = 70, ["founder_2"] = 30 }),
                    ["FND-C03"] = "none",
                    ["FND-C04"] = "none",
                    ["COR-C01"] = "one",
                    ["COR-C02A"] = "kz",
                    ["IP-01"] = "ready",
                    ["IP-02"] = JsonSerializer.SerializeToElement(new[] { "code", "database", "design" }),
                    ["IP-03"] = JsonSerializer.SerializeToElement(new[] { "contractors", "former" }),
                    ["IP-04"] = "none",
                    ["IP-05"] = "agreed",
                    ["IP-07"] = "missing_all",
                    ["IP-08"] = "dispute",
                    ["IP-10"] = "not_reviewed",
                    ["TEAM-01"] = JsonSerializer.SerializeToElement(new[] { "freelancers", "external_devs" }),
                    ["TEAM-02"] = "1_2",
                    ["TEAM-03"] = "many_missing",
                    ["PROD-01"] = "first",
                    ["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies" }),
                    ["PROD-03"] = JsonSerializer.SerializeToElement(new[] { "website" }),
                    ["DATA-01"] = "no",
                    ["AI-01"] = "no",
                    ["CONTRACT-01"] = JsonSerializer.SerializeToElement(new[] { "none" }),
                    ["INVEST-01"] = "searching",
                    ["INVEST-02"] = "no",
                    ["INVEST-04"] = "none",
                    ["INVEST-08"] = "none"
                }
            }
        };

        // Fetch latest real user sessions that have answers
        var liveLeads = _leads.ListLeads()
            .Where(l => l.SessionAnswers != null && !string.IsNullOrWhiteSpace((string)l.SessionAnswers) && ((string)l.SessionAnswers).Length > 2)
            .Take(25).Select(l =>
        {
            Dictionary<string, object> parsedAnswers;
            try
            {
                parsedAnswers = JsonSerializer.Deserialize<Dictionary<string, object>>((string)l.SessionAnswers) ?? new();
            }
            catch
            {
                parsedAnswers = new();
            }

            string rawId = (string)l.Id;
            string cleanId = rawId.StartsWith("session_") ? rawId.Substring(8) : rawId;
            string shortId = cleanId.Substring(0, Math.Min(8, cleanId.Length));

            string companyStr = (string)(l.Company ?? "");
            string nameStr = (string)(l.Name ?? "");

            string title = !string.IsNullOrEmpty(companyStr)
                ? companyStr
                : (!string.IsNullOrEmpty(nameStr) && !nameStr.StartsWith("Сессия"))
                    ? nameStr
                    : $"Сессия {shortId}";

            return new
            {
                id = rawId,
                title = $"{title} ({parsedAnswers.Count} ответов)",
                description = $"Ответов: {parsedAnswers.Count} | Создан: {l.CreatedAt} | Heat: {l.HeatLabel}",
                badge = "👤 Пользователь",
                answers = parsedAnswers
            };
        }).Where(l => l.answers.Count > 0).ToList();

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
                    try
                    {
                        if (val is JsonElement je)
                        {
                            if (je.ValueKind == JsonValueKind.Array)
                            {
                                var ids = je.EnumerateArray()
                                    .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText())
                                    .Where(x => !string.IsNullOrEmpty(x))
                                    .ToList();
                                var labels = ids.Select(id => q.Options?.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Label ?? id).ToList();
                                answerText = labels.Count > 0 ? string.Join(", ", labels) : "—";
                            }
                            else if (je.ValueKind == JsonValueKind.Object)
                            {
                                answerText = je.GetRawText();
                            }
                            else if (je.ValueKind == JsonValueKind.String)
                            {
                                string str = je.GetString() ?? "";
                                var opt = q.Options?.FirstOrDefault(o => o.Id.Equals(str, StringComparison.OrdinalIgnoreCase));
                                answerText = opt != null ? opt.Label : str;
                            }
                            else
                            {
                                answerText = je.GetRawText();
                            }
                        }
                        else
                        {
                            string rawStr = val.ToString()!;
                            var opt = q.Options?.FirstOrDefault(o => o.Id.Equals(rawStr, StringComparison.OrdinalIgnoreCase));
                            answerText = opt != null ? opt.Label : rawStr;
                        }
                    }
                    catch
                    {
                        answerText = val.ToString() ?? "—";
                    }
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
        var answers = new Dictionary<string, object>();

        if (body.TryGetProperty("answers", out var aProp) && aProp.ValueKind == JsonValueKind.Object)
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, object>>(aProp.GetRawText()) ?? new();
        }

        var result = _scoringEngine.ComputeResult(answers);
        var facts = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answers);
        var reportCtx = FenixLegalOs.Scoring.Report.ReportEngine.AssembleReportContext(result, facts, "admin-test", "Стартап");
        
        // Build the exact JSON input payload we send to LLM
        var inputPayload = new
        {
            projectProfile = new
            {
                projectName = reportCtx.ProjectName,
                keyFacts = reportCtx.Profile.KeyFacts.Select(f => new { f.Label, f.Value }),
                baselineNarrative = reportCtx.Profile.ConfigurationNarrative
            },
            overall = new
            {
                score = reportCtx.Overall.Score,
                band = reportCtx.Overall.Band,
                confidence = reportCtx.Overall.Confidence,
                topDrivers = reportCtx.Overall.TopDrivers
            },
            focusModules = reportCtx.FocusModules.Select(m => new
            {
                sectionId = m.SectionId,
                title = m.Title,
                score = m.Score,
                band = m.ScoreBand,
                maxSeverity = m.MaxSeverity.ToString(),
                findings = m.Findings.Select(f => new
                {
                    findingCode = f.FindingCode,
                    title = f.Title,
                    severity = f.Severity.ToString(),
                    whyFound = f.WhyFound,
                    whyItMatters = f.WhyItMatters,
                    recommendation = f.Recommendation
                })
            }),
            topFindings = reportCtx.TopFindings.Select(t => new
            {
                findingCode = t.FindingCode,
                title = t.Title,
                severity = t.Severity.ToString(),
                shortSummary = t.ShortSummary
            }),
            actionPlan = reportCtx.ActionPlan.Select(a => new
            {
                number = a.Number,
                title = a.Title,
                priorityGroup = a.PriorityGroup
            }),
            fenixLaw = new
            {
                requiresLegalWork = reportCtx.FenixLaw.RequiresLegalWork,
                serviceAreas = reportCtx.FenixLaw.ServiceCards.Select(s => s.Title)
            }
        };

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var narratives = await _aiReportService.GenerateReportNarrativesAsync(reportCtx);
        sw.Stop();

        return Ok(new
        {
            memo = narratives.ExecutiveConclusion,
            narratives,
            inputPayload,
            durationMs = sw.ElapsedMilliseconds,
            model = "gpt-5.6-sol"
        });
    }

    [HttpPost("testbench/generate-pdf")]
    public async Task<IActionResult> GenerateTestBenchPdf([FromBody] JsonElement body)
    {
        if (!IsAdmin()) return Unauthorized();
        var answers = new Dictionary<string, object>();

        if (body.TryGetProperty("answers", out var aProp) && aProp.ValueKind == JsonValueKind.Object)
        {
            answers = JsonSerializer.Deserialize<Dictionary<string, object>>(aProp.GetRawText()) ?? new();
        }

        string projectName = body.TryGetProperty("projectName", out var pProp) ? pProp.GetString() ?? "Стартап" : "Стартап";
        string sessionId = "admin_tb_" + Guid.NewGuid().ToString("N")[..8];

        var result = _scoringEngine.ComputeResult(answers);
        var facts = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answers);

        // Optional custom/cached narratives if provided
        FenixLegalOs.Models.Report.ReportNarrativesDto? rawNarratives = null;
        if (body.TryGetProperty("narratives", out var nProp) && nProp.ValueKind == JsonValueKind.Object)
        {
            rawNarratives = JsonSerializer.Deserialize<FenixLegalOs.Models.Report.ReportNarrativesDto>(nProp.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }

        var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, sessionId, projectName, rawNarratives: rawNarratives);
        if (pdfBytes == null) return Problem("PDF generation failed");

        return File(pdfBytes, "application/pdf", $"Fenix_SLS_Report_{projectName}_{sessionId}.pdf");
    }

    [HttpGet("leads/{id}/pdf")]
    public async Task<IActionResult> GetLeadPdf(string id)
    {
        if (!IsAdmin()) return Unauthorized();
        var lead = _leads.GetLead(id);
        if (lead == null) return NotFound(new { error = "lead_not_found" });

        var answersDict = lead.SessionAnswers != null
            ? JsonSerializer.Deserialize<Dictionary<string, object>>((string)lead.SessionAnswers) ?? new()
            : new();

        var result = answersDict.Count > 0
            ? _scoringEngine.ComputeResult(answersDict)
            : (lead.SessionResult != null ? JsonSerializer.Deserialize<ScoreResult>((string)lead.SessionResult) : null);

        if (result == null) return NotFound(new { error = "invalid_result" });

        var facts = FenixLegalOs.Scoring.Core.FactNormalizer.NormalizeFacts(answersDict);
        string companyName = !string.IsNullOrWhiteSpace((string)lead.Company) ? (string)lead.Company : "Стартап";

        var pdfBytes = await _pdfService.GeneratePdfAsync(result, facts, lead.SessionId ?? id, companyName);
        if (pdfBytes == null) return Problem("PDF generation failed");

        return File(pdfBytes, "application/pdf", $"Fenix_SLS_Report_{lead.SessionId ?? id}.pdf");
    }

    // ─── Risk Management Endpoints ─────────────────────────────────────────

    [HttpGet("risks")]
    public IActionResult GetRisks([FromQuery] string? sectionId, [FromQuery] string? severity, [FromQuery] string? priority, [FromQuery] string? search)
    {
        if (!IsAdmin()) return Unauthorized();

        var list = _riskRepo.GetAllRisks(sectionId, severity, priority, search);
        var all = _riskRepo.GetAllRisks();

        var stats = new
        {
            total = all.Count,
            blockers = all.Count(r => r.Severity == Models.Enums.RiskSeverity.Blocker),
            critical = all.Count(r => r.Severity == Models.Enums.RiskSeverity.Critical),
            high = all.Count(r => r.Severity == Models.Enums.RiskSeverity.High),
            medium = all.Count(r => r.Severity == Models.Enums.RiskSeverity.Medium),
            info = all.Count(r => r.Severity == Models.Enums.RiskSeverity.Info),
            now = all.Count(r => r.Priority == Models.Enums.RiskPriority.Now),
            thirtyDays = all.Count(r => r.Priority == Models.Enums.RiskPriority.ThirtyDays),
            beforeRound = all.Count(r => r.Priority == Models.Enums.RiskPriority.BeforeRound),
            later = all.Count(r => r.Priority == Models.Enums.RiskPriority.Later),
            lawyerRequired = all.Count(r => r.LawyerRequired)
        };

        return Ok(new
        {
            stats,
            risks = list
        });
    }

    [HttpGet("risks/{code}")]
    public IActionResult GetRisk(string code)
    {
        if (!IsAdmin()) return Unauthorized();

        var risk = _riskRepo.GetRiskByCode(code);
        if (risk == null) return NotFound(new { error = "risk_not_found" });

        return Ok(risk);
    }

    [HttpPut("risks/{code}")]
    public IActionResult UpdateRisk(string code, [FromBody] RiskDefinition updated)
    {
        if (!IsAdmin()) return Unauthorized();
        if (updated == null) return BadRequest(new { error = "invalid_payload" });

        updated.Code = code;
        bool ok = _riskRepo.UpdateRisk(updated);
        if (!ok) return NotFound(new { error = "update_failed" });

        _leads.AuditLog("admin", "update_risk", code);
        return Ok(new { ok = true, risk = updated });
    }

    [HttpPost("risks/reset")]
    public IActionResult ResetRisks()
    {
        if (!IsAdmin()) return Unauthorized();

        _riskRepo.ResetToDefaults();
        _leads.AuditLog("admin", "reset_risks", "all");

        return Ok(new { ok = true, message = "Каталог рисков успешно сброшен к эталонным настройкам DataBank" });
    }
}
