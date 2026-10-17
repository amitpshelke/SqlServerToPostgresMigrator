using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IIdentitySynchronizer
{
    Task SynchronizeAsync(
        IReadOnlyList<TableDefinition> tables,
        CancellationToken cancellationToken = default);
}