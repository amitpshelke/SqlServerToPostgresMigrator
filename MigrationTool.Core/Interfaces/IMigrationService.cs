namespace MigrationTool.Core.Interfaces;

public interface IMigrationService
{
    Task<IReadOnlyList<Models.TableDefinition>> InspectAsync(CancellationToken cancellationToken = default);

    Task<int> GenerateSchemaAsync(string outputDirectory, CancellationToken cancellationToken = default);

    Task CreatePostgreSqlSchemaAsync(CancellationToken cancellationToken = default);

    Task CreatePostgreSqlConstraintsAsync(CancellationToken cancellationToken = default);
}