namespace MigrationTool.Core.Models;

public sealed class ValidationIssue
{
    public string TableName { get; init; } = string.Empty;

    public string ObjectName { get; init; } = string.Empty;

    public string ValidationType { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}