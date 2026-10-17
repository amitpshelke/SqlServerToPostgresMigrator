namespace MigrationTool.Core.Models;

public sealed class ColumnDefinition
{
    public string Name { get; init; } = string.Empty;

    public string SqlServerDataType { get; init; } = string.Empty;

    public string PostgreSqlDataType { get; set; } = string.Empty;

    public int? MaxLength { get; init; }

    public byte? Precision { get; init; }

    public byte? Scale { get; init; }

    public bool IsNullable { get; init; }

    public bool IsIdentity { get; init; }

    public long? IdentitySeed { get; set; }

    public long? IdentityIncrement { get; set; }

    public string? DefaultValue { get; init; }

    public int OrdinalPosition { get; init; }
}