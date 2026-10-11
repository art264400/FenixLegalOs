namespace FenixLegalOs.Models;

/// <summary>A full independent correction of a completed report; source session remains immutable.</summary>
public sealed class ReportRevision
{
    public string Id { get; set; } = "";
    public string SessionId { get; set; } = "";
    public string? BaseRevisionId { get; set; }
    public int Number { get; set; }
    public int Version { get; set; }
    public string Status { get; set; } = "draft";
    public string AnswersJson { get; set; } = "{}";
    public string BaseAnswersJson { get; set; } = "{}";
    public string QuestionsJson { get; set; } = "[]";
    public string SectionsJson { get; set; } = "[]";
    public string BankFingerprint { get; set; } = "";
    public string Reason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string UpdatedAt { get; set; } = "";
    public string? PublishedAt { get; set; }
    public string? ResultJson { get; set; }
    public byte[]? PdfBytes { get; set; }
    public string? Error { get; set; }
}

public sealed class AnswerEditorRequest
{
    public string? RevisionId { get; set; }
    public int Version { get; set; }
    public string? BaseRevisionId { get; set; }
    public Dictionary<string, object> Answers { get; set; } = new();
    public string Reason { get; set; } = "";
}

public sealed class RevisionVersionRequest
{
    public int Version { get; set; }
}
