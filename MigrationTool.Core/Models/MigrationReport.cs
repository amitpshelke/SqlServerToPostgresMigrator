namespace MigrationTool.Core.Models;

public sealed class MigrationReport
{
    public DateTime StartedAt { get; init; }

    public DateTime CompletedAt { get; init; }

    public TimeSpan Duration =>
        CompletedAt - StartedAt;

    public List<MigrationTableResult> Tables { get; init; } = [];

    public int PlannedTables { get; init; }

    public int TotalTables =>
        Tables.Count;

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
            PlannedTables - TotalTables);

    public long TotalRowsMigrated =>
        Tables.Sum(t => t.RowsMigrated);

    public bool IsSuccess =>
        FailedTables == 0 &&
        CancelledTables == 0;

    public IReadOnlyList<MigrationTableResult> FailedTableResults =>
        Tables
            .Where(t =>
                t.Status == MigrationTableStatus.Failed)
            .ToList();

    public IReadOnlyList<MigrationTableResult> SuccessfulTableResults =>
        Tables
            .Where(t =>
                t.Status == MigrationTableStatus.Success)
            .ToList();

    public IReadOnlyList<MigrationTableResult> CancelledTableResults =>
        Tables
            .Where(t =>
                t.Status == MigrationTableStatus.Cancelled)
            .ToList();
}