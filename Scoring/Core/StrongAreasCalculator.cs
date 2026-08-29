using FenixLegalOs.Models;

namespace FenixLegalOs.Scoring.Core;

public class StrongAreasCalculator
{
    public static List<string> CalculateStrongAreas(
        IEnumerable<DimensionScore> allDimensionScores,
        List<RiskFinding> mergedFindings)
    {
        var strongAreas = new List<string>();
        foreach (var dim in allDimensionScores)
        {
            if (dim.Score >= 80)
            {
                bool hasSevereRisk = mergedFindings.Any(r =>
                    GetAffectedDimensions(r.Code).Contains(dim.DimensionId) &&
                    r.Severity is "CRITICAL" or "HIGH" or "BLOCKER");

                if (!hasSevereRisk)
                {
                    strongAreas.Add(GetDimensionDisplayName(dim.DimensionId));
                }
            }
        }
        return strongAreas.Distinct().ToList();
    }

    public static List<string> GetAffectedDimensions(string riskCode)
    {
        return riskCode switch
        {
            // Founders (Canonical §25 — 18 Findings)
            "FND_ACTIVE_DISPUTE" => new() { "existing_dispute" },
            "FND_EQUITY_DISPUTE" => new() { "equity_clarity" },
            "FND_DEAD_EQUITY" => new() { "early_exit_equity", "commitment" },
            "FND_DEADLOCK" => new() { "deadlock" },
            "FND_DEPARTED_UNRESOLVED" => new() { "early_exit_equity", "exit_continuity" },
            "FND_CONFLICT_OF_INTEREST" => new() { "conflict_of_interest" },
            "FND_ROLE_AMBIGUITY" => new() { "roles" },
            "FND_COMMITMENT_MISMATCH" => new() { "commitment" },
            "FND_EQUITY_NOT_FORMALIZED" => new() { "equity_clarity" },
            "FND_EQUITY_AMBIGUITY" => new() { "equity_clarity" },
            "FND_NO_VESTING" => new() { "early_exit_equity" },
            "FND_INCOMPLETE_LEAVER_RULES" => new() { "early_exit_equity" },
            "FND_GOVERNANCE_AMBIGUITY" => new() { "governance" },
            "FND_NO_DEADLOCK_PROTECTION" => new() { "deadlock" },
            "FND_EXIT_RULES_MISSING" => new() { "exit_continuity" },
            "FND_CONTRIBUTION_AMBIGUITY" => new() { "founder_contributions" },
            "FND_STRATEGIC_MISALIGNMENT" => new() { "strategic_alignment" },
            "FND_DOCUMENTATION_GAP" => new() { "governance" },

            // Corporate
            "COR_NO_ENTITY_FOR_ACTIVITY" => new() { "ownership_accuracy", "entity_alignment" },
            "COR_OWNERSHIP_DISPUTE" or "COR_OWNERSHIP_MISMATCH" => new() { "ownership_accuracy" },
            "COR_CAP_TABLE_GAP" or "COR_CAP_TABLE_UNRELIABLE" => new() { "cap_table" },
            "COR_UNDOCUMENTED_EQUITY" => new() { "equity_commitments" },
            "COR_CORPORATE_HISTORY_GAP" => new() { "corporate_history" },
            "COR_APPROVAL_GAP" => new() { "corporate_approvals" },
            "COR_AUTHORITY_GAP" => new() { "authority" },
            "COR_ENTITY_MISMATCH" => new() { "entity_alignment" },
            "COR_RECORDS_GAP" => new() { "records" },
            "COR_HIDDEN_CONTROL" => new() { "ownership_accuracy" },

            // IP
            "IP_PRODUCT_RIGHTS_UNCONFIRMED" => new() { "overall_rights" },
            "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" => new() { "founder_rights" },
            "IP_CONTRACTOR_RIGHTS_GAP" => new() { "external_creators", "employee_rights" },
            "IP_FORMER_DEVELOPER_GAP" => new() { "external_creators" },
            "IP_STUDIO_RIGHTS_GAP" => new() { "external_creators" },
            "IP_EMPLOYER_RISK" => new() { "external_employer" },
            "IP_THIRD_PARTY_COMPONENTS" or "IP_EXTERNAL_DEPENDENCY" => new() { "third_party_dependencies" },
            "IP_ACCESS_CONTROL" => new() { "technical_control" },
            "IP_BRAND_DOMAIN_CONTROL" or "IP_DOMAIN_BRAND_CONTROL" or "IP_BRAND_REGISTRATION_INFO" => new() { "brand_domain" },
            "IP_CONTENT_RIGHTS" => new() { "content_provenance" },

            _ => throw new InvalidOperationException($"Unmapped severe risk code '{riskCode}' in GetAffectedDimensions mapping invariant.")
        };
    }

    public static string GetDimensionDisplayName(string dimId)
    {
        return dimId switch
        {
            "equity_split" or "equity_clarity" => "Распределение долей основателей",
            "vesting" or "early_exit_equity" => "Вестинг и фиксация участия",
            "roles" => "Роли и ответственность",
            "ip_transfer" or "ip_assignment" => "Передача прав от основателей",
            "governance" => "Корпоративное управление сооснователей",
            "deadlock" => "Механизмы разрешения тупиков",
            "ownership_accuracy" => "Соответствие долей и реестра",
            "cap_table" => "Таблица долей и история",
            "equity_commitments" => "Фиксация опционов и обещаний",
            "corporate_history" => "История изменений капитала",
            "corporate_approvals" => "Корпоративные решения и одобрения",
            "authority" => "Полномочия и лимиты сделок",
            "entity_alignment" => "Оформление активов на компанию",
            "records" => "Корпоративный архив и учет",
            "overall_rights" => "Права на продукт в целом",
            "founder_rights" => "Передача прав от создателей-основателей",
            "employee_rights" => "Служебные произведения сотрудников",
            "external_creators" => "Оформление прав с подрядчиками",
            "external_employer" => "Чистота от прав прошлых работодателей",
            "third_party_dependencies" => "Лицензии сторонних библиотек",
            "technical_control" => "Контроль технических аккаунтов",
            "brand_domain" => "Права на домен и бренд",
            "content_provenance" => "Происхождение данных и контента",
            _ => dimId
        };
    }
}
