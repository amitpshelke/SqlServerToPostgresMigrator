namespace MigrationTool.Core.Models;

public sealed class MigrationState
{
    public string MigrationId { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; set; }

    public MigrationStateStatus Status { get; set; }

    public int PlannedTables { get; init; }

    public List<MigrationStateTable> Tables { get; init; } = [];

    public int SuccessfulTables =>
        Tables.Count(t =>
            t.Status == MigrationTableStatus.Success);

    public int FailedTables =>
        Tables.Count(t =>
            t.Status == MigrationTableStatus.Failed);

    public int CancelledTables =>
        Tables.Count(t =>
            t.Status == MigrationTableStatus.Cancelled);

    public int NotStartedTables =>
        Math.Max(
            0,
            PlannedTables - Tables.Count);

    public long TotalRowsMigrated =>
        Tables.Sum(t => t.RowsMigrated);
}