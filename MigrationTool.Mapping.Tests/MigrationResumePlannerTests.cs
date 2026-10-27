using MigrationTool.Core.Models;
using MigrationTool.Core.Services;

namespace MigrationTool.Core.Tests;

public class MigrationResumePlannerTests
{
    private readonly MigrationResumePlanner _planner = new();

    [Fact]
    public void GetTablesToMigrate_ShouldSkipSuccessfulTables()
    {
        var tables = new[]
        {
            CreateTable("tbl_Candidate"),
            CreateTable("tbl_Job"),
            CreateTable("tbl_Permission")
        };

        var state = CreateState(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Candidate",
                Status = MigrationTableStatus.Success
            },
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Job",
                Status = MigrationTableStatus.Success
            },
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Permission",
                Status = MigrationTableStatus.Failed
            });

        var result = _planner.GetTablesToMigrate(
            tables,
            state);

        Assert.Single(result);
        Assert.Equal("dbo.tbl_Permission", result[0].FullName);
    }

    [Fact]
    public void GetTablesToMigrate_ShouldIncludeTablesNotInSavedState()
    {
        var tables = new[]
        {
            CreateTable("tbl_Candidate"),
            CreateTable("tbl_Job")
        };

        var state = CreateState(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Candidate",
                Status = MigrationTableStatus.Success
            });

        var result = _planner.GetTablesToMigrate(
            tables,
            state);

        Assert.Single(result);
        Assert.Equal("dbo.tbl_Job", result[0].FullName);
    }

    [Fact]
    public void GetTablesToMigrate_ShouldRetryFailedAndRunningTables()
    {
        var tables = new[]
        {
            CreateTable("tbl_Candidate"),
            CreateTable("tbl_Job"),
            CreateTable("tbl_Permission")
        };

        var state = CreateState(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Candidate",
                Status = MigrationTableStatus.Success
            },
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Job",
                Status = MigrationTableStatus.Failed
            },
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Permission",
                Status = MigrationTableStatus.Running
            });

        var result = _planner.GetTablesToMigrate(
            tables,
            state);

        Assert.Equal(2, result.Count);

        Assert.Equal(
            "dbo.tbl_Job",
            result[0].FullName);

        Assert.Equal(
            "dbo.tbl_Permission",
            result[1].FullName);
    }

    [Fact]
    public void GetTablesToMigrate_ShouldPreserveTableOrder()
    {
        var tables = new[]
        {
            CreateTable("tbl_Candidate"),
            CreateTable("tbl_Job"),
            CreateTable("tbl_Permission")
        };

        var state = CreateState(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Permission",
                Status = MigrationTableStatus.Failed
            });

        var result = _planner.GetTablesToMigrate(
            tables,
            state);

        Assert.Equal(
            new[]
            {
                "dbo.tbl_Candidate",
                "dbo.tbl_Job",
                "dbo.tbl_Permission"
            },
            result.Select(t => t.FullName).ToArray());
    }

    private static TableDefinition CreateTable(string tableName)
    {
        return new TableDefinition
        {
            SchemaName = "dbo",
            TableName = tableName
        };
    }

    private static MigrationState CreateState(
        params MigrationStateTable[] tables)
    {
        return new MigrationState
        {
            MigrationId = "migration-test-001",
            StartedAt = DateTime.UtcNow,
            Status = MigrationStateStatus.Failed,
            PlannedTables = 3,
            Tables = [.. tables]
        };
    }
}