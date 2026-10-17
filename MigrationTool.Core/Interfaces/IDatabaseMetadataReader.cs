using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDatabaseMetadataReader
{
    Task<IReadOnlyList<TableDefinition>> GetTablesAsync(
        CancellationToken cancellationToken = default);
}