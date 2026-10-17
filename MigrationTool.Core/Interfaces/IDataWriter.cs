using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface IDataWriter
{
    Task<long> WriteTableAsync(
        TableDefinition table,
        IAsyncEnumerable<DataRow> rows,
        CancellationToken cancellationToken = default);
}