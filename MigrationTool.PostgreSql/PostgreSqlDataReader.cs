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

    public async Task<PrimaryKeyDefinition?> GetPrimaryKeyAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            tc.constraint_name,
            kcu.column_name,
            kcu.ordinal_position
        FROM information_schema.table_constraints tc
        INNER JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
           AND tc.table_schema = kcu.table_schema
           AND tc.table_name = kcu.table_name
        WHERE tc.constraint_type = 'PRIMARY KEY'
          AND tc.table_schema = @schemaName
          AND tc.table_name = @tableName
        ORDER BY kcu.ordinal_position;
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

        string? constraintName = null;

        var columns = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            constraintName ??= reader.GetString(0);

            columns.Add(reader.GetString(1));
        }

        if (constraintName is null)
        {
            return null;
        }

        return new PrimaryKeyDefinition
        {
            Name = constraintName,
            Columns = columns
        };
    }

    public async Task<IReadOnlyList<UniqueConstraintDefinition>> GetUniqueConstraintsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            tc.constraint_name,
            kcu.column_name,
            kcu.ordinal_position
        FROM information_schema.table_constraints tc
        INNER JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
           AND tc.table_schema = kcu.table_schema
           AND tc.table_name = kcu.table_name
        WHERE tc.constraint_type = 'UNIQUE'
          AND tc.table_schema = @schemaName
          AND tc.table_name = @tableName
        ORDER BY
            tc.constraint_name,
            kcu.ordinal_position;
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

        var constraints =
            new Dictionary<string, UniqueConstraintDefinition>(
                StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            var constraintName =
                reader.GetString(0);

            var columnName =
                reader.GetString(1);

            if (!constraints.TryGetValue(
                constraintName,
                out var constraint))
            {
                constraint =
                    new UniqueConstraintDefinition
                    {
                        Name = constraintName
                    };

                constraints.Add(
                    constraintName,
                    constraint);
            }

            constraint.Columns.Add(columnName);
        }

        return constraints.Values.ToList();
    }

    public async Task<IReadOnlyList<ForeignKeyDefinition>> GetForeignKeysAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            tc.constraint_name,
            kcu.column_name,
            kcu.ordinal_position,
            ccu.table_schema AS referenced_schema,
            ccu.table_name AS referenced_table,
            ccu.column_name AS referenced_column,
            rc.delete_rule,
            rc.update_rule
        FROM information_schema.table_constraints tc
        INNER JOIN information_schema.key_column_usage kcu
            ON tc.constraint_name = kcu.constraint_name
           AND tc.table_schema = kcu.table_schema
           AND tc.table_name = kcu.table_name
        INNER JOIN information_schema.constraint_column_usage ccu
            ON tc.constraint_name = ccu.constraint_name
           AND tc.table_schema = ccu.table_schema
        INNER JOIN information_schema.referential_constraints rc
            ON tc.constraint_name = rc.constraint_name
           AND tc.table_schema = rc.constraint_schema
        WHERE tc.constraint_type = 'FOREIGN KEY'
          AND tc.table_schema = @schemaName
          AND tc.table_name = @tableName
        ORDER BY
            tc.constraint_name,
            kcu.ordinal_position;
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

        var constraints =
            new Dictionary<string, ForeignKeyDefinition>(
                StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(cancellationToken))
        {
            var constraintName =
                reader.GetString(0);

            var columnName =
                reader.GetString(1);

            var referencedSchema =
                reader.GetString(3);

            var referencedTable =
                reader.GetString(4);

            var referencedColumn =
                reader.GetString(5);

            var deleteAction =
                reader.GetString(6);

            var updateAction =
                reader.GetString(7);

            if (!constraints.TryGetValue(
                constraintName,
                out var constraint))
            {
                constraint =
                    new ForeignKeyDefinition
                    {
                        Name = constraintName,
                        ReferencedSchema = referencedSchema,
                        ReferencedTable = referencedTable,
                        DeleteAction = NormalizeReferentialAction(
                            deleteAction),
                        UpdateAction = NormalizeReferentialAction(
                            updateAction)
                    };

                constraints.Add(
                    constraintName,
                    constraint);
            }

            constraint.Columns.Add(columnName);
            constraint.ReferencedColumns.Add(referencedColumn);
        }

        return constraints.Values.ToList();
    }

    public async Task<IReadOnlyList<IndexDefinition>> GetIndexesAsync(
    TableDefinition table,
    CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            idx.relname AS index_name,

            i.indisunique AS is_unique,

            i.indisprimary AS is_primary_key,

            EXISTS
            (
                SELECT 1
                FROM pg_constraint c
                WHERE c.conindid = idx.oid
                  AND c.contype = 'u'
            ) AS is_unique_constraint,

            COALESCE(
                (
                    SELECT string_agg(
                        att.attname,
                        ',' ORDER BY key_data.ordinality
                    )
                    FROM unnest(i.indkey)
                        WITH ORDINALITY AS key_data(attnum, ordinality)
                    INNER JOIN pg_attribute att
                        ON att.attrelid = tbl.oid
                       AND att.attnum = key_data.attnum
                    WHERE key_data.ordinality <= i.indnkeyatts
                ),
                ''
            ) AS columns,

            COALESCE(
                (
                    SELECT string_agg(
                        att.attname,
                        ',' ORDER BY include_data.ordinality
                    )
                    FROM unnest(i.indkey)
                        WITH ORDINALITY AS include_data(attnum, ordinality)
                    INNER JOIN pg_attribute att
                        ON att.attrelid = tbl.oid
                       AND att.attnum = include_data.attnum
                    WHERE include_data.ordinality > i.indnkeyatts
                ),
                ''
            ) AS included_columns,

            pg_get_expr(
                i.indpred,
                i.indrelid
            ) AS filter_definition

        FROM pg_index i

        INNER JOIN pg_class idx
            ON idx.oid = i.indexrelid

        INNER JOIN pg_class tbl
            ON tbl.oid = i.indrelid

        INNER JOIN pg_namespace ns
            ON ns.oid = tbl.relnamespace

        WHERE ns.nspname = @schemaName
          AND tbl.relname = @tableName

        ORDER BY idx.relname;
        """;

        var indexes = new List<IndexDefinition>();

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
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columns =
                ParseCommaSeparatedList(
                    reader.GetString(4));

            var includedColumns =
                ParseCommaSeparatedList(
                    reader.GetString(5));

            indexes.Add(
                new IndexDefinition
                {
                    Name = reader.GetString(0),

                    IsUnique = reader.GetBoolean(1),

                    IsPrimaryKey = reader.GetBoolean(2),

                    IsUniqueConstraint = reader.GetBoolean(3),

                    Columns = columns,

                    IncludedColumns = includedColumns,

                    FilterDefinition =
                        reader.IsDBNull(6)
                            ? null
                            : reader.GetString(6)
                });
        }

        return indexes;
    }

    public async Task<IReadOnlyList<DefaultConstraintDefinition>> GetDefaultConstraintsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            column_name,
            column_default
        FROM information_schema.columns
        WHERE table_schema = @schemaName
          AND table_name = @tableName
          AND column_default IS NOT NULL
        ORDER BY ordinal_position;
        """;

        var defaults =
            new List<DefaultConstraintDefinition>();

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

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName =
                reader.GetString(0);

            var definition =
                reader.GetString(1);

            defaults.Add(
                new DefaultConstraintDefinition
                {
                    ColumnName = columnName,
                    Definition = definition
                });
        }

        return defaults;
    }

    public async Task<IReadOnlyList<CheckConstraintDefinition>> GetCheckConstraintsAsync(
        TableDefinition table,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
        SELECT
            con.conname,
            pg_get_constraintdef(
                con.oid,
                true
            )
        FROM pg_constraint con

        INNER JOIN pg_class tbl
            ON tbl.oid = con.conrelid

        INNER JOIN pg_namespace ns
            ON ns.oid = tbl.relnamespace

        WHERE con.contype = 'c'
          AND ns.nspname = @schemaName
          AND tbl.relname = @tableName

        ORDER BY con.conname;
        """;

        var constraints =
            new List<CheckConstraintDefinition>();

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

        while (await reader.ReadAsync(cancellationToken))
        {
            constraints.Add(
                new CheckConstraintDefinition
                {
                    Name = reader.GetString(0),
                    Definition = reader.GetString(1)
                });
        }

        return constraints;
    }

    private static List<string> ParseCommaSeparatedList(
     string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static string NormalizeReferentialAction(
    string action)
    {
        return action.Trim().ToUpperInvariant() switch
        {
            "NO ACTION" => "NO ACTION",
            "RESTRICT" => "RESTRICT",
            "CASCADE" => "CASCADE",
            "SET NULL" => "SET NULL",
            "SET DEFAULT" => "SET DEFAULT",
            _ => action.Trim().ToUpperInvariant()
        };
    }

    private static string QuoteIdentifier(
        string identifier)
    {
        return "\"" +
               identifier.Replace("\"", "\"\"") +
               "\"";
    }
}