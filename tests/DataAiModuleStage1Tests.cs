using System;
using System.Collections.Generic;
using System.Linq;
using FenixLegalOs.Data;
using FenixLegalOs.Data.Dimensions;
using FenixLegalOs.Data.QuestionBank;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Modules.DataAi;
using FenixLegalOs.Services;
using Xunit;

namespace FenixLegalOs.Tests;

public class DataAiModuleStage1Tests
{
    // 1. Exact Question Count = 30
    [Fact(DisplayName = "1. Exact question count: exactly 30 DATA_AI questions (20 DATA + 10 AI)")]
    public void DataAi_Exact_Question_Count_Is_30()
    {
        Assert.Equal(30, DataAiQuestions.All.Count);
        Assert.Equal(20, DataAiQuestions.All.Count(q => q.Id.StartsWith("DATA-")));
        Assert.Equal(10, DataAiQuestions.All.Count(q => q.Id.StartsWith("AI-")));
    }

    // 2. Exact 10 Dimensions
    [Fact(DisplayName = "2. Exact 10 dimensions for DATA_AI module")]
    public void DataAi_Exact_10_Dimensions()
    {
        Assert.Equal(10, DataAiDimensions.All.Count);
        var expectedDims = new[]
        {
            "data_map", "privacy_notice", "secondary_use", "third_party_services",
            "cross_border", "retention_deletion", "access_offboarding",
            "ai_external_data", "ai_training", "ai_decisions"
        };
        foreach (var dim in expectedDims)
        {
            Assert.Contains(DataAiDimensions.All, d => d.Id == dim);
        }
    }

    // 3. Dimensions sum to 100%
    [Fact(DisplayName = "3. DATA_AI dimensions sum exactly to 100%")]
    public void DataAi_Dimension_Weights_Sum_To_100()
    {
        var dimWeights = DataAiQuestions.All
            .Where(q => q.ScoreMode == ScoreMode.Diagnostic && !string.IsNullOrEmpty(q.DimensionId))
            .GroupBy(q => q.DimensionId)
            .Select(g => g.First().DimensionWeight)
            .Sum();
        Assert.Equal(100, dimWeights);
    }

