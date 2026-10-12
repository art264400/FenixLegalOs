using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Report;

public sealed record FindingRemediation(
    string Title, string WhatToDo, string WhyNow, string ExpectedResult, bool RequiresClarification)
{
    // Clarification can coexist with proven parts of a composite finding.
    public bool HasConfirmedIssue { get; init; } = true;
}

/// <summary>Presentation of an actual finding, without expanding it into a service package.</summary>
public static class FindingRemediationBuilder
{
    public static FindingRemediation Build(RiskFinding finding, SharedFactStore? facts = null)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var source = DataBank.Risks.FirstOrDefault(r => r.Code.Equals(finding.Code, StringComparison.OrdinalIgnoreCase));
        var fallbackReason = (finding.SectionId.Length > 0 ? finding.SectionId : source?.SectionId) switch
        {
            "product" => "Выявленный вопрос влияет на понятность условий и пользовательский путь продукта.",
            "data" => "Выявленный вопрос связан с обработкой персональных данных и согласованностью документов с фактическими процессами.",
            _ => "Рекомендация связана с выявленным в анкете вопросом."
        };
        var reason = First(finding.WhyItMatters, source?.WhyItMatters, fallbackReason);

        string? Value(string key, string questionId = "")
        {
            if (facts != null && facts.Facts.TryGetValue(key, out var value) && value != null)
                return value is bool b ? (b ? "true" : "false") : value.ToString();
            var answer = finding.Basis.FirstOrDefault(b => b.QuestionId.Equals(questionId, StringComparison.OrdinalIgnoreCase))?.AnswerId;
            // Basis normally contains normalized values; also accept historic raw answers.
            return (questionId, answer) switch
            {
                ("PROD-13A" or "PROD-18A" or "PROD-18B" or "DATA-10A" or "DATA-11" or "AI-03" or "AI-04" or "AI-06A", "no") => "none",
                ("PROD-15", "not_explained") => "none",
                ("AI-05", "sometimes" or "core") => "true",
                ("AI-06", "user_data") => "true",
                ("INVEST-02A", "no") => "none",
                _ => answer
            };
        }

        var newRoundPlanned = Value("investment.timing") is "6_12m" or "3_6m" or "active_search" or "specific_investor" or "terms_received";
        reason = InvestmentNarrativePresentation.GetWhyItMatters(finding.Code, reason, newRoundPlanned);

        FindingRemediation Do(string title, string what, string result) => new(title, what, reason, result, false);
        FindingRemediation Check(string title, string what, string result) => new(title, what,
            "Ответы не позволяют подтвердить состояние этого вопроса. Сначала нужны сведения и проверка, затем — работа с установленными недостатками.", result, true) { HasConfirmedIssue = false };
        FindingRemediation KnownOrCheck(string? answer, string title, string what, string result,
            string checkTitle, string checkWhat) => IsUnknown(answer)
                ? Check(checkTitle, checkWhat, "Проверка завершена; подтвержденные недостатки и необходимые действия определены.")
                : Do(title, what, result);

