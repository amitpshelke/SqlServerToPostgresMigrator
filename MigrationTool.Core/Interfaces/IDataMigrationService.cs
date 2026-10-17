using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDataMigrationService
{
    IReadOnlyList<TableDefinition> GetMigrationOrder(
       IReadOnlyList<TableDefinition> tables);

    Task<long> MigrateTableAsync(
        TableDefinition table,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task MigrateAllTablesAsync(
        IReadOnlyList<TableDefinition> tables,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}