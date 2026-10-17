namespace MigrationTool.Core.Models;

public sealed class CheckConstraintDefinition
{
    public string Name { get; init; } = string.Empty;

    public string Definition { get; init; } = string.Empty;
}