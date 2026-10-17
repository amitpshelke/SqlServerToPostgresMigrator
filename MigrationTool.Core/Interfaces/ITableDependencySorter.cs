using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface ITableDependencySorter
{
    IReadOnlyList<TableDefinition> Sort(
        IReadOnlyList<TableDefinition> tables);
}