namespace MigrationTool.Core.Models;

public enum MigrationTableStatus
{
    Pending,
    Running,
    Success,
    Failed,
    Cancelled
}