namespace FenixLegalOs.Models;

public class UserSessionSummaryDto
{
    public string Id { get; set; } = "";
    public string CreatedAt { get; set; } = "";
    public string? UpdatedAt { get; set; }
    public string? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsPaid { get; set; }
    public string? PaidAt { get; set; }
    public int? OverallScore { get; set; }
    public string? LevelTitle { get; set; }
    public int CriticalCount { get; set; }
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public string? CompanyName { get; set; }
    public bool HasPdf { get; set; }
}
