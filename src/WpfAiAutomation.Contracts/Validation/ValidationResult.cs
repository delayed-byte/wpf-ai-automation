namespace WpfAiAutomation.Contracts;

public sealed record ValidationError(string Field, string Message, ToolErrorCode ErrorCode = ToolErrorCode.InvalidRequest);

public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public static ValidationResult Success { get; } = new(Array.Empty<ValidationError>());
}
