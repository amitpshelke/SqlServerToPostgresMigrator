using MigrationTool.Core.Models;
using MigrationTool.Core.Services;

namespace MigrationTool.Mapping.Tests;

public class TableDependencySorterTests
{
    [Fact]
    public void Sort_ShouldPlaceParentBeforeChild()
    {
        var parent = new TableDefinition
        {
            SchemaName = "dbo",
            TableName = "Parent"
        };

        var child = new TableDefinition
        {
            SchemaName = "dbo",
            TableName = "Child",
            ForeignKeys =
            [
                new ForeignKeyDefinition
                {
                    Name = "FK_Child_Parent",
                    ReferencedSchema = "dbo",
                    ReferencedTable = "Parent"
                }
            ]
        };

        var tables = new[]
        {
            child,
            parent
        };

        var sorter = new TableDependencySorter();

        var result = sorter.Sort(tables);

        Assert.Equal(
            "Parent",
            result[0].TableName);

        Assert.Equal(
            "Child",
            result[1].TableName);
    }

    [Fact]
    public void Sort_ShouldThrow_WhenCircularDependencyExists()
    {
        var tableA = new TableDefinition
        {
            SchemaName = "dbo",
            TableName = "TableA"
        };

        var tableB = new TableDefinition
        {
            SchemaName = "dbo",
            TableName = "TableB"
        };

        tableA.ForeignKeys.Add(
            new ForeignKeyDefinition
            {
                Name = "FK_A_B",
                ReferencedSchema = "dbo",
                ReferencedTable = "TableB"
            });

        tableB.ForeignKeys.Add(
            new ForeignKeyDefinition
            {
                Name = "FK_B_A",
                ReferencedSchema = "dbo",
                ReferencedTable = "TableA"
            });

        var sorter = new TableDependencySorter();

        Assert.Throws<InvalidOperationException>(
            () => sorter.Sort(
                new[] { tableA, tableB }));
    }
}