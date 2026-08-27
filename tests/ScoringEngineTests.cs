using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class ScoringEngineTests
{
    private readonly ScoringEngine _engine;
    private readonly QuestionRepository _repository;
    private readonly string _tempDbPath;

    public ScoringEngineTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repository = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(_repository);
    }

    [Fact(DisplayName = "1.1 Сооснователи: Дедлок 50/50, устные договорённости и конфликт активируют критический риск")]
    public void Deadlock_50_50_Should_Trigger_Critical_Risk()
    {
        // Сценарий: 2 сооснователя, устные договорённости, спор по долям, отсутствует механизм выхода из дедлока
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "dispute",
            ["FND-C04"] = "none",
            ["FND-01"] = "active_conflict",
            ["FND-02"] = "disputed",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "none",
            ["COR-C01"] = "none"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        var foundersSec = result.Sections.FirstOrDefault(s => s.SectionId == "founders");
        Assert.NotNull(foundersSec);
        Assert.True(foundersSec.Score <= 20, $"Ожидался низкий балл по сооснователям при критическом конфликте и дедлоке, получено: {foundersSec.Score}");
        Assert.True(result.Overall <= 50, $"Ожидался общий балл <= 50, получено: {result.Overall}");
        Assert.True(result.CriticalCount >= 1, $"Ожидался минимум 1 критический риск, получено: {result.CriticalCount}");
        
        var hasDeadlockRisk = result.Risks.Any(r => 
            r.Severity is "CRITICAL" or "HIGH" &&
            (r.Code.StartsWith("FND_", StringComparison.OrdinalIgnoreCase) || 
             r.Code.Contains("FOUNDERS", StringComparison.OrdinalIgnoreCase) || 
             r.Title.Contains("доли", StringComparison.OrdinalIgnoreCase) || 
             r.Title.Contains("тупик", StringComparison.OrdinalIgnoreCase) ||
             r.Title.Contains("основател", StringComparison.OrdinalIgnoreCase)));
        
        Assert.True(hasDeadlockRisk, "Ожидалось наличие риска, связанного с сооснователями или дедлоком");
    }

    [Fact(DisplayName = "1.2 Сооснователи: Соло-фаундер — блок сооснователей признаётся применимым со 100 баллами без ложных рисков")]
    public void Solo_Founder_Should_Mark_Founders_Section_Applicable_Or_Skipped()
    {
        // Сценарий: Единственный основатель (соло-фаундер)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "kz_llp",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 0);
        var foundersSec = result.Sections.FirstOrDefault(s => s.SectionId == "founders");
        Assert.NotNull(foundersSec);
        Assert.Equal("APPLICABLE", foundersSec.Status);
        Assert.Equal(100, foundersSec.Score);
    }

    [Fact(DisplayName = "2.1 Корпоративная структура: Соло-фаундер с группой компаний корректно рассчитывает скоринг")]
    public void Solo_Founder_With_Multiple_Entities_Should_Score_Correctly()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "kz",
            ["COR-C02B"] = "3",
            ["COR-01"] = "dispute",
            ["COR-02"] = "none",
            ["COR-03"] = "unclear_terms",
            ["COR-04"] = "missing",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-08"] = "organized",
            ["COR-07_GROUP"] = "minor_exceptions",
            ["COR-T01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.True(result.Overall > 0, $"Ожидался Overall > 0, получено: {result.Overall}");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("APPLICABLE", corpSec.Status);
        Assert.NotNull(corpSec.Score);
    }

    [Fact(DisplayName = "2.2 Корпоративная структура: Pre-incorporation на стадии идеи без деятельности не активирует риск отсутствия юрлица")]
    public void Pre_Incorporation_Idea_Stage_Should_Not_Trigger_No_Entity_Risk()
    {
        // Сценарий: Соло-фаундер, юрлица нет, но нет и активной коммерческой деятельности
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.DoesNotContain(result.Risks, r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        Assert.NotNull(corpSec);
        Assert.Equal("N_A", corpSec.Status);
    }

    [Fact(DisplayName = "2.3 Корпоративная структура: Pre-incorporation с коммерческой выручкой или нанятой командой активирует HIGH риск COR_NO_ENTITY_FOR_ACTIVITY")]
    public void Pre_Incorporation_With_Active_Revenue_Or_Team_Should_Trigger_COR_NO_ENTITY_FOR_ACTIVITY()
    {
        // Сценарий: Юрлица нет, но проект ведёт коммерческую деятельность с командой и выручкой
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["TEAM-C01"] = "contractors",
            ["REV-01"] = "active"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        var finding = result.Risks.FirstOrDefault(r => r.Code == "COR_NO_ENTITY_FOR_ACTIVITY");
        Assert.NotNull(finding);
        Assert.Equal("HIGH", finding.Severity);
        Assert.Equal("ENTITY_ALIGNMENT", finding.RootCauseGroup);
    }

    [Fact(DisplayName = "2.4 Идеальный юридический сетап: Полное соблюдение лучших практик дает высокий балл (Overall >= 75) и 0 критических рисков")]
    public void All_Best_Practices_Should_Produce_High_Score()
    {
        // Сценарий: Полностью оформленные сооснователи с подписанным SHA, вестингом и выходом из дедлока
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "3",
            ["FND-C03"] = "none",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "full",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "clean"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall >= 75, $"Ожидался балл >= 75, получено: {result.Overall}");
        Assert.Equal(0, result.CriticalCount);
    }

    [Fact(DisplayName = "2.5 Корпоративная структура: Одно юрлицо формирует структуру фактов и нарратив Казахстана")]
    public void Single_Company_Builds_Single_Narrative_And_Calculates_COR07()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, facts["company.entityCount"]);
        Assert.Equal(false, facts["company.groupStructure"]);
        Assert.Equal("kz", facts["company.primaryJurisdiction"]);
        
        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("Казахстан", narrative);
        Assert.Contains("одну компанию", narrative);
    }

    [Fact(DisplayName = "2.6 Корпоративная структура: Группа компаний формирует холдинговый нарратив МФЦА с ролями сущностей")]
    public void Group_Structure_Builds_Detailed_Narrative_With_Roles()
    {
        // Arrange
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "aifc",
            ["COR-C02B"] = "2",
            ["COR-C02C"] = new List<Dictionary<string, object>>
            {
                new()
                {
                    ["jurisdiction"] = "kz",
                    ["roles"] = new List<string> { "clients", "payments" }
                }
            },
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07_GROUP"] = "aligned"
        };

        // Act
        var result = _engine.ComputeResult(answers);
        var facts = FactNormalizer.NormalizeFacts(answers).Facts;

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, facts["company.entityCount"]);
        Assert.Equal(true, facts["company.groupStructure"]);
        Assert.Equal("aifc", facts["company.primaryJurisdiction"]);

        var narrative = facts["company.structureNarrative"]?.ToString() ?? "";
        Assert.Contains("МФЦА", narrative);
        Assert.Contains("2 компаний", narrative);
    }

    [Fact(DisplayName = "3.1 Интеллектуальная собственность: Идеально защищенный IP-контур даёт 100 баллов и 0 критических рисков")]
    public void IP_Happy_Path_Fully_Protected_Should_Score_100()
    {
        // Сценарий: Полностью оформленные права на продукт, чистый домен и корпоративные аккаунты
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "full",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "signed",
            ["COR-04"] = "complete",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07_AIFC"] = "clean",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",

            // IP Module Answers
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "app", "web", "brand", "domain" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "all",       // overall_rights: 1.0
            ["IP-05"] = "assigned",  // founder_rights: 1.0
            ["IP-07"] = "all",       // external_creators: 1.0
            ["IP-10"] = "no",        // external_employer: 1.0
            ["IP-11"] = "no",        // 3rd party context
            ["IP-12"] = "no",        // external dependency: 1.0
            ["IP-13"] = "company",   // technical control: 1.0
            ["IP-14"] = "company",   // brand & domain: 1.0
            ["IP-15"] = "clear"      // content provenance: 1.0
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");
        Assert.NotNull(ipSec);
        Assert.Equal("APPLICABLE", ipSec.Status);
        Assert.Equal(100, ipSec.Score);
        Assert.DoesNotContain(result.Risks, r => r.SectionId == "ip" && r.Severity is "CRITICAL" or "HIGH");
    }

    [Fact(DisplayName = "3.2 Интеллектуальная собственность: Отсутствие документов на ключевой продукт активирует CRITICAL риск и подавляет дочерние разрывы")]
    public void IP_Unconfirmed_Product_Rights_Should_Trigger_Critical_And_Suppress_Gaps()
    {
        // Сценарий: Зарегистрированное юрлицо, готовый продукт, но нет документов о принадлежности (IP-04 = none)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders", "contractors", "studio" },
            ["IP-04"] = "none",              // Триггерит IP_PRODUCT_RIGHTS_UNCONFIRMED (CRITICAL)
            ["IP-05"] = "agreed",            // IP_FOUNDER_RIGHTS_NOT_TRANSFERRED (должен быть подавлен)
            ["IP-07"] = "unclear_clause",    // IP_CONTRACTOR_RIGHTS_GAP (должен быть подавлен)
            ["IP-09"] = "unknown_chain"      // IP_STUDIO_RIGHTS_GAP (должен быть подавлен)
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var criticalRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.NotNull(criticalRisk);
        Assert.Equal("CRITICAL", criticalRisk.Severity);
        Assert.Equal("IP_OWNERSHIP", criticalRisk.RootCauseGroup);

        // Проверка канонического механизма подавления дублирующих рисков
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_CONTRACTOR_RIGHTS_GAP");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_STUDIO_RIGHTS_GAP");
    }

    [Fact(DisplayName = "3.3 Интеллектуальная собственность: Спор с бывшим разработчиком активирует CRITICAL риск IP_FORMER_DEVELOPER_GAP")]
    public void IP_Former_Developer_Dispute_Should_Trigger_Critical_Risk()
    {
        // Сценарий: Бывший разработчик с открытым юридическим спором
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders", "former" },
            ["IP-04"] = "main",
            ["IP-08"] = "dispute" // Триггерит IP_FORMER_DEVELOPER_GAP (CRITICAL)
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var formerRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_FORMER_DEVELOPER_GAP");
        Assert.NotNull(formerRisk);
        Assert.Equal("CRITICAL", formerRisk.Severity);
        Assert.Equal("KEY_DEVELOPER", formerRisk.RootCauseGroup);
    }

    [Fact(DisplayName = "3.4 Интеллектуальная собственность: Использование ресурсов работодателя при создании продукта триггерит CRITICAL риск служебного произведения")]
    public void IP_Moonlighting_With_Employer_Resources_Should_Trigger_Critical_Employer_Risk()
    {
        // Сценарий: Основатель создавал продукт в период найма и использовал ресурсы прежнего работодателя
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-10"] = "not_reviewed",
            ["IP-10A"] = "yes" // Ресурсы использовались -> CRITICAL
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var employerRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
        Assert.NotNull(employerRisk);
        Assert.Equal("CRITICAL", employerRisk.Severity);
        Assert.Equal("IP_EMPLOYER", employerRisk.RootCauseGroup);
    }

    [Fact(DisplayName = "3.5 Интеллектуальная собственность: Стадия идеи проходит по легкому пути без ложных критических штрафов")]
    public void IP_Idea_Stage_Should_Handle_Light_Path_Gracefully()
    {
        // Сценарий: Проект на стадии чистой идеи (IP-01 = idea)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.DoesNotContain(result.Risks, r => r.SectionId == "ip" && r.Severity is "CRITICAL" or "HIGH");
    }

    [Fact(DisplayName = "3.6 Интеллектуальная собственность: Незарегистрированный товарный знак не штрафует скоринг и создает информационный риск")]
    public void IP_Brand_Not_Registered_Should_Not_Penalize_Score_And_Create_Info_Risk()
    {
        // Сценарий: Все права оформлены, бренд пока не зарегистрирован как товарный знак
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "brand" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "no",
            ["IP-11"] = "no",
            ["IP-12"] = "no",
            ["IP-13"] = "company",
            ["IP-14"] = "brand_not_registered", // Бренд не зарегистрирован
            ["IP-15"] = "clear"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");
        Assert.NotNull(ipSec);
        Assert.Equal(100, ipSec.Score); // Отсутствие товарного знака на ранней стадии не штрафует скоринг
        var brandInfo = result.Risks.FirstOrDefault(r => r.Code == "IP_BRAND_REGISTRATION_INFO");
        Assert.NotNull(brandInfo);
        Assert.Equal("INFO", brandInfo.Severity);
    }

    [Fact(DisplayName = "3.7 Интеллектуальная собственность: Маршрутизация IP-10 → IP-10A управляет видимостью и весами вопроса о ресурсах работодателя")]
    public void IP_Routing_IP10_To_IP10A_Should_Control_Visibility_And_Weights()
    {
        // Когда IP-10 == 'no', IP-10A должен быть скрыт
        var q10A = DataBank.Questions.First(q => q.Id == "IP-10A");
        var answersNo = new Dictionary<string, object> { ["IP-10"] = "no" };
        Assert.False(ConditionsEvaluator.IsVisible(q10A.ShowIf, answersNo));

        // Когда IP-10 in [unrelated, lawyer_checked, not_reviewed, unknown], IP-10A должен быть показан
        foreach (var opt in new[] { "unrelated", "lawyer_checked", "not_reviewed", "unknown" })
        {
            var answersVisible = new Dictionary<string, object> { ["IP-10"] = opt };
            Assert.True(ConditionsEvaluator.IsVisible(q10A.ShowIf, answersVisible), $"Ожидалась видимость IP-10A при IP-10 = {opt}");
        }
    }

    [Fact(DisplayName = "3.8 Интеллектуальная собственность: Маршрутизация IP-11 → IP-11A управляет видимостью аудита сторонних компонентов")]
    public void IP_Routing_IP11_To_IP11A_Should_Control_Visibility_And_Weights()
    {
        // Когда IP-11 == 'no', IP-11A должен быть скрыт
        var q11A = DataBank.Questions.First(q => q.Id == "IP-11A");
        var answersNo = new Dictionary<string, object> { ["IP-11"] = "no" };
        Assert.False(ConditionsEvaluator.IsVisible(q11A.ShowIf, answersNo));

        // Когда IP-11 in [yes, likely, unknown], IP-11A должен быть показан
        foreach (var opt in new[] { "yes", "likely", "unknown" })
        {
            var answersVisible = new Dictionary<string, object> { ["IP-11"] = opt };
            Assert.True(ConditionsEvaluator.IsVisible(q11A.ShowIf, answersVisible), $"Ожидалась видимость IP-11A при IP-11 = {opt}");
        }
    }

    [Fact(DisplayName = "3.9 Интеллектуальная собственность: Граничные условия Rule Engine для рисков прежнего работодателя (проверка юристом vs отсутствие проверки)")]
    public void IP_Employer_Risk_Rule_Engine_Boundary_Verification()
    {
        // 1. Несвязанный профиль найма + ресурсы НЕ использовались -> НЕТ риска IP_EMPLOYER_RISK
        var cleanAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "unrelated",
            ["IP-10A"] = "no"
        };
        var res1 = _engine.ComputeResult(cleanAnswers);
        Assert.DoesNotContain(res1.Risks, r => r.Code == "IP_EMPLOYER_RISK");

        // 2. Проверено юристом + ресурсы НЕ использовались -> НЕТ риска IP_EMPLOYER_RISK
        var lawyerCheckedAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "lawyer_checked",
            ["IP-10A"] = "no"
        };
        var res2 = _engine.ComputeResult(lawyerCheckedAnswers);
        Assert.DoesNotContain(res2.Risks, r => r.Code == "IP_EMPLOYER_RISK");

        // 3. Не проверялось + ресурсы использовались на готовом продукте -> CRITICAL severity (CORE / ресурсы использовались)
        var critAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "not_reviewed",
            ["IP-10A"] = "yes"
        };
        var res3 = _engine.ComputeResult(critAnswers);
        var rCrit = res3.Risks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
        Assert.NotNull(rCrit);
        Assert.Equal("CRITICAL", rCrit.Severity);
    }

    [Fact(DisplayName = "3.10 Интеллектуальная собственность: Граничные условия Rule Engine для сторонних компонентов (системный аудит vs отсутствие аудита)")]
    public void IP_Third_Party_Components_Rule_Engine_Boundary_Verification()
    {
        // 1. Сторонние библиотеки используются + системный аудит условий -> НЕТ риска
        var cleanAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-11"] = "yes",
            ["IP-11A"] = "yes"
        };
        var res1 = _engine.ComputeResult(cleanAnswers);
        Assert.DoesNotContain(res1.Risks, r => r.Code == "IP_THIRD_PARTY_COMPONENTS");

        // 2. Сторонние библиотеки используются + условия НЕ проверялись -> Rule Engine активирует MEDIUM риск
        var riskAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-11"] = "yes",
            ["IP-11A"] = "no"
        };
        var res2 = _engine.ComputeResult(riskAnswers);
        var rTp = res2.Risks.FirstOrDefault(r => r.Code == "IP_THIRD_PARTY_COMPONENTS");
        Assert.NotNull(rTp);
        Assert.Equal("MEDIUM", rTp.Severity);
    }

    [Fact(DisplayName = "4.1 Точный скоринг: Идеальный профиль дает ровно 100 баллов, Level 'strong' и статус 'Сильная основа'")]
    public void Exact_Score_Gold_Standard_Clean_Profile_Gives_100_Overall()
    {
        // Сценарий: 100% идеальные ответы по всем направлениям
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "full",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "yes",
            ["FND-06"] = "written",
            ["FND-07"] = "full",
            ["FND-08"] = "written",
            ["COR-C01"] = "aifc",
            ["COR-02"] = "registered",
            ["COR-03"] = "signed",
            ["COR-04"] = "complete",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07_AIFC"] = "clean",
            ["COR-08"] = "organized",
            ["COR-T01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "app", "web", "brand", "domain" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "no",
            ["IP-11"] = "no",
            ["IP-12"] = "no",
            ["IP-13"] = "company",
            ["IP-14"] = "company",
            ["IP-15"] = "clear"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        Assert.Equal(100, result.Overall);
        Assert.Equal("strong", result.Level);
        Assert.Equal("Сильная основа", result.LevelTitle);
        Assert.Equal(0, result.CriticalCount);
        Assert.Equal(0, result.HighCount);
    }

    [Fact(DisplayName = "4.2 Точный скоринг: Исключение веса юрлица из знаменателя при pre-incorporation (точность взвешивания)")]
    public void Exact_Score_Pre_Incorporation_Weight_Exclusion_Math()
    {
        // Сценарий: Pre-incorporation (COR-C01 = none). Секция corporate получает статус N/A (вес 0 в знаменателе).
        // Проверяем, что общий балл вычисляется ровно как взвешенное среднее применимых секций (Founders 100%, IP 100% -> Overall = 100).
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "no",
            ["IP-11"] = "no",
            ["IP-12"] = "no",
            ["IP-13"] = "company",
            ["IP-14"] = "company",
            ["IP-15"] = "clear"
        };

        var result = _engine.ComputeResult(answers);

        Assert.NotNull(result);
        var fndSec = result.Sections.FirstOrDefault(s => s.SectionId == "founders");
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");

        Assert.Equal(100, fndSec?.Score);
        Assert.Equal("N_A", corpSec?.Status);
        Assert.Null(corpSec?.Score);
        Assert.Equal(100, ipSec?.Score);
        Assert.Equal(100, result.Overall);
    }

    [Fact(DisplayName = "4.3 Точный скоринг: Корпоративная структура — расчет балла по §23.2 и §24 (Cap table, история)")]
    public void Exact_Score_Corporate_Section_Mixed_Compliance_Calculation()
    {
        // Нормативные веса §23.2 и баллы ответов §24:
        // COR-01: match -> score 1.0 (weight 20%, within 100%)           => 20.0 * 1.0 = 20.00
        // COR-02: current_plus_separate -> score 0.8 (weight 15%, 100%)  => 15.0 * 0.8 = 12.00
        // COR-03: documented_included -> score 1.0 (weight 10%, 100%)    => 10.0 * 1.0 = 10.00
        // COR-04: main_docs -> score 0.7 (weight 15% * 70% = 10.5%)      => 10.5 * 0.7 = 7.35
        // COR-04A: yes -> score 1.0 (weight 15% * 30% = 4.5%)            => 4.5 * 1.0  = 4.50
        // COR-05: systematic -> score 1.0 (weight 12%, 100%)             => 12.0 * 1.0 = 12.00
        // COR-06: clear_limits -> score 1.0 (weight 10%, 100%)           => 10.0 * 1.0 = 10.00
        // COR-07: aligned -> score 1.0 (weight 13%, 100%)                => 13.0 * 1.0 = 13.00
        // COR-08: organized -> score 1.0 (weight 5%, 100%)               => 5.0 * 1.0  = 5.00
        // Сумма применимых весов: 20 + 15 + 10 + 10.5 + 4.5 + 12 + 10 + 13 + 5 = 100.0%
        // Взвешенная сумма: 20.0 + 12.0 + 10.0 + 7.35 + 4.50 + 12.0 + 10.0 + 13.0 + 5.0 = 93.85
        // Ожидаемый балл: Round(93.85 / 100.0 * 100) = 94%

        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "current_plus_separate",
            ["COR-03"] = "documented_included",
            ["COR-04"] = "main_docs",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized"
        };

        var result = _engine.ComputeResult(answers);
        var corpSec = result.Sections.FirstOrDefault(s => s.SectionId == "corporate");

        Assert.NotNull(corpSec);
        Assert.Equal("APPLICABLE", corpSec.Status);

        // Ручной расчёт формулы: Round(93.85) = 94
        const int expectedScore = 94;
        Assert.Equal(expectedScore, corpSec.Score);
    }

    [Fact(DisplayName = "4.4 Точный скоринг: Градация пороговых уровней [PROJECT_OVERRIDE: structural_risks < 40, material_gaps 40-59, attention 60-79, strong >= 80]")]
    public void Exact_Score_Level_Threshold_Classifications()
    {
        // ИСТОЧНИК ПОРОГОВЫХ ЗНАЧЕНИЙ: PROJECT_OVERRIDE
        // В нормативных разделах §§22–27 каноническая классификация уровней не зафиксирована (в §§22–27 описаны веса, вопросы и правила рисков).
        // Пороги уровней зафиксированы в архитектуре проекта (ScoringEngine) как константы:
        // [0..39]   -> "structural_risks" ("Структурные вопросы")
        // [40..59]  -> "material_gaps"    ("Существенные пробелы")
        // [60..79]  -> "attention"        ("Есть вопросы, требующие внимания")
        // [80..100] -> "strong"           ("Сильная основа")

        Assert.Equal("structural_risks", ScoringEngine.GetLevel(0));
        Assert.Equal("structural_risks", ScoringEngine.GetLevel(39));
        Assert.Equal("material_gaps", ScoringEngine.GetLevel(40));
        Assert.Equal("material_gaps", ScoringEngine.GetLevel(59));
        Assert.Equal("attention", ScoringEngine.GetLevel(60));
        Assert.Equal("attention", ScoringEngine.GetLevel(79));
        Assert.Equal("strong", ScoringEngine.GetLevel(80));
        Assert.Equal("strong", ScoringEngine.GetLevel(100));

        Assert.Equal("Структурные вопросы", ScoringEngine.GetLevelTitle("structural_risks"));
        Assert.Equal("Существенные пробелы", ScoringEngine.GetLevelTitle("material_gaps"));
        Assert.Equal("Есть вопросы, требующие внимания", ScoringEngine.GetLevelTitle("attention"));
        Assert.Equal("Сильная основа", ScoringEngine.GetLevelTitle("strong"));
    }

    [Fact(DisplayName = "4.5 Точный скоринг: Расчёт процента уверенности (Confidence Score) при неизвестных ответах")]
    public void Exact_Score_Confidence_Calculation_Based_On_Unknown_Answers()
    {
        // Сценарий: Все ответы известны точно -> Confidence = 100%
        var knownAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-07"] = "aligned"
        };
        var resKnown = _engine.ComputeResult(knownAnswers);
        Assert.Equal(100, resKnown.Confidence);
        Assert.Equal("Высокая определенность ответов.", resKnown.ConfidenceText);

        // Сценарий: Несколько ответов 'unknown' -> Confidence снижается
        var unknownAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C04"] = "unknown",
            ["FND-01"] = "none",
            ["FND-02"] = "unknown",
            ["FND-03"] = "unknown",
            ["FND-04"] = "unknown",
            ["FND-05"] = "unknown",
            ["FND-06"] = "unknown",
            ["FND-07"] = "unknown",
            ["FND-08"] = "unknown",
            ["COR-C01"] = "none"
        };
        var resUnknown = _engine.ComputeResult(unknownAnswers);
        Assert.True(resUnknown.Confidence < 70, $"Ожидался низкий Confidence при ответах 'unknown', получено: {resUnknown.Confidence}");
    }

    [Fact(DisplayName = "4.6 Точный скоринг: Интеллектуальная собственность — точный расчет весов IP по §23.3 и §24")]
    public void Exact_Score_IP_Section_Weighted_Components_Math()
    {
        // Нормативные веса §23.3 и баллы ответов §24:
        // IP-04 (overall_rights): main -> score 0.75 (weight 22%, within 100%)       => 22.0 * 0.75 = 16.50
        // IP-05 (founder_rights): assigned -> score 1.00 (weight 12%, within 100%)   => 12.0 * 1.00 = 12.00
        // IP-06 (employee_rights): all -> score 1.00 (weight 10%, within 100%)       => 10.0 * 1.00 = 10.00
        // IP-13 (technical_control): company -> score 1.00 (weight 8%, within 100%)  => 8.0 * 1.00  = 8.00
        // IP-14 (brand_domain): company -> score 1.00 (weight 4%, within 100%)       => 4.0 * 1.00  = 4.00
        // IP-15 (content_provenance): clear -> score 1.00 (weight 6%, within 100%)    => 6.0 * 1.00  = 6.00
        // Сумма применимых весов: 22 + 12 + 10 + 8 + 4 + 6 = 62.0%
        // Взвешенная сумма: 16.50 + 12.00 + 10.00 + 8.00 + 4.00 + 6.00 = 56.50
        // Ожидаемый нормализованный балл: Round((56.50 / 62.0) * 100) = Round(91.129) = 91%

        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code", "brand", "database" },
            ["IP-03"] = new List<string> { "founders", "employees" },
            ["IP-04"] = "main",
            ["IP-05"] = "assigned",
            ["IP-06"] = "all",
            ["IP-13"] = "company",
            ["IP-14"] = "company",
            ["IP-15"] = "clear"
        };

        var result = _engine.ComputeResult(answers);
        var ipSec = result.Sections.FirstOrDefault(s => s.SectionId == "ip");

        Assert.NotNull(ipSec);
        Assert.Equal("APPLICABLE", ipSec.Status);

        // Ручной расчёт формулы: Round((56.50 / 62.0) * 100) = 91
        const int expectedScore = 91;
        Assert.Equal(expectedScore, ipSec.Score);
    }

    [Fact(DisplayName = "5.1 Нормативная точность: FactNormalizer преобразует answerId в точные канонические факты §24")]
    public void Normative_FactNormalizer_Maps_Canonical_Enums_Correctly()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "inactive_exist",
            ["FND-C03"] = "departed_unresolved",
            ["COR-C01"] = "one",
            ["COR-02"] = "none",
            ["IP-05"] = "agreed",
            ["IP-12"] = "no",
            ["REV-01"] = "b2b_sales",
            ["TEAM-C01"] = "employees"
        };

        var store = FactNormalizer.NormalizeFacts(answers);
        var f = store.Facts;

        // Invariant §22.1 & §24: inactive_exist -> activeCount="unknown", count="multiple", inactiveExists=true
        Assert.Equal("unknown", f["founders.activeCount"]);
        Assert.Equal("multiple", f["founders.count"]);
        Assert.True((bool)f["founders.inactiveExists"]!);
        Assert.Equal("unresolved", f["founders.departedFounderStatus"]);

        // §24 Corporate mappings
        Assert.Equal("incorporated", f["company.entityStatus"]);
        Assert.Equal("unreliable", f["capital.capTableStatus"]);

        // §24 IP mappings: agreed -> agreed_not_completed, no -> none
        Assert.Equal("agreed_not_completed", f["ip.founderRights"]);
        Assert.Equal("none", f["ip.externalDependency"]);

        // Activity facts
        Assert.True((bool)f["company.hasRevenue"]!);
        Assert.True((bool)f["team.hasNonFounderTeam"]!);
    }

    [Fact(DisplayName = "5.2 Инженерная безопасность: ConditionsEvaluator fail-closed выбрасывает исключение при неизвестном операторе")]
    public void Normative_ConditionsEvaluator_Fails_Closed_On_Unknown_Operator()
    {
        var rule = new ConditionalRule
        {
            QuestionId = "FND-C01",
            Op = "unsupported_evil_operator",
            Value = "solo"
        };

        var answers = new Dictionary<string, object> { ["FND-C01"] = "solo" };

        Assert.Throws<InvalidOperationException>(() =>
        {
            ConditionsEvaluator.EvaluateRule(rule, answers, null);
        });
    }

    [Fact(DisplayName = "5.3 Изоляция Rule Engine: Ответы не создают карточки рисков в обход SharedFactStore")]
    public void Normative_Rule_Engine_Does_Not_Bypass_FactStore()
    {
        // Сценарий: ответ выбран, но факты не удовлетворяют правилу
        // IP-14 = "brand_not_registered" создаёт только INFO карточку, не создаёт ложный HIGH риск
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "brand" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-14"] = "brand_not_registered"
        };

        var result = _engine.ComputeResult(answers);

        // Должен быть только INFO риск регистрации бренда, без ложных HIGH/CRITICAL
        Assert.Equal(0, result.CriticalCount);
        Assert.Equal(0, result.HighCount);
        Assert.Contains(result.Risks, r => r.Code == "IP_BRAND_REGISTRATION_INFO" && r.Severity == "INFO");
    }

    [Fact(DisplayName = "5.4 Каноническая супрессия (§25): IP_PRODUCT_RIGHTS_UNCONFIRMED подавляет дочерние риски передачи прав")]
    public void Normative_Suppression_Suppresses_Child_Findings()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders", "contractors" },
            ["IP-04"] = "none", // Активирует родительский CRITICAL IP_PRODUCT_RIGHTS_UNCONFIRMED
            ["IP-05"] = "founder_owned", // Дочерний риск IP_FOUNDER_RIGHTS_NOT_TRANSFERRED должен быть подавлен
            ["IP-07"] = "no_contract" // Дочерний риск IP_CONTRACTOR_RIGHTS_GAP должен быть подавлен
        };

        var result = _engine.ComputeResult(answers);

        Assert.Contains(result.Risks, r => r.Code == "IP_PRODUCT_RIGHTS_UNCONFIRMED");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_FOUNDER_RIGHTS_NOT_TRANSFERRED");
        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_CONTRACTOR_RIGHTS_GAP");
    }

    [Fact(DisplayName = "5.5 Acceptance Criteria §20: Сильные стороны определяются на уровне Dimension (Score >= 80 без HIGH/CRITICAL)")]
    public void Normative_Strong_Areas_Dimension_Level_Evaluation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-03"] = "none",
            ["COR-04"] = "none",
            ["COR-05"] = "systematic",
            ["COR-06"] = "unclear", // Вызывает HIGH риск COR_AUTHORITY_GAP в dimension 'authority'
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized"
        };

        var result = _engine.ComputeResult(answers);

        // Сильные стороны должны включать 'Соответствие долей и реестра' и 'Корпоративные решения и одобрения',
        // но НЕ должны включать 'Полномочия и лимиты сделок' из-за риска COR_AUTHORITY_GAP
        Assert.Contains(result.Strengths, s => s.Contains("Соответствие долей") || s.Contains("реестра"));
        Assert.DoesNotContain(result.Strengths, s => s.Contains("Полномочия"));
    }

    [Fact(DisplayName = "5.6 Правило работодателя: IP-10=lawyer_checked + IP-10A=yes триггерит CRITICAL риск IP_EMPLOYER_RISK")]
    public void Normative_Employer_Risk_Lawyer_Checked_With_Resources_Used_Triggers_Critical()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "lawyer_checked",
            ["IP-10A"] = "yes"
        };

        var result = _engine.ComputeResult(answers);

        var employerRisk = result.Risks.FirstOrDefault(r => r.Code == "IP_EMPLOYER_RISK");
        Assert.NotNull(employerRisk);
        Assert.Equal("CRITICAL", employerRisk.Severity);
    }

    [Fact(DisplayName = "5.7 Правило работодателя: IP-10=unrelated + IP-10A=no не создаёт IP_EMPLOYER_RISK")]
    public void Normative_Employer_Risk_Unrelated_With_No_Resources_Used_Produces_No_Risk()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["IP-01"] = "ready",
            ["IP-02"] = new List<string> { "code" },
            ["IP-03"] = new List<string> { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-10"] = "unrelated",
            ["IP-10A"] = "no"
        };

        var result = _engine.ComputeResult(answers);

        Assert.DoesNotContain(result.Risks, r => r.Code == "IP_EMPLOYER_RISK");
    }

    [Fact(DisplayName = "5.8 Нормализация IP-10: явный ответ 'unknown' сохраняет факт 'unknown' без преобразования в 'no'")]
    public void Normative_IP10_Unknown_Normalizes_To_Unknown_Fact()
    {
        var answers = new Dictionary<string, object>
        {
            ["IP-10"] = "unknown"
        };

        var store = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal("unknown", store.Facts["ip.externalEmployerCreation"]);
    }

    [Fact(DisplayName = "5.9 Безопасность фактов: Неотвеченные вопросы не создают ложных синтетических позитивных фактов")]
    public void Normative_Unanswered_Questions_Do_Not_Generate_Synthetic_Positive_Facts()
    {
        // Передаем пустой словарь ответов
        var emptyAnswers = new Dictionary<string, object>();
        var store = FactNormalizer.NormalizeFacts(emptyAnswers);
        var f = store.Facts;

        // Корпоративные факты не должны синтезировать "complete", "systematic", "match"
        Assert.False(f.ContainsKey("capital.ownershipMatch"), "Отсутствующий COR-01 не должен создавать 'match'");
        Assert.False(f.ContainsKey("capital.capTableStatus"), "Отсутствующий COR-02 не должен создавать 'complete'");
        Assert.False(f.ContainsKey("corporate.approvals"), "Отсутствующий COR-05 не должен создавать 'systematic'");
        Assert.False(f.ContainsKey("corporate.authority"), "Отсутствующий COR-06 не должен создавать 'clear_limits'");

        // IP факты не должны синтезировать "all", "assigned", "clear"
        Assert.False(f.ContainsKey("ip.overallRightsEvidence"), "Отсутствующий IP-04 не должен создавать 'all'");
        Assert.False(f.ContainsKey("ip.founderRights"), "Отсутствующий IP-05 не должен создавать 'assigned'");
        Assert.False(f.ContainsKey("ip.contentProvenance"), "Отсутствующий IP-15 не должен создавать 'clear'");
    }

    [Fact(DisplayName = "5.10 Независимость Strong Areas: HIGH риск одной dimension не блокирует Strong Area другой dimension в той же секции")]
    public void Normative_Strong_Areas_Dimension_Independence_Within_Same_Section()
    {
        // Сценарий: В блоке Corporate 'ownership_accuracy' идеален (COR-01=match, score 100),
        // но 'authority' содержит HIGH риск (COR-06=unclear, score 0).
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "match",      // Dimension: ownership_accuracy (score 100, no risk)
            ["COR-02"] = "complete",   // Dimension: cap_table (score 100, no risk)
            ["COR-03"] = "none",       // Dimension: equity_commitments (score 100, no risk)
            ["COR-04"] = "none",       // Dimension: corporate_history (score 100, no risk)
            ["COR-05"] = "systematic", // Dimension: corporate_approvals (score 100, no risk)
            ["COR-06"] = "unclear",    // Dimension: authority (score 0, triggers HIGH COR_AUTHORITY_GAP)
            ["COR-07"] = "aligned",    // Dimension: entity_alignment (score 100, no risk)
            ["COR-08"] = "organized"   // Dimension: records (score 100, no risk)
        };

        var result = _engine.ComputeResult(answers);

        // 'ownership_accuracy' (Соответствие долей) и 'cap_table' ДОЛЖНЫ присутствовать в Strengths
        Assert.Contains(result.Strengths, s => s.Contains("Соответствие долей") || s.Contains("реестра"));
        Assert.Contains(result.Strengths, s => s.Contains("Таблица долей"));

        // 'authority' (Полномочия) НЕ ДОЛЖНО присутствовать в Strengths из-за риска COR_AUTHORITY_GAP
        Assert.DoesNotContain(result.Strengths, s => s.Contains("Полномочия"));
    }

    [Fact(DisplayName = "5.11 Множественные риски: Два риска разных dimensions одной секции обрабатываются независимо")]
    public void Normative_Multiple_Risks_In_Different_Dimensions_Are_Treated_Independently()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "dispute",   // Dimension: ownership_accuracy -> HIGH/CRITICAL COR_OWNERSHIP_DISPUTE
            ["COR-02"] = "none",      // Dimension: cap_table -> HIGH COR_CAP_TABLE_GAP
            ["COR-03"] = "none",
            ["COR-04"] = "none",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized"
        };

        var result = _engine.ComputeResult(answers);

        // Оба риска должны быть обнаружены как отдельные независимые правовые проблемы
        Assert.Contains(result.Risks, r => r.Code == "COR_CAP_TABLE_UNRELIABLE");
        Assert.Contains(result.Risks, r => r.Code == "COR_OWNERSHIP_DISPUTE");

        // Ни 'ownership_accuracy', ни 'cap_table' не должны стать Strong Areas
        Assert.DoesNotContain(result.Strengths, s => s.Contains("Соответствие долей"));
        Assert.DoesNotContain(result.Strengths, s => s.Contains("Таблица долей"));

        // А незатронутые dimensions ('corporate_approvals', 'records') могут быть Strong Areas
        Assert.Contains(result.Strengths, s => s.Contains("Корпоративные решения") || s.Contains("одобрения"));
    }

    // ====================================================================================
    // РАЗДЕЛ 6: ЭТАЛОННЫЕ ТЕСТЫ (GOLDEN SCORING TESTS) И ИНВАРИАНТЫ БЕЗОПАСНОСТИ (§20-§27)
    // ====================================================================================

    [Fact(DisplayName = "6.0 [CANONICAL §23] Реестр весов модулей в QuestionRepository точно равен §23 (Founders=15, Corporate=12, IP=18)")]
    public void Canonical_Module_Weights_Match_Section_23_Specification()
    {
        var sections = _repository.GetSections();
        var fndSec = sections.FirstOrDefault(s => s.Id == "founders");
        var corpSec = sections.FirstOrDefault(s => s.Id == "corporate");
        var ipSec = sections.FirstOrDefault(s => s.Id == "ip");

        Assert.NotNull(fndSec);
        Assert.NotNull(corpSec);
        Assert.NotNull(ipSec);

        // Нормативные веса §23
        Assert.Equal(15, fndSec.Weight);
        Assert.Equal(12, corpSec.Weight);
        Assert.Equal(18, ipSec.Weight);
    }

    [Fact(DisplayName = "6.1 [CANONICAL §20-§27] Инвариант безопасности Strong Area: Все severe риски детерминированно смаплены на существующие DimensionId")]
    public void Invariant_Every_Severe_Risk_Has_Deterministic_Valid_Dimension_Mapping()
    {
        var allRisks = _repository.GetRisks();
        var allValidDimensions = _repository.GetQuestions()
            .Select(q => q.DimensionId)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .ToHashSet();

        var severeRisks = allRisks.Where(r => r.Severity is "CRITICAL" or "HIGH" or "BLOCKER").ToList();
        Assert.NotEmpty(severeRisks);

        foreach (var risk in severeRisks)
        {
            var mappedDims = ScoringEngine.GetAffectedDimensions(risk.Code);
            Assert.True(mappedDims.Count > 0, $"Риск '{risk.Code}' уровня {risk.Severity} должен мапиться хотя бы на одну dimension.");

            foreach (var dim in mappedDims)
            {
                Assert.True(allValidDimensions.Contains(dim),
                    $"Риск '{risk.Code}' ссылается на неизвестный DimensionId '{dim}'.");
            }
        }

        // Проверяем fail-closed поведение: неизвестный severe risk должен выбрасывать InvalidOperationException
        Assert.Throws<InvalidOperationException>(() => ScoringEngine.GetAffectedDimensions("UNKNOWN_CRITICAL_RISK_CODE"));
    }

    [Fact(DisplayName = "6.2 [CANONICAL §23.2 & §24] Golden Score: Идеально и полностью отвеченный модуль даёт ровно 100 баллов")]
    public void Golden_Score_Perfect_Fully_Answered_Module_Returns_Exact_100()
    {
        // Нормативный источник: §23.2 и §24
        // Все 8 диагностических вопросов Corporate отвечены на 1.0 -> балл = 100
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "match",      // 1.0, weight 20
            ["COR-02"] = "complete",   // 1.0, weight 15
            ["COR-03"] = "none",       // 1.0, weight 10
            ["COR-04"] = "none",       // 1.0, weight 15
            ["COR-05"] = "systematic", // 1.0, weight 12
            ["COR-06"] = "clear_limits",// 1.0, weight 10
            ["COR-07"] = "aligned",    // 1.0, weight 13
            ["COR-08"] = "organized"   // 1.0, weight 5
        };

        var result = _engine.ComputeResult(answers);
        var corpSection = result.Sections.First(s => s.SectionId == "corporate");

        Assert.NotNull(corpSection.Score);
        Assert.Equal(100, corpSection.Score.Value);
    }

    [Fact(DisplayName = "6.3 [CANONICAL §23.2 & §24] Golden Score: Точный расчёт составного балла мульти-вопросной dimension")]
    public void Golden_Score_Mixed_Score_Dimension_Calculation()
    {
        // Нормативный источник: §23.2 и §24
        // Dimension 'corporate_history' содержит:
        // COR-04 (weight 70%): main_docs (score 0.7 -> 70% * 0.7 = 49)
        // COR-04A (weight 30%): yes (score 1.0 -> 30% * 1.0 = 30)
        // Итоговый балл dimension = (49 + 30) / 100 * 100 = 79 баллов.
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-04"] = "main_docs",
            ["COR-04A"] = "yes"
        };

        var result = _engine.ComputeResult(answers);
        var corpSection = result.Sections.First(s => s.SectionId == "corporate");
        var historyDim = corpSection.Dimensions.First(d => d.DimensionId == "corporate_history");

        Assert.Equal(79, historyDim.Score);
    }

    [Fact(DisplayName = "6.4 [CANONICAL §23.2 & §24] Golden Score: Точный расчёт dimension при скрытом условном подвопросе (N/A)")]
    public void Golden_Score_Dimension_With_Conditional_NA_Subquestion()
    {
        // Нормативный источник: §23.2 и §24
        // COR-04 = complete (1.0, weight 70%). Подвопрос COR-04A скрыт (ShowIf COR-04 != 'none' && != 'complete').
        // Нормализованный знаменатель внутри dimension = 70.
        // Балл dimension = (1.0 * 70) / 70 * 100 = 100.
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-04"] = "complete"
        };

        var result = _engine.ComputeResult(answers);
        var corpSection = result.Sections.First(s => s.SectionId == "corporate");
        var historyDim = corpSection.Dimensions.First(d => d.DimensionId == "corporate_history");

        Assert.Equal(100, historyDim.Score);
    }

    [Fact(DisplayName = "6.5 [PROJECT_OVERRIDE / INCOMPLETE_SESSION_POLICY] Частично отвеченная dimension рассчитывается по отвеченным вопросам")]
    public void Golden_Score_Partially_Answered_Dimension()
    {
        // Источник: PROJECT_OVERRIDE (Incomplete Session Policy)
        // В dimension 'corporate_history' отвечен только COR-04 = main_docs (score 0.7, weight 70), а COR-04A не отвечен.
        // Балл dimension = (0.7 * 70) / 70 * 100 = 70 баллов.
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-04"] = "main_docs"
        };

        var result = _engine.ComputeResult(answers);
        var corpSection = result.Sections.First(s => s.SectionId == "corporate");
        var historyDim = corpSection.Dimensions.First(d => d.DimensionId == "corporate_history");

        Assert.Equal(70, historyDim.Score);
    }

    [Fact(DisplayName = "6.6 [PROJECT_OVERRIDE / INCOMPLETE_SESSION_POLICY] Применимый, но полностью неотвеченный модуль получает Score = null")]
    public void Golden_Score_Applicable_Completely_Unanswered_Module_Has_Null_Score()
    {
        // Источник: PROJECT_OVERRIDE (Incomplete Session Policy)
        // COR-C01 = one (модуль Corporate применим), но ни один диагностический вопрос COR-01..08 не отвечен.
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one"
        };

        var result = _engine.ComputeResult(answers);
        var corpSection = result.Sections.First(s => s.SectionId == "corporate");

        Assert.Equal("APPLICABLE", corpSection.Status);
        Assert.Null(corpSection.Score);
        Assert.Empty(corpSection.Dimensions);
    }

    [Fact(DisplayName = "6.7 [CANONICAL §23] Golden Score: Взвешенный общий балл (Overall Score) по нескольким применимым модулям")]
    public void Golden_Score_Overall_Score_Weighted_Across_Applicable_Modules()
    {
        // =========================================================================================
        // НОРМАТИВНЫЙ РАСЧЁТ CANONICAL §23 (Founders=15%, Corporate=12%, IP=18%):
        // Founders (соло) = 100 баллов (вес 15%)
        // Corporate: COR-01 (planned_change, score 0.80, w=20) + COR-02 (current_plus_separate, score 0.80, w=15)
        //   Corporate Score = (80*20 + 80*15)/35 = 80 баллов (вес 12%)
        //   CANONICAL §23 Overall = Round((100 * 15 + 80 * 12) / (15 + 12)) = Round(2460 / 27) = 91 балл.
        // =========================================================================================
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "planned_change",
            ["COR-02"] = "current_plus_separate"
        };

        var result = _engine.ComputeResult(answers);
        var fndSec = result.Sections.First(s => s.SectionId == "founders");
        var corpSec = result.Sections.First(s => s.SectionId == "corporate");

        Assert.Equal(100, fndSec.Score);
        Assert.Equal(80, corpSec.Score);

        int expectedOverall = (int)Math.Round((100.0 * 15.0 + 80.0 * 12.0) / (15.0 + 12.0));
        Assert.Equal(91, expectedOverall);
        Assert.Equal(91, result.Overall);
    }

    [Fact(DisplayName = "6.8 [PROJECT_OVERRIDE / INCOMPLETE_SESSION_CONFIDENCE_POLICY] Golden Score: Точный процент уверенности при смеси known, partial и unknown ответов")]
    public void Golden_Score_Confidence_Mixed_Classes_Exact_Formula()
    {
        // =========================================================================================
        // CANONICAL §23 FULL_SESSION_CONFIDENCE:
        //   Знаменатель включает все применимые диагностические вопросы блока (Corporate sum = 100)
        //   Weighted Sum = (1.0 * 20) + (0.5 * 15) + (0.0 * 10) = 27.5
        //   Full-session Canonical Confidence = Round(27.5 / 100 * 100) = 28%
        // =========================================================================================
        // PROJECT_OVERRIDE / INCOMPLETE_SESSION_CONFIDENCE_POLICY:
        //   Знаменатель отслеживает только фактически отвеченные вопросы (Answered sum = 45)
        //   Incomplete Session Confidence = Round(27.5 / 45 * 100) = 61%
        // =========================================================================================
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "one",
            ["COR-01"] = "match",     // known -> 1.0 (w=20) => 20.0
            ["COR-02"] = "irregular", // partial -> 0.5 (w=15) => 7.5
            ["COR-03"] = "unknown"    // unknown -> 0.0 (w=10) => 0.0
        };

        var result = _engine.ComputeResult(answers);
        Assert.Equal(61, result.Confidence);
    }

    [Fact(DisplayName = "6.9 [PROJECT_OVERRIDE / INCOMPLETE_SESSION_POLICY] Golden Score: Ноль отвеченных диагностических вопросов даёт Overall = 0 и Confidence = 0")]
    public void Golden_Score_Zero_Answered_Diagnostic_Questions_Returns_Zero_Overall_And_Zero_Confidence()
    {
        // Источник: PROJECT_OVERRIDE (Incomplete Session Policy)
        // Передаем пустые ответы (0 отвеченных диагностических вопросов)
        var emptyAnswers = new Dictionary<string, object>();
        var result = _engine.ComputeResult(emptyAnswers);

        Assert.Equal(0, result.Overall);
        Assert.Equal(0, result.Confidence);
        Assert.Equal(0, result.AnsweredCount);
    }

    [Fact(DisplayName = "6.10 [CANONICAL §25] Инвариант чистоты: Ни один RiskFinding в результатах скоринга не начинается с 'R_FOUNDERS_'")]
    public void Invariant_No_Production_Finding_Code_Starts_With_Legacy_R_Founders()
    {
        // Генерируем полный набор ответов с различными проблемами фаундеров
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C03"] = "departed_unresolved",
            ["FND-C04"] = "oral",
            ["FND-01"] = "active_conflict",
            ["FND-02"] = "disputed",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "none",
            ["FND-08"] = "none",
            ["FND-09"] = "dispute",
            ["FND-10"] = "active_competition"
        };

        var result = _engine.ComputeResult(answers);
        Assert.NotEmpty(result.Risks);

        foreach (var finding in result.Risks)
        {
            Assert.False(finding.Code.StartsWith("R_FOUNDERS_", StringComparison.OrdinalIgnoreCase),
                $"Обнаружен legacy-код риска '{finding.Code}'! Все риски блока Founders должны иметь канонический префикс 'FND_'.");
        }
    }

        [Fact(DisplayName = "6.11 [CANONICAL §25] Полнота реестра и 100% Activation Coverage для всех 18 канонических находок FND_*")]
    public void Canonical_Founders_Finding_Registry_And_Activation_Coverage()
    {
        var expectedCanonicalFndCodes = new[]
        {
            "FND_ACTIVE_DISPUTE",
            "FND_EQUITY_DISPUTE",
            "FND_DEAD_EQUITY",
            "FND_DEADLOCK",
            "FND_DEPARTED_UNRESOLVED",
            "FND_CONFLICT_OF_INTEREST",
            "FND_ROLE_AMBIGUITY",
            "FND_COMMITMENT_MISMATCH",
            "FND_EQUITY_NOT_FORMALIZED",
            "FND_EQUITY_AMBIGUITY",
            "FND_NO_VESTING",
            "FND_INCOMPLETE_LEAVER_RULES",
            "FND_GOVERNANCE_AMBIGUITY",
            "FND_NO_DEADLOCK_PROTECTION",
            "FND_EXIT_RULES_MISSING",
            "FND_CONTRIBUTION_AMBIGUITY",
            "FND_STRATEGIC_MISALIGNMENT",
            "FND_DOCUMENTATION_GAP"
        };

        var registeredRisks = _repository.GetRisks().Select(r => r.Code).ToHashSet();

        foreach (var expectedCode in expectedCanonicalFndCodes)
        {
            Assert.True(registeredRisks.Contains(expectedCode),
                $"Канонический риск §25 '{expectedCode}' отсутствует в реестре QuestionRepository.");
        }

        // =====================================================================
        // ACTIVATION COVERAGE: 18 ДЕТЕРМИНИРОВАННЫХ ТЕСТ-КЕЙСОВ ДЛЯ КАЖДОГО FND
        // =====================================================================
        var activationTestCases = new Dictionary<string, Dictionary<string, object>>
        {
            ["FND_ACTIVE_DISPUTE"] = new() { ["FND-C01"] = "2", ["FND-01"] = "active_conflict" },
            ["FND_EQUITY_DISPUTE"] = new() { ["FND-C01"] = "2", ["FND-04"] = "dispute" },
            ["FND_DEAD_EQUITY"] = new() { ["FND-C01"] = "2", ["FND-03"] = "stopped", ["FND-05"] = "none" },
            ["FND_DEADLOCK"] = new() { ["FND-C01"] = "2", ["FND-C02"] = new List<double> { 50, 50 }, ["FND-06A"] = "broad_unanimity", ["FND-07"] = "none" },
            ["FND_DEPARTED_UNRESOLVED"] = new() { ["FND-C01"] = "2", ["FND-C03"] = "unresolved" },
            ["FND_CONFLICT_OF_INTEREST"] = new() { ["FND-C01"] = "2", ["FND-10"] = "employer_same_field" },
            ["FND_ROLE_AMBIGUITY"] = new() { ["FND-C01"] = "2", ["FND-02"] = "overlap" },
            ["FND_COMMITMENT_MISMATCH"] = new() { ["FND-C01"] = "2", ["FND-03"] = "below_expected" },
            ["FND_EQUITY_NOT_FORMALIZED"] = new() { ["FND-C01"] = "2", ["FND-04"] = "verbal" },
            ["FND_EQUITY_AMBIGUITY"] = new() { ["FND-C01"] = "2", ["FND-04"] = "ambiguous" },
            ["FND_NO_VESTING"] = new() { ["FND-C01"] = "2", ["FND-05"] = "not_discussed" },
            ["FND_INCOMPLETE_LEAVER_RULES"] = new() { ["FND-C01"] = "2", ["FND-05A"] = "none" },
            ["FND_GOVERNANCE_AMBIGUITY"] = new() { ["FND-C01"] = "2", ["FND-06"] = "none" },
            ["FND_NO_DEADLOCK_PROTECTION"] = new() { ["FND-C01"] = "2", ["FND-C02"] = new List<double> { 90, 10 }, ["FND-07"] = "only_agree" },
            ["FND_EXIT_RULES_MISSING"] = new() { ["FND-C01"] = "2", ["FND-08"] = "none" },
            ["FND_CONTRIBUTION_AMBIGUITY"] = new() { ["FND-C01"] = "2", ["FND-09"] = "material_unclear" },
            ["FND_STRATEGIC_MISALIGNMENT"] = new() { ["FND-C01"] = "2", ["FND-11"] = "material_difference" },
            ["FND_DOCUMENTATION_GAP"] = new() { ["FND-C01"] = "2", ["FND-C04"] = "informal" }
        };

        foreach (var (code, answers) in activationTestCases)
        {
            var res = _engine.ComputeResult(answers);
            Assert.True(res.Risks.Any(r => r.Code == code), 
                $"Failed to activate canonical risk '{code}' with answers: [{string.Join(", ", answers.Select(kv => $"{kv.Key}={kv.Value}"))}]. Actual risks in result: [{string.Join(", ", res.Risks.Select(r => r.Code))}]");
        }
    }

    [Fact(DisplayName = "6.12 [CANONICAL §25] Семантическая дифференциация: Разделение FND_EQUITY_DISPUTE, FND_EQUITY_AMBIGUITY и FND_EQUITY_NOT_FORMALIZED")]
    public void Semantic_Differentiation_Of_Equity_Findings()
    {
        // 1. Сценарий: Открытый спор по долям -> FND_EQUITY_DISPUTE (CRITICAL)
        var disputeAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "dispute"
        };
        var resDispute = _engine.ComputeResult(disputeAnswers);
        Assert.Contains(resDispute.Risks, r => r.Code == "FND_EQUITY_DISPUTE" && r.Severity == "CRITICAL");
        Assert.DoesNotContain(resDispute.Risks, r => r.Code == "FND_EQUITY_NOT_FORMALIZED");
        Assert.DoesNotContain(resDispute.Risks, r => r.Code == "FND_EQUITY_AMBIGUITY");

        // 2. Сценарий: Противоречивые обещания долей -> FND_EQUITY_AMBIGUITY (HIGH)
        var ambigAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "ambiguous"
        };
        var resAmbig = _engine.ComputeResult(ambigAnswers);
        Assert.Contains(resAmbig.Risks, r => r.Code == "FND_EQUITY_AMBIGUITY" && r.Severity == "HIGH");
        Assert.DoesNotContain(resAmbig.Risks, r => r.Code == "FND_EQUITY_DISPUTE");
        Assert.DoesNotContain(resAmbig.Risks, r => r.Code == "FND_EQUITY_NOT_FORMALIZED");

        // 3. Сценарий: Устная договоренность -> FND_EQUITY_NOT_FORMALIZED (MEDIUM)
        var verbalAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-04"] = "verbal"
        };
        var resVerbal = _engine.ComputeResult(verbalAnswers);
        Assert.Contains(resVerbal.Risks, r => r.Code == "FND_EQUITY_NOT_FORMALIZED" && r.Severity == "MEDIUM");
        Assert.DoesNotContain(resVerbal.Risks, r => r.Code == "FND_EQUITY_DISPUTE");
        Assert.DoesNotContain(resVerbal.Risks, r => r.Code == "FND_EQUITY_AMBIGUITY");
    }

    [Fact(DisplayName = "6.13 [CANONICAL §27.2] Точные условия активации FND_DEADLOCK vs FND_NO_DEADLOCK_PROTECTION и каноническая супрессия")]
    public void Canonical_FND_Deadlock_Strict_Section_27_2_Rules()
    {
        // 1. Позитивный сценарий Deadlock §27.2:
        //    - 2 active founders (FND-C01 = "2")
        //    - near-equal ownership/control (FND-C02 = "equal_50_50")
        //    - keyDecisionMode = "broad_unanimity" или "material_unanimity" (FND-06A = "broad_unanimity")
        //    - score(FND-07) <= 0.15 (FND-07 = "none")
        //    -> Активирует FND_DEADLOCK (CRITICAL) и подавляет FND_GOVERNANCE_AMBIGUITY и FND_NO_DEADLOCK_PROTECTION
        var deadlockAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new List<double> { 50, 50 },
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none"
        };

        var resDeadlock = _engine.ComputeResult(deadlockAnswers);
        Assert.Contains(resDeadlock.Risks, r => r.Code == "FND_DEADLOCK" && r.Severity == "CRITICAL");
        Assert.DoesNotContain(resDeadlock.Risks, r => r.Code == "FND_NO_DEADLOCK_PROTECTION");
        Assert.DoesNotContain(resDeadlock.Risks, r => r.Code == "FND_GOVERNANCE_AMBIGUITY");

        // 2. Негативный сценарий 1: Наличие явного контроля (FND-C02 = "clear_majority") НЕ активирует FND_DEADLOCK
        var majorityControlAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new List<double> { 90, 10 },
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none"
        };
        var resMajControl = _engine.ComputeResult(majorityControlAnswers);
        Assert.DoesNotContain(resMajControl.Risks, r => r.Code == "FND_DEADLOCK");
        Assert.Contains(resMajControl.Risks, r => r.Code == "FND_NO_DEADLOCK_PROTECTION" && r.Severity == "HIGH");

        // 3. Негативный сценарий 2: Порядок голосования простым большинством (FND-06A = "majority") НЕ активирует FND_DEADLOCK
        var majorityVotingAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new List<double> { 50, 50 },
            ["FND-06A"] = "majority",
            ["FND-07"] = "none"
        };
        var resMajVoting = _engine.ComputeResult(majorityVotingAnswers);
        Assert.DoesNotContain(resMajVoting.Risks, r => r.Code == "FND_DEADLOCK");
        Assert.Contains(resMajVoting.Risks, r => r.Code == "FND_NO_DEADLOCK_PROTECTION" && r.Severity == "HIGH");
    }
    [Fact(DisplayName = "6.14 [CANONICAL §24 & §27.2] 2 фаундера без FND-C02: nearEqualControl не true и FND_DEADLOCK не возникает")]
    public void Two_Founders_Without_FND_C02_Do_Not_Trigger_Deadlock()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none"
            // FND-C02 не передан -> nearEqualControl = false -> дедлок не может быть подтвержден
        };

        var result = _engine.ComputeResult(answers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "FND_DEADLOCK");
        Assert.Contains(result.Risks, r => r.Code == "FND_NO_DEADLOCK_PROTECTION");
    }

    [Fact(DisplayName = "6.15 [CANONICAL §24] inactive_exist нормализуется в activeCount='unknown' и не трактуется как 2")]
    public void Inactive_Exist_Normalizes_To_Unknown_Active_Count()
    {
        var store = FactNormalizer.NormalizeFacts(new Dictionary<string, object>
        {
            ["FND-C01"] = "inactive_exist"
        });

        Assert.Equal("unknown", store.Facts["founders.activeCount"]);
        Assert.Equal(true, store.Facts["founders.inactiveExists"]);

        var res = _engine.ComputeResult(new Dictionary<string, object>
        {
            ["FND-C01"] = "inactive_exist",
            ["FND-06A"] = "broad_unanimity",
            ["FND-07"] = "none"
        });

        // Так как activeCount unknown, правило дедлока для ровно 2 фаундеров не срабатывает
        Assert.DoesNotContain(res.Risks, r => r.Code == "FND_DEADLOCK");
    }

    [Fact(DisplayName = "6.16 [CANONICAL §24] FND-01=material выставляет activeDispute=true, но не триггерит FND_ACTIVE_DISPUTE")]
    public void Material_Dispute_Sets_Fact_True_Without_Triggering_Active_Dispute_Risk()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-01"] = "material"
        };

        var store = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal(true, store.Facts["founders.activeDispute"]);
        Assert.Equal("material", store.Facts["founders.disputeLevel"]);

        var result = _engine.ComputeResult(answers);
        Assert.DoesNotContain(result.Risks, r => r.Code == "FND_ACTIVE_DISPUTE");
        Assert.Contains(result.Risks, r => r.Code == "FND_DOCUMENTATION_GAP");
    }

    [Fact(DisplayName = "6.17 [CANONICAL §24] active_conflict и formal_dispute активируют FND_ACTIVE_DISPUTE (CRITICAL)")]
    public void Active_Conflict_And_Formal_Dispute_Trigger_FND_Active_Dispute()
    {
        var res1 = _engine.ComputeResult(new Dictionary<string, object> { ["FND-C01"] = "2", ["FND-01"] = "active_conflict" });
        Assert.Contains(res1.Risks, r => r.Code == "FND_ACTIVE_DISPUTE" && r.Severity == "CRITICAL");

        var res2 = _engine.ComputeResult(new Dictionary<string, object> { ["FND-C01"] = "2", ["FND-01"] = "formal_dispute" });
        Assert.Contains(res2.Risks, r => r.Code == "FND_ACTIVE_DISPUTE" && r.Severity == "CRITICAL");
    }

    [Fact(DisplayName = "6.18 [CANONICAL §27.2] FND-C03=dispute и FND-08=already_unresolved активируют FND_DEPARTED_UNRESOLVED")]
    public void Departed_Dispute_And_Unresolved_Departure_Trigger_Departed_Unresolved()
    {
        var res1 = _engine.ComputeResult(new Dictionary<string, object> { ["FND-C01"] = "2", ["FND-C03"] = "dispute" });
        Assert.Contains(res1.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED" && r.Severity == "CRITICAL");

        var res2 = _engine.ComputeResult(new Dictionary<string, object> { ["FND-C01"] = "2", ["FND-08"] = "already_unresolved" });
        Assert.Contains(res2.Risks, r => r.Code == "FND_DEPARTED_UNRESOLVED" && r.Severity == "CRITICAL");
    }

    [Fact(DisplayName = "6.19 [CANONICAL §20] Strategic Misalignment блокирует Strong Area 'strategic_alignment', а не 'governance'")]
    public void Strategic_Misalignment_Blocks_Strategic_Alignment_Dimension_Only()
    {
        var affected = ScoringEngine.GetAffectedDimensions("FND_STRATEGIC_MISALIGNMENT");
        Assert.Contains("strategic_alignment", affected);
        Assert.DoesNotContain("governance", affected);

        // Проверяем, что идеальный governance (FND-06=written, FND-06A=majority) получает Strong Area даже при конфликте стратегии (FND-11=conflict)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-06"] = "written",
            ["FND-06A"] = "majority",
            ["FND-11"] = "conflict"
        };

        var result = _engine.ComputeResult(answers);
        var governanceDim = result.Sections.SelectMany(s => s.Dimensions).FirstOrDefault(d => d.DimensionId == "governance");
        var stratDim = result.Sections.SelectMany(s => s.Dimensions).FirstOrDefault(d => d.DimensionId == "strategic_alignment");

        Assert.NotNull(governanceDim);
        Assert.True(governanceDim.Score >= 80, $"Governance score should be >= 80, got {governanceDim.Score}");
        Assert.Contains(result.Strengths, s => s.Contains("управление", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Strengths, s => s.Contains("стратег", StringComparison.OrdinalIgnoreCase));
    }
}
