using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface ITargetDataReader
{
    Task<long> GetRowCountAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<bool> TableExistsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);
}