namespace MigrationTool.Core.Models;

public sealed class MigrationStateTable
{
    public string TableName { get; init; } = string.Empty;

    public MigrationTableStatus Status { get; set; }

    public long RowsMigrated { get; set; }

    public long? TotalRows { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }
}