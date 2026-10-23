using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDataMigrationService
{
    IReadOnlyList<TableDefinition> GetMigrationOrder(
        IReadOnlyList<TableDefinition> tables);

    Task<MigrationTableResult> MigrateTableAsync(
        TableDefinition table,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<MigrationReport> MigrateAllTablesAsync(
        IReadOnlyList<TableDefinition> tables,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}