namespace SahelBundleKeyboard.Core.Validation;

public sealed record ValidationError(string Field, string Message);

public sealed class ValidationResult
{
    private readonly List<ValidationError> _errors = [];

    public bool IsValid => _errors.Count == 0;

    public IReadOnlyList<ValidationError> Errors => _errors;

    public void AddError(string field, string message)
    {
        _errors.Add(new ValidationError(field, message));
    }

    public ValidationResult Merge(ValidationResult other)
    {
        _errors.AddRange(other._errors);
        return this;
    }

    public string ToAggregatedMessage()
    {
        return string.Join(Environment.NewLine, _errors.Select(e => e.Message));
    }

    public static ValidationResult Success() => new();
}
