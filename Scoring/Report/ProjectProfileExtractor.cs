using System.Globalization;
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

        var entityCount = GetStringVal(facts, "company.entityCount");
        var isGroup = (facts.Facts.TryGetValue("company.groupStructure", out var group) && group is true)
            || entityStatus is "multiple" or "holding";
        var groupCountText = entityCount switch
        {
            "2" => "2 юрлица",
            "3" => "3 юрлица",
            "4" => "4 и более юрлиц",
            _ => "число не указано"
        };
        var entityVal = isUnincorporated ? "Не зарегистрировано"
            : isInProcess ? "В процессе регистрации"
            : isGroup ? $"Группа компаний ({groupCountText})"
            : "Зарегистрировано (одно юрлицо)";
        var structureNarrative = GetStringVal(facts, "company.structureNarrative")
            .Replace("\r", "").Replace(":\n- ", ": ").Replace("\n- ", "; ").Replace("\n", " ").Trim().TrimEnd('.');

        keyFacts.Add(new FactItemDto { Key = "entity", Label = "Юридическое лицо", Value = entityVal, Icon = "building" });

        var jur = GetStringVal(facts, "company.primaryJurisdiction");
        var jurisdictions = GetListVal(facts, "company.jurisdictions").ToList();
        if (!string.IsNullOrEmpty(jur)) jurisdictions.Insert(0, jur);
        jurisdictions = jurisdictions.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToList();
        var jurVal = jurisdictions.Count == 0 ? "Не указана" : string.Join(", ", jurisdictions.Select(FormatJurisdiction));
        keyFacts.Add(new FactItemDto { Key = "jurisdiction", Label = "Юрисдикция", Value = jurVal, Icon = "globe" });
        // 2. Founders & Equity
        var fCount = GetStringVal(facts, "founders.count");
        var equityShares = GetEquityShares(facts);
        var equityShareCount = equityShares.Count;
        var fVal = fCount switch
        {
            "1" or "solo" => "1 основатель",
            "2" => "2 сооснователя",
            "3" => "3 сооснователя",
            "4plus" or "4" => "4+ сооснователей",
            "multiple" or "formal_only" when equityShareCount == 2 => "2 сооснователя (не все активны)",
            "multiple" or "formal_only" when equityShareCount == 3 => "3 сооснователя (не все активны)",
            "multiple" or "formal_only" when equityShareCount >= 4 => "4+ сооснователей (не все активны)",
            "multiple" or "formal_only" => "Несколько сооснователей (не все активны)",
            _ => "Не указано"
        };
        keyFacts.Add(new FactItemDto { Key = "founders", Label = "Основатели", Value = fVal, Icon = "users" });

        var eqDist = GetStringVal(facts, "founders.equityDistribution");
        var is5050 = facts.Facts.TryGetValue("founders.isEqual5050", out var eq) && eq is true;
        var eqVal = fCount is "1" or "solo" ? "100% (единственный основатель)"
            : equityShares.Count >= 2 ? string.Join(" / ", equityShares.Select(share => share.ToString("0.##", CultureInfo.InvariantCulture)))
            : is5050 ? "50 / 50" : eqDist switch
            {
                "equal" or "equal_50_50" => "Равные доли",
                "majority" => "Мажоритарная доля",
                "minority_pool" => "С пулом опционов",
                _ => "Не указано"
            };
        keyFacts.Add(new FactItemDto { Key = "equity", Label = "Распределение долей", Value = eqVal, Icon = "scale" });

        // 3. Product & IP
        var stage = GetStringVal(facts, "product.stage");
        var stageVal = stage switch
        {
            "idea" => "Идея",
            "prototype" => "Прототип / тестовая версия",
            "live_or_ready" or "ready" => "Готовый продукт",
            "multiple_products" or "multiple" => "Несколько продуктов",
            _ => "Не указано"
        };
        keyFacts.Add(new FactItemDto { Key = "stage", Label = "Состояние продукта", Value = stageVal, Icon = "rocket" });

        var userStage = GetStringVal(facts, "product.userStage");
        var noLiveUsers = userStage == "prelaunch"
            || (facts.Facts.TryGetValue("product.liveUsers", out var liveUsers) && liveUsers is false);
        var usageVal = noLiveUsers ? "Пользователей пока нет" : userStage switch
        {
            "first_users" or "first" => "Есть первые пользователи",
            "regular" => "Есть постоянные пользователи",
            "large" => "Используется большим количеством пользователей",
            _ => "Не указано"
        };
        keyFacts.Add(new FactItemDto { Key = "usage", Label = "Использование продукта", Value = usageVal, Icon = "user_check" });

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
        var userTypes = GetListVal(facts, "product.userTypes");
        var users = GetStringVal(facts, "product.targetAudience");
        var userOptions = Data.DataBank.Questions.FirstOrDefault(q => q.Id == "PROD-02")?.Options;
        var usersVal = noLiveUsers ? "Пользователей пока нет"
            : userTypes.Count > 0
            ? string.Join(", ", userTypes.Select(type => userOptions?.FirstOrDefault(option => option.Id == type)?.Label ?? "Не указано").Distinct())
            : users switch
            {
                "b2b" => "B2B-клиенты",
                "b2c" => "Физические лица (B2C)",
                "both" => "B2B и B2C",
                _ => "Не указано"
            };
        keyFacts.Add(new FactItemDto { Key = "users", Label = "Пользователи", Value = usersVal, Icon = "user_check" });
        var productDescription = $"Продукт: {stageVal}. Использование: {usageVal}.";
        var productBlock = $"Продукт: {stageVal}.\nИспользование: {usageVal}.";
        if (!noLiveUsers)
        {
            productDescription += $" Аудитория: {usersVal}.";
            productBlock += $"\nАудитория: {usersVal}.";
        }
        var invTiming = GetStringVal(facts, "investment.timing");
        var invVal = invTiming switch
        {
            "terms_received" => "Уже получили предложение с условиями сделки",
            "specific_investor" => "Уже обсуждаем условия с конкретным инвестором",
            "active_search" => "Уже ищем инвесторов",
            "3_6m" => "Планируем в ближайшие 3–6 месяцев",
            "6_12m" => "Планируем в ближайшие 6–12 месяцев",
            "within_12m" => "Возможно в течение ближайшего года",
            "none" => "Нет, пока не планируем",
            _ => "Не указано"
        };
        keyFacts.Add(new FactItemDto { Key = "investment", Label = "Инвестиции", Value = invVal, Icon = "coins" });

        // Build deterministic configuration narrative (2-4 neutral sentences)
        var name = string.IsNullOrWhiteSpace(projectName) ? "Проект" : projectName;
        var jurisdictionSuffix = jurisdictions.Count == 0
            ? " Юрисдикция проекта не указана."
            : $" в юрисдикции {jurVal}.";
        var p1 = isUnincorporated
            ? $"{name} работает без зарегистрированного юридического лица.{jurisdictionSuffix}"
            : isInProcess
            ? $"{name} находится в процессе регистрации юридического лица.{jurisdictionSuffix}"
            : isGroup && !string.IsNullOrEmpty(structureNarrative)
            ? $"{name}: {structureNarrative}."
            : $"{name} осуществляет деятельность через зарегистрированную структуру.{jurisdictionSuffix}";

        var p2 = fCount is "1" or "solo"
            ? "В проекте один ключевой основатель, осуществляющий единоличное управление."
            : $"В проекте {fVal}. Распределение долей: {eqVal}.";

        var rightsDesc = rightsVal.StartsWith("права ", StringComparison.OrdinalIgnoreCase) 
            ? rightsVal.Substring(6).Trim() 
            : rightsVal.StartsWith("права", StringComparison.OrdinalIgnoreCase) 
            ? rightsVal.Substring(5).Trim() 
            : rightsVal;

        var p3 = $"{productDescription} Создатели продукта: {creatorsVal}. При этом права на созданные результаты {rightsDesc.ToLowerInvariant()}.";

        var narrative = $"{p1} {p2} {p3}";

        return new ProjectProfileDto
        {
            ConfigurationBlocks = new List<FactItemDto>
            {
                new() { Label = "Компания и юрисдикция", Value = $"Название: {name}. Юридическое лицо: {(string.IsNullOrEmpty(entityStatus) ? "не указано" : entityVal)}. Юрисдикции: {jurVal}.{(isGroup && !string.IsNullOrEmpty(structureNarrative) ? " " + structureNarrative : "")}" },
                new() { Label = "Основатели и управление", Value = $"Состав: {fVal}. Распределение долей: {eqVal}." },
                new() { Label = "Продукт и стадия", Value = productBlock },
                new() { Label = "Команда и права на разработки", Value = $"Создатели продукта: {(ipCreatorsList.Count == 0 ? "не указаны" : creatorsVal)}. Права на результаты: {(string.IsNullOrEmpty(ipRights) ? "сведения не указаны" : rightsVal)}." },
                new() { Label = "Данные и ИИ", Value = $"Персональные данные: {DescribeFlag(facts, "data.personalDataProcessed", "обрабатываются", "не обрабатываются по ответам анкеты")}. ИИ: {DescribeFlag(facts, "ai.used", "используется", "не используется")}." },
                new() { Label = "Договоры и инвестиции", Value = $"B2B-отношения: {DescribeFlag(facts, "contracts.b2bRelevant", "присутствуют", "не заявлены")}. Инвестиционные планы: {(string.IsNullOrEmpty(invTiming) ? "не указаны" : invVal)}." }
            },
            KeyFacts = keyFacts,
            ConfigurationNarrative = narrative
        };
    }

    private static string FormatJurisdiction(string code) => code switch
    {
        "kz" => "Казахстан",
        "aifc" => "МФЦА",
        "us" => "США (Делавэр)",
        "uae" => "ОАЭ",
        "uk" => "Великобритания",
        "cy" => "Кипр",
        "sg" => "Сингапур",
        "eu" => "Европейский союз",
        "kg" => "Кыргызстан",
        "uz" => "Узбекистан",
        "other" => "Другая юрисдикция",
        _ => "Не указана"
    };

    private static List<double> GetEquityShares(SharedFactStore facts)
    {
        var shares = new List<double>();
        foreach (var value in GetListVal(facts, "founders.equityShares"))
        {
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var share) &&
                !double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out share)) return new();
            if (!double.IsFinite(share) || share <= 0 || share > 100) return new();
            shares.Add(share);
        }
        return shares;
    }

    private static string DescribeFlag(SharedFactStore facts, string key, string yes, string no)
    {
        return facts.Facts.TryGetValue(key, out var value) && value is bool flag
            ? (flag ? yes : no)
            : "не указано";
    }

    private static List<string> GetListVal(SharedFactStore facts, string key)
    {
        if (!facts.Facts.TryGetValue(key, out var val) || val == null) return new List<string>();
        if (val is string text) return new List<string> { text };
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