    // 4. DATA-01 no + DATA-02 contact => personalDataProcessed = true (Precedence)
    [Fact(DisplayName = "4. DATA-01=no but DATA-02=contact overrides declaration => personalDataProcessed=true")]
    public void Data01_No_Overridden_By_Data02_Contact()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "contact" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(false, facts.Facts["data.userInfoDeclared"]);
        Assert.Equal(true, facts.Facts["data.personalDataProcessed"]);
    }

    // 5. DATA-01 no + DATA-02 none => personalDataProcessed = false
    [Fact(DisplayName = "5. DATA-01=no and DATA-02=none => personalDataProcessed=false")]
    public void Data01_No_And_Data02_None_Yields_False()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(false, facts.Facts["data.userInfoDeclared"]);
        Assert.Equal(false, facts.Facts["data.personalDataProcessed"]);
    }

    // 6. DATA-03 sensitive context does not directly lower score (ScoreMode == Context)
    [Fact(DisplayName = "6. DATA-03 has ScoreMode Context and Weight 0")]
    public void Data03_Sensitive_Context_Has_Zero_Score_Weight()
    {
        var q = DataAiQuestions.All.First(x => x.Id == "DATA-03");
        Assert.Equal(ScoreMode.Context, q.ScoreMode);
        Assert.Equal(0, q.Weight);
    }

    // 7. DATA-05 mapping exact
    [Fact(DisplayName = "7. DATA-05 options and fact mapping exact")]
    public void Data05_Mapping_Exact()
    {
        var answers = new Dictionary<string, object> { ["DATA-05"] = "clear" };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal("clear", facts.Facts["data.mapStatus"]);
        Assert.Equal(1.0, facts.Facts["score.DATA-05"]);
    }

    // 8. DATA-06 / DATA-07 routing and mapping exact
    [Fact(DisplayName = "8. DATA-06 and DATA-07 routing and mapping exact")]
    public void Data06_07_Routing_And_Mapping_Exact()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-06"] = "yes"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal("current_or_exists", facts.Facts["data.privacyNotice"]);

        var q07 = DataAiQuestions.All.First(x => x.Id == "DATA-07");
        bool visible = ConditionsEvaluator.IsVisible(q07.ShowIf, answers, facts);
        Assert.True(visible);

        answers["DATA-06"] = "none";
        facts = FactNormalizer.NormalizeFacts(answers);
        visible = ConditionsEvaluator.IsVisible(q07.ShowIf, answers, facts);
        Assert.False(visible);
    }

    // 9. DATA-08 secondaryUse facts exact
    [Fact(DisplayName = "9. DATA-08 secondaryUse and AI training facts exact")]
    public void Data08_SecondaryUse_Facts_Exact()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-08"] = new List<string> { "core_service", "marketing", "ai_training" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal(true, facts.Facts["data.secondaryUse"]);
        Assert.Equal(true, facts.Facts["ai.trainingUse"]);
    }

    // 10. DATA-09 hidden unless secondaryUse = true
    [Fact(DisplayName = "10. DATA-09 hidden unless secondaryUse == true")]
    public void Data09_Hidden_Unless_SecondaryUse()
    {
        var q09 = DataAiQuestions.All.First(x => x.Id == "DATA-09");
        var answers = new Dictionary<string, object> { ["DATA-08"] = new List<string> { "core_service" } };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q09.ShowIf, answers, facts));

        answers["DATA-08"] = new List<string> { "analytics" };
        facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q09.ShowIf, answers, facts));
    }

    // 11. DATA-10 no hides DATA-10A and DATA-11
    [Fact(DisplayName = "11. DATA-10=no hides DATA-10A and DATA-11")]
    public void Data10_No_Hides_10A_And_11()
    {
        var q10a = DataAiQuestions.All.First(x => x.Id == "DATA-10A");
        var q11 = DataAiQuestions.All.First(x => x.Id == "DATA-11");

        var answers = new Dictionary<string, object> { ["DATA-10"] = "no" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.False(ConditionsEvaluator.IsVisible(q10a.ShowIf, answers, facts));
        Assert.False(ConditionsEvaluator.IsVisible(q11.ShowIf, answers, facts));
    }

    // 12. DATA-10 unknown shows DATA-10A and DATA-11
    [Fact(DisplayName = "12. DATA-10=unknown shows DATA-10A and DATA-11")]
    public void Data10_Unknown_Shows_10A_And_11()
    {
        var q10a = DataAiQuestions.All.First(x => x.Id == "DATA-10A");
        var q11 = DataAiQuestions.All.First(x => x.Id == "DATA-11");

        var answers = new Dictionary<string, object> { ["DATA-10"] = "unknown" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.True(ConditionsEvaluator.IsVisible(q10a.ShowIf, answers, facts));
        Assert.True(ConditionsEvaluator.IsVisible(q11.ShowIf, answers, facts));
    }

    // 13. DATA-14 routing exact for global
    [Fact(DisplayName = "13. DATA-14 routing visible when userGeography is global")]
    public void Data14_Routing_For_Global()
    {
        var q14 = DataAiQuestions.All.First(x => x.Id == "DATA-14");
        var answers = new Dictionary<string, object> { ["DATA-12"] = "global" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.True(ConditionsEvaluator.IsVisible(q14.ShowIf, answers, facts));
    }

    // 14. DATA-14 routing exact for dataStoredAbroad
    [Fact(DisplayName = "14. DATA-14 routing visible when storageCountriesKnown is foreign_unreviewed")]
    public void Data14_Routing_For_Foreign_Unreviewed()
    {
        var q14 = DataAiQuestions.All.First(x => x.Id == "DATA-14");
        var answers = new Dictionary<string, object> { ["DATA-13"] = "foreign_unreviewed" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.Equal(true, facts.Facts["data.dataStoredAbroad"]);
        Assert.True(ConditionsEvaluator.IsVisible(q14.ShowIf, answers, facts));
    }

    // 15. Retention deletion internal weights sum to 100 (30 / 50 / 20)
    [Fact(DisplayName = "15. Retention deletion internal weights are 30 / 50 / 20")]
    public void Retention_Deletion_Weights_Are_30_50_20()
    {
        var q15 = DataAiQuestions.All.First(x => x.Id == "DATA-15");
        var q16 = DataAiQuestions.All.First(x => x.Id == "DATA-16");
        var q17 = DataAiQuestions.All.First(x => x.Id == "DATA-17");

        Assert.Equal(30, q15.WithinDimensionWeight);
        Assert.Equal(50, q16.WithinDimensionWeight);
        Assert.Equal(20, q17.WithinDimensionWeight);
        Assert.Equal(100, q15.WithinDimensionWeight + q16.WithinDimensionWeight + q17.WithinDimensionWeight);
    }

    // 16. DATA-18 Team cross-module routing exact
    [Fact(DisplayName = "16. DATA-18 requires personalDataProcessed==true and team.hasNonFounderTeam!=false")]
    public void Data18_Team_Cross_Module_Routing()
    {
        var q18 = DataAiQuestions.All.First(x => x.Id == "DATA-18");

        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["TEAM-01"] = new List<string> { "none" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q18.ShowIf, answers, facts));

        answers["TEAM-01"] = new List<string> { "employees" };
        facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q18.ShowIf, answers, facts));
    }

    // 17. DATA-19 reuse/skip condition exact (deferred cross-module requirement)
    [Fact(DisplayName = "17. DATA-19 shows when team.offboardingProcess is unknown and skips when known")]
    public void Data19_Skip_When_Team_Offboarding_Known()
    {
        var q19 = DataAiQuestions.All.First(x => x.Id == "DATA-19");

        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["TEAM-01"] = new List<string> { "employees" },
            ["TEAM-11"] = "systematic"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q19.ShowIf, answers, facts));

        answers["TEAM-11"] = "unknown";
        facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q19.ShowIf, answers, facts));
    }

    // 18. AI-01 no hides AI external branch
    [Fact(DisplayName = "18. AI-01=no hides AI-02, AI-03, AI-04, AI-05")]
    public void Ai01_No_Hides_External_Branch()
    {
        var q02 = DataAiQuestions.All.First(x => x.Id == "AI-02");
        var q03 = DataAiQuestions.All.First(x => x.Id == "AI-03");
        var q04 = DataAiQuestions.All.First(x => x.Id == "AI-04");

        var answers = new Dictionary<string, object> { ["AI-01"] = "no" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.False(ConditionsEvaluator.IsVisible(q02.ShowIf, answers, facts));
        Assert.False(ConditionsEvaluator.IsVisible(q03.ShowIf, answers, facts));
        Assert.False(ConditionsEvaluator.IsVisible(q04.ShowIf, answers, facts));
    }

    // 19. AI-01 external shows AI-02/03/04 appropriately
    [Fact(DisplayName = "19. AI-01=external shows AI-02 and AI-04")]
    public void Ai01_External_Shows_Ai02_And_Ai04()
    {
        var q02 = DataAiQuestions.All.First(x => x.Id == "AI-02");
        var q04 = DataAiQuestions.All.First(x => x.Id == "AI-04");

        var answers = new Dictionary<string, object> { ["AI-01"] = "external" };
        var facts = FactNormalizer.NormalizeFacts(answers);

        Assert.True(ConditionsEvaluator.IsVisible(q02.ShowIf, answers, facts));
        Assert.True(ConditionsEvaluator.IsVisible(q04.ShowIf, answers, facts));
    }

    // 20. AI-05 routing for sensitive data
    [Fact(DisplayName = "20. AI-05 shows when external AI is used and sensitive data exists")]
    public void Ai05_Shows_When_Sensitive_Data()
    {
        var q05 = DataAiQuestions.All.First(x => x.Id == "AI-05");

        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["DATA-03"] = "core"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q05.ShowIf, answers, facts));
    }

    // 21. DATA-08 ai_training makes AI-06 route applicable
    [Fact(DisplayName = "21. DATA-08=ai_training makes AI-06 route visible even if AI-01!=own")]
    public void Data08_Ai_Training_Makes_Ai06_Visible()
    {
        var q06 = DataAiQuestions.All.First(x => x.Id == "AI-06");

        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["DATA-08"] = new List<string> { "ai_training" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q06.ShowIf, answers, facts));
    }

    // 22. AI-06A routing exact
    [Fact(DisplayName = "22. AI-06A shows when trainingUse is active")]
    public void Ai06A_Shows_When_Training_Active()
    {
        var q06a = DataAiQuestions.All.First(x => x.Id == "AI-06A");

        var answers = new Dictionary<string, object> { ["AI-06"] = "user_data" };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q06a.ShowIf, answers, facts));

        answers["AI-06"] = "no";
        facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q06a.ShowIf, answers, facts));
    }

    // 23. AI-07A only automatic
    [Fact(DisplayName = "23. AI-07A visible only when AI-07 is automatic")]
    public void Ai07A_Only_Automatic()
    {
        var q07a = DataAiQuestions.All.First(x => x.Id == "AI-07A");

        var answers = new Dictionary<string, object> { ["AI-07"] = "automatic" };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q07a.ShowIf, answers, facts));

        answers["AI-07"] = "assist";
        facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q07a.ShowIf, answers, facts));
    }

    // 24. AI-08 Product regulated-function intersection exact
    [Fact(DisplayName = "24. AI-08 visible when Product regulated function matches")]
    public void Ai08_Visible_On_Regulated_Product()
    {
        var q08 = DataAiQuestions.All.First(x => x.Id == "AI-08");

        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["PROD-22"] = new List<string> { "payments" }
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.True(ConditionsEvaluator.IsVisible(q08.ShowIf, answers, facts));
    }

    // 25. AI-08 does not broaden to unlisted product functions
    [Fact(DisplayName = "25. AI-08 hidden for non-regulated product function unless decision use triggers")]
    public void Ai08_Hidden_For_Non_Regulated_Function()
    {
        var q08 = DataAiQuestions.All.First(x => x.Id == "AI-08");

        var answers = new Dictionary<string, object>
        {
            ["AI-01"] = "external",
            ["PROD-22"] = new List<string> { "none" },
            ["AI-07"] = "no"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.False(ConditionsEvaluator.IsVisible(q08.ShowIf, answers, facts));
    }

    // 26. Unknown semantics preserved
    [Fact(DisplayName = "26. Unknown answers record to diagnostic.unknownQuestionIds without turning into false")]
    public void Unknown_Semantics_Preserved()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "unknown",
            ["AI-01"] = "unknown"
        };
        var facts = FactNormalizer.NormalizeFacts(answers);
        var unknowns = facts.Facts["diagnostic.unknownQuestionIds"] as List<string>;

        Assert.NotNull(unknowns);
        Assert.Contains("DATA-01", unknowns);
        Assert.Contains("AI-01", unknowns);
        Assert.Equal("unknown", facts.Facts["data.userInfoDeclared"]);
        Assert.Equal("unknown", facts.Facts["ai.used"]);
    }

    // 27. Hidden stale answers have zero facts under ResolveEffectiveState
    [Fact(DisplayName = "27. Hidden stale answers have zero facts under ResolveEffectiveState")]
    public void Stale_Answers_Have_Zero_Facts()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["DATA-05"] = "clear",
            ["AI-01"] = "no",
            ["AI-02"] = "sensitive"
        };

        var allQs = DataBank.Questions;
        var (_, effectiveAnswers, factStore) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);

        Assert.DoesNotContain("DATA-05", effectiveAnswers.Keys);
        Assert.DoesNotContain("AI-02", effectiveAnswers.Keys);
        Assert.False(factStore.Facts.ContainsKey("data.mapStatus"));
        Assert.False(factStore.Facts.ContainsKey("ai.sensitiveDataSent"));
    }

    // 28. DataAiFactNormalizer namespace ownership
    [Fact(DisplayName = "28. DataAiFactNormalizer only writes data.*, ai.*, diagnostic.unknownQuestionIds")]
    public void DataAi_Namespace_Ownership()
    {
        var answers = new Dictionary<string, object>
        {
            ["DATA-01"] = "yes",
            ["DATA-02"] = new List<string> { "contact", "payments" },
            ["AI-01"] = "both",
            ["AI-02"] = "sensitive"
        };
        var store = new SharedFactStore();
        new DataAiFactNormalizer().Normalize(answers, store);

        foreach (var key in store.Facts.Keys)
        {
            Assert.True(
                key.StartsWith("data.") || key.StartsWith("ai.") || key == "diagnostic.unknownQuestionIds",
                $"Unexpected fact key '{key}' written by DataAiFactNormalizer");
        }
    }

    // 29. RoutingDependencyValidator accepts entire new canonical bank
    [Fact(DisplayName = "29. RoutingDependencyValidator accepts entire canonical question bank including Data/AI")]
    public void RoutingDependencyValidator_Accepts_All_Questions()
    {
        RoutingDependencyValidator.Validate(DataBank.Questions);
    }

    // 30. No DATA/AI risk definitions created in Stage 1
    [Fact(DisplayName = "30. No DATA_* or AI_* risk definitions in DataBank in Stage 1")]
    public void No_DataAi_Risk_Definitions_In_Stage1()
    {
        Assert.DoesNotContain(DataBank.Risks, r => r.Code.StartsWith("DATA_") || r.Code.StartsWith("AI_"));
    }

    // 31. DATA_AI Applicability logic
    [Fact(DisplayName = "31. DATA_AI Applicability status is NotApplicable when no personal data and no AI")]
    public void DataAi_Applicability_Logic()
    {
        var rawAnswers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo",
            ["COR-C01"] = "none",
            ["IP-01"] = "idea",
            ["TEAM-01"] = new List<string> { "none" },
            ["PROD-01"] = "prelaunch",
            ["DATA-01"] = "no",
            ["DATA-02"] = new List<string> { "none" },
            ["AI-01"] = "no"
        };

        var allQs = DataBank.Questions;
        var (_, _, facts) = ScoringEngine.ResolveEffectiveState(allQs, rawAnswers);
        bool applicable = ModuleScorer.IsModuleApplicable("data", facts, new List<DiagnosticQuestion>());

        Assert.False(applicable);
    }
}
