using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Interfaces;

public interface IModuleRuleEngine
{
    string ModuleId { get; }
    IReadOnlyList<RiskFinding> Evaluate(SharedFactStore facts, IReadOnlyList<RiskDefinition> definitions);
}
