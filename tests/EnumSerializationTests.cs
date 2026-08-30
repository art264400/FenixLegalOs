using System.Text.Json;
using FenixLegalOs.Models.Enums;
using Xunit;

namespace FenixLegalOs.Tests;

public class EnumSerializationTests
{
    // =========================================================================
    // 1. EXACT CANONICAL ROUND-TRIP SERIALIZATION & DESERIALIZATION TESTS
    // =========================================================================

    [Theory]
    [InlineData(QuestionType.Single, "\"single\"")]
    [InlineData(QuestionType.Multiple, "\"multiple\"")]
    [InlineData(QuestionType.Boolean, "\"boolean\"")]
    [InlineData(QuestionType.Text, "\"text\"")]
    [InlineData(QuestionType.Number, "\"number\"")]
    [InlineData(QuestionType.EquityInputs, "\"equity_inputs\"")]
    [InlineData(QuestionType.EntityBuilder, "\"entity_builder\"")]
    public void QuestionType_ExactRoundTrip(QuestionType val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<QuestionType>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ScoreMode.Context, "\"context\"")]
    [InlineData(ScoreMode.Diagnostic, "\"diagnostic\"")]
    [InlineData(ScoreMode.Trigger, "\"trigger\"")]
    public void ScoreMode_ExactRoundTrip(ScoreMode val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ScoreMode>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ConditionalOperator.Eq, "\"eq\"")]
    [InlineData(ConditionalOperator.Neq, "\"neq\"")]
    [InlineData(ConditionalOperator.In, "\"in\"")]
    [InlineData(ConditionalOperator.NotIn, "\"notIn\"")]
    [InlineData(ConditionalOperator.Contains, "\"contains\"")]
    [InlineData(ConditionalOperator.NotContains, "\"notContains\"")]
    [InlineData(ConditionalOperator.Answered, "\"answered\"")]
    [InlineData(ConditionalOperator.Gte, "\"gte\"")]
    [InlineData(ConditionalOperator.Lte, "\"lte\"")]
    public void ConditionalOperator_ExactRoundTrip(ConditionalOperator val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ConditionalOperator>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(RiskSeverity.Info, "\"INFO\"")]
    [InlineData(RiskSeverity.Medium, "\"MEDIUM\"")]
    [InlineData(RiskSeverity.High, "\"HIGH\"")]
    [InlineData(RiskSeverity.Critical, "\"CRITICAL\"")]
    [InlineData(RiskSeverity.Blocker, "\"BLOCKER\"")]
    public void RiskSeverity_ExactRoundTrip(RiskSeverity val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<RiskSeverity>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(RiskPriority.Now, "\"NOW\"")]
    [InlineData(RiskPriority.ThirtyDays, "\"30_DAYS\"")]
    [InlineData(RiskPriority.BeforeRound, "\"BEFORE_ROUND\"")]
    [InlineData(RiskPriority.Later, "\"LATER\"")]
    public void RiskPriority_ExactRoundTrip(RiskPriority val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<RiskPriority>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ResolutionType.SelfService, "\"self_service\"")]
    [InlineData(ResolutionType.CheckWithLawyer, "\"check_with_lawyer\"")]
    [InlineData(ResolutionType.LawyerRequired, "\"lawyer_required\"")]
    public void ResolutionType_ExactRoundTrip(ResolutionType val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ResolutionType>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ConfidenceClass.Known, "\"known\"")]
    [InlineData(ConfidenceClass.Partial, "\"partial\"")]
    [InlineData(ConfidenceClass.Unknown, "\"unknown\"")]
    public void ConfidenceClass_ExactRoundTrip(ConfidenceClass val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ConfidenceClass>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ApplicabilityStatus.Applicable, "\"APPLICABLE\"")]
    [InlineData(ApplicabilityStatus.NotApplicable, "\"N_A\"")]
    public void ApplicabilityStatus_ExactRoundTrip(ApplicabilityStatus val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ApplicabilityStatus>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(LegalScoreLevel.Strong, "\"strong\"")]
    [InlineData(LegalScoreLevel.Attention, "\"attention\"")]
    [InlineData(LegalScoreLevel.MaterialGaps, "\"material_gaps\"")]
    [InlineData(LegalScoreLevel.StructuralRisks, "\"structural_risks\"")]
    public void LegalScoreLevel_ExactRoundTrip(LegalScoreLevel val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<LegalScoreLevel>(json);
        Assert.Equal(val, deserialized);
    }

    [Theory]
    [InlineData(ValidationErrorCode.UnknownQuestion, "\"UNKNOWN_QUESTION\"")]
    [InlineData(ValidationErrorCode.NullValue, "\"NULL_VALUE\"")]
    [InlineData(ValidationErrorCode.EmptyValue, "\"EMPTY_VALUE\"")]
    [InlineData(ValidationErrorCode.InvalidType, "\"INVALID_TYPE\"")]
    [InlineData(ValidationErrorCode.InvalidOption, "\"INVALID_OPTION\"")]
    [InlineData(ValidationErrorCode.EmptySelection, "\"EMPTY_SELECTION\"")]
    [InlineData(ValidationErrorCode.EmptyItem, "\"EMPTY_ITEM\"")]
    [InlineData(ValidationErrorCode.MutuallyExclusiveConflict, "\"MUTUALLY_EXCLUSIVE_CONFLICT\"")]
    [InlineData(ValidationErrorCode.InvalidNumber, "\"INVALID_NUMBER\"")]
    [InlineData(ValidationErrorCode.EmptyShares, "\"EMPTY_SHARES\"")]
    [InlineData(ValidationErrorCode.OutOfRangeShare, "\"OUT_OF_RANGE_SHARE\"")]
    [InlineData(ValidationErrorCode.InvalidEntityFormat, "\"INVALID_ENTITY_FORMAT\"")]
    [InlineData(ValidationErrorCode.InvalidJurisdiction, "\"INVALID_JURISDICTION\"")]
    public void ValidationErrorCode_ExactRoundTrip(ValidationErrorCode val, string expectedJson)
    {
        string json = JsonSerializer.Serialize(val);
        Assert.Equal(expectedJson, json);

        var deserialized = JsonSerializer.Deserialize<ValidationErrorCode>(json);
        Assert.Equal(val, deserialized);
    }

    // =========================================================================
    // 2. FAIL-CLOSED NEGATIVE TESTS (UNKNOWN VALUES MUST THROW JSONEXCEPTION)
    // =========================================================================

    [Theory]
    [InlineData("\"LOW\"")] // Canonical cleanup: LOW removed from runtime vocabulary
    [InlineData("\"SUPER_CRITICAL\"")]
    [InlineData("\"EXTREME\"")]
    [InlineData("\"medium_risk\"")]
    [InlineData("\"123\"")]
    public void RiskSeverity_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RiskSeverity>(invalidJson));
    }

    [Theory]
    [InlineData("\"diagnostik\"")]
    [InlineData("\"score\"")]
    [InlineData("\"unknown_mode\"")]
    public void ScoreMode_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ScoreMode>(invalidJson));
    }

    [Theory]
    [InlineData("\"equity_split\"")] // Verified: canonical is equity_inputs, alias rejected fail-closed
    [InlineData("\"dropdown\"")]
    [InlineData("\"matrix\"")]
    public void QuestionType_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuestionType>(invalidJson));
    }

    [Theory]
    [InlineData("\"unknown_op\"")]
    [InlineData("\"like\"")]
    [InlineData("\"between\"")]
    public void ConditionalOperator_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ConditionalOperator>(invalidJson));
    }

    [Theory]
    [InlineData("\"self\"")] // Legacy value replaced with self_service, fail-closed on self
    [InlineData("\"auto\"")]
    [InlineData("\"mandatory_lawyer\"")]
    public void ResolutionType_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ResolutionType>(invalidJson));
    }

    [Theory]
    [InlineData("\"NOT_APPLICABLE\"")]
    [InlineData("\"NA\"")]
    [InlineData("\"active\"")]
    public void ApplicabilityStatus_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ApplicabilityStatus>(invalidJson));
    }

    [Theory]
    [InlineData("\"green\"")]
    [InlineData("\"red\"")]
    [InlineData("\"good\"")]
    public void LegalScoreLevel_UnknownValues_ThrowsJsonException(string invalidJson)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LegalScoreLevel>(invalidJson));
    }
}
