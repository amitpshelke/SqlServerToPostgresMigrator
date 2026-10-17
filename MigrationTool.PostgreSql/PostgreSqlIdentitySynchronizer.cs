using Microsoft.Extensions.Logging;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using Npgsql;

namespace MigrationTool.PostgreSql;

public sealed class PostgreSqlIdentitySynchronizer : IIdentitySynchronizer
{
    private readonly string _connectionString;
    private readonly ILogger<PostgreSqlIdentitySynchronizer> _logger;

    public PostgreSqlIdentitySynchronizer(string connectionString, ILogger<PostgreSqlIdentitySynchronizer> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SynchronizeAsync(IReadOnlyList<TableDefinition> tables, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identityColumns = table.Columns.Where(c => c.IsIdentity).ToList();

            foreach (var column in identityColumns)
            {
                await SynchronizeColumnAsync(connection, table, column, cancellationToken);
            }
        }
    }

    private async Task SynchronizeColumnAsync(NpgsqlConnection connection, TableDefinition table, ColumnDefinition column, CancellationToken cancellationToken)
    {
        var maxValueSql = $"""
            SELECT MAX({QuoteIdentifier(column.Name)})
            FROM {QuoteIdentifier(table.SchemaName)}.
                 {QuoteIdentifier(table.TableName)};
            """;

        await using var maxCommand = new NpgsqlCommand(maxValueSql, connection);
        var result = await maxCommand.ExecuteScalarAsync(cancellationToken);

        if (result is null || result == DBNull.Value)
        {
            return;
        }

        var maxValue = Convert.ToInt64(result);

        var sequenceSql = """
            SELECT pg_get_serial_sequence(
                @tableName,
                @columnName
            );
            """;
         
        await using var sequenceCommand = new NpgsqlCommand(sequenceSql, connection);
        sequenceCommand.Parameters.AddWithValue("tableName", $"{QuoteIdentifier(table.SchemaName)}.{QuoteIdentifier(table.TableName)}");
        sequenceCommand.Parameters.AddWithValue("columnName", column.Name);

        var sequence = await sequenceCommand.ExecuteScalarAsync(cancellationToken);
        if (sequence is null || sequence == DBNull.Value)
        {
            return;
        }

        var setValueSql = """
            SELECT setval(
                @sequenceName,
                @maxValue,
                true
            );
            """;

        await using var setValueCommand = new NpgsqlCommand(setValueSql, connection);
        setValueCommand.Parameters.AddWithValue("sequenceName", sequence.ToString()!);
        setValueCommand.Parameters.AddWithValue("maxValue", maxValue);
        await setValueCommand.ExecuteScalarAsync(cancellationToken);
        _logger.LogInformation("Identity synchronized: {Table}.{Column} → {MaxValue}", table.FullName, column.Name, maxValue);
    }

    private static string QuoteIdentifier(string identifier)
    {
        return "\"" +
               identifier.Replace("\"", "\"\"") +
               "\"";
    }
}