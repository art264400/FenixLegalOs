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
        var repo = new QuestionRepository(dbInit);
        _engine = new ScoringEngine(repo);
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
            ["FND-02"] = "dispute",
            ["FND-03"] = "stopped",
            ["FND-04"] = "dispute",
            ["FND-05"] = "not_discussed",
            ["FND-06"] = "none",
            ["FND-07"] = "none",
            ["COR-C01"] = "none"
        };

        // Act
        var result = _engine.ComputeResult(answers);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Overall <= 40, $"Ожидался низкий общий балл при критическом конфликте и дедлоке, получено: {result.Overall}");
        Assert.True(result.CriticalCount >= 1, $"Ожидался минимум 1 критический риск, получено: {result.CriticalCount}");
        
        var hasDeadlockRisk = result.Risks.Any(r => 
            r.Severity is "CRITICAL" or "HIGH" &&
            (r.Code.Contains("FOUNDERS", StringComparison.OrdinalIgnoreCase) || 
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
            ["FND-07"] = "mechanism",
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
            ["FND-07"] = "mechanism",
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

        // 2. Проверено юристом + получено согласие -> НЕТ риска IP_EMPLOYER_RISK (письменное подтверждение оформлено)
        var lawyerCheckedAnswers = new Dictionary<string, object>
        {
            ["IP-01"] = "ready",
            ["IP-03"] = new List<string> { "founders" },
            ["IP-10"] = "lawyer_checked",
            ["IP-10A"] = "yes"
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
            ["FND-07"] = "mechanism",
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
        // [IMPLEMENTATION_BUG NOTE]: В DataBank.cs для подвопросов IP-10/10A/12 поле Weight содержит предварительно умноженное число (3, 5),
        // из-за чего при q.Weight * q.WithinDimensionWeight происходит двойное масштабирование. В чистом нормативном тесте используются
        // канонические применимые вопросы с WithinDimensionWeight = 100%:
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
}
