using FenixLegalOs.Models;
using FenixLegalOs.Models.Report;
using FenixLegalOs.Scoring.Core;

namespace FenixLegalOs.Scoring.Report;

public static class ProjectProfileExtractor
{
    public static ProjectProfileDto ExtractProfile(SharedFactStore facts, string? projectName = null)
    {
        var keyFacts = new List<FactItemDto>();

        // 1. Entity status & Jurisdiction
        var entityStatus = GetStringVal(facts, "company.entityStatus");
        var isUnincorporated = entityStatus is "not_incorporated" or "none" or "no_entity" or "";
        var isInProcess = entityStatus is "registering" or "in_process";

        var entityVal = isUnincorporated ? "Не зарегистрировано"
            : isInProcess ? "В процессе регистрации"
            : entityStatus is "multiple" or "holding" ? "Группа / холдинг"
            : "Зарегистрировано (одно юрлицо)";

        keyFacts.Add(new FactItemDto { Key = "entity", Label = "Юридическое лицо", Value = entityVal, Icon = "building" });

        var jur = GetStringVal(facts, "company.primaryJurisdiction");
        if (string.IsNullOrEmpty(jur)) jur = GetStringVal(facts, "company.jurisdictions");

        var jurVal = jur switch
        {
            "kz" => "Казахстан",
            "aifc" => "МФЦА",
            "us" => "США (Делавэр)",
            "uae" => "ОАЭ",
            "uk" => "Великобритания",
            _ => "Казахстан"
        };
        keyFacts.Add(new FactItemDto { Key = "jurisdiction", Label = "Юрисдикция", Value = jurVal, Icon = "globe" });

        // 2. Founders & Equity
        var fCount = GetStringVal(facts, "founders.count");
        var fVal = fCount switch
        {
            "1" or "solo" => "1 основатель",
            "2" => "2 сооснователя",
            "3" => "3 сооснователя",
            "4plus" or "4" => "4+ сооснователей",
            "formal_only" => "Несколько (не все активны)",
            _ => "1 основатель"
        };
        keyFacts.Add(new FactItemDto { Key = "founders", Label = "Основатели", Value = fVal, Icon = "users" });

        var eqDist = GetStringVal(facts, "founders.equityDistribution");
        var is5050 = facts.Facts.TryGetValue("founders.isEqual5050", out var eq) && eq is true;
        var eqVal = fCount is "1" or "solo" ? "100% (единственный основатель)"
            : is5050 ? "50 / 50" : eqDist switch
            {
                "equal" or "equal_50_50" => "Равные доли",
                "majority" => "Мажоритарная доля",
                "minority_pool" => "С пулом опционов",
                _ => is5050 ? "50 / 50" : "Распределены"
            };
        keyFacts.Add(new FactItemDto { Key = "equity", Label = "Распределение долей", Value = eqVal, Icon = "scale" });

        // 3. Product & IP
        var stage = GetStringVal(facts, "product.stage");
        var stageVal = stage switch
        {
            "idea" => "Идея",
            "prototype" => "Прототип",
            "mvp" => "MVP (первые пользователи)",
            "commercial" or "scaling" => "Коммерческий запуск",
            _ => "Прототип / MVP"
        };
        keyFacts.Add(new FactItemDto { Key = "stage", Label = "Стадия продукта", Value = stageVal, Icon = "rocket" });

        var ipCreatorsList = GetListVal(facts, "ip.creators");
        string creatorsVal;
        if (ipCreatorsList.Count > 0)
        {
            var hasFounders = ipCreatorsList.Any(c => c.Contains("founder"));
            var hasContractors = ipCreatorsList.Any(c => c.Contains("contractor") || c.Contains("freelancer") || c.Contains("external") || c.Contains("studio") || c == "both");
            var hasFormer = ipCreatorsList.Any(c => c.Contains("former") || c.Contains("departed"));
            var hasEmployees = ipCreatorsList.Any(c => c.Contains("employee"));

            var parts = new List<string>();
            if (hasFounders) parts.Add("Основатели");
            if (hasEmployees) parts.Add("штатные сотрудники");
            if (hasContractors) parts.Add("внешние разработчики");
            if (hasFormer) parts.Add("бывшие участники команды");

            if (parts.Count == 0)
            {
                creatorsVal = "Основатели и подрядчики";
            }
            else if (parts.Count == 1)
            {
                creatorsVal = parts[0] == "штатные сотрудники" ? "Штатная команда"
                    : parts[0] == "внешние разработчики" ? "Внешние разработчики"
                    : parts[0] == "бывшие участники команды" ? "Бывшие разработчики / участники"
                    : "Только основатели";
            }
            else
            {
                creatorsVal = string.Join(", ", parts.Take(parts.Count - 1)) + " и " + parts.Last();
            }
        }
        else
        {
            creatorsVal = "Основатели и подрядчики";
        }
        keyFacts.Add(new FactItemDto { Key = "creators", Label = "Кто создает продукт", Value = creatorsVal, Icon = "code" });

        var ipRights = GetStringVal(facts, "ip.overallRightsEvidence");
        if (string.IsNullOrEmpty(ipRights))
        {
            var assigned = GetStringVal(facts, "ip.assignedToCompany");
            var docs = GetStringVal(facts, "ip.founderAssignmentDocs");
            if (assigned is "yes" or "all" || docs is "all_signed" or "assignment_complete")
            {
                ipRights = "all";
            }
        }

        var rightsVal = isUnincorporated
            ? ipRights switch
            {
                "all" or "full" or "all_signed" => "Закреплены за основателями (договоры подписаны)",
                "main" or "partial" => "Частично оформлены / не консолидированы",
                "none" or "missing" => "Права не оформлены (у создателей)",
                _ => "Частично оформлены / не консолидированы"
            }
            : ipRights switch
            {
                "all" or "full" or "all_signed" => "Переданы компании полностью",
                "main" or "partial" => "Переданы компании не полностью",
                "none" or "missing" => "Права не оформлены",
                _ => "Переданы компании не полностью"
            };
        keyFacts.Add(new FactItemDto { Key = "ip_rights", Label = "Права на продукт", Value = rightsVal, Icon = "shield" });

        // 4. Team, Audience & Investment
        var users = GetStringVal(facts, "product.targetAudience");
        var usersVal = users switch
        {
            "b2b" => "B2B-клиенты",
            "b2c" => "Физические лица (B2C)",
            "both" => "B2B и B2C",
            _ => "Физические лица и бизнес"
        };
        keyFacts.Add(new FactItemDto { Key = "users", Label = "Пользователи", Value = usersVal, Icon = "user_check" });

        var invTiming = GetStringVal(facts, "investment.timing");
        var invVal = invTiming switch
        {
            "terms_received" => "Получен Term Sheet",
            "specific_investor" => "Переговоры с инвестором",
            "active_search" => "Активный поиск раунда",
            "3_6m" => "Раунд в течение 3–6 месяцев",
            "6_12m" or "within_12m" => "Раунд в течение года",
            "none" => "Пока не привлекались",
            _ => "Пока не привлекались"
        };
        keyFacts.Add(new FactItemDto { Key = "investment", Label = "Инвестиции", Value = invVal, Icon = "coins" });

        // Build deterministic configuration narrative (2-4 neutral sentences)
        var name = string.IsNullOrWhiteSpace(projectName) ? "Проект" : projectName;
        var p1 = isUnincorporated
            ? $"{name} находится на ранней стадии и работает без зарегистрированного юридического лица в юрисдикции {jurVal}."
            : isInProcess
            ? $"{name} находится в процессе регистрации юридического лица в юрисдикции {jurVal}."
            : $"{name} осуществляет деятельность через структуру в юрисдикции {jurVal}.";

        var p2 = fCount is "1" or "solo"
            ? "В проекте один ключевой основатель, осуществляющий единоличное управление."
            : $"В проекте {fVal} с распределением долей {eqVal}.";

        var rightsDesc = rightsVal.StartsWith("права ", StringComparison.OrdinalIgnoreCase) 
            ? rightsVal.Substring(6).Trim() 
            : rightsVal.StartsWith("права", StringComparison.OrdinalIgnoreCase) 
            ? rightsVal.Substring(5).Trim() 
            : rightsVal;

        var p3 = $"Разработка продукта ({stageVal}) ведется с участием: {creatorsVal}. При этом права на созданные результаты {rightsDesc.ToLowerInvariant()}.";

        var narrative = $"{p1} {p2} {p3}";

        return new ProjectProfileDto
        {
            KeyFacts = keyFacts,
            ConfigurationNarrative = narrative
        };
    }

    private static List<string> GetListVal(SharedFactStore facts, string key)
    {
        if (!facts.Facts.TryGetValue(key, out var val) || val == null) return new List<string>();
        if (val is List<string> ls) return ls;
        if (val is IEnumerable<string> ies) return ies.ToList();
        if (val is System.Collections.IEnumerable en)
        {
            var res = new List<string>();
            foreach (var item in en)
            {
                if (item != null) res.Add(item.ToString() ?? "");
            }
            return res;
        }
        return new List<string> { val.ToString() ?? "" };
    }

    private static string GetStringVal(SharedFactStore facts, string key)
    {
        if (!facts.Facts.TryGetValue(key, out var val) || val == null) return "";
        if (val is string s) return s;
        if (val is IEnumerable<string> list) return list.FirstOrDefault() ?? "";
        if (val is System.Collections.IEnumerable en)
        {
            foreach (var item in en)
            {
                if (item != null) return item.ToString() ?? "";
            }
        }
        return val.ToString() ?? "";
    }
}
