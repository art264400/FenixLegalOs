using FenixLegalOs.Data;
using FenixLegalOs.Models;
using FenixLegalOs.Models.Enums;
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
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50.0, ["founder_2"] = 50.0 },
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
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == ValidationErrorCode.EmptyValue);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-02" && e.ErrorCode == ValidationErrorCode.EmptyValue);
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
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == ValidationErrorCode.InvalidOption);
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
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-01" && e.ErrorCode == ValidationErrorCode.InvalidOption);
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
        Assert.Contains(result.Errors, e => e.QuestionId == "UNKNOWN-999" && e.ErrorCode == ValidationErrorCode.UnknownQuestion);
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
            ["IP-02"] = new[] { "code", "invalid_item_xyz" }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "IP-02" && e.ErrorCode == ValidationErrorCode.InvalidOption);
    }

    [Fact]
    public void Tampering_MultiSelectMutualExclusion_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["IP-03"] = new[] { "none", "founders" } // 'none' cannot be combined with specific options
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "IP-03" && e.ErrorCode == ValidationErrorCode.MutuallyExclusiveConflict);
    }

        [Fact]
    public void CanonicalFndC02_ObjectMap_PassesValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C01"] = "2",
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 50, ["founder_2"] = 50 }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Tampering_EquityOutOfRange_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C02"] = new Dictionary<string, object> { ["founder_1"] = 150.0, ["founder_2"] = -10.0 }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-C02" && e.ErrorCode == ValidationErrorCode.OutOfRangeShare);
    }

    [Fact]
    public void Tampering_EquityArrayFormat_FailsValidation_With_InvalidType()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C02"] = new[] { 50.0, 50.0 }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-C02" && e.ErrorCode == ValidationErrorCode.InvalidType);
    }

    [Fact]
    public void Tampering_EquityFreeFormString_FailsValidation_With_InvalidType()
    {
        var answers = new Dictionary<string, object>
        {
            ["FND-C02"] = "50% each"
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "FND-C02" && e.ErrorCode == ValidationErrorCode.InvalidType);
    }

    [Fact]
    public void Tampering_EntityBuilderInvalidJurisdiction_FailsValidation()
    {
        var answers = new Dictionary<string, object>
        {
            ["COR-C02C"] = new object[]
            {
                new Dictionary<string, object> { ["name"] = "SubCo", ["jurisdiction"] = "atlantis_fake_jur" }
            }
        };

        var result = AnswerValidator.Validate(answers, _repo.GetQuestions());
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.QuestionId == "COR-C02C" && e.ErrorCode == ValidationErrorCode.InvalidJurisdiction);
    }

    [Fact]
    public void HardenedFactStore_InvalidInput_DoesNotCorruptStore()
    {
        // Even if invalid answers bypass validator, FactNormalizer should ignore or not fabricate invalid facts
        var answers = new Dictionary<string, object>
        {
            ["FND-01"] = "invalid_status",
            ["FND-04"] = "unknown_status"
        };

        var facts = FactNormalizer.NormalizeFacts(answers);

        // Raw invalid strings must not produce canonical dispute booleans or valid statuses
        Assert.False(facts.Facts.TryGetValue("founders.disputeLevel", out var dl) && dl is "active" or "formal");
        Assert.False(facts.Facts.TryGetValue("founders.equityClarity", out var eq) && eq is "registered" or "signed" or "dispute");
    }
}
