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
        Assert.Equal(8, DataBank.Sections.Count);

        // 2. Questions Total & by Module
        Assert.Equal(150, DataBank.Questions.Count);
        Assert.Equal(17, FoundersQuestions.All.Count);
        Assert.Equal(15, CorporateQuestions.All.Count);
        Assert.Equal(17, IpQuestions.All.Count);
        Assert.Equal(17, TeamQuestions.All.Count);
        Assert.Equal(28, ProductQuestions.All.Count);
        Assert.Equal(30, DataAiQuestions.All.Count);
        Assert.Equal(9, ContractQuestions.All.Count);
        Assert.Equal(17, InvestmentQuestions.All.Count);

        // 3. Risks Total & by Module
        Assert.Equal(100, DataBank.Risks.Count);
        Assert.Equal(18, FoundersRisks.All.Count);
        Assert.Equal(11, CorporateRisks.All.Count);
        Assert.Equal(12, IpRisks.All.Count);
        Assert.Equal(13, TeamRisks.All.Count);
        Assert.Equal(13, ProductRisks.All.Count);
        Assert.Equal(15, DataAiRisks.All.Count);
        Assert.Equal(6, ContractRisks.All.Count);
        Assert.Equal(12, InvestmentRisks.All.Count);

        // 4. Questions Aggregation Matches Module Lists Exactly
        var aggregatedQuestions = new List<DiagnosticQuestion>();
        aggregatedQuestions.AddRange(FoundersQuestions.All);
        aggregatedQuestions.AddRange(CorporateQuestions.All);
        aggregatedQuestions.AddRange(IpQuestions.All);
        aggregatedQuestions.AddRange(TeamQuestions.All);
        aggregatedQuestions.AddRange(ProductQuestions.All);
        aggregatedQuestions.AddRange(DataAiQuestions.All);
        aggregatedQuestions.AddRange(ContractQuestions.All);
        aggregatedQuestions.AddRange(InvestmentQuestions.All);

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
        aggregatedRisks.AddRange(ProductRisks.All);
        aggregatedRisks.AddRange(DataAiRisks.All);
        aggregatedRisks.AddRange(ContractRisks.All);
        aggregatedRisks.AddRange(InvestmentRisks.All);

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
}
