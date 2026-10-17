using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class DataMigrationService : IDataMigrationService
{
    private readonly IDataReader _dataReader;
    private readonly IDataWriter _dataWriter;
    private readonly IIdentitySynchronizer _identitySynchronizer;
    private readonly ITableDependencySorter _dependencySorter;

    public DataMigrationService(IDataReader dataReader, IDataWriter dataWriter, IIdentitySynchronizer identitySynchronizer, ITableDependencySorter dependencySorter)
    {
        _dataReader = dataReader;
        _dataWriter = dataWriter;
        _identitySynchronizer = identitySynchronizer;
        _dependencySorter = dependencySorter;
    }

    public async Task<long> MigrateTableAsync(
    TableDefinition table,
    IProgress<MigrationProgress>? progress = null,
    CancellationToken cancellationToken = default)
    {
        var totalRows = await _dataReader.GetRowCountAsync(table, cancellationToken);

        var rows = _dataReader.ReadTableAsync(table, cancellationToken);

        var rowCount = await _dataWriter.WriteTableAsync(table, 
                                                         ReportProgressAsync(table, totalRows, rows, progress, cancellationToken),
                                                         cancellationToken);

        if (table.Columns.Any(c => c.IsIdentity))
        {
            await _identitySynchronizer.SynchronizeAsync(new[] { table }, cancellationToken);
        }
        return rowCount;
    }

    public async Task MigrateAllTablesAsync(IReadOnlyList<TableDefinition> tables, IProgress<MigrationProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var orderedTables = GetMigrationOrder(tables);
        foreach (var table in orderedTables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await MigrateTableAsync(table, progress, cancellationToken);
        }
    }

    public IReadOnlyList<TableDefinition> GetMigrationOrder(IReadOnlyList<TableDefinition> tables)
    {
        return _dependencySorter.Sort(tables);
    }

    private static async IAsyncEnumerable<DataRow> ReportProgressAsync(TableDefinition table, 
                                                                       long totalRows, 
                                                                       IAsyncEnumerable<DataRow> rows, 
                                                                       IProgress<MigrationProgress>? progress,
                                                                       [System.Runtime.CompilerServices.EnumeratorCancellation]CancellationToken cancellationToken)
    {
        long rowsMigrated = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            rowsMigrated++;
            yield return row;

            if (progress is not null && (rowsMigrated % 1000 == 0 || rowsMigrated == totalRows))
            {
                progress.Report(new MigrationProgress
                                    {
                                        TableName = table.FullName,
                                        RowsMigrated = rowsMigrated,
                                        TotalRows = totalRows
                                    });
            }
        }
    }
}