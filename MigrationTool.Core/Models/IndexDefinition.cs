namespace MigrationTool.Core.Models;

public sealed class IndexDefinition
{
    public string Name { get; init; } = string.Empty;

    public List<string> Columns { get; init; } = [];

    public bool IsUnique { get; init; }

    public bool IsClustered { get; init; }

    public bool IsPrimaryKey { get; init; }

    public bool IsUniqueConstraint { get; init; }

    public bool IsDisabled { get; init; }

    public string? FilterDefinition { get; init; }

    public List<string> IncludedColumns { get; init; } = [];
}