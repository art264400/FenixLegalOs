using System.Text;
using System.Text.Json;
using Dapper;
using FenixLegalOs.Controllers;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Report;
using FenixLegalOs.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FenixLegalOs.Tests;

public sealed class ReportRevisionTests : IDisposable
{
    private readonly string _dbPath;
    private readonly DbInitializer _db;
    private readonly QuestionRepository _bank;
    private readonly SessionRepository _sessions;
    private readonly ReportRevisionRepository _revisions;
    private readonly LeadRepository _leads;
    private readonly UserRepository _users;
    private readonly ScoringEngine _scoring;
    private readonly FakePdf _pdf = new();
    private readonly ReportRevisionService _service;
    private readonly IConfiguration _config;
    private readonly byte[] _originalPdf = Encoding.ASCII.GetBytes("%PDF-1.7\noriginal\n%%EOF");

    public ReportRevisionTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"fenix_revisions_{Guid.NewGuid():N}.db");
        _config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FENIX_DB_PATH"] = _dbPath }).Build();
        _db = new DbInitializer(_config);
        _db.Initialize();
        _bank = new QuestionRepository(_db);
        _sessions = new SessionRepository(_db);
        _revisions = new ReportRevisionRepository(_db);
        _leads = new LeadRepository(_db);
        _users = new UserRepository(_db);
        _scoring = new ScoringEngine(_bank);
        _service = new ReportRevisionService(_sessions, _revisions, _bank, _scoring, _pdf, _leads, _users);
    }

    private sealed class FakePdf : IReportRevisionPdfGenerator
    {
        public int Calls;
        public SharedFactStore? LastFacts;
        public ScoreResult? LastResult;
        public string? LastCompanyName;
        public bool Fail;
        public TaskCompletionSource<bool>? Started;
        public TaskCompletionSource<bool>? Continue;
        public async Task<byte[]?> GenerateAsync(ScoreResult result, SharedFactStore facts, string sessionId, string companyName)
        {
            Calls++;
            LastFacts = facts;
            LastResult = result;
            LastCompanyName = companyName;
            Started?.TrySetResult(true);
            if (Continue != null) await Continue.Task;
            if (Fail) return null;
            return Encoding.ASCII.GetBytes($"%PDF-1.7\nrevision-{Calls}-{result.Overall}\n%%EOF");
        }
    }

    private Dictionary<string, object> CompleteAnswers(bool prelaunch = false)
    {
        var known = _bank.GetQuestions().Select(q => q.Id).ToHashSet();
        var answers = ScenarioArtifactsGenerator.CreateHealthyPreset().Where(kv => known.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (prelaunch) answers["PROD-01"] = "prelaunch";
        return FillAll(answers);
    }

    private Dictionary<string, object> FillAll(Dictionary<string, object> answers)
    {
        for (int iteration = 0; iteration < 20; iteration++)
        {
            var (visible, effective, _) = ScoringEngine.ResolveEffectiveState(_bank.GetQuestions(), answers);
            answers = effective;
            var validation = FenixLegalOs.Scoring.Validation.AnswerValidator.Validate(answers, _bank.GetQuestions());
            foreach (var error in validation.Errors) answers.Remove(error.QuestionId);
            var missing = visible.Where(q => !answers.ContainsKey(q.Id)).ToList();
            if (missing.Count == 0) return answers;
            foreach (var q in missing)
            {
                var option = q.Options?.OrderByDescending(o => o.Score ?? 0).FirstOrDefault()?.Id ?? "unknown";
                answers[q.Id] = q.Type switch
                {
                    QuestionType.Multiple => JsonSerializer.SerializeToElement(new[] { option }),
                    QuestionType.EquityInputs => JsonSerializer.SerializeToElement(new Dictionary<string, double> { ["founder_1"] = 50, ["founder_2"] = 30, ["founder_3"] = 20 }),
                    QuestionType.EntityBuilder => JsonSerializer.SerializeToElement(new[] { new { index = 2, jurisdiction = "kz", roles = new[] { "clients" } } }),
                    _ => option
                };
            }
        }
        throw new InvalidOperationException("Fixture questionnaire did not converge.");
    }

    private string CreateCompleted(bool prelaunch = false)
    {
        var id = _sessions.CreateSession();
        var answers = CompleteAnswers(prelaunch);
        Assert.True(_sessions.CompleteSession(id, JsonSerializer.Serialize(answers), _scoring.ComputeResult(answers)));
        Assert.True(_sessions.SavePdf(id, _originalPdf));
        _sessions.MarkSessionPaid(id, 49990, "test");
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("UPDATE sessions SET user_id='owner-test',terms_accepted=1 WHERE id=@id", new { id });
        return id;
    }

    private ReportRevision SaveDraft(string id, Dictionary<string, object>? answers = null)
    {
        var session = _sessions.GetSession(id)!;
        answers ??= JsonSerializer.Deserialize<Dictionary<string, object>>(session.AnswersJson)!;
        _service.Save(id, new AnswerEditorRequest { Answers = answers, BaseRevisionId = session.ReportRevisionId, Reason = "Исправление по данным клиента" }, "admin:test");
        return _revisions.GetDraft(id)!;
    }

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

    [Fact]
    public async Task CorrectedEquity_ReachesPdfGeneratorAndAllProfileDescriptions()
    {
        var id = _sessions.CreateSession();
        var original = CompleteAnswers();
        original["FND-C01"] = "2";
        original["FND-C02"] = JsonSerializer.SerializeToElement(new { founder_1 = 50, founder_2 = 50 });
        original = FillAll(original);
        Assert.True(_sessions.CompleteSession(id, JsonSerializer.Serialize(original), _scoring.ComputeResult(original)));
        var corrected = JsonSerializer.Deserialize<Dictionary<string, object>>(_sessions.GetSession(id)!.AnswersJson)!;
        corrected["FND-C02"] = JsonSerializer.SerializeToElement(new { founder_1 = 60, founder_2 = 40 });
        var draft = SaveDraft(id, FillAll(corrected));
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        Assert.Equal(new[] { 60d, 40d }, Assert.IsType<List<double>>(_pdf.LastFacts!.Facts["founders.equityShares"]));
        var report = ReportEngine.AssembleReportContext(_pdf.LastResult!, _pdf.LastFacts!, id, "Компания с исправленными долями");
        Assert.Equal("60 / 40", report.Profile.KeyFacts.First(f => f.Key == "equity").Value);
        var foundersBlock = report.Profile.ConfigurationBlocks.First(f => f.Label == "Основатели и управление").Value;
        Assert.Contains("60 / 40", foundersBlock);
        Assert.DoesNotContain("не указано", foundersBlock, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Распределение долей: 60 / 40", report.Profile.ConfigurationNarrative);
        Assert.DoesNotContain("Распределены", report.Profile.ConfigurationNarrative);
    }

    [Fact]
    public async Task CorrectedStructureAndAudience_ReachesPdfProfileWithoutMutatingFacts()
    {
        var id = _sessions.CreateSession();
        var original = CompleteAnswers();
        original["COR-C01"] = "multiple";
        original["COR-C02A"] = "kz";
        original["COR-C02B"] = "2";
        original["COR-C02C"] = JsonSerializer.SerializeToElement(new[] { new { index = 2, jurisdiction = "uk", roles = new[] { "clients" } } });
        original["PROD-01"] = "regular";
        original["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "consumers" });
        original = FillAll(original);
        Assert.True(_sessions.CompleteSession(id, JsonSerializer.Serialize(original), _scoring.ComputeResult(original)));
        var corrected = JsonSerializer.Deserialize<Dictionary<string, object>>(_sessions.GetSession(id)!.AnswersJson)!;
        corrected["COR-C02C"] = JsonSerializer.SerializeToElement(new[] { new { index = 2, jurisdiction = "us", roles = new[] { "ip_assets", "hiring" } } });
        corrected["PROD-02"] = JsonSerializer.SerializeToElement(new[] { "companies", "client_employees" });
        var draft = SaveDraft(id, FillAll(corrected));
        await _service.RecalculateAsync(id, draft.Id, draft.Version);

        Assert.True(Assert.IsType<bool>(_pdf.LastFacts!.Facts["company.groupStructure"]));
        Assert.Equal(2, Assert.IsType<int>(_pdf.LastFacts.Facts["company.entityCount"]));
        var jurisdictions = Assert.IsType<List<string>>(_pdf.LastFacts.Facts["company.jurisdictions"]);
        Assert.Equal(new[] { "kz", "us" }, jurisdictions);
        Assert.Equal(new[] { "companies", "client_employees" }, Assert.IsType<List<string>>(_pdf.LastFacts.Facts["product.userTypes"]));
        var factsBeforeProjection = JsonSerializer.Serialize(_pdf.LastFacts.Facts);
        var report = ReportEngine.AssembleReportContext(_pdf.LastResult!, _pdf.LastFacts, id, "Две компании");
        Assert.Equal("Группа компаний (2 юрлица)", report.Profile.KeyFacts.First(f => f.Key == "entity").Value);
        var jurisdictionFact = report.Profile.KeyFacts.First(f => f.Key == "jurisdiction").Value;
        Assert.Contains("Казахстан", jurisdictionFact);
        Assert.Contains("США", jurisdictionFact);
        Assert.DoesNotContain("Великобритания", jurisdictionFact);
        var companyBlock = report.Profile.ConfigurationBlocks.First(f => f.Label == "Компания и юрисдикция").Value;
        Assert.Contains("Группа компаний (2 юрлица)", companyBlock);
        Assert.Contains("Компания 2 (США", companyBlock);
        Assert.Contains("владение продуктом или важными активами", companyBlock);
        Assert.Contains("найм команды и разработчиков", companyBlock);
        Assert.DoesNotContain("ip_assets", companyBlock);
        Assert.DoesNotContain("hiring", companyBlock);
        Assert.DoesNotContain("одно юрлицо", companyBlock);
        Assert.Contains("Компания 2 (США", report.Profile.ConfigurationNarrative);
        var productBlock = report.Profile.ConfigurationBlocks.First(f => f.Label == "Продукт и стадия").Value;
        Assert.Contains("Аудитория: Компании, Сотрудники компаний-клиентов", productBlock);
        Assert.DoesNotContain("Аудитория: Не указана", productBlock);
        Assert.Equal("Компании, Сотрудники компаний-клиентов", report.Profile.KeyFacts.First(f => f.Key == "users").Value);
        Assert.Equal(factsBeforeProjection, JsonSerializer.Serialize(_pdf.LastFacts.Facts));
    }

    [Theory]
    [InlineData("holding", "холдинг")]
    [InlineData("clients", "работа с клиентами / заключение договоров")]
    [InlineData("payments", "получение платежей и выручки")]
    [InlineData("ip_assets", "владение продуктом или важными активами")]
    [InlineData("hiring", "найм команды и разработчиков")]
    [InlineData("other", "другое")]
    [InlineData("unknown", "назначение не уточнено")]
    public void CurrentEntityRoles_AreHumanReadableInReport(string role, string label)
    {
        var facts = new SharedFactStore();
        var answers = new Dictionary<string, object>
        {
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "kz",
            ["COR-C02B"] = "2",
            ["COR-C02C"] = JsonSerializer.SerializeToElement(new[] { new { index = 2, jurisdiction = "us", roles = new[] { role } } })
        };
        new FenixLegalOs.Scoring.Modules.Corporate.CorporateFactNormalizer().Normalize(answers, facts);
        var profile = ProjectProfileExtractor.ExtractProfile(facts);
        var companyBlock = profile.ConfigurationBlocks.First(f => f.Label == "Компания и юрисдикция").Value;
        Assert.Contains(label, companyBlock);
        Assert.Contains(label, profile.ConfigurationNarrative);
        Assert.DoesNotContain(role, companyBlock);
        Assert.DoesNotContain(role, profile.ConfigurationNarrative);
    }

    [Fact]
    public void SingleStringFacts_AreDisplayedAsWholeValues()
    {
        var facts = new SharedFactStore();
        facts.Facts["company.jurisdictions"] = "kz";
        facts.Facts["product.userTypes"] = "companies";
        var profile = ProjectProfileExtractor.ExtractProfile(facts);
        Assert.Equal("Казахстан", profile.KeyFacts.First(f => f.Key == "jurisdiction").Value);
        Assert.Equal("Компании", profile.KeyFacts.First(f => f.Key == "users").Value);
    }

    [Fact]
    public void MissingEquity_IsConsistentlyMarkedAsUnknownInProfile()
    {
        var facts = new SharedFactStore();
        facts.Facts["founders.count"] = 2;
        var profile = ProjectProfileExtractor.ExtractProfile(facts);
        Assert.Equal("Не указано", profile.KeyFacts.First(f => f.Key == "equity").Value);
        Assert.Contains("Не указано", profile.ConfigurationBlocks.First(f => f.Label == "Основатели и управление").Value);
        Assert.Contains("Распределение долей: Не указано", profile.ConfigurationNarrative);
        Assert.DoesNotContain("Распределены", profile.ConfigurationNarrative);
    }

    [Fact]
    public void PreviewAndSave_RemoveHiddenAnswersWithoutChangingPublishedReport()
    {
        var id = CreateCompleted();
        var original = _sessions.GetOriginalSession(id)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, object>>(original.AnswersJson)!;
        answers["PROD-01"] = "prelaunch";
        var request = new AnswerEditorRequest { Answers = answers, Reason = "Продукт ещё не запущен" };
        var preview = Json(_service.Preview(id, request));
        Assert.False(preview.GetProperty("answers").TryGetProperty("PROD-02", out _));
        Assert.Null(_revisions.GetDraft(id));
        var draft = SaveDraft(id, answers);
        Assert.DoesNotContain("PROD-02", JsonSerializer.Deserialize<Dictionary<string, object>>(draft.AnswersJson)!.Keys);
        Assert.Equal(original.AnswersJson, _sessions.GetSession(id)!.AnswersJson);
        Assert.Equal(_originalPdf, _sessions.GetPdf(id));
        Assert.Equal(0, _pdf.Calls);
    }

    [Fact]
    public async Task NewlyVisibleQuestions_BlockRecalculationUntilAnswered()
    {
        var id = CreateCompleted(prelaunch: true);
        var answers = JsonSerializer.Deserialize<Dictionary<string, object>>(_sessions.GetSession(id)!.AnswersJson)!;
        answers["PROD-01"] = "first";
        var draft = SaveDraft(id, answers);
        var editor = Json(_service.GetEditor(id));
        var question = editor.GetProperty("sections").EnumerateArray().SelectMany(s => s.GetProperty("questions").EnumerateArray()).First(q => q.GetProperty("id").GetString() == "PROD-02");
        Assert.True(question.GetProperty("missing").GetBoolean());
        var ex = await Assert.ThrowsAsync<ReportRevisionException>(() => _service.RecalculateAsync(id, draft.Id, draft.Version));
        Assert.Equal("incomplete_answers", ex.Code);
        Assert.Equal(0, _pdf.Calls);
        Assert.Equal("draft", _revisions.GetDraft(id)!.Status);
    }

    [Fact]
    public async Task Publish_SwitchesAllEffectiveReadsAndKeepsSourceOwnerAndPayment()
    {
        var id = CreateCompleted();
        var original = _sessions.GetOriginalSession(id)!;
        var answers = JsonSerializer.Deserialize<Dictionary<string, object>>(original.AnswersJson)!;
        answers["PROD-04"] = "none";
        var draft = SaveDraft(id, FillAll(answers));
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        var ready = _revisions.GetDraft(id)!;
        Assert.Equal("ready", ready.Status);
        Assert.Equal(original.ResultJson, _sessions.GetSession(id)!.ResultJson);
        _service.Publish(id, ready.Id, ready.Version, "admin:reviewer");
        var published = _sessions.GetSession(id)!;
        Assert.Equal(ready.Id, published.ReportRevisionId);
        Assert.Equal(1, published.ReportRevisionNumber);
        Assert.Equal(ready.AnswersJson, published.AnswersJson);
        Assert.Equal(ready.ResultJson, published.ResultJson);
        Assert.Equal(ready.PdfBytes, _sessions.GetPdf(id));
        Assert.Equal(original.Paid, published.Paid);
        Assert.Equal(original.PaymentAmount, published.PaymentAmount);
        Assert.Equal(original.UserId, published.UserId);
        var source = _sessions.GetOriginalSession(id)!;
        Assert.Equal(original.AnswersJson, source.AnswersJson);
        Assert.Equal(original.ResultJson, source.ResultJson);
        Assert.Equal(_originalPdf, source.PdfBytes);
        Assert.False(_sessions.SaveAnswers(id, "{}", null));
        Assert.False(_sessions.CompleteSession(id, "{}", new ScoreResult()));
        Assert.Null(_revisions.GetDraft(id));
        Assert.Equal("published", _revisions.Get(id, ready.Id)!.Status);
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        Assert.Equal(1, conn.ExecuteScalar<int>("SELECT COUNT(*) FROM audit_log WHERE actor='admin:reviewer' AND action='report_revision_published'"));
    }

    [Fact]
    public async Task GenerationFailure_NeverPublishesOrOverwritesExistingPdf()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        _pdf.Fail = true;
        var ex = await Assert.ThrowsAsync<ReportRevisionException>(() => _service.RecalculateAsync(id, draft.Id, draft.Version));
        Assert.Equal("pdf_generation_failed", ex.Code);
        var failed = _revisions.GetDraft(id)!;
        Assert.Equal("failed", failed.Status);
        Assert.Null(failed.ResultJson);
        Assert.Null(failed.PdfBytes);
        Assert.Throws<ReportRevisionException>(() => _service.Publish(id, failed.Id, failed.Version, "admin:test"));
        Assert.Null(_sessions.GetSession(id)!.ReportRevisionId);
        Assert.Equal(_originalPdf, _sessions.GetPdf(id));
    }

    [Fact]
    public void ConcurrentDraftEdits_RejectStaleVersionsAndOnlyAllowOneDraft()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        var request = new AnswerEditorRequest { RevisionId = draft.Id, Version = draft.Version,
            Answers = JsonSerializer.Deserialize<Dictionary<string, object>>(draft.AnswersJson)!, Reason = "Исправленная причина" };
        _service.Save(id, request, "admin:one");
        var ex = Assert.Throws<ReportRevisionException>(() => _service.Save(id, request, "admin:two"));
        Assert.Equal("revision_conflict", ex.Code);
        Assert.Throws<ReportRevisionException>(() => SaveDraft(id));
        Assert.Single(_revisions.List(id));
    }

    [Fact]
    public async Task InFlightGeneration_BlocksEditsAndDuplicateRuns()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        _pdf.Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _pdf.Continue = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var generation = _service.RecalculateAsync(id, draft.Id, draft.Version);
        await _pdf.Started.Task;
        var generating = _revisions.GetDraft(id)!;
        Assert.Equal("generating", generating.Status);
        Assert.Throws<ReportRevisionException>(() => _service.Save(id, new AnswerEditorRequest { RevisionId = draft.Id, Version = generating.Version,
            Answers = JsonSerializer.Deserialize<Dictionary<string, object>>(draft.AnswersJson)! }, "admin:test"));
        await Assert.ThrowsAsync<ReportRevisionException>(() => _service.RecalculateAsync(id, draft.Id, generating.Version));
        Assert.Throws<ReportRevisionException>(() => _service.Discard(id, draft.Id, generating.Version, "admin:test"));
        _pdf.Continue.TrySetResult(true);
        await generation;
        Assert.Equal(1, _pdf.Calls);
    }

    [Fact]
    public void StaleGeneratorLease_CanBeRetriedAndLateResultsAreRejected()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        Assert.True(_revisions.BeginGeneration(id, draft.Id, draft.Version));
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("UPDATE report_revisions SET updated_at=@expired WHERE id=@id", new { expired = DateTime.UtcNow.AddMinutes(-21).ToString("o"), id = draft.Id });
        var recovered = _revisions.GetDraft(id)!;
        Assert.Equal("failed", recovered.Status);
        Assert.False(_revisions.FinishGeneration(id, draft.Id, draft.Version + 1, "{}", _originalPdf));
        _service.Discard(id, recovered.Id, recovered.Version, "admin:test");
        Assert.Null(_revisions.GetDraft(id));
        Assert.NotNull(SaveDraft(id));
    }

    [Fact]
    public void NullUnknownAndInvalidAnswers_AreRejectedBeforePersistence()
    {
        var id = CreateCompleted();
        Assert.Equal("invalid_answers", Assert.Throws<ReportRevisionException>(() => _service.Save(id, new AnswerEditorRequest { Answers = null! }, "admin:test")).Code);
        Assert.Equal("unknown_question", Assert.Throws<ReportRevisionException>(() => _service.Save(id, new AnswerEditorRequest { Answers = new() { ["missing-question"] = "yes" } }, "admin:test")).Code);
        Assert.Equal("validation_failed", Assert.Throws<ReportRevisionException>(() => _service.Save(id, new AnswerEditorRequest { Answers = new() { ["FND-C01"] = "fake" } }, "admin:test")).Code);
        Assert.Null(_revisions.GetDraft(id));
    }

    [Fact]
    public async Task CompanySnapshot_IsUsedForPreviewAndRegenerationInsteadOfChangingProfile()
    {
        var id = CreateCompleted();
        using (var conn = new SqliteConnection(_db.ConnectionString))
        {
            conn.Open();
            conn.Execute("UPDATE sessions SET company_name='Confirmed company' WHERE id=@id", new { id });
        }
        Assert.Equal("Confirmed company", Json(_service.GetEditor(id)).GetProperty("companyName").GetString());
        var draft = SaveDraft(id);
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        Assert.Equal("Confirmed company", _pdf.LastCompanyName);
        Assert.Equal(_originalPdf, _sessions.GetOriginalSession(id)!.PdfBytes);
    }

    [Fact]
    public async Task AllAdminEndpoints_DenyAnonymousRequests()
    {
        var id = CreateCompleted();
        var admin = new AdminSessionService(_config);
        var controller = new AdminReportRevisionsController(admin, _service, _revisions, _sessions, _leads)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<UnauthorizedObjectResult>(controller.Get(id));
        Assert.IsType<UnauthorizedObjectResult>(controller.GetByLead("session_" + id));
        Assert.IsType<UnauthorizedObjectResult>(controller.Save(id, new()));
        Assert.IsType<UnauthorizedObjectResult>(controller.Preview(id, new()));
        Assert.IsType<UnauthorizedObjectResult>(controller.Publish(id, "fake", new()));
        Assert.IsType<UnauthorizedObjectResult>(controller.Discard(id, "fake", new()));
        Assert.IsType<UnauthorizedObjectResult>(controller.Pdf(id, "original"));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Recalculate(id, "fake", new()));
    }

    [Fact]
    public async Task UserPdf_DoesNotReuseOriginalCacheAndRejectsOldExpectedRevision()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        var ready = _revisions.GetDraft(id)!;
        _service.Publish(id, ready.Id, ready.Version, "admin:test");
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("pdf_report_" + id, _originalPdf);
        var context = new DefaultHttpContext();
        context.RequestServices = new Microsoft.Extensions.DependencyInjection.ServiceCollection().AddSingleton<IMemoryCache>(cache).BuildServiceProvider();
        var ai = new AiReportService(_config);
        var env = new TestEnvironment();
        var controller = new SessionsController(_sessions, _leads, _scoring, new TypstPdfService(env, ai), ai, new SettingsRepository(_db), _bank)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        var file = Assert.IsType<FileContentResult>(await controller.DownloadPdf(id, ready.Id));
        Assert.Equal(ready.PdfBytes, file.FileContents);
        Assert.Equal(ready.Id, context.Response.Headers["X-Report-Revision-Id"].ToString());
        Assert.IsType<ConflictObjectResult>(await controller.DownloadPdf(id, "original"));
        var result = Json(Assert.IsType<OkObjectResult>(controller.GetResult(id)).Value!);
        Assert.Equal(ready.Id, result.GetProperty("reportRevisionId").GetString());
        var responseAnswers = Json(Assert.IsType<OkObjectResult>(controller.GetAnswers(id)).Value!);
        Assert.Equal(ready.Id, responseAnswers.GetProperty("reportRevisionId").GetString());
    }

    [Fact]
    public async Task SecondPublication_UsesPublishedAnswersAsBaseAndKeepsBothHistoricalPdfs()
    {
        var id = CreateCompleted();
        var original = _sessions.GetOriginalSession(id)!;
        var first = SaveDraft(id);
        await _service.RecalculateAsync(id, first.Id, first.Version);
        var firstReady = _revisions.GetDraft(id)!;
        _service.Publish(id, firstReady.Id, firstReady.Version, "admin:first");
        var answers = JsonSerializer.Deserialize<Dictionary<string, object>>(_sessions.GetSession(id)!.AnswersJson)!;
        answers["INVEST-01"] = "3_6";
        var second = SaveDraft(id, FillAll(answers));
        Assert.Equal(first.Id, second.BaseRevisionId);
        Assert.Equal(firstReady.AnswersJson, second.BaseAnswersJson);
        await _service.RecalculateAsync(id, second.Id, second.Version);
        var secondReady = _revisions.GetDraft(id)!;
        _service.Publish(id, secondReady.Id, secondReady.Version, "admin:second");
        Assert.Equal("superseded", _revisions.Get(id, first.Id)!.Status);
        Assert.Equal(firstReady.PdfBytes, _revisions.Get(id, first.Id)!.PdfBytes);
        Assert.Equal(secondReady.PdfBytes, _sessions.GetPdf(id));
        Assert.Equal(2, _sessions.GetSession(id)!.ReportRevisionNumber);
        Assert.Equal(original.AnswersJson, _sessions.GetOriginalSession(id)!.AnswersJson);
        Assert.Equal(_originalPdf, _sessions.GetOriginalSession(id)!.PdfBytes);
        var history = Json(_service.GetEditor(id)).GetProperty("revisions");
        Assert.Equal(3, history.GetArrayLength());
        Assert.Throws<ReportRevisionException>(() => _service.Publish(id, first.Id, firstReady.Version, "admin:late"));
    }

    [Fact]
    public async Task BankChange_BlocksStaleDraftAndDiscardAllowsFreshSnapshot()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("UPDATE questions SET question=question || ' (уточнено)' WHERE id='FND-C01'");
        var ex = await Assert.ThrowsAsync<ReportRevisionException>(() => _service.RecalculateAsync(id, draft.Id, draft.Version));
        Assert.Equal("question_bank_changed", ex.Code);
        _service.Discard(id, draft.Id, draft.Version, "admin:test");
        var fresh = SaveDraft(id);
        Assert.NotEqual(draft.BankFingerprint, fresh.BankFingerprint);
        Assert.Contains("уточнено", JsonSerializer.Deserialize<List<DiagnosticQuestion>>(fresh.QuestionsJson)!.First(q => q.Id == "FND-C01").Question);
    }

    [Fact]
    public async Task PublishWithoutPersistedPdf_IsRejectedEvenWhenStatusSaysReady()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        var ready = _revisions.GetDraft(id)!;
        using var conn = new SqliteConnection(_db.ConnectionString);
        conn.Open();
        conn.Execute("UPDATE report_revisions SET pdf_bytes=NULL WHERE id=@id", new { id = ready.Id });
        Assert.Throws<ReportRevisionException>(() => _service.Publish(id, ready.Id, ready.Version, "admin:test"));
        Assert.Null(_sessions.GetSession(id)!.ReportRevisionId);
        Assert.Equal(_originalPdf, _sessions.GetPdf(id));
    }

    [Fact]
    public async Task ExistingAdminPdfDownload_ReturnsPublishedPersistedFile()
    {
        var id = CreateCompleted();
        var draft = SaveDraft(id);
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        var ready = _revisions.GetDraft(id)!;
        _service.Publish(id, ready.Id, ready.Version, "admin:test");
        var context = new DefaultHttpContext();
        var token = Guid.NewGuid().ToString("N");
        AdminSessionService.AddTestToken(token);
        context.Request.Headers.Cookie = $"{AdminSessionService.CookieName}={token}";
        var ai = new AiReportService(_config);
        var controller = new AdminController(_leads, _bank, new RiskRepository(_db), _scoring, ai, new SettingsRepository(_db),
            new TypstPdfService(new TestEnvironment(), ai), new AdminSessionService(_config), _sessions)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        var file = Assert.IsType<FileContentResult>(await controller.GetLeadPdf("session_" + id));
        Assert.Equal(ready.PdfBytes, file.FileContents);
        Assert.Equal(ready.Id, context.Response.Headers["X-Report-Revision-Id"].ToString());
        Assert.Equal(_originalPdf, _sessions.GetOriginalSession(id)!.PdfBytes);
        AdminSessionService.ActiveTokens.TryRemove(token, out _);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicationAfterAccessFilter_NeverLabelsNewPdfAsOriginal(bool cached)
    {
        var id = CreateCompleted();
        var captured = _sessions.GetSession(id)!;
        captured.PdfBytes = null; // access filter ran before the initial file was persisted
        var draft = SaveDraft(id);
        await _service.RecalculateAsync(id, draft.Id, draft.Version);
        var ready = _revisions.GetDraft(id)!;
        _service.Publish(id, ready.Id, ready.Version, "admin:test");
        var cache = new MemoryCache(new MemoryCacheOptions());
        if (cached) cache.Set("pdf_report_" + id, _originalPdf);
        var context = new DefaultHttpContext();
        context.Items["DiagnosticSession"] = captured;
        context.RequestServices = new ServiceCollection().AddSingleton<IMemoryCache>(cache).BuildServiceProvider();
        var ai = new AiReportService(_config);
        var controller = new SessionsController(_sessions, _leads, _scoring, new TypstPdfService(new TestEnvironment(), ai), ai, new SettingsRepository(_db), _bank)
        { ControllerContext = new ControllerContext { HttpContext = context } };
        Assert.IsType<ConflictObjectResult>(await controller.DownloadPdf(id, "original"));
        var file = Assert.IsType<FileContentResult>(await controller.DownloadPdf(id));
        Assert.Equal(ready.PdfBytes, file.FileContents);
        Assert.Equal(ready.Id, context.Response.Headers["X-Report-Revision-Id"].ToString());
        if (cached) Assert.Equal(_originalPdf, cache.Get<byte[]>("pdf_report_" + id));
        else Assert.False(cache.TryGetValue("pdf_report_" + id, out _));
        Assert.Equal(ready.PdfBytes, cache.Get<byte[]>($"pdf_report_{id}_{ready.Id}"));
    }

    private sealed class TestEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "FenixLegalOs";
        public string WebRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            if (File.Exists(path)) File.Delete(path);
    }
}
