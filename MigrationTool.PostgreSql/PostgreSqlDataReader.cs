using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using Npgsql;

namespace MigrationTool.PostgreSql;

public sealed class PostgreSqlDataReader : ITargetDataReader
{
    private readonly string _connectionString;

    public PostgreSqlDataReader(
        string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<long> GetRowCountAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var sql = $"""
            SELECT COUNT(*)
            FROM {QuoteIdentifier(table.SchemaName)}.
                 {QuoteIdentifier(table.TableName)};
            """;

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.CommandTimeout = 0;

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToInt64(result);
    }

    public async Task<bool> TableExistsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS
            (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = @schemaName
                  AND table_name = @tableName
            );
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "schemaName",
            table.SchemaName);

        command.Parameters.AddWithValue(
            "tableName",
            table.TableName);

        var result =
            await command.ExecuteScalarAsync(
                cancellationToken);

        return Convert.ToBoolean(result);
    }

    public async Task<IReadOnlyList<ColumnDefinition>> GetColumnsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                column_name,
                data_type,
                character_maximum_length,
                numeric_precision,
                numeric_scale,
                is_nullable,
                ordinal_position,
                is_identity
            FROM information_schema.columns
            WHERE table_schema = @schemaName
              AND table_name = @tableName
            ORDER BY ordinal_position;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var command =
            new NpgsqlCommand(sql, connection);

        command.Parameters.AddWithValue(
            "schemaName",
            table.SchemaName);

        command.Parameters.AddWithValue(
            "tableName",
            table.TableName);

        await using var reader =
            await command.ExecuteReaderAsync(
                cancellationToken);

        var columns =
            new List<ColumnDefinition>();

        while (await reader.ReadAsync(
            cancellationToken))
        {
            var dataType = reader.GetString(1);

            int? maxLength =
                reader.IsDBNull(2)
                    ? null
                    : reader.GetInt32(2);

            byte? precision =
                reader.IsDBNull(3)
                    ? null
                    : Convert.ToByte(
                        reader.GetValue(3));

            byte? scale =
                reader.IsDBNull(4)
                    ? null
                    : Convert.ToByte(
                        reader.GetValue(4));

            var isNullable =
                reader.GetString(5)
                    .Equals(
                        "YES",
                        StringComparison.OrdinalIgnoreCase);

            var ordinalPosition =
                reader.GetInt32(6);

            var isIdentity =
                reader.GetString(7)
                    .Equals(
                        "YES",
                        StringComparison.OrdinalIgnoreCase);

            columns.Add(
                 new ColumnDefinition
                 {
                     Name = reader.GetString(0),
                     PostgreSqlDataType = dataType,
                     MaxLength = maxLength,
                     Precision = precision,
                     Scale = scale,
                     IsNullable = isNullable,
                     OrdinalPosition = ordinalPosition,
                     IsIdentity = isIdentity
                 });
        }

        return columns;
    }

    private static string QuoteIdentifier(
        string identifier)
    {
        return "\"" +
               identifier.Replace("\"", "\"\"") +
               "\"";
    }
}