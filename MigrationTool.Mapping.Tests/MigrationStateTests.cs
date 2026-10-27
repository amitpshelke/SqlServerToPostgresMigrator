using MigrationTool.Core.Models;

namespace MigrationTool.Core.Tests;

public class MigrationStateTests
{
    [Fact]
    public void MigrationState_ShouldCalculateSuccessfulTables()
    {
        var state = new MigrationState
        {
            MigrationId = "migration-test-001",
            StartedAt = DateTime.UtcNow,
            Status = MigrationStateStatus.Running,
            PlannedTables = 3,
            Tables =
            [
                new MigrationStateTable
                {
                    TableName = "dbo.tbl_Candidate",
                    Status = MigrationTableStatus.Success,
                    RowsMigrated = 10
                },

                new MigrationStateTable
                {
                    TableName = "dbo.tbl_Job",
                    Status = MigrationTableStatus.Success,
                    RowsMigrated = 20
                },

                new MigrationStateTable
                {
                    TableName = "dbo.tbl_User",
                    Status = MigrationTableStatus.Failed,
                    RowsMigrated = 0,
                    ErrorCode = "23505",
                    ErrorMessage = "Duplicate key"
                }
            ]
        };

        Assert.Equal(2, state.SuccessfulTables);
        Assert.Equal(1, state.FailedTables);
        Assert.Equal(0, state.CancelledTables);
        Assert.Equal(0, state.NotStartedTables);
        Assert.Equal(30, state.TotalRowsMigrated);
    }
}
