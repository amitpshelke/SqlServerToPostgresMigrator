using System.Reflection;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using MigrationTool.Core.Services;

namespace MigrationTool.Core.Tests;

public class ResumeMigrationCancellationTests
{
    [Fact]
    public async Task ResumeMigrationAsync_WhenCancelledBetweenTables_PersistsCancelledState()
    {
        using var cancellationTokenSource =
            new CancellationTokenSource();

        var now = DateTime.UtcNow;

        var state = new MigrationState
        {
            MigrationId = "migration-test-resume-cancel",
            StartedAt = now.AddMinutes(-10),
            Status = MigrationStateStatus.Failed,
            PlannedTables = 2,
            Tables =
            [
                new MigrationStateTable
                {
                    TableName = "dbo.tbl_Candidate",
                    Status = MigrationTableStatus.Success,
                    RowsMigrated = 10,
                    TotalRows = 10,
                    StartedAt = now.AddMinutes(-9),
                    CompletedAt = now.AddMinutes(-8)
                },
                new MigrationStateTable
                {
                    TableName = "dbo.tbl_Job",
                    Status = MigrationTableStatus.Failed,
                    RowsMigrated = 0,
                    TotalRows = 5,
                    StartedAt = now.AddMinutes(-7),
                    CompletedAt = now.AddMinutes(-6),
                    ErrorCode = "23505",
                    ErrorMessage = "Previous migration failure"
                }
            ]
        };

        var stateStore = new InMemoryMigrationStateStore(state);

        // Simulate Ctrl+C after the saved state has been loaded
        // but before the next table starts migrating.
        var dependencySorter =
            new CancellingTableDependencySorter(
                cancellationTokenSource);

        var service = new DataMigrationService(
            CreateUnusedProxy<IDataReader>(),
            CreateUnusedProxy<IDataWriter>(),
            CreateUnusedProxy<IIdentitySynchronizer>(),
            new NoOpMigrationLogger(),
            dependencySorter,
            stateStore,
            new MigrationResumePlanner());

        var tables = new[]
        {
            CreateTable("tbl_Candidate"),
            CreateTable("tbl_Job")
        };

        var report = await service.ResumeMigrationAsync(
            state.MigrationId,
            tables,
            cancellationToken: cancellationTokenSource.Token);

        // The original migration ID must remain unchanged.
        Assert.Equal(
            "migration-test-resume-cancel",
            stateStore.State.MigrationId);

        // The overall state must record cancellation.
        Assert.Equal(
            MigrationStateStatus.Cancelled,
            stateStore.State.Status);

        Assert.NotNull(stateStore.State.CompletedAt);

        // A previously successful table must remain successful.
        Assert.Equal(
            MigrationTableStatus.Success,
            GetTableState(stateStore.State, "dbo.tbl_Candidate").Status);

        // The table that could not be retried must be marked cancelled.
        var cancelledTable =
            GetTableState(stateStore.State, "dbo.tbl_Job");

        Assert.Equal(
            MigrationTableStatus.Cancelled,
            cancelledTable.Status);

        // The saved state must not lose the successful result.
        Assert.Equal(1, stateStore.State.SuccessfulTables);
        Assert.Equal(1, stateStore.State.CancelledTables);
        Assert.Equal(0, stateStore.State.FailedTables);

        // The returned report must reflect the same result.
        Assert.Equal(
            MigrationTableStatus.Success,
            report.Tables.Single(
                t => t.TableName == "dbo.tbl_Candidate").Status);

        Assert.Equal(
            MigrationTableStatus.Cancelled,
            report.Tables.Single(
                t => t.TableName == "dbo.tbl_Job").Status);
    }

    private static TableDefinition CreateTable(string tableName)
    {
        return new TableDefinition
        {
            SchemaName = "dbo",
            TableName = tableName
        };
    }

    private static MigrationStateTable GetTableState(
        MigrationState state,
        string tableName)
    {
        return state.Tables.Single(
            t => string.Equals(
                t.TableName,
                tableName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static T CreateUnusedProxy<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnusedDependencyProxy>();
    }

    private sealed class InMemoryMigrationStateStore
        : IMigrationStateStore
    {
        public MigrationState State { get; private set; }

        public InMemoryMigrationStateStore(MigrationState state)
        {
            State = state;
        }

        public Task SaveAsync(
            MigrationState state,
            CancellationToken cancellationToken = default)
        {
            State = state;
            return Task.CompletedTask;
        }

        public Task<MigrationState?> LoadAsync(
            string migrationId,
            CancellationToken cancellationToken = default)
        {
            MigrationState? result =
                string.Equals(
                    migrationId,
                    State.MigrationId,
                    StringComparison.OrdinalIgnoreCase)
                    ? State
                    : null;

            return Task.FromResult(result);
        }

        public Task UpdateTableAsync(
            string migrationId,
            MigrationStateTable table,
            CancellationToken cancellationToken = default)
        {
            var existing = State.Tables.FirstOrDefault(
                t => string.Equals(
                    t.TableName,
                    table.TableName,
                    StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                State.Tables.Add(table);
            }
            else
            {
                State.Tables[State.Tables.IndexOf(existing)] = table;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CancellingTableDependencySorter
        : ITableDependencySorter
    {
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellingTableDependencySorter(
            CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public IReadOnlyList<TableDefinition> Sort(
            IReadOnlyList<TableDefinition> tables)
        {
            _cancellationTokenSource.Cancel();
            return tables;
        }
    }

    private sealed class NoOpMigrationLogger : IMigrationLogger
    {
        public void MigrationStarted(int totalTables) { }

        public void TableMigrationStarted(string tableName) { }

        public void TableMigrationCompleted(MigrationTableResult result) { }

        public void TableMigrationFailed(MigrationTableResult result) { }

        public void MigrationCompleted(MigrationReport report) { }

        public void MigrationCancelled(MigrationReport report) { }
    }
}

// These dependencies should never be called in this cancellation test.
public class UnusedDependencyProxy : DispatchProxy
{
    protected override object? Invoke(
        MethodInfo? targetMethod,
        object?[]? args)
    {
        throw new InvalidOperationException(
            $"Unexpected dependency invocation: {targetMethod?.Name}");
    }
}