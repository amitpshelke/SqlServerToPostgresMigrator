using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class MigrationResumePlanner
{
    public IReadOnlyList<TableDefinition> GetTablesToMigrate(
        IReadOnlyList<TableDefinition> orderedTables,
        MigrationState state)
    {
        ArgumentNullException.ThrowIfNull(orderedTables);
        ArgumentNullException.ThrowIfNull(state);

        var successfulTables = state.Tables
            .Where(t => t.Status == MigrationTableStatus.Success)
            .Select(t => t.TableName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return orderedTables
            .Where(table => !successfulTables.Contains(table.FullName))
            .ToList();
    }
}