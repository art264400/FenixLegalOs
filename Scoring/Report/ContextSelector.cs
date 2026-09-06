using FenixLegalOs.Models.Enums;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public static class ContextSelector
{
    public static CompactSharedProjectContextDto ExtractCompactSharedContext(ReportContext ctx)
    {
        var facts = ctx.Profile?.KeyFacts ?? new List<FactItemDto>();

        string GetFact(string key) =>
            facts.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase))?.Value ?? string.Empty;

        var fundraisingVal = GetFact("invest_readiness");
        if (string.IsNullOrWhiteSpace(fundraisingVal) && ctx.InvestmentReadiness != null)
        {
            fundraisingVal = ctx.InvestmentReadiness.IsApplicable
                ? $"{ctx.InvestmentReadiness.Category} (базовый скор: {ctx.InvestmentReadiness.BaseScore})"
                : "Не применимо к текущей стадии";
        }

        return new CompactSharedProjectContextDto
        {
            Jurisdiction = GetFact("jurisdiction"),
            EntityStatus = GetFact("entity"),
            Founders = GetFact("founders"),
            Ownership = GetFact("equity"),
            ProductStage = GetFact("stage"),
            ProductCreators = GetFact("creators"),
            Users = GetFact("users"),
            Fundraising = fundraisingVal,
            OverallScore = ctx.Overall?.Score ?? 0,
            OverallBand = ctx.Overall?.Band ?? string.Empty,
            OverallConfidence = ctx.Overall?.Confidence ?? 0
        };
    }

    public static List<string> SelectCrossModuleSignals(ReportContext ctx, string sectionId)
    {
        var signals = new List<string>();
        var normalizedSection = sectionId.Trim().ToLowerInvariant();

        switch (normalizedSection)
        {
            case "ip":
                // Signal 1: Team contractor context for IP
                var hasContractorsInTeam = ctx.AllFindings.Any(f =>
                    f.SectionId.Equals("team", StringComparison.OrdinalIgnoreCase) &&
                    (f.Code.Contains("CONTRACTOR", StringComparison.OrdinalIgnoreCase) ||
                     f.Code.Contains("FREELANCER", StringComparison.OrdinalIgnoreCase) ||
                     f.Code.Contains("WRITTEN_CONTRACTS", StringComparison.OrdinalIgnoreCase)));

                var creatorsFact = ctx.Profile?.KeyFacts.FirstOrDefault(f => f.Key == "creators")?.Value ?? string.Empty;
                if (hasContractorsInTeam || creatorsFact.Contains("разработчик", StringComparison.OrdinalIgnoreCase) || creatorsFact.Contains("подрядчик", StringComparison.OrdinalIgnoreCase))
                {
                    signals.Add("Команда: к разработке привлекались внешние разработчики/подрядчики, документальное оформление прав требует сквозной проверки.");
                }

                // Signal 2: Investment blocker relevance
                if (ctx.InvestmentReadiness?.CrossModuleBlockers.Any(b => b.SectionId.Equals("ip", StringComparison.OrdinalIgnoreCase)) == true)
                {
                    signals.Add("Инвестиции: выявленные дефекты оформления прав на продукт квалифицированы как прямой блокер Due Diligence.");
                }
                break;

            case "corporate":
                // Signal 1: Founders deadlock/equity overlap
                var hasDeadlock = ctx.AllFindings.Any(f =>
                    f.SectionId.Equals("founders", StringComparison.OrdinalIgnoreCase) &&
                    (f.Code.Contains("DEADLOCK", StringComparison.OrdinalIgnoreCase) || f.Code.Contains("5050", StringComparison.OrdinalIgnoreCase)));
                if (hasDeadlock)
                {
                    signals.Add("Основатели: в структуре управления выявлен риск тупиковых ситуаций (deadlock), влияющий на принятие корпоративных решений.");
                }

                // Signal 2: Team equity promises
                var hasOptionOrEquityPromise = ctx.AllFindings.Any(f =>
                    f.SectionId.Equals("team", StringComparison.OrdinalIgnoreCase) &&
                    (f.Code.Contains("OPTION", StringComparison.OrdinalIgnoreCase) || f.Code.Contains("EQUITY", StringComparison.OrdinalIgnoreCase)));
                if (hasOptionOrEquityPromise)
                {
                    signals.Add("Команда: имеются неформализованные обещания долей или опционов ключевым сотрудникам.");
                }
                break;

            case "founders":
                // Signal 1: Corporate entity status
                var entityFact = ctx.Profile?.KeyFacts.FirstOrDefault(f => f.Key == "entity")?.Value ?? string.Empty;
                if (entityFact.Contains("Не зарегистрировано", StringComparison.OrdinalIgnoreCase))
                {
                    signals.Add("Корпоративный контур: юридическое лицо еще не зарегистрировано, отношения основателей действуют вне формальной компании.");
                }
                break;

            case "data" or "data_ai":
                // Signal 1: Product user flow and stage
                var usersFact = ctx.Profile?.KeyFacts.FirstOrDefault(f => f.Key == "users")?.Value ?? string.Empty;
                var stageFact = ctx.Profile?.KeyFacts.FirstOrDefault(f => f.Key == "stage")?.Value ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(usersFact))
                {
                    signals.Add($"Продукт: пользовательский контур — {usersFact} (стадия: {stageFact}).");
                }
                break;

            case "product":
                // Signal 1: Data/AI privacy constraints
                var hasDataIssues = ctx.AllFindings.Any(f =>
                    f.SectionId.Equals("data", StringComparison.OrdinalIgnoreCase) &&
                    (f.Severity is RiskSeverity.Blocker or RiskSeverity.Critical));
                if (hasDataIssues)
                {
                    signals.Add("Данные: в контуре обработки данных выявлены критические регуляторные риски, требующие отражения в правилах продукта.");
                }
                break;

            case "investment":
                // Material cross-module DD blockers
                if (ctx.InvestmentReadiness?.CrossModuleBlockers != null && ctx.InvestmentReadiness.CrossModuleBlockers.Count > 0)
                {
                    foreach (var blocker in ctx.InvestmentReadiness.CrossModuleBlockers.Take(3))
                    {
                        signals.Add($"Блокер Due Diligence ({blocker.ModuleTitle}): {blocker.Title}");
                    }
                }
                break;
        }

        return signals;
    }
}
