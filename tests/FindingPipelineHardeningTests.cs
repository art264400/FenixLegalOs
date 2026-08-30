using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Core;
using Xunit;

namespace FenixLegalOs.Tests;

public class FindingPipelineHardeningTests
{
    private static RiskFinding CreateFinding(string code, RiskSeverity severity = RiskSeverity.High, string rootGroup = "GENERAL")
    {
        var def = DataBank.Risks.FirstOrDefault(r => r.Code == code);
        return new RiskFinding
        {
            Code = code,
            Severity = def?.Severity ?? severity,
            Priority = def?.Priority ?? RiskPriority.Now,
            RootCauseGroup = def?.RootCauseGroup ?? rootGroup,
            SectionId = def?.SectionId ?? "test",
            Title = def?.Title ?? code,
            Finding = def?.Finding ?? code,
            WhyItMatters = def?.WhyItMatters ?? code,
            AffectedDimensions = def?.AffectedDimensions ?? new List<string>()
        };
    }

    [Fact(DisplayName = "1. [Suppression Semantics] Одинаковый RootCauseGroup без explicit SuppressCodes сохраняет оба findings")]
    public void Same_RootCauseGroup_Without_Explicit_SuppressCode_Preserves_Both_Findings()
    {
        // TEAM_UNCLEAR_TERMS (Medium) и TEAM_NO_WRITTEN_AGREEMENTS (High) имеют одинаковый RootCauseGroup = TEAM_AGREEMENTS
        var raw = new List<RiskFinding>
        {
            CreateFinding("TEAM_UNCLEAR_TERMS"),
            CreateFinding("TEAM_NO_WRITTEN_AGREEMENTS")
        };

        var result = FindingProcessor.MergeAndSuppressFindings(raw, new SharedFactStore());

        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Code == "TEAM_UNCLEAR_TERMS");
        Assert.Contains(result, f => f.Code == "TEAM_NO_WRITTEN_AGREEMENTS");
    }

    [Fact(DisplayName = "2. [Directional Suppression] Супрессия строго направленная (A -> B != B -> A)")]
    public void Suppression_Is_Strictly_Directional()
    {
        // TEAM_KEY_PERSON_UNDOCUMENTED suppresses TEAM_NO_WRITTEN_AGREEMENTS
        var rawWithSuppressor = new List<RiskFinding>
        {
            CreateFinding("TEAM_KEY_PERSON_UNDOCUMENTED"),
            CreateFinding("TEAM_NO_WRITTEN_AGREEMENTS")
        };
        var res1 = FindingProcessor.MergeAndSuppressFindings(rawWithSuppressor, new SharedFactStore());
        Assert.Single(res1);
        Assert.Equal("TEAM_KEY_PERSON_UNDOCUMENTED", res1[0].Code);

        // Когда присутствует только TEAM_NO_WRITTEN_AGREEMENTS, он не подавляет никого
        var rawWithoutSuppressor = new List<RiskFinding>
        {
            CreateFinding("TEAM_NO_WRITTEN_AGREEMENTS")
        };
        var res2 = FindingProcessor.MergeAndSuppressFindings(rawWithoutSuppressor, new SharedFactStore());
        Assert.Single(res2);
        Assert.Equal("TEAM_NO_WRITTEN_AGREEMENTS", res2[0].Code);
    }

    [Fact(DisplayName = "3. [Explicit Metadata] Супрессия строго следует явным метаданным активных findings")]
    public void Suppression_Follows_Explicit_Active_Metadata_Only()
    {
        // FND_EQUITY_AMBIGUITY подавляет FND_EQUITY_NOT_FORMALIZED
        // FND_EQUITY_DISPUTE подавляет FND_EQUITY_NOT_FORMALIZED и FND_EQUITY_AMBIGUITY
        var raw = new List<RiskFinding>
        {
            CreateFinding("FND_EQUITY_DISPUTE"),
            CreateFinding("FND_EQUITY_AMBIGUITY"),
            CreateFinding("FND_EQUITY_NOT_FORMALIZED")
        };

        var result = FindingProcessor.MergeAndSuppressFindings(raw, new SharedFactStore());
        Assert.Single(result);
        Assert.Equal("FND_EQUITY_DISPUTE", result[0].Code);
    }

    [Fact(DisplayName = "4. [Absent Suppressor] Если супрессор отсутствует, зависимый риск не удаляется")]
    public void Absent_Suppressor_Does_Not_Suppress_Child_Risk()
    {
        // IP_FORMER_DEVELOPER_GAP отсутствует -> TEAM_FORMER_ACCESS_RISK и IP_CONTRACTOR_RIGHTS_GAP сохраняются
        var raw = new List<RiskFinding>
        {
            CreateFinding("TEAM_FORMER_ACCESS_RISK"),
            CreateFinding("IP_CONTRACTOR_RIGHTS_GAP")
        };

        var result = FindingProcessor.MergeAndSuppressFindings(raw, new SharedFactStore());
        Assert.Equal(2, result.Count);
        Assert.Contains(result, f => f.Code == "TEAM_FORMER_ACCESS_RISK");
        Assert.Contains(result, f => f.Code == "IP_CONTRACTOR_RIGHTS_GAP");
    }

    [Fact(DisplayName = "5. [Fail Closed] Неизвестный RiskCode выбрасывает InvalidOperationException")]
    public void Unknown_RiskCode_Throws_InvalidOperationException()
    {
        var raw = new List<RiskFinding>
        {
            new() { Code = "UNKNOWN_RISK_XYZ", Severity = RiskSeverity.High }
        };

        Assert.Throws<InvalidOperationException>(() =>
            FindingProcessor.MergeAndSuppressFindings(raw, new SharedFactStore()));
    }

    [Fact(DisplayName = "6.1 [Metadata Invariant] Все SuppressCodes разрешаются в существующие RiskDefinitions")]
    public void All_SuppressCodes_Resolve_To_Existing_RiskDefinitions()
    {
        var allCodes = DataBank.Risks.Select(r => r.Code).ToHashSet();
        foreach (var def in DataBank.Risks)
        {
            foreach (var sc in def.SuppressCodes)
            {
                Assert.True(allCodes.Contains(sc),
                    $"RiskDefinition '{def.Code}' references non-existent SuppressCode '{sc}'.");
            }
        }
    }

    [Fact(DisplayName = "6.2 [Metadata Invariant] Ни один RiskDefinition не подавляет сам себя")]
    public void No_RiskDefinition_Suppresses_Itself()
    {
        foreach (var def in DataBank.Risks)
        {
            Assert.DoesNotContain(def.Code, def.SuppressCodes);
        }
    }

    [Fact(DisplayName = "6.3 [Metadata Invariant] Отсутствуют дубликаты в списках SuppressCodes")]
    public void No_Duplicate_SuppressCodes_In_Any_RiskDefinition()
    {
        foreach (var def in DataBank.Risks)
        {
            Assert.Equal(def.SuppressCodes.Count, def.SuppressCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }
    }

    [Fact(DisplayName = "6.4 [Metadata Invariant] Граф супрессий не содержит циклов (строгий DAG)")]
    public void Suppression_Graph_Has_No_Cycles()
    {
        var riskDict = DataBank.Risks.ToDictionary(r => r.Code, r => r.SuppressCodes);
        var visited = new Dictionary<string, int>(); // 0: unvisited, 1: visiting, 2: visited

        bool Dfs(string node, List<string> path)
        {
            visited[node] = 1;
            if (riskDict.TryGetValue(node, out var neighbors))
            {
                foreach (var next in neighbors)
                {
                    if (visited.GetValueOrDefault(next, 0) == 1)
                    {
                        throw new InvalidOperationException($"Cycle detected: {string.Join(" -> ", path)} -> {next}");
                    }
                    if (visited.GetValueOrDefault(next, 0) == 0)
                    {
                        var newPath = new List<string>(path) { next };
                        if (Dfs(next, newPath)) return true;
                    }
                }
            }
            visited[node] = 2;
            return false;
        }

        foreach (var code in riskDict.Keys)
        {
            if (visited.GetValueOrDefault(code, 0) == 0)
            {
                Dfs(code, new List<string> { code });
            }
        }
    }

    [Fact(DisplayName = "7. [Deterministic Output] Разный порядок входных findings дает идентичный результат")]
    public void Deterministic_Output_Across_Input_Permutations()
    {
        var f1 = CreateFinding("TEAM_KEY_PERSON_UNDOCUMENTED");
        var f2 = CreateFinding("TEAM_NO_WRITTEN_AGREEMENTS");
        var f3 = CreateFinding("TEAM_WORK_FORMAT_MISMATCH");
        var f4 = CreateFinding("COR_OWNERSHIP_DISPUTE");

        var orderA = new List<RiskFinding> { f1, f2, f3, f4 };
        var orderB = new List<RiskFinding> { f4, f3, f2, f1 };
        var orderC = new List<RiskFinding> { f2, f4, f1, f3 };

        var resA = FindingProcessor.MergeAndSuppressFindings(orderA, new SharedFactStore());
        var resB = FindingProcessor.MergeAndSuppressFindings(orderB, new SharedFactStore());
        var resC = FindingProcessor.MergeAndSuppressFindings(orderC, new SharedFactStore());

        Assert.Equal(resA.Select(x => x.Code), resB.Select(x => x.Code));
        Assert.Equal(resA.Select(x => x.Code), resC.Select(x => x.Code));
    }

    [Fact(DisplayName = "8. [Canonical Suppressions] TEAM_KEY_PERSON_UNDOCUMENTED подавляет ровно 3 канонических риска")]
    public void Team_Key_Person_Undocumented_Suppresses_Exactly_3_Risks()
    {
        var raw = new List<RiskFinding>
        {
            CreateFinding("TEAM_KEY_PERSON_UNDOCUMENTED"),
            CreateFinding("TEAM_NO_WRITTEN_AGREEMENTS"),
            CreateFinding("TEAM_UNCLEAR_TERMS"),
            CreateFinding("TEAM_CONFIDENTIALITY_GAP")
        };

        var res = FindingProcessor.MergeAndSuppressFindings(raw, new SharedFactStore());
        Assert.Single(res);
        Assert.Equal("TEAM_KEY_PERSON_UNDOCUMENTED", res[0].Code);
    }
}
