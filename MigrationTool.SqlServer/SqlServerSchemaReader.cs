using System.Data;
using Microsoft.Data.SqlClient;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.SqlServer;

public sealed class SqlServerSchemaReader : ISchemaReader
{
    private readonly string _connectionString;

    public SqlServerSchemaReader(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyList<TableDefinition>> ReadSchemaAsync(
    IReadOnlyList<TableDefinition> tables,
    CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        var tableMap = tables.ToDictionary(
            t => t.FullName,
            StringComparer.OrdinalIgnoreCase);

        await ReadPrimaryKeysAsync(
            connection,
            tableMap,
            cancellationToken);

        await ReadForeignKeysAsync(
            connection,
            tableMap,
            cancellationToken);

        await ReadUniqueConstraintsAsync(
            connection,
            tableMap,
            cancellationToken);

        await ReadCheckConstraintsAsync(
            connection,
            tableMap,
            cancellationToken);

        await ReadIndexesAsync(
            connection,
            tableMap,
            cancellationToken);

        await ReadIdentityInformationAsync(
            connection,
            tableMap,
            cancellationToken);

        return tables;
    }

    private static TableDefinition? FindTable(
     IReadOnlyDictionary<string, TableDefinition> tables,
     string schemaName,
     string tableName)
    {
        var key = $"{schemaName}.{tableName}";

        return tables.TryGetValue(key, out var table)
            ? table
            : null;
    }

    private static async Task ReadPrimaryKeysAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            sch.name AS SchemaName,
            tbl.name AS TableName,
            kc.name AS ConstraintName,
            col.name AS ColumnName,
            ic.key_ordinal
        FROM sys.key_constraints kc
        INNER JOIN sys.tables tbl
            ON kc.parent_object_id = tbl.object_id
        INNER JOIN sys.schemas sch
            ON tbl.schema_id = sch.schema_id
        INNER JOIN sys.index_columns ic
            ON kc.parent_object_id = ic.object_id
            AND kc.unique_index_id = ic.index_id
        INNER JOIN sys.columns col
            ON ic.object_id = col.object_id
            AND ic.column_id = col.column_id
        WHERE
            kc.type = 'PK'
            AND tbl.is_ms_shipped = 0
        ORDER BY
            sch.name,
            tbl.name,
            kc.name,
            ic.key_ordinal;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(
                reader.GetOrdinal("SchemaName"));

            var tableName = reader.GetString(
                reader.GetOrdinal("TableName"));

            var columnName = reader.GetString(
                reader.GetOrdinal("ColumnName"));

            var constraintName = reader.GetString(
                reader.GetOrdinal("ConstraintName"));

            var table = FindTable(
                tables,
                schemaName,
                tableName);

            if (table is null)
                continue;

            if (table.PrimaryKey is null)
            {
                table.PrimaryKey = new PrimaryKeyDefinition
                {
                    Name = constraintName
                };
            }

            table.PrimaryKey.Columns.Add(columnName);
        }
    }

    private static async Task ReadForeignKeysAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            sch.name AS SchemaName,
            tbl.name AS TableName,
            fk.name AS ConstraintName,

            parent_col.name AS ColumnName,

            ref_sch.name AS ReferencedSchema,
            ref_tbl.name AS ReferencedTable,
            ref_col.name AS ReferencedColumn,

            fkc.constraint_column_id,

            fk.delete_referential_action_desc AS DeleteAction,
            fk.update_referential_action_desc AS UpdateAction

        FROM sys.foreign_keys fk

        INNER JOIN sys.foreign_key_columns fkc
            ON fk.object_id = fkc.constraint_object_id

        INNER JOIN sys.tables tbl
            ON fk.parent_object_id = tbl.object_id

        INNER JOIN sys.schemas sch
            ON tbl.schema_id = sch.schema_id

        INNER JOIN sys.columns parent_col
            ON fkc.parent_object_id = parent_col.object_id
            AND fkc.parent_column_id = parent_col.column_id

        INNER JOIN sys.tables ref_tbl
            ON fk.referenced_object_id = ref_tbl.object_id

        INNER JOIN sys.schemas ref_sch
            ON ref_tbl.schema_id = ref_sch.schema_id

        INNER JOIN sys.columns ref_col
            ON fkc.referenced_object_id = ref_col.object_id
            AND fkc.referenced_column_id = ref_col.column_id

        WHERE tbl.is_ms_shipped = 0

        ORDER BY
            sch.name,
            tbl.name,
            fk.name,
            fkc.constraint_column_id;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(
                reader.GetOrdinal("SchemaName"));

            var tableName = reader.GetString(
                reader.GetOrdinal("TableName"));

            var constraintName = reader.GetString(
                reader.GetOrdinal("ConstraintName"));

            var columnName = reader.GetString(
                reader.GetOrdinal("ColumnName"));

            var referencedSchema = reader.GetString(
                reader.GetOrdinal("ReferencedSchema"));

            var referencedTable = reader.GetString(
                reader.GetOrdinal("ReferencedTable"));

            var referencedColumn = reader.GetString(
                reader.GetOrdinal("ReferencedColumn"));

            var deleteAction = reader.GetString(
                reader.GetOrdinal("DeleteAction"));

            var updateAction = reader.GetString(
                reader.GetOrdinal("UpdateAction"));

            var table = FindTable(
                tables,
                schemaName,
                tableName);

            if (table is null)
                continue;

            var foreignKey = table.ForeignKeys
                .FirstOrDefault(x =>
                    x.Name.Equals(
                        constraintName,
                        StringComparison.OrdinalIgnoreCase));

            if (foreignKey is null)
            {
                foreignKey = new ForeignKeyDefinition
                {
                    Name = constraintName,
                    ReferencedSchema = referencedSchema,
                    ReferencedTable = referencedTable,
                    DeleteAction = deleteAction,
                    UpdateAction = updateAction
                };

                table.ForeignKeys.Add(foreignKey);
            }

            foreignKey.Columns.Add(columnName);
            foreignKey.ReferencedColumns.Add(referencedColumn);
        }
    }

    private static async Task ReadUniqueConstraintsAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            sch.name AS SchemaName,
            tbl.name AS TableName,
            kc.name AS ConstraintName,
            col.name AS ColumnName,
            ic.key_ordinal
        FROM sys.key_constraints kc
        INNER JOIN sys.tables tbl
            ON kc.parent_object_id = tbl.object_id
        INNER JOIN sys.schemas sch
            ON tbl.schema_id = sch.schema_id
        INNER JOIN sys.index_columns ic
            ON kc.parent_object_id = ic.object_id
            AND kc.unique_index_id = ic.index_id
        INNER JOIN sys.columns col
            ON ic.object_id = col.object_id
            AND ic.column_id = col.column_id
        WHERE
            kc.type = 'UQ'
            AND tbl.is_ms_shipped = 0
        ORDER BY
            sch.name,
            tbl.name,
            kc.name,
            ic.key_ordinal;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(
                reader.GetOrdinal("SchemaName"));

            var tableName = reader.GetString(
                reader.GetOrdinal("TableName"));

            var constraintName = reader.GetString(
                reader.GetOrdinal("ConstraintName"));

            var columnName = reader.GetString(
                reader.GetOrdinal("ColumnName"));

            var table = FindTable(
                tables,
                schemaName,
                tableName);

            if (table is null)
                continue;

            var constraint = table.UniqueConstraints
                .FirstOrDefault(x =>
                    x.Name.Equals(
                        constraintName,
                        StringComparison.OrdinalIgnoreCase));

            if (constraint is null)
            {
                constraint = new UniqueConstraintDefinition
                {
                    Name = constraintName
                };

                table.UniqueConstraints.Add(constraint);
            }

            constraint.Columns.Add(columnName);
        }
    }

    private static async Task ReadCheckConstraintsAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            sch.name AS SchemaName,
            tbl.name AS TableName,
            cc.name AS ConstraintName,
            cc.definition AS Definition
        FROM sys.check_constraints cc
        INNER JOIN sys.tables tbl
            ON cc.parent_object_id = tbl.object_id
        INNER JOIN sys.schemas sch
            ON tbl.schema_id = sch.schema_id
        WHERE
            tbl.is_ms_shipped = 0
        ORDER BY
            sch.name,
            tbl.name,
            cc.name;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(
                reader.GetOrdinal("SchemaName"));

            var tableName = reader.GetString(
                reader.GetOrdinal("TableName"));

            var constraintName = reader.GetString(
                reader.GetOrdinal("ConstraintName"));

            var definition = reader.GetString(
                reader.GetOrdinal("Definition"));

            var table = FindTable(
                tables,
                schemaName,
                tableName);

            if (table is null)
                continue;

            table.CheckConstraints.Add(
                new CheckConstraintDefinition
                {
                    Name = constraintName,
                    Definition = definition
                });
        }
    }

    private static async Task ReadIdentityInformationAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            s.name AS SchemaName,
            t.name AS TableName,
            c.name AS ColumnName,
            ic.seed_value AS IdentitySeed,
            ic.increment_value AS IdentityIncrement
        FROM sys.identity_columns ic
        INNER JOIN sys.tables t
            ON ic.object_id = t.object_id
        INNER JOIN sys.schemas s
            ON t.schema_id = s.schema_id
        INNER JOIN sys.columns c
            ON ic.object_id = c.object_id
            AND ic.column_id = c.column_id
        WHERE
            t.is_ms_shipped = 0
        ORDER BY
            s.name,
            t.name,
            c.column_id;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(
                reader.GetOrdinal("SchemaName"));

            var tableName = reader.GetString(
                reader.GetOrdinal("TableName"));

            var columnName = reader.GetString(
                reader.GetOrdinal("ColumnName"));

            var table = FindTable(
                tables,
                schemaName,
                tableName);

            if (table is null)
                continue;

            var column = table.Columns.FirstOrDefault(
                x => x.Name.Equals(
                    columnName,
                    StringComparison.OrdinalIgnoreCase));

            if (column is null)
                continue;

            column.IdentitySeed = Convert.ToInt64(reader["IdentitySeed"]);
            column.IdentityIncrement = Convert.ToInt64(reader["IdentityIncrement"]);
        }
    }

    private static async Task ReadIndexesAsync(
    SqlConnection connection,
    IReadOnlyDictionary<string, TableDefinition> tables,
    CancellationToken cancellationToken)
    {
        const string sql = """
        SELECT
            s.name AS SchemaName,
            t.name AS TableName,
            i.name AS IndexName,

            i.is_unique AS IsUnique,
            i.type AS IndexType,
            i.is_primary_key AS IsPrimaryKey,
            i.is_unique_constraint AS IsUniqueConstraint,
            i.is_disabled AS IsDisabled,
            i.filter_definition AS FilterDefinition,

            c.name AS ColumnName,
            ic.key_ordinal,
            ic.is_included_column,
            ic.index_column_id

        FROM sys.indexes i

        INNER JOIN sys.tables t
            ON i.object_id = t.object_id

        INNER JOIN sys.schemas s
            ON t.schema_id = s.schema_id

        INNER JOIN sys.index_columns ic
            ON i.object_id = ic.object_id
            AND i.index_id = ic.index_id

        INNER JOIN sys.columns c
            ON ic.object_id = c.object_id
            AND ic.column_id = c.column_id

        WHERE
            t.is_ms_shipped = 0
            AND i.name IS NOT NULL
            AND i.type IN (1, 2)
        
        ORDER BY
            s.name,
            t.name,
            i.name,
            ic.key_ordinal,
            ic.index_column_id;
        """;

        await using var command = new SqlCommand(sql, connection);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var schemaName = reader.GetString(reader.GetOrdinal("SchemaName"));
            var tableName = reader.GetString(reader.GetOrdinal("TableName"));
            var indexName = reader.GetString(reader.GetOrdinal("IndexName"));
            var columnName = reader.GetString(reader.GetOrdinal("ColumnName"));
            var isUnique = reader.GetBoolean(reader.GetOrdinal("IsUnique"));
            var indexType = Convert.ToInt32(reader.GetValue(reader.GetOrdinal("IndexType")));
            var isPrimaryKey = reader.GetBoolean(reader.GetOrdinal("IsPrimaryKey"));
            var isUniqueConstraint = reader.GetBoolean(reader.GetOrdinal("IsUniqueConstraint"));
            var isDisabled = reader.GetBoolean(reader.GetOrdinal("IsDisabled"));
            var filterDefinition = reader.IsDBNull(reader.GetOrdinal("FilterDefinition")) ? null  : reader.GetString(reader.GetOrdinal("FilterDefinition"));
            var isIncludedColumn = reader.GetBoolean(reader.GetOrdinal("is_included_column"));

            var table = FindTable(tables, schemaName, tableName);

            if (table is null)
                continue;

            var index = table.Indexes
                             .FirstOrDefault(x => x.Name.Equals(indexName, StringComparison.OrdinalIgnoreCase));

            if (index is null)
            {
                index = new IndexDefinition
                {
                    Name = indexName,
                    IsUnique = isUnique,
                    IsClustered = indexType == 1,
                    IsPrimaryKey = isPrimaryKey,
                    IsUniqueConstraint = isUniqueConstraint,
                    IsDisabled = isDisabled,
                    FilterDefinition = filterDefinition
                };

                table.Indexes.Add(index);
            }

            if (isIncludedColumn)
            {
                index.IncludedColumns.Add(columnName);
            }
            else
            {
                index.Columns.Add(columnName);
            }
        }
    }
}