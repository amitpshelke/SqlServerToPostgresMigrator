using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDataValidator
{
    Task<ValidationReport> ValidateRowCountsAsync(
        IReadOnlyList<TableDefinition> tables,
        CancellationToken cancellationToken = default);

    Task<ValidationReport> ValidateSchemaAsync(
        IReadOnlyList<TableDefinition> tables,
        CancellationToken cancellationToken = default);
}