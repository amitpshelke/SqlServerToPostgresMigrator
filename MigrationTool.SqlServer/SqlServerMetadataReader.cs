using Microsoft.Data.SqlClient;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.SqlServer;

public sealed class SqlServerMetadataReader : IDatabaseMetadataReader
{
    private readonly string _connectionString;

    public SqlServerMetadataReader(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<IReadOnlyList<TableDefinition>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        var tables = new Dictionary<string, TableDefinition>(StringComparer.OrdinalIgnoreCase);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                s.name AS SchemaName,
                t.name AS TableName,
                c.name AS ColumnName,
                ty.name AS DataType,
                c.max_length AS MaxLength,
                c.precision AS PrecisionValue,
                c.scale AS ScaleValue,
                c.is_nullable AS IsNullable,
                c.is_identity AS IsIdentity,
                c.column_id AS ColumnId,
                dc.definition AS DefaultValue
            FROM sys.tables t
            INNER JOIN sys.schemas s
                ON t.schema_id = s.schema_id
            INNER JOIN sys.columns c
                ON t.object_id = c.object_id
            INNER JOIN sys.types ty
                ON c.user_type_id = ty.user_type_id
            LEFT JOIN sys.default_constraints dc
                ON c.default_object_id = dc.object_id
            WHERE t.is_ms_shipped = 0
            ORDER BY
                s.name,
                t.name,
                c.column_id;
            """;

        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(reader.GetOrdinal("SchemaName"));
            var tableName = reader.GetString(reader.GetOrdinal("TableName"));
            var column = CreateColumnDefinition(reader);
            var key = $"{schemaName}.{tableName}";

            if (!tables.TryGetValue(key, out var table))
            {
                table = new TableDefinition
                {
                    SchemaName = schemaName,
                    TableName = tableName
                };

                tables.Add(key, table);
            }

            table.Columns.Add(column);
        }

        return tables.Values.ToList();
    }

    private static ColumnDefinition CreateColumnDefinition(SqlDataReader reader)
    {
        return new ColumnDefinition
        {
            Name = reader.GetString(reader.GetOrdinal("ColumnName")),
            SqlServerDataType = reader.GetString(reader.GetOrdinal("DataType")),
            MaxLength = reader.IsDBNull(reader.GetOrdinal("MaxLength")) ? null : Convert.ToInt32(reader.GetValue(reader.GetOrdinal("MaxLength"))),
            Precision = reader.IsDBNull(reader.GetOrdinal("PrecisionValue")) ? null : Convert.ToByte(reader.GetValue(reader.GetOrdinal("PrecisionValue"))),
            Scale = reader.IsDBNull(reader.GetOrdinal("ScaleValue")) ? null : Convert.ToByte(reader.GetValue(reader.GetOrdinal("ScaleValue"))),
            IsNullable = reader.GetBoolean(reader.GetOrdinal("IsNullable")), 
            IsIdentity = reader.GetBoolean(reader.GetOrdinal("IsIdentity")),
            OrdinalPosition = reader.GetInt32(reader.GetOrdinal("ColumnId")),

            DefaultValue = reader.IsDBNull(reader.GetOrdinal("DefaultValue")) ? null : reader.GetString(reader.GetOrdinal("DefaultValue"))
        };
    }
}