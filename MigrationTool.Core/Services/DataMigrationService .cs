using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class DataMigrationService : IDataMigrationService
{
    private readonly IDataReader _dataReader;
    private readonly IDataWriter _dataWriter;
    private readonly IIdentitySynchronizer _identitySynchronizer;
    private readonly ITableDependencySorter _dependencySorter;
    private readonly IMigrationLogger _migrationLogger;

    public DataMigrationService(
    IDataReader dataReader,
    IDataWriter dataWriter,
    IIdentitySynchronizer identitySynchronizer,
    ITableDependencySorter dependencySorter,
    IMigrationLogger migrationLogger)
    {
        _dataReader = dataReader;
        _dataWriter = dataWriter;
        _identitySynchronizer = identitySynchronizer;
        _dependencySorter = dependencySorter;
        _migrationLogger = migrationLogger;
    }

    public IReadOnlyList<TableDefinition> GetMigrationOrder(
        IReadOnlyList<TableDefinition> tables)
    {
        return _dependencySorter.Sort(tables);
    }

    public async Task<MigrationTableResult> MigrateTableAsync(
        TableDefinition table,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _migrationLogger.TableMigrationStarted(table.FullName);

        var startedAt = DateTime.UtcNow;

        long totalRows = 0;
        long rowsMigrated = 0;

        try
        {
            totalRows =
                await _dataReader.GetRowCountAsync(
                    table,
                    cancellationToken);

            var rows =
                _dataReader.ReadTableAsync(
                    table,
                    cancellationToken);

            rowsMigrated =
                await _dataWriter.WriteTableAsync(
                    table,
                    ReportProgressAsync(
                        table,
                        totalRows,
                        rows,
                        progress,
                        cancellationToken),
                    cancellationToken);

            if (table.Columns.Any(c => c.IsIdentity))
            {
                await _identitySynchronizer.SynchronizeAsync(
                    new[] { table },
                    cancellationToken);
            }

            var result = new MigrationTableResult
            {
                TableName = table.FullName,
                Status = MigrationTableStatus.Success,
                RowsMigrated = rowsMigrated,
                TotalRows = totalRows,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow
            };
            _migrationLogger.TableMigrationCompleted(result);
            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new MigrationTableResult
            {
                TableName = table.FullName,
                Status = MigrationTableStatus.Cancelled,
                RowsMigrated = rowsMigrated,
                TotalRows = totalRows,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                ErrorMessage = "Migration was cancelled."
            };

            return result;
        }
        catch (Exception ex)
        {
            var result = new MigrationTableResult
            {
                TableName = table.FullName,
                Status = MigrationTableStatus.Failed,
                RowsMigrated = rowsMigrated,
                TotalRows = totalRows,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                ErrorCode = MigrationErrorFormatter.GetErrorCode(ex),
                ErrorMessage = MigrationErrorFormatter.Format(ex)
            };

            _migrationLogger.TableMigrationFailed(result);

            return result;
        }
    }

    public async Task<MigrationReport> MigrateAllTablesAsync(
        IReadOnlyList<TableDefinition> tables,
        IProgress<MigrationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        var orderedTables = GetMigrationOrder(tables);
        _migrationLogger.MigrationStarted(orderedTables.Count);

        var report = new MigrationReport
        {
            StartedAt = startedAt,
            PlannedTables = orderedTables.Count
        };

        foreach (var table in orderedTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await MigrateTableAsync(table, progress, cancellationToken);
            report.Tables.Add(result);

            if (result.Status != MigrationTableStatus.Success)
            {
                break;
            }
        }

        var finalReport = new MigrationReport
        {
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            PlannedTables = orderedTables.Count,
            Tables = report.Tables
        };

        if (finalReport.IsSuccess)
        {
            _migrationLogger.MigrationCompleted(finalReport);
        }
        else if (finalReport.CancelledTables > 0)
        {
            _migrationLogger.MigrationCancelled(finalReport);
        }

        return finalReport;
    }

    private static async IAsyncEnumerable<DataRow> ReportProgressAsync(
        TableDefinition table,
        long totalRows,
        IAsyncEnumerable<DataRow> rows,
        IProgress<MigrationProgress>? progress,
        [System.Runtime.CompilerServices.EnumeratorCancellation]
        CancellationToken cancellationToken)
    {
        long rowsMigrated = 0;

        await foreach (var row in rows
            .WithCancellation(cancellationToken))
        {
            rowsMigrated++;

            yield return row;

            if (progress is not null &&
                (rowsMigrated % 1000 == 0 ||
                 rowsMigrated == totalRows))
            {
                progress.Report(
                    new MigrationProgress
                    {
                        TableName = table.FullName,
                        RowsMigrated = rowsMigrated,
                        TotalRows = totalRows
                    });
            }
        }
    }
}