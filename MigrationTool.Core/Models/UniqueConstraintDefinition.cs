namespace MigrationTool.Core.Models;

public sealed class UniqueConstraintDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<string> Columns { get; init; } = [];
}