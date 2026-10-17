using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDataReader
{
    IAsyncEnumerable<DataRow> ReadTableAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default);

    Task<long> GetRowCountAsync(
    TableDefinition table,
    CancellationToken cancellationToken = default);
}