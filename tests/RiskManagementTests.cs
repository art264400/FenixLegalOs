using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Repositories;
using FenixLegalOs.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

/// <summary>
/// Тесты динамического управления рисками через RiskRepository и интеграции с движком отчётов.
/// </summary>
public class RiskManagementTests
{
    private readonly RiskRepository _riskRepo;
    private readonly QuestionRepository _qRepo;
    private readonly ScoringEngine _scoringEngine;

    public RiskManagementTests()
    {
        var tempDb = Path.Combine(Path.GetTempPath(), $"test_risk_mgmt_{Guid.NewGuid():N}.db");
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = tempDb
        }).Build();

        var dbInit = new DbInitializer(config);
        dbInit.Initialize();

        _riskRepo = new RiskRepository(dbInit);
        _qRepo = new QuestionRepository(dbInit);
        _scoringEngine = new ScoringEngine(_qRepo);
    }

    [Fact(DisplayName = "1. Репозиторий рисков загружает полный каталог из БД и поддерживает фильтрацию")]
    public void GetAllRisks_Loads_From_Db_And_Filters()
    {
        var all = _riskRepo.GetAllRisks();
        Assert.NotEmpty(all);
        Assert.Equal(100, all.Count);

        var ipRisks = _riskRepo.GetAllRisks(sectionId: "ip");
        Assert.NotEmpty(ipRisks);
        Assert.All(ipRisks, r => Assert.Equal("ip", r.SectionId));

        var blockers = _riskRepo.GetAllRisks(severity: "Blocker");
        Assert.NotEmpty(blockers);
        Assert.All(blockers, r => Assert.Equal(RiskSeverity.Blocker, r.Severity));

        var searched = _riskRepo.GetAllRisks(search: "DEADLOCK");
        Assert.NotEmpty(searched);
        Assert.Contains(searched, r => r.Code == "FND_DEADLOCK");
    }

    [Fact(DisplayName = "2. Обновление формулировки и критичности риска успешно сохраняется в БД")]
    public void UpdateRisk_Persists_Changes_And_Invalidates_Cache()
    {
        var original = _riskRepo.GetRiskByCode("IP_CONTRACTOR_RIGHTS_GAP");
        Assert.NotNull(original);

        var modified = new RiskDefinition
        {
            Code = original.Code,
            SectionId = original.SectionId,
            Severity = RiskSeverity.Blocker,
            Priority = RiskPriority.Now,
            RootCauseGroup = original.RootCauseGroup,
            Title = "Кастомный заголовок: Критический дефект прав на код",
            Finding = "Кастомное описание: Код написан внешними разработчиками без договора.",
            WhyItMatters = "Инвестор не сможет войти в сделку без закрытия этого дефекта.",
            Recommendation = "Срочно подписать трехстороннее соглашение об отчуждении прав.",
            Recommendations = new List<string> { "Шаг 1: Провести аудит", "Шаг 2: Подписать акт" },
            SuppressCodes = original.SuppressCodes,
            Modules = original.Modules,
            LawyerRequired = true,
            Resolution = ResolutionType.LawyerRequired,
            ServiceCode = original.ServiceCode,
            Cta = original.Cta
        };

        bool ok = _riskRepo.UpdateRisk(modified);
        Assert.True(ok);

        var loaded = _riskRepo.GetRiskByCode("IP_CONTRACTOR_RIGHTS_GAP");
        Assert.NotNull(loaded);
        Assert.Equal("Кастомный заголовок: Критический дефект прав на код", loaded.Title);
        Assert.Equal("Инвестор не сможет войти в сделку без закрытия этого дефекта.", loaded.WhyItMatters);
        Assert.Equal(RiskSeverity.Blocker, loaded.Severity);
        Assert.True(loaded.LawyerRequired);
    }

    [Fact(DisplayName = "3. Сброс рисков к заводским настройкам возвращает эталонные данные DataBank")]
    public void ResetToDefaults_Restores_Original_DataBank_Values()
    {
        var risk = _riskRepo.GetRiskByCode("FND_DEADLOCK");
        Assert.NotNull(risk);

        var mod = new RiskDefinition
        {
            Code = risk.Code,
            SectionId = risk.SectionId,
            Severity = RiskSeverity.Medium,
            Priority = RiskPriority.Later,
            RootCauseGroup = risk.RootCauseGroup,
            Title = "Временный измененный заголовок",
            Finding = "Временное описание",
            WhyItMatters = "Временный текст",
            Recommendation = "Временная рекомендация",
            Recommendations = new List<string>(),
            SuppressCodes = new List<string>(),
            Modules = risk.Modules,
            LawyerRequired = false,
            Resolution = ResolutionType.SelfService,
            ServiceCode = "",
            Cta = ""
        };

        _riskRepo.UpdateRisk(mod);
        Assert.Equal("Временный измененный заголовок", _riskRepo.GetRiskByCode("FND_DEADLOCK")?.Title);

        // Выполняем сброс
        _riskRepo.ResetToDefaults();

        var restored = _riskRepo.GetRiskByCode("FND_DEADLOCK");
        Assert.NotNull(restored);
        Assert.NotEqual("Временный измененный заголовок", restored.Title);
        Assert.Equal(RiskSeverity.Critical, restored.Severity);
    }
}
