using System.Collections.Generic;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Models;

/// <summary>
/// Каноническая декларация корректирующего юридического действия (ActionDefinition).
/// Аналог RiskDefinition для детерминированного Action Plan vNext.
/// </summary>
public class ActionDefinition
{
    public string ActionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ActionType { get; set; } = "LEGAL_WORK"; // LEGAL_DRAFTING, LEGAL_REVIEW, PROCESS_SETUP, PRODUCT_INTEGRATION
    public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.LegalWork;
    public RiskPriority DefaultPriority { get; set; } = RiskPriority.Now;
    public string SectionId { get; set; } = string.Empty;
    public string BusinessReason { get; set; } = string.Empty; // Почему действие необходимо именно сейчас
    public string RequiredOutcome { get; set; } = string.Empty; // Достигаемый юридический результат (специфичный для данного действия)
    public string WhatToDo { get; set; } = string.Empty; // Конкретная инструкция по исполнению
    public List<string> Dependencies { get; set; } = new(); // ActionIds, которые должны предшествовать данному действию
    public List<string> SupportedFindingCodes { get; set; } = new(); // Коды находок, которые закрывает данное действие
}
