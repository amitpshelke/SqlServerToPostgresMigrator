namespace MigrationTool.Core.Models;

public sealed class TableDefinition
{
    public string SchemaName { get; init; } = string.Empty;

    public string TableName { get; init; } = string.Empty;

    public List<ColumnDefinition> Columns { get; init; } = [];

    public PrimaryKeyDefinition? PrimaryKey { get; set; }

    public List<ForeignKeyDefinition> ForeignKeys { get; init; } = [];

    public List<UniqueConstraintDefinition> UniqueConstraints { get; init; } = [];

    public List<CheckConstraintDefinition> CheckConstraints { get; init; } = [];

    public List<IndexDefinition> Indexes { get; init; } = [];

    public string FullName => $"{SchemaName}.{TableName}";
}