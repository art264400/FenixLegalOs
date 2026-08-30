using FenixLegalOs.Models.Enums;

namespace FenixLegalOs.Scoring.Validation;

public class ValidationError
{
    public string QuestionId { get; set; } = "";
    public ValidationErrorCode ErrorCode { get; set; } = ValidationErrorCode.UnknownQuestion;
    public string Message { get; set; } = "";
    public object? ProvidedValue { get; set; }

    public ValidationError() { }

    public ValidationError(string questionId, ValidationErrorCode errorCode, string message, object? providedValue = null)
    {
        QuestionId = questionId;
        ErrorCode = errorCode;
        Message = message;
        ProvidedValue = providedValue;
    }
}

public class ValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public List<ValidationError> Errors { get; set; } = new();

    public static ValidationResult Success() => new();

    public static ValidationResult Failure(List<ValidationError> errors) => new() { Errors = errors };

    public static ValidationResult Failure(string questionId, ValidationErrorCode errorCode, string message, object? providedValue = null)
    {
        return new ValidationResult
        {
            Errors = new List<ValidationError> { new(questionId, errorCode, message, providedValue) }
        };
    }

    public void AddError(string questionId, ValidationErrorCode errorCode, string message, object? providedValue = null)
    {
        Errors.Add(new ValidationError(questionId, errorCode, message, providedValue));
    }
}
