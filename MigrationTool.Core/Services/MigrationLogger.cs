using Microsoft.Extensions.Logging;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class MigrationLogger : IMigrationLogger
{
    private readonly ILogger<MigrationLogger> _logger;

    public MigrationLogger(
        ILogger<MigrationLogger> logger)
    {
        _logger = logger;
    }

    public void MigrationStarted(
        int totalTables)
    {
        _logger.LogInformation(
            "Migration started. Total tables: {TotalTables}",
            totalTables);
    }

    public void TableMigrationStarted(
        string tableName)
    {
        _logger.LogInformation(
            "Table migration started: {TableName}",
            tableName);
    }

    public void TableMigrationCompleted(
        MigrationTableResult result)
    {
        _logger.LogInformation(
            "Table migration completed: {TableName}. Rows migrated: {RowsMigrated}",
            result.TableName,
            result.RowsMigrated);
    }

    public void TableMigrationFailed(
        MigrationTableResult result)
    {
        _logger.LogError(
             "Table migration failed: {TableName}. Rows migrated: {RowsMigrated}. ErrorCode: {ErrorCode}. Error: {ErrorMessage}",
             result.TableName,
             result.RowsMigrated,
             result.ErrorCode ?? "N/A",
             result.ErrorMessage);
    }

    public void MigrationCompleted(
        MigrationReport report)
    {
        _logger.LogInformation(
            "Migration completed. Tables: {TotalTables}, Succeeded: {SuccessfulTables}, Failed: {FailedTables}, Rows migrated: {TotalRowsMigrated}",
            report.TotalTables,
            report.SuccessfulTables,
            report.FailedTables,
            report.TotalRowsMigrated);
    }

    public void MigrationCancelled(
        MigrationReport report)
    {
        _logger.LogWarning(
            "Migration cancelled. Tables attempted: {TotalTables}, Succeeded: {SuccessfulTables}, Failed: {FailedTables}, Rows migrated: {TotalRowsMigrated}",
            report.TotalTables,
            report.SuccessfulTables,
            report.FailedTables,
            report.TotalRowsMigrated);
    }
}