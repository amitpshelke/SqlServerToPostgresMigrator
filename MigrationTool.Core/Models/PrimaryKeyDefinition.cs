namespace MigrationTool.Core.Models;

public sealed class PrimaryKeyDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<string> Columns { get; init; } = [];
}