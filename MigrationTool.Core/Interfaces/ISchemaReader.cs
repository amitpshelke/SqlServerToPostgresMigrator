using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface ISchemaReader
{
    Task<IReadOnlyList<TableDefinition>> ReadSchemaAsync(IReadOnlyList<TableDefinition> tables, CancellationToken cancellationToken = default);
}