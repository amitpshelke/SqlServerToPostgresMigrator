using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface ISchemaWriter
{
    Task CreateSchemaAsync(IReadOnlyList<TableDefinition> tables, CancellationToken cancellationToken = default);

    Task CreateConstraintsAndIndexesAsync(IReadOnlyList<TableDefinition> tables, CancellationToken cancellationToken = default);
}