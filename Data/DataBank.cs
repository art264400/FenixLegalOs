using System.Collections.Generic;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;

namespace FenixLegalOs.Data;

public static class DataBank
{
    public const string QuestionBankVersion = "1.1.0-founders-focus";
    public const string ScoringEngineVersion = "1.1.0";
    public const string RiskLibraryVersion = "1.1.0";

    public static readonly List<DiagnosticSection> Sections = new()
    {
        new("founders", 1, "Сооснователи", "Founders", 15),
        new("corporate", 2, "Корпоративная структура", "Corporate", 12),
        new("ip", 3, "Интеллектуальная собственность", "IP", 18),
        new("team", 4, "Команда и сотрудники", "Team", 10)
    };

    public static readonly List<DiagnosticQuestion> Questions =
    [
        ..FoundersQuestions.All,
        ..CorporateQuestions.All,
        ..IpQuestions.All,
        ..TeamQuestions.All
    ];

    public static readonly List<RiskDefinition> Risks =
    [
        ..FoundersRisks.All,
        ..CorporateRisks.All,
        ..IpRisks.All,
        ..TeamRisks.All
    ];

    public static readonly List<DimensionDefinition> Dimensions =
    [
        ..FoundersDimensions.All,
        ..CorporateDimensions.All,
        ..IpDimensions.All,
        ..TeamDimensions.All
    ];

    public static string GetDimensionDisplayName(string dimensionId)
    {
        var dim = Dimensions.FirstOrDefault(d => d.Id == dimensionId);
        if (dim != null) return dim.DisplayName;

        // Backward compatibility for legacy aliases
        if (dimensionId == "equity_split")
        {
            var eq = Dimensions.FirstOrDefault(d => d.Id == "equity_clarity");
            if (eq != null) return eq.DisplayName;
        }

        return dimensionId;
    }
}
