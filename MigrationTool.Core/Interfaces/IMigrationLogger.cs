namespace MigrationTool.Core.Interfaces;

using MigrationTool.Core.Models;

public interface IMigrationLogger
{
    void MigrationStarted(
        int totalTables);

    void TableMigrationStarted(
        string tableName);

    void TableMigrationCompleted(
        MigrationTableResult result);

    void TableMigrationFailed(
        MigrationTableResult result);

    void MigrationCompleted(
        MigrationReport report);

    void MigrationCancelled(
        MigrationReport report);
}