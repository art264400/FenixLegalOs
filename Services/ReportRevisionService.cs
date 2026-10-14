using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Validation;

namespace FenixLegalOs.Services;

public interface IReportRevisionPdfGenerator
{
    Task<byte[]?> GenerateAsync(ScoreResult result, SharedFactStore facts, string sessionId, string companyName);
}

public sealed class ReportRevisionPdfGenerator(TypstPdfService pdf) : IReportRevisionPdfGenerator
{
    public Task<byte[]?> GenerateAsync(ScoreResult result, SharedFactStore facts, string sessionId, string companyName)
        => pdf.GeneratePdfAsync(result, facts, sessionId, companyName);
}

public sealed class ReportRevisionException(string code, string message, int status = 400, object? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public object? Details { get; } = details;
}

public sealed class ReportRevisionService(
    SessionRepository sessions, ReportRevisionRepository revisions, QuestionRepository bank,
    ScoringEngine scoring, IReportRevisionPdfGenerator pdf, LeadRepository leads, UserRepository users)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public object GetEditor(string sessionId)
    {
        var session = RequireCompleted(sessionId);
        var draft = revisions.GetDraft(sessionId);
        return RenderEditor(session, draft, draft == null ? ReadAnswers(session.AnswersJson) : ReadAnswers(draft.AnswersJson));
    }

    public object Preview(string sessionId, AnswerEditorRequest request)
    {
        var session = RequireCompleted(sessionId);
        if (request.Answers == null) throw new ReportRevisionException("invalid_answers", "Передайте ответы анкеты.");
        var draft = CheckRequest(session, request);
        var questions = draft == null ? bank.GetQuestions() : ReadQuestions(draft);
        var answers = CleanAnswers(request.Answers, questions);
        return RenderEditor(session, draft, answers, request.Reason, request.Answers.Keys.Except(answers.Keys).ToList());
    }

    public object Save(string sessionId, AnswerEditorRequest request, string actor)
    {
        var session = RequireCompleted(sessionId);
        if (request.Answers == null) throw new ReportRevisionException("invalid_answers", "Передайте ответы анкеты.");
        var draft = CheckRequest(session, request);
        if ((request.Reason ?? "").Length > 2000) throw new ReportRevisionException("invalid_reason", "Причина должна содержать не больше 2000 символов.");
        var questions = draft == null ? bank.GetQuestions() : ReadQuestions(draft);
        EnsureBank(draft);
        var answers = CleanAnswers(request.Answers, questions);
        var json = JsonSerializer.Serialize(answers);
        var reason = (request.Reason ?? "").Trim();
        if (draft == null)
        {
            var now = DateTime.UtcNow.ToString("o");
            draft = new ReportRevision
            {
                Id = Guid.NewGuid().ToString(), SessionId = sessionId, BaseRevisionId = session.ReportRevisionId,
                AnswersJson = json, BaseAnswersJson = session.AnswersJson,
                QuestionsJson = JsonSerializer.Serialize(questions), SectionsJson = JsonSerializer.Serialize(bank.GetSections()),
                BankFingerprint = Fingerprint(), Reason = reason, CreatedBy = actor, CreatedAt = now, UpdatedAt = now
            };
            if (!revisions.Create(draft)) throw Conflict();
            leads.AuditLog(actor, "report_revision_created", $"{sessionId}: {draft.Id}");
        }
        else if (!revisions.Save(sessionId, draft.Id, request.Version, json, reason, actor)) throw Conflict();
        return GetEditor(sessionId);
    }

    public async Task<object> RecalculateAsync(string sessionId, string revisionId, int version)
    {
        RequireCompleted(sessionId);
        var draft = RequireRevision(sessionId, revisionId);
        EnsureBank(draft);
        if (draft.Version != version) throw Conflict();
        if (draft.Reason.Trim().Length < 3) throw new ReportRevisionException("reason_required", "Укажите причину исправления перед пересчётом.");
        var questions = ReadQuestions(draft);
        var answers = CleanAnswers(ReadAnswers(draft.AnswersJson), questions);
        var (visible, _, _) = ScoringEngine.ResolveEffectiveState(questions, answers);
        var missing = visible.Where(q => !answers.ContainsKey(q.Id)).Select(q => q.Id).ToList();
        if (missing.Count > 0) throw new ReportRevisionException("incomplete_answers", "Заполните все вопросы актуальной ветки анкеты перед пересчётом.", details: missing);
        if (!revisions.BeginGeneration(sessionId, revisionId, version)) throw Conflict();
        try
        {
            var result = scoring.ComputeResult(answers);
            var facts = FactNormalizer.NormalizeFacts(answers);
            var bytes = await pdf.GenerateAsync(result, facts, sessionId, CompanyName(sessions.GetSession(sessionId)!));
            if (bytes is not { Length: > 4 } || !bytes.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
                throw new InvalidOperationException("PDF generation did not produce a valid PDF.");
            if (!revisions.FinishGeneration(sessionId, revisionId, version + 1, JsonSerializer.Serialize(result), bytes)) throw Conflict();
        }
        catch (ReportRevisionException) { throw; }
        catch
        {
            revisions.FailGeneration(sessionId, revisionId, version + 1);
            throw new ReportRevisionException("pdf_generation_failed", "Не удалось сформировать исправленный отчёт. Предыдущая версия доступна пользователю; пересчёт можно повторить.", 500);
        }
        return GetEditor(sessionId);
    }

    public object Discard(string sessionId, string revisionId, int version, string actor)
    {
        RequireCompleted(sessionId);
        if (!revisions.Discard(sessionId, revisionId, version, actor)) throw Conflict();
        return GetEditor(sessionId);
    }

    public object Publish(string sessionId, string revisionId, int version, string actor)
    {
        RequireCompleted(sessionId);
        var revision = RequireRevision(sessionId, revisionId);
        EnsureBank(revision);
        if (!revisions.Publish(sessionId, revisionId, version, actor)) throw Conflict();
        return GetEditor(sessionId);
    }

    private object RenderEditor(DiagnosticSession session, ReportRevision? draft, Dictionary<string, object> rawAnswers,
        string? reasonOverride = null, List<string>? removed = null)
    {
        var questions = draft == null ? bank.GetQuestions() : ReadQuestions(draft);
        var sections = draft == null ? bank.GetSections() : JsonSerializer.Deserialize<List<DiagnosticSection>>(draft.SectionsJson, JsonOptions) ?? new();
        var (visible, answers, _) = ScoringEngine.ResolveEffectiveState(questions, rawAnswers);
        var original = ReadAnswers(draft?.BaseAnswersJson ?? session.AnswersJson);
        var originalSession = sessions.GetOriginalSession(session.Id)!;
        var history = new List<object>
        {
            new { id = "original", number = 0, status = "original", createdAt = originalSession.CompletedAt,
                publishedAt = originalSession.CompletedAt, reason = "Исходный отчёт", pdfAvailable = originalSession.PdfBytes is { Length: > 0 }, createdBy = "user" }
        };
        history.AddRange(revisions.List(session.Id).Select(r => (object)new
        {
            id = r.Id, number = r.Number, status = r.Status, createdAt = r.CreatedAt,
            publishedAt = r.PublishedAt, reason = r.Reason, pdfAvailable = r.PdfBytes is { Length: > 0 }, createdBy = r.CreatedBy
        }));
        return new
        {
            sessionId = session.Id, companyName = CompanyName(session),
            publishedRevisionId = session.ReportRevisionId, publishedRevisionNumber = session.ReportRevisionNumber,
            answers, originalAnswers = original, removedQuestionIds = removed ?? new List<string>(),
            sections = sections.OrderBy(s => s.Order).Select(s => new
            {
                id = s.Id, title = s.Title, order = s.Order,
                questions = visible.Where(q => q.SectionId == s.Id).Select(q => new
                {
                    id = q.Id, question = q.Question, explanation = q.Explanation, type = q.Type, options = q.Options,
                    answer = answers.GetValueOrDefault(q.Id), originalAnswer = original.GetValueOrDefault(q.Id),
                    missing = !answers.ContainsKey(q.Id), changed = !SameAnswer(answers.GetValueOrDefault(q.Id), original.GetValueOrDefault(q.Id))
                }).ToList()
            }).Where(s => s.questions.Count > 0).ToList(),
            draft = draft == null ? null : new
            {
                id = draft.Id, version = draft.Version, status = draft.Status, reason = reasonOverride ?? draft.Reason,
                result = string.IsNullOrEmpty(draft.ResultJson) ? null : JsonSerializer.Deserialize<ScoreResult>(draft.ResultJson),
                pdfAvailable = draft.PdfBytes is { Length: > 0 }, error = draft.Error
            },
            revisions = history
        };
    }

    private ReportRevision? CheckRequest(DiagnosticSession session, AnswerEditorRequest request)
    {
        if (!string.Equals(session.ReportRevisionId, request.BaseRevisionId, StringComparison.Ordinal)) throw Conflict();
        if (request.RevisionId == null)
        {
            if (request.Version != 0 || revisions.GetDraft(session.Id) != null) throw Conflict();
            return null;
        }
        var draft = RequireRevision(session.Id, request.RevisionId);
        if (draft.Version != request.Version || draft.BaseRevisionId != session.ReportRevisionId || draft.Status is not ("draft" or "ready" or "failed")) throw Conflict();
        return draft;
    }

    private DiagnosticSession RequireCompleted(string sessionId)
    {
        var session = sessions.GetSession(sessionId) ?? throw new ReportRevisionException("not_found", "Диагностика не найдена.", 404);
        if (string.IsNullOrWhiteSpace(session.CompletedAt) || string.IsNullOrWhiteSpace(session.ResultJson))
            throw new ReportRevisionException("report_not_completed", "Исправление доступно после завершения диагностики.", 409);
        return session;
    }

    private ReportRevision RequireRevision(string sessionId, string revisionId)
        => revisions.Get(sessionId, revisionId) ?? throw new ReportRevisionException("revision_not_found", "Редакция отчёта не найдена.", 404);

    private static Dictionary<string, object> CleanAnswers(Dictionary<string, object> raw, List<DiagnosticQuestion> questions)
    {
        var knownIds = questions.Select(q => q.Id).ToHashSet(StringComparer.Ordinal);
        if (raw.Keys.Any(id => !knownIds.Contains(id))) throw new ReportRevisionException("unknown_question", "В ответах указан неизвестный вопрос.");
        var (_, clean, _) = ScoringEngine.ResolveEffectiveState(questions, raw);
        var validation = AnswerValidator.Validate(clean, questions);
        if (!validation.IsValid) throw new ReportRevisionException("validation_failed", "Проверьте значения изменённых ответов.", details: validation.Errors);
        return clean;
    }

    private string CompanyName(DiagnosticSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.CompanyName)) return session.CompanyName;
        var lead = leads.FindLeadsBySession(session.Id).FirstOrDefault();
        string? company = lead?.Company as string;
        if (string.IsNullOrWhiteSpace(company) && session.UserId != null) company = users.GetUserById(session.UserId)?.Company;
        return string.IsNullOrWhiteSpace(company) ? "Стартап" : company;
    }

    private string Fingerprint()
    {
        var snapshot = JsonSerializer.Serialize(new { questions = bank.GetQuestions(), sections = bank.GetSections(), risks = bank.GetRisks(),
            questionVersion = DataBank.QuestionBankVersion, engineVersion = DataBank.ScoringEngineVersion, riskVersion = DataBank.RiskLibraryVersion });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
    }

    private void EnsureBank(ReportRevision? revision)
    {
        if (revision != null && revision.BankFingerprint != Fingerprint())
            throw new ReportRevisionException("question_bank_changed", "Методика изменилась после создания черновика. Создайте новую редакцию на актуальной методике.", 409);
    }

    private static List<DiagnosticQuestion> ReadQuestions(ReportRevision revision)
        => JsonSerializer.Deserialize<List<DiagnosticQuestion>>(revision.QuestionsJson, JsonOptions) ?? new();
    private static Dictionary<string, object> ReadAnswers(string json)
        => JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions) ?? new();
    private static bool SameAnswer(object? left, object? right) => JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);
    private static ReportRevisionException Conflict() => new("revision_conflict", "Редакция уже изменена в другой вкладке или опубликован новый отчёт. Обновите страницу.", 409);
}
