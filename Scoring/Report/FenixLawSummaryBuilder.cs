using System.Text.RegularExpressions;
using FenixLegalOs.Models.Report;

namespace FenixLegalOs.Scoring.Report;

public sealed record FenixLawSummaryRow(string Title, string Task, string? Note = null);

/// <summary>Short presentation of grounded service items; the full items remain in the report.</summary>
public static class FenixLawSummaryBuilder
{
    public const int MaximumTaskLength = 220;

    public static FenixLawSummaryRow Build(FenixLawServiceCardDto card)
    {
        ArgumentNullException.ThrowIfNull(card);
        var title = ShortTitle(card.Title);
        var items = card.Items.Where(item => !string.IsNullOrWhiteSpace(item.FindingCode) ||
            !string.IsNullOrWhiteSpace(item.Recommendation) || !string.IsNullOrWhiteSpace(item.Title)).ToList();
        if (items.Count == 0)
            return new(title, LegacyTask(card.Description));

        var parts = items.Select(item => Describe(item)).ToList();
        var subjects = parts.Select(part => part.Subject).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var allUnknown = items.All(item => !item.HasConfirmedIssue);
        var anyUnknown = items.Any(item => item.RequiresClarification || !item.HasConfirmedIssue);
        if (allUnknown)
            return new(title, Fit("Уточнить ", subjects, "; определить работу только по подтвержденным пробелам.", items.Count));

        var actions = parts.Select(part => part.Action.TrimEnd('.', ' '))
            .Where(action => action.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var task = string.Join("; ", actions.Select((action, index) => index == 0 ? action : LowerFirst(action))) + ".";
        if (anyUnknown)
            task += " Неизвестные сведения проверить; исправлять только подтвержденные недостатки.";
        if (task.Length <= MaximumTaskLength) return new(title, task);

        // Summarize every topic instead of keeping an arbitrary first N findings. When
        // even the topic list is too long, explicitly refer to the complete plan.
        return new(title, Fit(anyUnknown ? "Проверить и урегулировать " : "Урегулировать ", subjects,
            anyUnknown ? "; работать только с подтвержденными недостатками." : "; действия — по плану отчета.", items.Count));
    }

    private static (string Subject, string Action) Describe(FenixLawServiceItemDto item)
    {
        var code = item.FindingCode.ToUpperInvariant();
        var recommendation = Normalize(item.Recommendation);
        var topic = Topic(code);
        if (!item.HasConfirmedIssue)
            return (topic.Subject, "Уточнить " + topic.Subject);
        if (code is "PROD_RULES_MISSING" or "PROD_NO_TERMS_OF_SERVICE" or
            "PROD_RULES_MISMATCH" or "PROD_RULES_DISCREPANCY" or "PROD_TERMS_MISMATCH" or
            "DATA_PRIVACY_NOTICE_MISSING" or "DATA_NO_PRIVACY_POLICY" or "DATA_PRIVACY_MISSING" or
            "DATA_PRIVACY_NOTICE_OUTDATED" or "DATA_PRIVACY_POLICY_INADEQUATE")
        {
            var document = code.StartsWith("PROD_") ? "пользовательские условия" : "политику конфиденциальности";
            var action = recommendation.Contains("Завершить", StringComparison.OrdinalIgnoreCase) ? "Завершить подготовку " :
                recommendation.Contains("существующ", StringComparison.OrdinalIgnoreCase) ||
                recommendation.Contains("действующ", StringComparison.OrdinalIgnoreCase) ? "Обновить " :
                recommendation.StartsWith("Подготовить", StringComparison.OrdinalIgnoreCase) ? "Подготовить " : "Проверить ";
            return (topic.Subject, action + document);
        }
        if (code == "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" && recommendation.Contains("создаваемую компанию", StringComparison.OrdinalIgnoreCase))
            return (topic.Subject, "Оформить обязательства по передаче прав в создаваемую компанию");
        if (code == "INVEST_PRIOR_INVESTMENT_UNCLEAR")
            return (topic.Subject, item.RequiresClarification
                ? "Завершить оформление прошлых инвестиций и уточнить права инвестора"
                : "Урегулировать оформление прошлых инвестиций и права инвестора");
        if (topic.Action.Length > 0) return topic;
        // Unrecognized/legacy codes cannot justify a newly invented legal task.
        return ("прочие выявленные вопросы", FirstCompleteClause(recommendation) is { Length: > 0 } clause && clause.Length <= MaximumTaskLength
            ? clause : "Выполнить адресные рекомендации из плана отчета");
    }

    private static (string Subject, string Action) Topic(string code) => code switch
    {
        "IP_PRODUCT_RIGHTS_UNCONFIRMED" or "IP_CONTRACTOR_RIGHTS_GAP" or "IP_FORMER_DEVELOPER_GAP" or "IP_STUDIO_RIGHTS_GAP" or "IP_EMPLOYER_RISK" => ("права на продукт", "Уточнить и оформить права на продукт"),
        "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" => ("права на продукт", "Оформить передачу прав основателей"),
        "IP_DOMAIN_BRAND_CONTROL" => ("домен и бренд", "Оформить контроль домена и бренда"),
        "IP_BRAND_REGISTRATION_INFO" => ("защиту бренда", "Оценить необходимость защиты бренда"),
        "IP_EXTERNAL_DEPENDENCY" => ("внешние зависимости", "Проверить внешние зависимости"),
        "IP_THIRD_PARTY_COMPONENTS" or "IP_CONTENT_RIGHTS" => ("лицензии и контент", "Проверить лицензии и права на сторонние материалы"),
        "IP_ACCESS_CONTROL" => ("доступы к продукту", "Упорядочить доступы к продукту"),
        "FND_GOVERNANCE_AMBIGUITY" or "FND_ROLE_AMBIGUITY" or "COR_APPROVAL_GAP" or "COR_AUTHORITY_GAP" => ("правила управления", "Закрепить правила управления"),
        "FND_DEADLOCK" or "FND_NO_DEADLOCK_PROTECTION" => ("выход из тупика", "Согласовать выход из тупика"),
        "FND_NO_VESTING" or "FND_DEAD_EQUITY" => ("связь доли с участием", "Связать долю с участием"),
        "FND_EXIT_RULES_MISSING" or "FND_INCOMPLETE_LEAVER_RULES" or "FND_DEPARTED_UNRESOLVED" => ("условия ухода", "Урегулировать условия ухода"),
        "FND_EQUITY_NOT_FORMALIZED" or "FND_EQUITY_AMBIGUITY" or "COR_UNDOCUMENTED_EQUITY" or "COR_CAP_TABLE_UNRELIABLE" => ("структуру долей", "Зафиксировать структуру долей"),
        "FND_DOCUMENTATION_GAP" or "COR_CORPORATE_HISTORY_GAP" or "COR_RECORDS_GAP" => ("действующие договоренности", "Уточнить действующие договоренности"),
        "FND_ACTIVE_DISPUTE" or "FND_EQUITY_DISPUTE" or "COR_OWNERSHIP_DISPUTE" or "COR_OWNERSHIP_MISMATCH" => ("разногласия о правах", "Урегулировать разногласия о правах"),
        "FND_CONFLICT_OF_INTEREST" or "FND_STRATEGIC_MISALIGNMENT" or "FND_COMMITMENT_MISMATCH" or "FND_CONTRIBUTION_AMBIGUITY" => ("условия участия", "Согласовать условия участия"),
        "COR_ENTITY_MISMATCH" or "COR_NO_ENTITY_FOR_ACTIVITY" => ("юридическую структуру", "Привести юридическую структуру в соответствие с деятельностью"),
        "COR_HIDDEN_CONTROL" => ("фактический контроль", "Уточнить фактический контроль компании"),
        "PROD_RULES_MISSING" or "PROD_RULES_MISMATCH" or "PROD_NO_TERMS_OF_SERVICE" or "PROD_RULES_DISCREPANCY" or "PROD_TERMS_MISMATCH" => ("пользовательские условия", "Проверить пользовательские условия"),
        "PROD_MULTI_COUNTRY_REVIEW" or "PROD_MARKET_REG_REVIEW" or "PROD_CROSS_BORDER_CONSUMER_RISK" => ("требования стран пользователей", "Проверить требования стран пользователей и адаптировать затронутые положения документов"),
        "PROD_MINORS_REVIEW" or "PROD_AGE_GATE_MISSING" => ("сценарии с несовершеннолетними", "Проверить сценарии с несовершеннолетними"),
        "PROD_REGULATORY_REVIEW" or "PROD_REGULATORY_RISK" => ("специальные требования", "Проверить специальные требования к функциям продукта"),
        "PROD_OFFER_UNCLEAR" => ("описание предложения", "Уточнить описание предложения до оплаты"),
        "PROD_ROLE_UNCLEAR" => ("роли сторон", "Уточнить роли сторон"),
        "PROD_ACCEPTANCE_WEAK" => ("принятие условий", "Доработать выявленные пробелы в принятии условий"),
        "PROD_PAYMENT_TRANSPARENCY" => ("раскрытие стоимости", "Раскрыть полную стоимость до оплаты"),
        "PROD_REFUND_RULES" or "PROD_REFUND_POLICY_GAP" => ("возвраты", "Уточнить порядок возвратов"),
        "PROD_SUBSCRIPTION_RULES" or "PROD_SUBSCRIPTION_AUTO_RENEWAL" => ("правила подписки", "Доработать выявленные условия подписки"),
        "PROD_ACCOUNT_RESTRICTIONS" => ("ограничения доступа", "Доработать выявленные правила ограничения доступа"),
        "PROD_USER_CONTENT_RULES" => ("пользовательский контент", "Доработать выявленные правила работы с пользовательским контентом"),
        "DATA_PRIVACY_NOTICE_MISSING" or "DATA_NO_PRIVACY_POLICY" or "DATA_PRIVACY_MISSING" or "DATA_PRIVACY_NOTICE_OUTDATED" or "DATA_PRIVACY_POLICY_INADEQUATE" => ("политику конфиденциальности", "Проверить политику конфиденциальности"),
        "DATA_MAP_INCOMPLETE" or "DATA_SECONDARY_USE_UNCLEAR" => ("потоки и цели обработки данных", "Уточнить потоки и цели обработки данных"),
        "DATA_THIRD_PARTY_UNKNOWN" or "AI_PROVIDER_TERMS_UNKNOWN" => ("условия внешних сервисов", "Проверить фактические передачи данных и условия внешних сервисов"),
        "DATA_CROSS_BORDER_REVIEW" => ("трансграничные передачи", "Проверить трансграничные передачи данных"),
        "DATA_RETENTION_UNDEFINED" or "DATA_DELETION_GAP" => ("хранение и удаление данных", "Доработать хранение и удаление данных"),
        "DATA_ACCESS_TOO_BROAD" => ("доступы к данным", "Ограничить доступы к данным"),
        "AI_USER_DATA_TRANSFER" or "AI_SENSITIVE_DATA_TRANSFER" => ("передачи данных в ИИ", "Проверить передачи данных в ИИ, условия сервиса и информирование пользователей"),
        "AI_TRAINING_NOT_DISCLOSED" => ("данные для развития ИИ", "Уточнить использование данных для развития ИИ и информирование пользователей"),
        "AI_AUTOMATED_DECISION" or "AI_HUMAN_REVIEW_GAP" => ("решения ИИ и их проверку", "Доработать проверку решений ИИ человеком"),
        "TEAM_NO_WRITTEN_AGREEMENTS" or "TEAM_KEY_PERSON_UNDOCUMENTED" or "TEAM_UNCLEAR_TERMS" => ("договоренности с командой", "Уточнить и оформить договоренности с командой"),
        "TEAM_WORK_FORMAT_MISMATCH" or "TEAM_FOREIGN_TEAM_REVIEW" => ("оформление работы команды", "Проверить оформление фактического формата работы команды"),
        "TEAM_CONFIDENTIALITY_GAP" => ("конфиденциальность", "Закрепить конфиденциальность"),
        "TEAM_RIGHTS_TO_WORK_GAP" => ("права на разработки команды", "Оформить права на разработки команды"),
        "TEAM_ACCESS_CONTROL_GAP" or "TEAM_PERSONAL_ACCOUNT_DEPENDENCY" or "TEAM_FORMER_ACCESS_RISK" => ("доступы команды", "Упорядочить доступы команды"),
        "TEAM_OFFBOARDING_GAP" => ("передачу дел", "Доработать передачу дел при уходе"),
        "TEAM_KEY_PERSON_DEPENDENCY" => ("зависимость от ключевых людей", "Снизить зависимость от ключевых людей"),
        "TEAM_EQUITY_PROMISE" => ("обещанные доли команде", "Урегулировать обещанные доли команде"),
        "CONTRACTS_NOT_FORMALIZED" => ("оформление сделок", "Оформить выявленные договоренности с контрагентами"),
        "CONTRACT_SCOPE_UNCLEAR" => ("предмет и приемку", "Уточнить предмет договоров и приемку"),
        "CONTRACT_RISK_ALLOCATION_WEAK" => ("договорную ответственность", "Доработать распределение договорной ответственности"),
        "CONTRACT_MODEL_MISMATCH" => ("соответствие договоров работе", "Обновить договоры под фактическую работу"),
        "CONTRACT_COUNTERPARTY_DEPENDENCY" => ("зависимость от контрагента", "Урегулировать выявленную зависимость от контрагента"),
        "CONTRACT_LARGE_DEAL_REVIEW" => ("условия существенной сделки", "Проверить условия существенной сделки"),
        "INVEST_PRIOR_INVESTMENT_UNCLEAR" => ("прошлые инвестиции и права инвестора", "Урегулировать прошлые инвестиции и права инвестора"),
        "INVEST_FUTURE_CAP_TABLE_UNCLEAR" => ("структуру капитала", "Уточнить структуру капитала"),
        "INVEST_DILUTION_NOT_MODELED" => ("изменение долей", "Посчитать изменение долей"),
        "INVEST_DD_DOCS_NOT_READY" => ("комплект документов", "Собрать и проверить комплект документов"),
        "INVEST_SELF_AWARENESS_GAP" or "INVEST_ROUND_BLOCKER" => ("выявленные юридические пробелы", "Разобрать выявленные юридические пробелы"),
        "INVEST_ROUND_NOT_DEFINED" => ("параметры раунда", "Определить параметры раунда"),
        "INVEST_RUNWAY_WARNING" or "INVEST_FIN_MODEL_WEAK" => ("финансовые сценарии", "Уточнить финансовые сценарии"),
        "INVEST_METRICS_UNVERIFIABLE" => ("подтверждение показателей", "Подготовить подтверждение показателей"),
        "INVEST_TERMS_NOT_UNDERSTOOD" or "INVEST_DEAL_UNREVIEWED" => ("условия инвестиционной сделки", "Проверить условия инвестиционной сделки"),
        _ => ("прочие выявленные вопросы", "")
    };

    private static string Fit(string prefix, List<string> subjects, string suffix, int itemCount)
    {
        var task = prefix + string.Join(", ", subjects) + suffix;
        return task.Length <= MaximumTaskLength ? task :
            prefix.StartsWith("Уточнить", StringComparison.Ordinal)
                ? $"Уточнить сведения по {itemCount} выявленным вопросам этого направления; определить работу только по подтвержденным пробелам. Подробности — в плане действий."
                : $"Разобрать {itemCount} выявленных вопросов этого направления и выполнить адресные рекомендации из плана действий; неизвестные сведения сначала проверить.";
    }

    private static string LegacyTask(string description)
    {
        var first = FirstCompleteClause(Normalize(description));
        if (first.Length > 0 && first.Length <= MaximumTaskLength &&
            Regex.IsMatch(first, @"^(Уточнить|Проверить|Сверить|Собрать|Определить|Доработать|Обновить|Актуализировать|Закрепить|Подготовить|Оформить|Завершить|Урегулировать|Согласовать|Посчитать)\b"))
            return first.TrimEnd('.') + ".";
        return "Уточнить содержание выявленных вопросов и определить адресные действия по плану отчета.";
    }

    private static string FirstCompleteClause(string text) => Regex.Match(text, @"^.+?(?:[.!?](?=\s|$)|$)").Value;
    private static string Normalize(string text) => Regex.Replace(text ?? "", @"\s+", " ").Trim();
    private static string LowerFirst(string text) => char.ToLowerInvariant(text[0]) + text[1..];
    private static string ShortTitle(string title) => title switch
    {
        "Права на продукт и интеллектуальную собственность" => "Права на продукт и бренд",
        "Основатели и корпоративная структура" => "Основатели и управление",
        "Пользовательский контур и оферта" => "Продукт и пользователи",
        "Персональные данные и процессы ИИ" => "Данные и ИИ",
        "Команда и привлеченные специалисты" => "Команда и специалисты",
        "Договорная обвязка и контрагенты" => "Договоры и контрагенты",
        "Подготовка к инвестициям (Data Room & DD)" => "Подготовка к инвестициям",
        "Инвестиционные планы и подготовка" => "Инвестиционные планы",
        "Инвестиционная история и обязательства" => "Инвестиционная история",
        _ => title
    };
}
