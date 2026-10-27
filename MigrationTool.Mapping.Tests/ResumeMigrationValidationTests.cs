using System.Reflection;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using MigrationTool.Core.Services;

namespace MigrationTool.Core.Tests;

public class ResumeMigrationValidationTests
{
    [Fact]
    public async Task ResumeMigrationAsync_ShouldRejectEmptyMigrationId()
    {
        var service = CreateService(null);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ResumeMigrationAsync(
                "",
                Array.Empty<TableDefinition>()));
    }

    [Fact]
    public async Task ResumeMigrationAsync_ShouldRejectMissingState()
    {
        var service = CreateService(null);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResumeMigrationAsync(
                "migration-does-not-exist",
                Array.Empty<TableDefinition>()));
    }

    [Fact]
    public async Task ResumeMigrationAsync_ShouldRejectCompletedMigration()
    {
        var state = CreateState(
            MigrationStateStatus.Completed,
            plannedTables: 1);

        var service = CreateService(state);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResumeMigrationAsync(
                state.MigrationId,
                new[] { CreateTable("tbl_Candidate") }));
    }

    [Fact]
    public async Task ResumeMigrationAsync_ShouldRejectMismatchedTableCount()
    {
        var state = CreateState(
            MigrationStateStatus.Failed,
            plannedTables: 2);

        var service = CreateService(state);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResumeMigrationAsync(
                state.MigrationId,
                new[] { CreateTable("tbl_Candidate") }));
    }

    [Fact]
    public async Task ResumeMigrationAsync_ShouldRejectUnknownSavedTable()
    {
        var state = CreateState(
            MigrationStateStatus.Failed,
            plannedTables: 1,
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Deleted",
                Status = MigrationTableStatus.Failed
            });

        var service = CreateService(state);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ResumeMigrationAsync(
                state.MigrationId,
                new[] { CreateTable("tbl_Candidate") }));
    }

    private static DataMigrationService CreateService(
        MigrationState? state)
    {
        return new DataMigrationService(
            CreateUnusedProxy<IDataReader>(),
            CreateUnusedProxy<IDataWriter>(),
            CreateUnusedProxy<IIdentitySynchronizer>(),
            new NoOpMigrationLogger(),
            new PassthroughTableDependencySorter(),
            new InMemoryMigrationStateStore(state),
            new MigrationResumePlanner());
    }

    private static MigrationState CreateState(
        MigrationStateStatus status,
        int plannedTables,
        params MigrationStateTable[] tables)
    {
        return new MigrationState
        {
            MigrationId = "migration-test-validation",
            StartedAt = DateTime.UtcNow,
            Status = status,
            PlannedTables = plannedTables,
            Tables = [.. tables]
        };
    }

    private static TableDefinition CreateTable(string tableName)
    {
        return new TableDefinition
        {
            SchemaName = "dbo",
            TableName = tableName
        };
    }

    private static T CreateUnusedProxy<T>()
        where T : class
    {
        return DispatchProxy.Create<T, UnusedDependencyProxy>();
    }

    private sealed class InMemoryMigrationStateStore
        : IMigrationStateStore
    {
        private readonly MigrationState? _state;

        public InMemoryMigrationStateStore(MigrationState? state)
        {
            _state = state;
        }

        public Task<MigrationState?> LoadAsync(
            string migrationId,
            CancellationToken cancellationToken = default)
        {
            MigrationState? result =
                _state is not null &&
                string.Equals(
                    migrationId,
                    _state.MigrationId,
                    StringComparison.OrdinalIgnoreCase)
                    ? _state
                    : null;

            return Task.FromResult(result);
        }

        public Task SaveAsync(
            MigrationState state,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task UpdateTableAsync(
            string migrationId,
            MigrationStateTable table,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class PassthroughTableDependencySorter
        : ITableDependencySorter
    {
        public IReadOnlyList<TableDefinition> Sort(
            IReadOnlyList<TableDefinition> tables)
        {
            return tables;
        }
    }

    private sealed class NoOpMigrationLogger : IMigrationLogger
    {
        public void MigrationStarted(int totalTables) { }

        public void TableMigrationStarted(string tableName) { }

        public void TableMigrationCompleted(
            MigrationTableResult result)
        { }

        public void TableMigrationFailed(
            MigrationTableResult result)
        { }

        public void MigrationCompleted(MigrationReport report) { }

        public void MigrationCancelled(MigrationReport report) { }
    }
}