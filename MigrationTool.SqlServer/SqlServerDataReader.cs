using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.SqlServer;

public sealed class SqlServerDataReader : IDataReader
{
    private readonly string _connectionString;

    public SqlServerDataReader(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async IAsyncEnumerable<DataRow> ReadTableAsync1(TableDefinition table, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var columns = table.Columns.OrderBy(c => c.OrdinalPosition).ToList();
        var columnList = string.Join(", ", columns.Select(c => QuoteIdentifier(c.Name)));

        var sql = $"""
            SELECT {columnList}
            FROM {QuoteIdentifier(table.SchemaName)}.
                 {QuoteIdentifier(table.TableName)}
            ORDER BY (SELECT NULL);
            """;

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 0;

        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == DBNull.Value)
                {
                    values[i] = null!;
                }
            }

            yield return new DataRow(values);
        }
    }

    public async IAsyncEnumerable<DataRow> ReadTableAsync(TableDefinition table, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        Console.WriteLine($"Connected to SQL Server database: {connection.Database}");

        var columns = table.Columns.OrderBy(c => c.OrdinalPosition).ToList();
        Console.WriteLine($"Table: {table.FullName}");
        Console.WriteLine($"Column count: {columns.Count}");

        var columnList = string.Join(", ", columns.Select(c => QuoteIdentifier(c.Name)));

        var sql = $"""
        SELECT {columnList}
        FROM {QuoteIdentifier(table.SchemaName)}.
             {QuoteIdentifier(table.TableName)};
        """;

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 0;

        await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);
        Console.WriteLine($"Reader field count: {reader.FieldCount}");

        var rowCount = 0;

        while (await reader.ReadAsync(cancellationToken))
        {
            rowCount++;
            var values = new object[reader.FieldCount];
            reader.GetValues(values);

            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] == DBNull.Value)
                {
                    values[i] = null!;
                }
            }

            yield return new DataRow(values);
        }

        Console.WriteLine($"SQL Server rows read: {rowCount}");
    }

    public async Task<long> GetRowCountAsync(TableDefinition table, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var sql = $"""
        SELECT COUNT_BIG(*)
        FROM {QuoteIdentifier(table.SchemaName)}.
             {QuoteIdentifier(table.TableName)};
        """;

        await using var command = new SqlCommand(sql, connection);
        command.CommandTimeout = 0;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "[" +
               identifier.Replace("]", "]]") +
               "]";
    }
}