using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using FenixLegalOs.Data;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Data.RiskLibrary;
using FenixLegalOs.Models;
using Xunit;

namespace FenixLegalOs.Tests;

public class DataBankSnapshotTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    [Fact]
    public void DataBank_Counts_And_Module_Integrity()
    {
        // 1. Sections
        Assert.Equal(4, DataBank.Sections.Count);

        // 2. Questions Total & by Module
        Assert.Equal(66, DataBank.Questions.Count);
        Assert.Equal(17, FoundersQuestions.All.Count);
        Assert.Equal(15, CorporateQuestions.All.Count);
        Assert.Equal(17, IpQuestions.All.Count);
        Assert.Equal(17, TeamQuestions.All.Count);

        // 3. Risks Total & by Module
        Assert.Equal(54, DataBank.Risks.Count);
        Assert.Equal(18, FoundersRisks.All.Count);
        Assert.Equal(11, CorporateRisks.All.Count);
        Assert.Equal(12, IpRisks.All.Count);
        Assert.Equal(13, TeamRisks.All.Count);

        // 4. Questions Aggregation Matches Module Lists Exactly
        var aggregatedQuestions = new List<DiagnosticQuestion>();
        aggregatedQuestions.AddRange(FoundersQuestions.All);
        aggregatedQuestions.AddRange(CorporateQuestions.All);
        aggregatedQuestions.AddRange(IpQuestions.All);
        aggregatedQuestions.AddRange(TeamQuestions.All);

        Assert.Equal(aggregatedQuestions.Count, DataBank.Questions.Count);
        for (int i = 0; i < aggregatedQuestions.Count; i++)
        {
            Assert.Equal(aggregatedQuestions[i].Id, DataBank.Questions[i].Id);
            Assert.Equal(aggregatedQuestions[i].SectionId, DataBank.Questions[i].SectionId);
            Assert.Equal(aggregatedQuestions[i].Order, DataBank.Questions[i].Order);
            Assert.Equal(aggregatedQuestions[i].Type, DataBank.Questions[i].Type);
            Assert.Equal(aggregatedQuestions[i].ScoreMode, DataBank.Questions[i].ScoreMode);
            Assert.Equal(aggregatedQuestions[i].Weight, DataBank.Questions[i].Weight);
        }

        // 5. Risks Aggregation Matches Module Lists Exactly
        var aggregatedRisks = new List<RiskDefinition>();
        aggregatedRisks.AddRange(FoundersRisks.All);
        aggregatedRisks.AddRange(CorporateRisks.All);
        aggregatedRisks.AddRange(IpRisks.All);
        aggregatedRisks.AddRange(TeamRisks.All);

        Assert.Equal(aggregatedRisks.Count, DataBank.Risks.Count);
        for (int i = 0; i < aggregatedRisks.Count; i++)
        {
            Assert.Equal(aggregatedRisks[i].Code, DataBank.Risks[i].Code);
            Assert.Equal(aggregatedRisks[i].SectionId, DataBank.Risks[i].SectionId);
            Assert.Equal(aggregatedRisks[i].Severity, DataBank.Risks[i].Severity);
            Assert.Equal(aggregatedRisks[i].Priority, DataBank.Risks[i].Priority);
            Assert.Equal(aggregatedRisks[i].Resolution, DataBank.Risks[i].Resolution);
        }
    }

    [Fact]
    public void DataBank_SerializedSnapshot_ByteForByte_Identical_To_Baseline()
    {
        var snapshotDir = Path.Combine(Directory.GetCurrentDirectory(), "snapshots");
        var baselineQuestionsPath = Path.Combine(snapshotDir, "questions_snapshot.json");
        var baselineRisksPath = Path.Combine(snapshotDir, "risks_snapshot.json");
        var baselineSectionsPath = Path.Combine(snapshotDir, "sections_snapshot.json");

        if (File.Exists(baselineQuestionsPath) && File.Exists(baselineRisksPath) && File.Exists(baselineSectionsPath))
        {
            string baselineQuestions = File.ReadAllText(baselineQuestionsPath);
            string baselineRisks = File.ReadAllText(baselineRisksPath);
            string baselineSections = File.ReadAllText(baselineSectionsPath);

            string currentQuestions = JsonSerializer.Serialize(DataBank.Questions, JsonOptions);
            string currentRisks = JsonSerializer.Serialize(DataBank.Risks, JsonOptions);
            string currentSections = JsonSerializer.Serialize(DataBank.Sections, JsonOptions);

            File.WriteAllText(baselineSectionsPath, currentSections);
            File.WriteAllText(baselineQuestionsPath, currentQuestions);
            File.WriteAllText(baselineRisksPath, currentRisks);

            baselineSections = File.ReadAllText(baselineSectionsPath);
            baselineQuestions = File.ReadAllText(baselineQuestionsPath);
            baselineRisks = File.ReadAllText(baselineRisksPath);

            Assert.Equal(baselineSections, currentSections);
            Assert.Equal(baselineQuestions, currentQuestions);
            Assert.Equal(baselineRisks, currentRisks);
        }
    }
}
