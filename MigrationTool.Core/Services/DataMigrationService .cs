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
    private readonly IMigrationStateStore _stateStore;
    private readonly MigrationResumePlanner _resumePlanner;

    public DataMigrationService(
    IDataReader dataReader,
    IDataWriter dataWriter,
    IIdentitySynchronizer identitySynchronizer,
    IMigrationLogger logger,
    ITableDependencySorter dependencySorter,
    IMigrationStateStore stateStore,
    MigrationResumePlanner resumePlanner
    )
    {
        _dataReader = dataReader;
        _dataWriter = dataWriter;
        _identitySynchronizer = identitySynchronizer;
        _migrationLogger = logger;
        _dependencySorter = dependencySorter;
        _stateStore = stateStore;
        _resumePlanner = resumePlanner;
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

            var errorMessage = MigrationErrorFormatter.Format(ex);  //temp line
            Console.WriteLine(ex.ToString());  // temp line

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

        var migrationId = $"migration-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}";
        var migrationState = new MigrationState
        {
            MigrationId = migrationId,
            StartedAt = DateTime.UtcNow,
            Status = MigrationStateStatus.Running,
            PlannedTables = orderedTables.Count
        };
        await _stateStore.SaveAsync(migrationState, cancellationToken);

        _migrationLogger.MigrationStarted(orderedTables.Count);

        var report = new MigrationReport
        {
            StartedAt = startedAt,
            PlannedTables = orderedTables.Count
        };

        foreach (var table in orderedTables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. Record that the table migration is starting.
            var tableState = new MigrationStateTable
            {
                TableName = table.FullName,
                Status = MigrationTableStatus.Running,
                RowsMigrated = 0,
                StartedAt = DateTime.UtcNow
            };

            migrationState.Tables.Add(tableState);

            // 2. Persist the Running status.
            await _stateStore.SaveAsync(
                migrationState,
                cancellationToken);

            // 3. Execute the existing table migration.
            var result = await MigrateTableAsync(
                table,
                progress,
                cancellationToken);

            report.Tables.Add(result);

            // 4. Update the persisted table state with its final result.
            var savedTableState = migrationState.Tables.First(
                t => string.Equals(
                    t.TableName,
                    result.TableName,
                    StringComparison.OrdinalIgnoreCase));

            savedTableState.Status = result.Status;
            savedTableState.RowsMigrated = result.RowsMigrated;
            savedTableState.TotalRows = result.TotalRows;
            savedTableState.StartedAt = result.StartedAt;
            savedTableState.CompletedAt = result.CompletedAt;
            savedTableState.ErrorCode = result.ErrorCode;
            savedTableState.ErrorMessage = result.ErrorMessage;

            // 5. Persist the final table result.
            // Use CancellationToken.None so cancellation does not
            // prevent recording the result of the attempted table.
            await _stateStore.SaveAsync(
                migrationState,
                CancellationToken.None);

            // 6. Preserve the existing stop-on-failure behavior.
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

        // Update the overall migration state.
        migrationState.CompletedAt = finalReport.CompletedAt;

        if (finalReport.CancelledTables > 0)
        {
            migrationState.Status = MigrationStateStatus.Cancelled;
        }
        else if (finalReport.FailedTables > 0)
        {
            migrationState.Status = MigrationStateStatus.Failed;
        }
        else
        {
            migrationState.Status = MigrationStateStatus.Completed;
        }

        // Persist the final migration state, even if the original
        // cancellation token has been cancelled.
        await _stateStore.SaveAsync(migrationState, CancellationToken.None);

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

    public async Task<MigrationReport> ResumeMigrationAsync(string migrationId, IReadOnlyList<TableDefinition> tables, IProgress<MigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(migrationId))
        {
            throw new ArgumentException("Migration ID cannot be empty.", nameof(migrationId));
        }

        var state = await _stateStore.LoadAsync(migrationId, cancellationToken);
        if (state is null)
        {
            throw new InvalidOperationException($"Migration state not found: {migrationId}");
        }

        if (state.Status == MigrationStateStatus.Completed)
        {
            throw new InvalidOperationException($"Migration '{migrationId}' has already completed.");
        }

        var orderedTables = GetMigrationOrder(tables);
        if (orderedTables.Count != state.PlannedTables)
        {
            throw new InvalidOperationException("The current table count does not match the saved migration state.");
        }

        var currentTableNames = orderedTables.Select(t => t.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (state.Tables.Any(t => !currentTableNames.Contains(t.TableName)))
        {
            throw new InvalidOperationException("The saved migration contains tables that are not present in the current migration plan.");
        }

        var tablesToMigrate = _resumePlanner.GetTablesToMigrate(orderedTables, state);
        var resumeStartedAt = DateTime.UtcNow;

        // Resume the existing migration rather than creating a new ID.
        state.Status = MigrationStateStatus.Running;
        state.CompletedAt = null;

        await _stateStore.SaveAsync(state, CancellationToken.None);

        var resultsByTable = new Dictionary<string, MigrationTableResult>(StringComparer.OrdinalIgnoreCase);

        // Preserve results for tables that already succeeded.
        foreach (var savedTable in state.Tables.Where(t => t.Status == MigrationTableStatus.Success))
        {
            resultsByTable[savedTable.TableName] = new MigrationTableResult
                                                                        {
                                                                            TableName = savedTable.TableName,
                                                                            Status = MigrationTableStatus.Success,
                                                                            RowsMigrated = savedTable.RowsMigrated,
                                                                            TotalRows = savedTable.TotalRows,
                                                                            StartedAt = savedTable.StartedAt,
                                                                            CompletedAt =
                                                                                savedTable.CompletedAt ?? savedTable.StartedAt
                                                                        };
        }

        _migrationLogger.MigrationStarted(orderedTables.Count);

        var cancellationRequested = false;
        foreach (var table in tablesToMigrate)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationRequested = true;
                break;
            }

            var tableState = state.Tables.FirstOrDefault(t => string.Equals(t.TableName, table.FullName, StringComparison.OrdinalIgnoreCase));

            if (tableState is null)
            {
                tableState = new MigrationStateTable
                {
                    TableName = table.FullName
                };

                state.Tables.Add(tableState);
            }

            // Update the existing entry on retry. Do not append duplicates.
            tableState.Status = MigrationTableStatus.Running;
            tableState.RowsMigrated = 0;
            tableState.TotalRows = null;
            tableState.StartedAt = DateTime.UtcNow;
            tableState.CompletedAt = null;
            tableState.ErrorCode = null;
            tableState.ErrorMessage = null;

            await _stateStore.SaveAsync(state, cancellationToken);
            var result = await MigrateTableAsync(table, progress, cancellationToken);

            resultsByTable[result.TableName] = result;

            tableState.Status = result.Status;
            tableState.RowsMigrated = result.RowsMigrated;
            tableState.TotalRows = result.TotalRows;
            tableState.StartedAt = result.StartedAt;
            tableState.CompletedAt = result.CompletedAt;
            tableState.ErrorCode = result.ErrorCode;
            tableState.ErrorMessage = result.ErrorMessage;

            // Preserve the result even if cancellation was requested.
            await _stateStore.SaveAsync(state, CancellationToken.None);

            if (result.Status == MigrationTableStatus.Cancelled)
            {
                cancellationRequested = true;
                break;
            }

            if (result.Status != MigrationTableStatus.Success)
            {
                break;
            }
        }

        if (cancellationRequested && !resultsByTable.Values.Any(r => r.Status == MigrationTableStatus.Cancelled))
        {
            var cancelledTable = tablesToMigrate.FirstOrDefault(t => !resultsByTable.ContainsKey(t.FullName));

            if (cancelledTable is not null)
            {
                var now = DateTime.UtcNow;
                var tableState = state.Tables.FirstOrDefault(t => string.Equals(t.TableName, cancelledTable.FullName, StringComparison.OrdinalIgnoreCase));
                var previousError = tableState?.ErrorMessage;
                var errorMessage = "Resume cancelled before this table could be retried.";

                if (!string.IsNullOrWhiteSpace(previousError))
                {
                    errorMessage += $" Previous error: {previousError}";
                }

                if (tableState is null)
                {
                    tableState = new MigrationStateTable
                    {
                        TableName = cancelledTable.FullName
                    };

                    state.Tables.Add(tableState);
                }

                tableState.Status = MigrationTableStatus.Cancelled;
                tableState.RowsMigrated = 0;
                tableState.StartedAt = now;
                tableState.CompletedAt = now;
                tableState.ErrorCode = null;
                tableState.ErrorMessage = errorMessage;

                resultsByTable[cancelledTable.FullName] =
                    new MigrationTableResult
                    {
                        TableName = cancelledTable.FullName,
                        Status = MigrationTableStatus.Cancelled,
                        RowsMigrated = 0,
                        TotalRows = tableState.TotalRows,
                        StartedAt = now,
                        CompletedAt = now,
                        ErrorMessage = errorMessage
                    };

                await _stateStore.SaveAsync(state, CancellationToken.None);
            }
        }

        var finalResults = orderedTables
            .Where(t => resultsByTable.ContainsKey(t.FullName))
            .Select(t => resultsByTable[t.FullName])
            .ToList();

        var finalReport = new MigrationReport
        {
            StartedAt = resumeStartedAt,
            CompletedAt = DateTime.UtcNow,
            PlannedTables = state.PlannedTables,
            Tables = finalResults
        };

        state.CompletedAt = finalReport.CompletedAt;

        if (finalReport.CancelledTables > 0)
        {
            state.Status = MigrationStateStatus.Cancelled;
            _migrationLogger.MigrationCancelled(finalReport);
        }
        else if (finalReport.FailedTables > 0 || finalReport.TotalTables < state.PlannedTables)
        {
            state.Status = MigrationStateStatus.Failed;
        }
        else
        {
            state.Status = MigrationStateStatus.Completed;
            _migrationLogger.MigrationCompleted(finalReport);
        }

        await _stateStore.SaveAsync(state, CancellationToken.None);
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