namespace FenixLegalOs.Scoring.Validation;

public class ValidationError
{
    public string QuestionId { get; set; } = "";
    public string ErrorCode { get; set; } = "";
    public string Message { get; set; } = "";
    public object? ProvidedValue { get; set; }

    public ValidationError() { }

    public ValidationError(string questionId, string errorCode, string message, object? providedValue = null)
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

    public static ValidationResult Failure(string questionId, string errorCode, string message, object? providedValue = null)
    {
        return new ValidationResult
        {
            Errors = new List<ValidationError> { new(questionId, errorCode, message, providedValue) }
        };
    }

    public void AddError(string questionId, string errorCode, string message, object? providedValue = null)
    {
        Errors.Add(new ValidationError(questionId, errorCode, message, providedValue));
    }
}
