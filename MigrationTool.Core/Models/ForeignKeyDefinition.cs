namespace MigrationTool.Core.Models;

public sealed class ForeignKeyDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<string> Columns { get; init; } = [];

    public string ReferencedSchema { get; init; } = string.Empty;

    public string ReferencedTable { get; init; } = string.Empty;

    public List<string> ReferencedColumns { get; init; } = [];

    public string DeleteAction { get; init; } = string.Empty;

    public string UpdateAction { get; init; } = string.Empty;
}