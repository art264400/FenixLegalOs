namespace FenixLegalOs.Models;

/// <summary>
/// Architecture A — Server-Driven Navigation:
/// Authoritative navigation state returned by the backend.
/// Frontend uses these fields exclusively for question routing.
/// Adding a new module requires ZERO changes to frontend navigation logic.
/// </summary>
public class NavigationState
{
    /// <summary>Ordered list of visible question IDs under current canonical routing state.</summary>
    public IReadOnlyList<string> VisibleQuestionIds { get; init; } = [];

    /// <summary>ID of the question currently being displayed. Null if no questions are visible.</summary>
    public string? CurrentQuestionId { get; init; }

    /// <summary>ID of the previous question in the visible list. Null if current is first.</summary>
    public string? PreviousQuestionId { get; init; }

    /// <summary>ID of the next question in the visible list. Null if current is last (questionnaire complete).</summary>
    public string? NextQuestionId { get; init; }

    /// <summary>1-based position of CurrentQuestion within the visible list.</summary>
    public int Current { get; init; }

    /// <summary>Total number of visible questions.</summary>
    public int TotalVisible { get; init; }
}
