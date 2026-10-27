using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IMigrationStateStore
{
    Task SaveAsync(
        MigrationState state,
        CancellationToken cancellationToken = default);

    Task<MigrationState?> LoadAsync(
        string migrationId,
        CancellationToken cancellationToken = default);

    Task UpdateTableAsync(
        string migrationId,
        MigrationStateTable table,
        CancellationToken cancellationToken = default);
}