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

    Task<PrimaryKeyDefinition?> GetPrimaryKeyAsync(
       TableDefinition table,
       CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UniqueConstraintDefinition>> GetUniqueConstraintsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ForeignKeyDefinition>> GetForeignKeysAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IndexDefinition>> GetIndexesAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DefaultConstraintDefinition>>
    GetDefaultConstraintsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckConstraintDefinition>>
        GetCheckConstraintsAsync(
            TableDefinition table,
            CancellationToken cancellationToken = default);
}