        switch (finding.Code.ToUpperInvariant())
        {
            case "INVEST_PRIOR_INVESTMENT_UNCLEAR":
            {
                var parts = new List<FindingRemediation>();
                var status = Value("investment.priorInvestmentStatus", "INVEST-02");
                var rights = Value("investment.priorRightsClarity", "INVEST-02A");
                if (status is "partial" or "informal")
                    parts.Add(Do("Завершить оформление прошлых инвестиций",
                        "Собрать документы и переводы по прошлым инвестициям; завершить неполное оформление действующих договоренностей и зафиксировать возникшие обязательства.",
                        "Действующие договоренности по прошлым инвестициям подтверждены документами."));
                else if (IsUnknown(status))
                    parts.Add(Check("Уточнить оформление прошлых инвестиций",
                        "Проверить, какие документы по прошлым инвестициям уже оформлены; завершить оформление только подтвержденных пробелов.",
                        "Состояние оформления прошлых инвестиций проверено."));
                if (rights is "unclear" or "none" or "unknown")
                    parts.Add(KnownOrCheck(rights, "Определить права прошлого инвестора",
                        "Определить права и обязательства по действующим договоренностям с прошлым инвестором и отразить их в единой структуре капитала.",
                        "Права прошлого инвестора и связанные обязательства определены и учтены в структуре капитала.",
                        "Уточнить права прошлого инвестора",
                        "Проверить документы и уточнить, какие права прошлого инвестора уже определены, а какие требуют согласования; зафиксировать только подтвержденные уточнения."));
                if (parts.Count > 0) return Combine(parts);
                break;
            }
            case "PROD_RULES_MISSING":
            case "PROD_NO_TERMS_OF_SERVICE":
                return KnownOrCheck(Value("product.userRulesStatus", "PROD-04"),
                    "Подготовить пользовательские условия",
                    "Подготовить условия под фактическую модель продукта и определить, как пользователь принимает их перед использованием сервиса.",
                    "Условия использования продукта подготовлены, порядок их принятия определен.",
                    "Проверить наличие пользовательских условий",
                    "Уточнить, какие условия уже действуют и как пользователь их принимает; подготовить недостающие условия только если их отсутствие подтвердится.");
            case "PROD_RULES_MISMATCH":
            case "PROD_RULES_DISCREPANCY":
            case "PROD_TERMS_MISMATCH":
                return KnownOrCheck(Value("product.rulesMatch", "PROD-05"),
                    "Актуализировать пользовательские условия",
                    "Сопоставить существующие пользовательские условия с фактической моделью и пользовательским путем. Обновить положения, которые расходятся с работой продукта, и зафиксировать новую версию.",
                    "Действующая версия условий соответствует фактической модели продукта.",
                    "Проверить актуальность пользовательских условий",
                    "Сверить существующие условия с фактической моделью продукта; доработать только обнаруженные расхождения.");
            case "PROD_OFFER_UNCLEAR":
                return KnownOrCheck(Value("product.offerClarity", "PROD-06"),
                    "Уточнить описание продукта до оплаты",
                    "Сверить описание продукта и обещания пользователю с фактической функциональностью. Уточнить неясные условия и ограничения в интерфейсе, маркетинговых материалах и соответствующих положениях действующих правил.",
                    "Пользователь до оплаты видит согласованное описание продукта и существенных ограничений.",
                    "Проверить ясность предложения пользователю",
                    "Проверить, что пользователь узнает о функциональности и ограничениях до оплаты; при подтвержденных расхождениях исправить соответствующие описания и положения правил.");
            case "PROD_ROLE_UNCLEAR":
                return KnownOrCheck(Value("product.roleClarity", "PROD-07A"),
                    "Уточнить роли сторон в продукте",
                    "Определить фактическую роль компании и других участников; уточнить соответствующие положения существующих правил и описание ролей в интерфейсе.",
                    "Пользователь понимает, кто оказывает услугу и за какие действия отвечает каждая сторона.",
                    "Проверить описание ролей сторон",
                    "Уточнить фактическую роль компании и других участников, затем проверить ее отражение в правилах и интерфейсе; исправить только подтвержденные пробелы.");
            case "PROD_ACCEPTANCE_WEAK":
            {
                var acceptance = Value("product.termsAcceptance", "PROD-08");
                var evidence = Value("product.acceptanceEvidence", "PROD-09");
                if (acceptance == "explicit" || acceptance == null && evidence != null)
                    return KnownOrCheck(evidence,
                        "Подтвердить факт принятия условий",
                        "Настроить сохранение доказательств уже используемого акцепта: пользователя, времени согласия и применимой версии условий.",
                        "Факт принятия пользователем конкретной версии условий можно подтвердить.",
                        "Проверить доказательства принятия условий",
                        "Проверить, какие сведения об уже используемом акцепте сохраняются и позволяют ли они подтвердить пользователя и версию условий; устранить только выявленные пробелы.");
                return KnownOrCheck(acceptance,
                    "Доработать принятие пользовательских условий",
                    acceptance == "no_rules"
                        ? "Подготовить недостающие пользовательские условия и встроить их явное принятие в соответствующий шаг пользовательского пути; сохранять факт согласия с применимой версией."
                        : "Доработать способ принятия действующих условий: встроить явное согласие в соответствующий шаг пользовательского пути и сохранять факт принятия применимой версии.",
                    "В пользовательском пути есть подтверждаемое принятие применимой версии условий.",
                    "Проверить порядок принятия условий",
                    "Уточнить, как пользователь принимает действующие условия и какие доказательства сохраняются; доработать акцепт только в части подтвержденных недостатков.");
            }
            case "PROD_PAYMENT_TRANSPARENCY":
                return KnownOrCheck(Value("product.priceTransparency", "PROD-11"),
                    "Раскрыть полную стоимость до оплаты",
                    "Перенести информацию о полной цене и дополнительных платежах на шаг до подтверждения оплаты; согласовать соответствующие положения действующих условий с интерфейсом.",
                    "Полная стоимость и дополнительные платежи известны до оплаты.",
                    "Проверить раскрытие полной стоимости",
                    "Проверить, какие сведения о цене и дополнительных платежах доступны до оплаты; исправить подтвержденные недостатки раскрытия.");
            case "PROD_REFUND_RULES":
            case "PROD_REFUND_POLICY_GAP":
                return KnownOrCheck(Value("product.refundRules", "PROD-12"),
                    "Уточнить правила возвратов",
                    "Определить применяемую политику возвратов и уточнить соответствующие положения существующих условий и пользовательский путь обращения за возвратом.",
                    "Пользователю и команде понятен применимый порядок возвратов.",
                    "Проверить правила возвратов",
                    "Уточнить действующую политику возвратов и проверить ее отражение в правилах и интерфейсе; доработать подтвержденные пробелы.");
            case "PROD_SUBSCRIPTION_RULES":
            case "PROD_SUBSCRIPTION_AUTO_RENEWAL":
            {
                var parts = new List<FindingRemediation>();
                var renewal = Value("product.autoRenewDisclosure", "PROD-13A");
                var autoRenew = Value("product.autoRenew", "PROD-13");
                if ((autoRenew == "true" || autoRenew == null && finding.Basis.Any(b => b.QuestionId == "PROD-13A")) &&
                    renewal is "terms_only" or "none" or "unknown")
                    parts.Add(KnownOrCheck(renewal, "Уточнить раскрытие автопродления",
                        "Явно показывать до оплаты условия автопродления и последующих списаний; согласовать этот блок с действующими правилами.",
                        "Условия автопродления и списаний раскрыты до оплаты.",
                        "Проверить раскрытие автопродления",
                        "Проверить, как до оплаты раскрываются автопродление и последующие списания; исправить только подтвержденные недостатки."));
                var cancellation = Value("product.subscriptionCancellation", "PROD-14");
                if (cancellation is "complex" or "undefined")
                    parts.Add(Do("Доработать отмену подписки",
                        "Определить понятный порядок отмены подписки, устранить лишние препятствия и отразить порядок в соответствующем разделе действующих условий.",
                        "Отмена подписки доступна по понятному и работоспособному сценарию."));
                if (Value("product.trialDisclosure", "PROD-15") == "none")
                    parts.Add(Do("Раскрыть списание после пробного периода",
                        "До начала пробного периода явно объяснить переход к платной подписке и последующее списание; согласовать этот блок с действующими условиями.",
                        "Пользователь заранее знает о переходе пробного периода в платную подписку."));
                if (parts.Count > 0) return Combine(parts);
                break;
            }
            case "PROD_ACCOUNT_RESTRICTIONS":
            {
                var parts = new List<FindingRemediation>();
                var suspension = Value("product.suspensionRules", "PROD-16");
                if (suspension is "case_by_case" or "none" or "unknown")
                    parts.Add(KnownOrCheck(suspension, "Уточнить основания ограничения доступа",
                        "Уточнить в действующих правилах основания ограничения доступа и порядок их применения; согласовать с фактической модерацией.",
                        "Основания и порядок ограничения доступа определены и последовательно применяются.",
                        "Проверить основания ограничения доступа",
                        "Уточнить действующие основания и порядок ограничения доступа; исправить только выявленные пробелы."));
                var payment = Value("product.suspensionPaymentRules", "PROD-17");
                var paid = Value("product.paid");
                if ((paid == "true" || paid == null && finding.Basis.Any(b => b.QuestionId == "PROD-17")) &&
                    payment is "individual" or "undefined" or "unknown")
                    parts.Add(KnownOrCheck(payment, "Уточнить расчеты при ограничении доступа",
                        "Уточнить в действующих правилах судьбу оплаченного периода и денежных средств при ограничении доступа; согласовать с обработкой таких случаев командой.",
                        "Определено, что происходит с оплатой при ограничении доступа.",
                        "Проверить расчеты при ограничении доступа",
                        "Уточнить, что происходит с оплатой при ограничении доступа, и проверить согласованность правил с практикой; доработать подтвержденные пробелы."));
                if (parts.Count > 0) return Combine(parts);
                break;
            }
            case "PROD_USER_CONTENT_RULES":
            case "PROD_UGC_UNREGULATED":
            {
                var parts = new List<FindingRemediation>();
                var restrictions = Value("product.ugcRestrictions", "PROD-18A");
                if (restrictions is "none" or "general" or "unknown")
                    parts.Add(KnownOrCheck(restrictions, "Уточнить ограничения пользовательского контента",
                        "Уточнить в действующих правилах запрещенный пользовательский контент и согласовать ограничения с модерацией.",
                        "Запрещенный пользовательский контент определен, правила согласованы с модерацией.",
                        "Проверить ограничения пользовательского контента",
                        "Проверить существующие ограничения пользовательского контента и их применение; доработать только подтвержденные пробелы."));
                var use = Value("product.ugcUseRules", "PROD-18B");
                if (use is "none" or "partial" or "unknown")
                    parts.Add(KnownOrCheck(use, "Уточнить права на использование контента",
                        "Уточнить в соответствующем разделе действующих правил права компании на фактические способы использования пользовательских материалов.",
                        "Правила охватывают фактические способы использования компанией пользовательских материалов.",
                        "Проверить права на использование контента",
                        "Проверить, какие права на пользовательские материалы уже предусмотрены и соответствуют ли они фактическому использованию; оформить только подтвержденные недостающие права."));
                var complaint = Value("product.ugcComplaint", "PROD-19");
                if (complaint is "no" or "partial" or "unknown")
                    parts.Add(KnownOrCheck(complaint, "Доработать процедуру жалоб на контент",
                        "Доработать прием и рассмотрение жалоб на пользовательский контент; описать применяемую процедуру в соответствующем разделе действующих правил.",
                        "Работает понятная процедура приема и рассмотрения жалоб на контент.",
                        "Проверить процедуру жалоб на контент",
                        "Уточнить действующую процедуру приема и рассмотрения жалоб на контент; исправить только подтвержденные пробелы."));
                if (IsUnknown(Value("product.ugc", "PROD-18")))
                    return Check("Уточнить использование пользовательского контента",
                        "Проверить, размещают ли пользователи собственные материалы. Если такой сценарий подтвердится, выполнить относящиеся к нему действия: " +
                        (parts.Count > 0 ? Join(parts.Select(p => p.WhatToDo)) : "проверить условия работы с пользовательскими материалами."),
                        "Установлен фактический сценарий использования контента; действия определены только для применимых правил.");
                if (parts.Count > 0) return Combine(parts);
                break;
            }
            case "PROD_MINORS_REVIEW":
            case "PROD_AGE_GATE_MISSING":
                return KnownOrCheck(Value("product.minorsReview", "PROD-20A"),
                    "Проверить сценарии с несовершеннолетними",
                    "Проверить применимые к фактическому сценарию требования для несовершеннолетних на ключевых рынках; дополнить проведенную проверку и необходимые ограничения.",
                    "Определены и реализованы применимые требования для сценариев с несовершеннолетними.",
                    "Уточнить проверку сценариев с несовершеннолетними",
                    "Уточнить, какая проверка сценариев с несовершеннолетними уже выполнена; закрыть только подтвержденные пробелы.");
            case "PROD_MULTI_COUNTRY_REVIEW":
            case "PROD_CROSS_BORDER_CONSUMER_RISK":
                return KnownOrCheck(Value("product.multiCountryReview", "PROD-21A"),
                    "Проверить требования основных рынков",
                    "Определить основные страны пользователей, дополнить проверку применимых локальных требований и адаптировать только затронутые положения продуктовых документов.",
                    "Применимые требования основных рынков проверены, необходимые положения документов уточнены.",
                    "Уточнить проверку требований основных рынков",
                    "Уточнить географию пользователей и выполненную проверку локальных требований; определить необходимую доработку по подтвержденным пробелам.");
            case "PROD_REGULATORY_REVIEW":
            case "PROD_REGULATORY_RISK":
                return Do("Проверить специальные требования к функциям продукта",
                    "Описать фактическую роль компании и потоки в отмеченных в анкете специальных функциях, определить страны запуска и проверить применимые специальные требования.",
                    "Определены применимые специальные требования к указанным функциям и необходимые действия.");
            case "DATA_THIRD_PARTY_UNKNOWN":
            {
                // The rule stores only its first trigger in Basis. Read both actual
                // facts so an unknown map cannot hide a proven unreviewed vendor.
                var parts = new List<FindingRemediation>();
                var map = Value("data.externalServiceMap", "DATA-10A");
                var review = Value("data.vendorTermsReview", "DATA-11");
                if (map is "partial" or "none" or "unknown")
                    parts.Add(KnownOrCheck(map, "Дополнить карту внешних сервисов",
                        "Дополнить перечень внешних сервисов: указать передаваемые каждому сервису данные и цели передачи.",
                        "Карта внешних сервисов отражает фактические передачи данных.",
                        "Уточнить карту внешних сервисов",
                        "Проверить, составлен ли перечень внешних сервисов и определены ли данные и цели передачи каждому из них; дополнить подтвержденные пробелы."));
                if (review is "known_not_reviewed" or "none" or "unknown")
                    parts.Add(KnownOrCheck(review, "Проверить условия внешних сервисов",
                        "Проверить существенные условия работы внешних сервисов с данными и зафиксировать применимые условия для фактически используемых сервисов.",
                        "Условия обработки данных фактически используемыми внешними сервисами проверены.",
                        "Уточнить проверку условий внешних сервисов",
                        "Уточнить, какие условия внешних сервисов уже проверены; проверить только оставшиеся существенные условия работы с данными."));
                if (parts.Count > 0) return Combine(parts);
                break;
            }
            case "AI_SENSITIVE_DATA_TRANSFER":
            {
                // Known disclosure/provider gaps can coexist with an unknown data
                // category; their reason must not assert that sensitive data is sent.
                reason = "Использование внешнего ИИ требует согласованности фактических потоков данных, пользовательской информации и условий поставщика.";
                var parts = new List<FindingRemediation>();
                var sensitive = Value("ai.sensitiveDataSent", "AI-05");
                var disclosure = Value("ai.userDisclosure", "AI-03");
                var provider = Value("ai.providerTermsReview", "AI-04");
                // "sensitive_transfer" is a synthetic rule marker, not confirmation
                // of the sensitiveDataSent fact; the rule also accepts unknown.
                if (sensitive is "true")
                    parts.Add(Do("Проверить передачу чувствительных данных во внешний ИИ",
                        "Определить необходимость передачи чувствительных данных во внешний ИИ и проверить настройки хранения и дальнейшего использования данных выбранным сервисом.",
                        "Объем передачи чувствительных данных обоснован, настройки внешнего сервиса проверены."));
                else
                    parts.Add(Check("Уточнить передачу чувствительных данных во внешний ИИ",
                        "Установить, попадают ли чувствительные данные во внешний ИИ. При подтверждении передачи определить ее необходимость и проверить настройки хранения и дальнейшего использования данных сервисом.",
                        "Факт передачи чувствительных данных установлен; необходимые ограничения определены для подтвержденного сценария."));
                if (disclosure is "partial" or "none")
                    parts.Add(Do("Уточнить информирование о внешнем ИИ",
                        "Доработать соответствующие положения действующих документов и интерфейса о фактическом использовании внешнего ИИ и передаваемых ему данных.",
                        "Пользовательская информация соответствует фактическому использованию внешнего ИИ."));
                else if (disclosure == null || IsUnknown(disclosure))
                    parts.Add(Check("Проверить информирование о внешнем ИИ",
                        "Проверить, что пользователю уже сообщается об использовании внешнего ИИ; дополнить только подтвержденные пробелы.",
                        "Состояние информирования проверено, необходимые уточнения определены."));
                if (provider is "not_specific" or "none")
                    parts.Add(Do("Проверить условия выбранного режима внешнего ИИ",
                        "Проверить условия выбранного режима внешнего сервиса ИИ о хранении и дальнейшем использовании данных; зафиксировать применимые ограничения.",
                        "Условия выбранного режима внешнего ИИ проверены, применимые ограничения зафиксированы."));
                else if (provider == null || IsUnknown(provider))
                    parts.Add(Check("Уточнить проверку условий внешнего ИИ",
                        "Уточнить, проверялись ли условия выбранного режима внешнего ИИ о хранении и дальнейшем использовании данных; закрыть подтвержденные пробелы проверки.",
                        "Объем проведенной проверки условий внешнего ИИ подтвержден."));
                return Combine(parts);
            }
            case "AI_TRAINING_NOT_DISCLOSED":
            {
                var training = Value("ai.trainingUse", "AI-06");
                var disclosure = Value("ai.trainingDisclosure", "AI-06A");
                if (IsUnknown(training) || training == "possible_undefined")
                    return Check("Уточнить использование данных для развития ИИ",
                        "Установить, используются ли пользовательские данные для обучения или улучшения модели. Если такое использование подтвердится, проверить его раскрытие и доработать соответствующие положения действующих документов и пользовательскую механику.",
                        "Фактическое использование данных для развития ИИ установлено; относящиеся к нему условия уточнены.");
                return KnownOrCheck(disclosure, "Уточнить информирование об использовании данных для развития ИИ",
                    "Определить данные, фактически используемые для обучения или улучшения модели; проверить возможность ограничения набора и доработать соответствующие положения действующих документов и пользовательскую механику.",
                    "Пользовательская информация соответствует фактическому использованию данных для развития ИИ.",
                    "Проверить информирование об использовании данных для развития ИИ",
                    "Уточнить, как пользователю объясняется использование данных для обучения или улучшения модели; доработать только подтвержденные пробелы в соответствующих положениях документов и интерфейсе.");
            }
            case "DATA_PRIVACY_NOTICE_OUTDATED":
            case "DATA_PRIVACY_POLICY_INADEQUATE":
                // An unknown match answer does not erase a proven old/template notice used with AI.
                var outdatedKnown = Value("data.privacyNoticeMatch", "DATA-07") is "changed" or "template_unchecked" ||
                    Value("data.privacyNotice", "DATA-06") is "old" or "template" && Value("ai.used") == "true";
                if (!outdatedKnown && HasUnknownBasis(finding))
                    return Check("Проверить актуальность политики конфиденциальности",
                        "Сверить существующую политику с фактическими потоками данных и используемыми сервисами; обновить только подтвержденные расхождения.",
                        "Проверена актуальность политики, подтвержденные расхождения устранены.");
                return Do("Актуализировать политику конфиденциальности",
                    "Сопоставить существующую политику конфиденциальности с фактическими потоками данных и обновить расходящиеся положения о целях, данных и используемых сервисах.",
                    "Актуальная политика описывает фактическую обработку данных и используемые сервисы.");
            case "DATA_PRIVACY_NOTICE_MISSING":
            case "DATA_NO_PRIVACY_POLICY":
            case "DATA_PRIVACY_MISSING":
                return KnownOrCheck(Value("data.privacyNotice", "DATA-06"),
                    Value("data.privacyNotice", "DATA-06") == "preparing" ? "Завершить подготовку политики конфиденциальности" : "Подготовить политику конфиденциальности",
                    Value("data.privacyNotice", "DATA-06") == "preparing"
                        ? "Завершить уже начатую подготовку политики конфиденциальности по фактическим потокам данных и сделать ее доступной пользователям."
                        : "Подготовить политику конфиденциальности по фактическим потокам данных и сделать ее доступной пользователям.",
                    "Пользователям доступна политика, описывающая фактическую обработку данных.",
                    "Проверить наличие политики конфиденциальности",
                    "Уточнить наличие и статус политики конфиденциальности; подготовить или завершить документ только при подтвержденном недостатке.");
        }

