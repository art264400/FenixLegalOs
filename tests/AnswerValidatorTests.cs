using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Repositories;
using FenixLegalOs.Scoring.Core;
using FenixLegalOs.Scoring.Validation;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FenixLegalOs.Tests;

public class AnswerValidatorTests
{
    private readonly QuestionRepository _repo;
    private readonly string _tempDbPath;

    public AnswerValidatorTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"test_fenix_val_{Guid.NewGuid():N}.db");
        var inMemoryConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FENIX_DB_PATH"] = _tempDbPath
        }).Build();

        var dbInit = new DbInitializer(inMemoryConfig);
        dbInit.Initialize();
        _repo = new QuestionRepository(dbInit);
    }

    [Fact]
    public void ValidAnswers_PassValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new[] { 50.0, 50.0 },
            ["FND-C03"] = "none",
            ["FND-C04"] = "signed",
            ["FND-01"] = "none",
            ["FND-02"] = "written",
            ["FND-03"] = "aligned",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "defined",
            ["FND-06"] = "written",
            ["FND-06A"] = "majority",
            ["FND-07"] = "full",
            ["FND-08"] = "full",
            ["FND-09"] = "documented",
            ["FND-10"] = "none",
            ["FND-11"] = "aligned",
            ["COR-C01"] = "one",
            ["COR-C02A"] = "kz",
            ["COR-01"] = "match",
            ["IP-01"] = "ready",
            ["IP-02"] = new[] { "code", "design" },
            ["IP-03"] = new[] { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void MissingAnswers_DoNotProduceValidationErrors()
    {
        // Partial answers (e.g. only 1 question answered)
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "solo"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.True(result.IsValid);

        var facts = FactNormalizer.NormalizeFacts(answers);
        // Ensure missing answers do NOT create empty strings or false defaults
        Assert.False(facts.Facts.ContainsKey("founders.roleClarity"));
        Assert.False(facts.Facts.ContainsKey("capital.ownershipMatch"));
        Assert.False(facts.Facts.ContainsKey("ip.overallRightsEvidence"));
    }

    [Fact]
    public void ExplicitUnknown_IsValidAndPreservesUnknown()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["IP-01"] = "ready",
            ["IP-04"] = "some",
            ["IP-10"] = "unknown",
            ["IP-10A"] = "unknown",
            ["IP-11A"] = "unknown"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.True(result.IsValid);

        var facts = FactNormalizer.NormalizeFacts(answers);
        Assert.Equal("unknown", facts.Facts["ip.externalEmployerCreation"]);
        Assert.Equal("unknown", facts.Facts["ip.employerResourcesUsed"]);
        Assert.Equal("unknown", facts.Facts["ip.thirdPartyTermsReview"]);
    }

    [Fact]
    public void Tampering_EmptyOrWhitespaceString_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = "",
            ["FND-02"] = "   "
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == "EMPTY_VALUE");
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-02" && e.ErrorCode == "EMPTY_VALUE");
    }

    [Fact]
    public void Tampering_RandomInvalidAnswerId_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = "hacked_exploit_value_123"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == "INVALID_OPTION");
    }

    [Fact]
    public void Tampering_CrossQuestionAnswerId_FailsValidation()
    {
        // "single" is a valid option for COR-C01, but completely invalid for FND-01
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = "single"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == "INVALID_OPTION");
    }

    [Fact]
    public void Tampering_UnknownQuestionId_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["UNKNOWN-999"] = "some_value"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "UNKNOWN-999" && e.ErrorCode == "UNKNOWN_QUESTION");
    }

    [Fact]
    public void Tampering_InvalidType_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = 12345 // Number instead of string
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01");
    }

    [Fact]
    public void Tampering_MultiSelectInvalidItem_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["IP-03"] = new[] { "founders", "nonexistent_creator_role" }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "IP-03" && e.ErrorCode == "INVALID_OPTION");
    }

    [Fact]
    public void Tampering_MultiSelectMutuallyExclusiveConflict_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["IP-02"] = new[] { "none", "code" } // "none" combined with "code"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "IP-02" && e.ErrorCode == "MUTUALLY_EXCLUSIVE_CONFLICT");
    }

    [Fact]
    public void Tampering_MalformedEquitySplit_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C02"] = new[] { 150.0, -20.0 } // Invalid percentages
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-C02" && e.ErrorCode == "OUT_OF_RANGE_SHARE");
    }

    [Fact]
    public void Tampering_MalformedEntityBuilder_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["COR-C02C"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(
                @"[ { ""jurisdiction"": ""invalid_country_xyz"" } ]")
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "COR-C02C" && e.ErrorCode == "INVALID_JURISDICTION");
    }

    [Fact]
    public void FactStore_InvariantCheck_NoEmptyStringsOrArbitraryValues()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new[] { 60.0, 40.0 },
            ["FND-C03"] = "resolved",
            ["FND-C04"] = "signed",
            ["FND-01"] = "minor",
            ["FND-02"] = "written",
            ["FND-03"] = "aligned",
            ["FND-04"] = "registered",
            ["FND-05"] = "vesting",
            ["FND-05A"] = "defined",
            ["FND-06"] = "written",
            ["FND-06A"] = "majority",
            ["FND-07"] = "full",
            ["FND-08"] = "full",
            ["FND-09"] = "documented",
            ["FND-10"] = "unrelated",
            ["FND-11"] = "aligned",
            ["COR-C01"] = "multiple",
            ["COR-C02A"] = "kz",
            ["COR-C02B"] = "2",
            ["COR-01"] = "match",
            ["COR-02"] = "complete",
            ["COR-03"] = "none",
            ["COR-04"] = "complete",
            ["COR-04A"] = "yes",
            ["COR-05"] = "systematic",
            ["COR-06"] = "clear_limits",
            ["COR-07"] = "aligned",
            ["COR-08"] = "organized",
            ["IP-01"] = "ready",
            ["IP-02"] = new[] { "code" },
            ["IP-03"] = new[] { "founders" },
            ["IP-04"] = "all",
            ["IP-05"] = "assigned",
            ["IP-06"] = "all",
            ["IP-07"] = "all",
            ["IP-08"] = "none",
            ["IP-09"] = "confirmed",
            ["IP-10"] = "no",
            ["IP-10A"] = "no",
            ["IP-11"] = "no",
            ["IP-11A"] = "no",
            ["IP-12"] = "no",
            ["IP-13"] = "company",
            ["IP-14"] = "company",
            ["IP-15"] = "clear"
        };

        var validationResult = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.True(validationResult.IsValid, string.Join("; ", validationResult.Errors.Select(e => $"{e.QuestionId}: {e.Message}")));

        var facts = FactNormalizer.NormalizeFacts(answers);

        foreach (var kvp in facts.Facts)
        {
            if (kvp.Value is string strVal)
            {
                Assert.False(string.IsNullOrWhiteSpace(strVal), $"Fact '{kvp.Key}' has an empty/whitespace string value!");
            }
            Assert.NotNull(kvp.Value);
        }
    }
}
