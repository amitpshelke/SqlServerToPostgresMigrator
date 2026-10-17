namespace MigrationTool.Core.Models;

public sealed class ValidationResult
{
    public string TableName { get; init; } = string.Empty;

    public string ValidationType { get; init; } = string.Empty;

    public bool IsValid { get; init; }

    public string Message { get; init; } = string.Empty;
}