        var recommendation = InvestmentNarrativePresentation.GetRecommendation(finding.Code,
            First(Join(InvestmentNarrativePresentation.GetRecommendations(finding.Code, finding.Recommendations, newRoundPlanned)),
                finding.Recommendation,
                source == null ? null : Join(InvestmentNarrativePresentation.GetRecommendations(finding.Code, source.Recommendations, newRoundPlanned)),
                source?.Recommendation),
            newRoundPlanned);
        var title = First(finding.Title, source?.Title, "Проверить выявленный вопрос");
        var unincorporated = Value("company.entityStatus") is "not_incorporated" or "none" or "no_entity";
        var founderIp = finding.Code is "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED" or "IP_FOUNDER_RIGHTS_MISSING" or "IP_FOUNDERS_NOT_ASSIGNED";
        var overallIp = finding.Code is "IP_PRODUCT_RIGHTS_UNCONFIRMED" or "IP_RIGHTS_NOT_TRANSFERRED" or "IP_CHAIN_OF_TITLE_BROKEN";
        if (unincorporated && (founderIp || overallIp))
        {
            var what = founderIp
                ? "Оформить соглашения между основателями о создании и последующей передаче прав на интеллектуальную собственность в создаваемую компанию."
                : "Провести инвентаризацию созданных компонентов и зафиксировать обязательства авторов и подрядчиков по отчуждению прав в создаваемую компанию.";
            var result = founderIp
                ? "Права на исходный код юридически зафиксированы за создателями с обязательством передачи в создаваемую компанию."
                : "Права на созданные результаты юридически зафиксированы за создателями с обязательством передачи в создаваемую компанию.";
            if (HasUnknownBasis(finding))
                return Check("Проверить оформление прав до создания компании",
                    "Проверить существующие документы и обязательства по передаче прав в создаваемую компанию. При подтвержденных пробелах: " + what, result);
            return Do(title, what, result);
        }
        if (HasUnknownBasis(finding))
        {
            var questions = finding.Basis.Where(IsUnknownBasis)
                .Select(b => DataBank.Questions.FirstOrDefault(q => q.Id == b.QuestionId)?.Question)
                .Where(q => !string.IsNullOrWhiteSpace(q)).Select(q => q!.TrimEnd('?', ' ', '.'));
            var subject = Join(questions, "; ");
            var what = subject.Length > 0
                ? "Уточнить сведения по вопросам: " + subject + "."
                : "Уточнить неизвестные сведения, послужившие основанием этого вывода.";
            if (!string.IsNullOrWhiteSpace(recommendation))
                what += " После проверки выполнить применимую к подтвержденным недостаткам часть рекомендации: " + recommendation;
            return Check("Уточнить сведения и проверить выявленный вопрос", what,
                "Неизвестные сведения уточнены; подтвержденные недостатки устранены в пределах выявленного вопроса.");
        }
        if (recommendation.Length == 0)
            recommendation = "Проверить вопрос «" + title.TrimEnd('.') + "» и определить адресные действия по его урегулированию.";
        var expectedResult = finding.Code switch
        {
            "INVEST_FUTURE_CAP_TABLE_UNCLEAR" => "Подготовлен единый расчет структуры долей с учетом действующих прав и обязательств.",
            "INVEST_DILUTION_NOT_MODELED" => "Посчитано изменение долей для выбранных сценариев суммы и оценки.",
            "INVEST_DD_DOCS_NOT_READY" => "Ключевые документы собраны в актуальную структуру; выявленные документальные пробелы устранены.",
            "INVEST_SELF_AWARENESS_GAP" => "Команда понимает выявленные юридические пробелы и порядок их устранения.",
            _ => "Выполнены рекомендации по выявленному вопросу, результат подтвержден документами или действующим процессом."
        };
        return Do(title, recommendation, expectedResult);
    }

    private static bool HasUnknownBasis(RiskFinding finding) => finding.Basis.Any(IsUnknownBasis);
    private static bool IsUnknownBasis(RiskFindingBasis basis) => IsUnknown(basis.AnswerId) ||
        DataBank.Questions.FirstOrDefault(q => q.Id == basis.QuestionId)?.Options?
            .Any(o => o.Id == basis.AnswerId && o.ConfidenceClass == ConfidenceClass.Unknown) == true;
    private static bool IsUnknown(string? value) => value is "unknown" or "not_sure" or "not_known";
    private static string First(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? "";
    private static string Join(IEnumerable<string> values, string separator = " ") => string.Join(separator,
        values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
    private static FindingRemediation Combine(IReadOnlyList<FindingRemediation> parts) => new(
        Join(parts.Select(p => p.Title), "; "), Join(parts.Select(p => p.WhatToDo)),
        Join(parts.Select(p => p.WhyNow)), Join(parts.Select(p => p.ExpectedResult)), parts.Any(p => p.RequiresClarification))
        { HasConfirmedIssue = parts.Any(p => p.HasConfirmedIssue) };
}
