using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class TableDependencySorter : ITableDependencySorter
{
    public IReadOnlyList<TableDefinition> Sort(
        IReadOnlyList<TableDefinition> tables)
    {
        var tableMap = tables.ToDictionary(
            GetTableKey,
            StringComparer.OrdinalIgnoreCase);

        // Number of parent dependencies for each table.
        var dependencyCount =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        // Parent -> children
        var dependents =
            new Dictionary<string, List<string>>(
                StringComparer.OrdinalIgnoreCase);

        // Initialize all tables.
        foreach (var table in tables)
        {
            var tableKey = GetTableKey(table);

            dependencyCount[tableKey] = 0;
            dependents[tableKey] = [];
        }

        // Build dependency graph.
        foreach (var table in tables)
        {
            var childKey = GetTableKey(table);

            foreach (var foreignKey in table.ForeignKeys)
            {
                var parentKey =
                    GetTableKey(
                        foreignKey.ReferencedSchema,
                        foreignKey.ReferencedTable);

                // Ignore references to tables that aren't part
                // of the current migration.
                if (!tableMap.ContainsKey(parentKey))
                {
                    continue;
                }

                dependencyCount[childKey]++;

                dependents[parentKey].Add(childKey);
            }
        }

        // Tables without dependencies can be migrated first.
        var ready = new Queue<string>(
            dependencyCount
                .Where(x => x.Value == 0)
                .Select(x => x.Key));

        var result =
            new List<TableDefinition>(tables.Count);

        while (ready.Count > 0)
        {
            var currentKey = ready.Dequeue();

            result.Add(tableMap[currentKey]);

            foreach (var childKey in dependents[currentKey])
            {
                dependencyCount[childKey]--;

                if (dependencyCount[childKey] == 0)
                {
                    ready.Enqueue(childKey);
                }
            }
        }

        // If not all tables were processed, there is a cycle.
        if (result.Count != tables.Count)
        {
            var cyclicTables = dependencyCount
                .Where(x => x.Value > 0)
                .Select(x => x.Key)
                .ToList();

            throw new InvalidOperationException(
                "Circular foreign-key dependency detected. " +
                $"Affected tables: {string.Join(
                    ", ",
                    cyclicTables)}");
        }

        return result;
    }

    private static string GetTableKey(
        TableDefinition table)
    {
        return GetTableKey(
            table.SchemaName,
            table.TableName);
    }

    private static string GetTableKey(
        string schemaName,
        string tableName)
    {
        return $"{schemaName}.{tableName}";
    }
}