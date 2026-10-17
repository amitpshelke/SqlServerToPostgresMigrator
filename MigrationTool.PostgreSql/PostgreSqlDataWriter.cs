using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using Npgsql;

namespace MigrationTool.PostgreSql;

public sealed class PostgreSqlDataWriter : IDataWriter
{
    private readonly string _connectionString;
    private readonly IDataValueConverter _valueConverter;

    public PostgreSqlDataWriter(
    string connectionString,
    IDataValueConverter valueConverter)
    {
        _connectionString = connectionString;
        _valueConverter = valueConverter;
    }

    public async Task<long> WriteTableAsync(
        TableDefinition table,
        IAsyncEnumerable<DataRow> rows,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var columns = table.Columns
            .OrderBy(c => c.OrdinalPosition)
            .ToList();

        var columnList = string.Join(
            ", ",
            columns.Select(c => QuoteIdentifier(c.Name)));

        var copySql = $"""
            COPY {QuoteIdentifier(table.SchemaName)}.
                 {QuoteIdentifier(table.TableName)}
                 ({columnList})
            FROM STDIN (FORMAT BINARY)
            """;

        await using var importer =
            await connection.BeginBinaryImportAsync(
                copySql,
                cancellationToken);

        long rowCount = 0;

        await foreach (var row in rows.WithCancellation(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await importer.StartRowAsync(cancellationToken);

            for (var i = 0; i < row.Values.Length; i++)
            {
                var column = columns[i];
                var value = _valueConverter.Convert(row.Values[i], column.SqlServerDataType, column.PostgreSqlDataType);
                await importer.WriteAsync(value, cancellationToken);
            }
            rowCount++;
        }

        await importer.CompleteAsync(cancellationToken);

        return rowCount;
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" +
               identifier.Replace("\"", "\"\"") +
               "\"";
    }
}