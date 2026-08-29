using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Interfaces;

public interface IFactNormalizer
{
    string ModuleId { get; }
    void Normalize(IReadOnlyDictionary<string, object> answers, SharedFactStore facts);
}
