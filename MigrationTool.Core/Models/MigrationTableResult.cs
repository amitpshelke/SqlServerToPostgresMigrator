namespace MigrationTool.Core.Models;

public sealed class MigrationTableResult
{
    public string TableName { get; init; } = string.Empty;
    public MigrationTableStatus Status { get; init; }
    public long RowsMigrated { get; init; }
    public long? TotalRows { get; init; }

    public DateTime StartedAt { get; init; }
    public DateTime CompletedAt { get; init; }

    public TimeSpan Duration =>
        CompletedAt - StartedAt;

    public string? ErrorCode { get; init; }

    public string? ErrorMessage { get; init; }

    public bool IsSuccess =>
        Status == MigrationTableStatus.Success;
}