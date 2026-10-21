namespace MigrationTool.Core.Models;

public sealed class DefaultConstraintDefinition
{
    public string Name { get; init; } = string.Empty;

    public string ColumnName { get; init; } = string.Empty;

    public string Definition { get; init; } = string.Empty;
}