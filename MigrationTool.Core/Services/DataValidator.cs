using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using System.Text.RegularExpressions;

namespace MigrationTool.Core.Services;

public sealed class DataValidator : IDataValidator
{
    private readonly IDataReader _sourceReader;
    private readonly ITargetDataReader _targetReader;


    public DataValidator(
        IDataReader sourceReader,
        ITargetDataReader targetReader)
    {
        _sourceReader = sourceReader;
        _targetReader = targetReader;
    }

    public async Task<ValidationReport> ValidateRowCountsAsync(
        IReadOnlyList<TableDefinition> tables,
        CancellationToken cancellationToken = default)
    {
        var report = new ValidationReport();

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceCount =
                await _sourceReader.GetRowCountAsync(
                    table,
                    cancellationToken);

            var targetExists =
                await _targetReader.TableExistsAsync(
                    table,
                    cancellationToken);

            if (!targetExists)
            {
                report.Results.Add(
                    new ValidationResult
                    {
                        TableName = table.FullName,
                        ValidationType = "Row Count",
                        IsValid = false,
                        Message =
                            "Target table does not exist."
                    });

                continue;
            }

            var targetCount =
                await _targetReader.GetRowCountAsync(
                    table,
                    cancellationToken);

            var isValid =
                sourceCount == targetCount;

            report.Results.Add(
                new ValidationResult
                {
                    TableName = table.FullName,
                    ValidationType = "Row Count",
                    IsValid = isValid,
                    Message =
                        $"Source: {sourceCount:N0}, " +
                        $"Target: {targetCount:N0}"
                });
        }

        return report;
    }

    public async Task<ValidationReport> ValidateSchemaAsync(
        IReadOnlyList<TableDefinition> tables,
        CancellationToken cancellationToken = default)
    {
        var report = new ValidationReport();

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var tableIssues =
            await ValidateTableSchemaAsync(
                table,
                cancellationToken);

            await ValidatePrimaryKeyAsync(
                table,
                tableIssues,
                cancellationToken);

            await ValidateUniqueConstraintsAsync(
                table,
                tableIssues,
                cancellationToken);

            await ValidateForeignKeysAsync(
                table,
                tableIssues,
                cancellationToken);

            await ValidateIndexesAsync(
                table,
                tableIssues,
                cancellationToken);

            await ValidateDefaultConstraintsAsync(
                table,
                tableIssues,
                cancellationToken);

            await ValidateCheckConstraintsAsync(
                table,
                tableIssues,
                cancellationToken);

            if (tableIssues.Count == 0)
            {
                report.Results.Add(
                    new ValidationResult
                    {
                        TableName = table.FullName,
                        ValidationType = "Schema",
                        IsValid = true,
                        Message = "Schema matches."
                    });
            }
            else
            {
                report.Results.Add(
                    new ValidationResult
                    {
                        TableName = table.FullName,
                        ValidationType = "Schema",
                        IsValid = false,
                        Message =
                            $"{tableIssues.Count} schema issue(s) found."
                    });

                report.Issues.AddRange(tableIssues);
            }
        }

        return report;
    }

    private async Task<List<ValidationIssue>>
        ValidateTableSchemaAsync(
            TableDefinition table,
            CancellationToken cancellationToken)
    {
        var issues =
            new List<ValidationIssue>();

        var exists =
            await _targetReader.TableExistsAsync(
                table,
                cancellationToken);

        if (!exists)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = table.TableName,
                    ValidationType = "Table",
                    Message =
                        "Target table does not exist."
                });

            return issues;
        }

        var targetColumns =
            await _targetReader.GetColumnsAsync(
                table,
                cancellationToken);

        if (table.Columns.Count !=
            targetColumns.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = table.TableName,
                    ValidationType = "Column Count",
                    Message =
                        $"Source has {table.Columns.Count} " +
                        $"columns, target has " +
                        $"{targetColumns.Count}."
                });
        }

        var sourceColumns =
            table.Columns
                .OrderBy(c => c.OrdinalPosition)
                .ToList();

        var targetColumnMap =
            targetColumns.ToDictionary(
                c => c.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceColumn in sourceColumns)
        {
            if (!targetColumnMap.TryGetValue(
                sourceColumn.Name,
                out var targetColumn))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceColumn.Name,
                        ValidationType = "Column",
                        Message =
                            "Column does not exist in target."
                    });

                continue;
            }

            ValidateColumn(
                table,
                sourceColumn,
                targetColumn,
                issues);
        }

        return issues;
    }

    private static void ValidateColumn(
     TableDefinition table,
     ColumnDefinition sourceColumn,
     ColumnDefinition targetColumn,
     List<ValidationIssue> issues)
    {
        ValidateDataType(
            table,
            sourceColumn,
            targetColumn,
            issues);

        if (sourceColumn.IsNullable !=
            targetColumn.IsNullable)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Nullable",
                    Message =
                        $"Expected nullable = " +
                        $"{sourceColumn.IsNullable}, " +
                        $"target has " +
                        $"{targetColumn.IsNullable}."
                });
        }

        if (sourceColumn.IsIdentity !=
            targetColumn.IsIdentity)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Identity",
                    Message =
                        $"Expected identity = " +
                        $"{sourceColumn.IsIdentity}, " +
                        $"target has " +
                        $"{targetColumn.IsIdentity}."
                });
        }
    }

    private static void ValidateDataType(
    TableDefinition table,
    ColumnDefinition sourceColumn,
    ColumnDefinition targetColumn,
    List<ValidationIssue> issues)
    {
        var source =
            ParsePostgreSqlType(
                sourceColumn.PostgreSqlDataType);

        var target =
            ParsePostgreSqlType(
                targetColumn.PostgreSqlDataType);

        if (!string.Equals(
            source.Type,
            target.Type,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Data Type",
                    Message =
                        $"Expected PostgreSQL type " +
                        $"'{sourceColumn.PostgreSqlDataType}', " +
                        $"target has " +
                        $"'{targetColumn.PostgreSqlDataType}'."
                });

            return;
        }

        ValidateLength(
            table,
            sourceColumn,
            targetColumn,
            source.Length,
            target.Length,
            issues);

        ValidatePrecisionScale(
            table,
            sourceColumn,
            targetColumn,
            issues);
    }

    private static string NormalizeType(
    string dataType)
    {
        var normalized =
            dataType
                .Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "character varying" => "varchar",
            "varchar" => "varchar",

            "character" => "char",
            "char" => "char",

            "timestamp without time zone" =>
                "timestamp",

            "timestamp" =>
                "timestamp",

            "timestamp with time zone" =>
                "timestamp with time zone",

            "time without time zone" =>
                "time",

            "time" =>
                "time",

            "boolean" =>
                "boolean",

            "integer" =>
                "integer",

            "bigint" =>
                "bigint",

            "smallint" =>
                "smallint",

            "numeric" =>
                "numeric",

            "real" =>
                "real",

            "double precision" =>
                "double precision",

            "date" =>
                "date",

            "uuid" =>
                "uuid",

            "bytea" =>
                "bytea",

            "xml" =>
                "xml",

            "text" =>
                "text",

            _ => normalized
        };
    }

    private static void ValidateLength(
     TableDefinition table,
     ColumnDefinition sourceColumn,
     ColumnDefinition targetColumn,
     int? sourceLength,
     int? targetLength,
     List<ValidationIssue> issues)
    {
        var sourceType =
            NormalizeType(
                sourceColumn.PostgreSqlDataType);

        if (sourceType != "varchar" &&
            sourceType != "char")
        {
            return;
        }

        if (sourceLength != targetLength)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Length",
                    Message =
                        $"Expected length = " +
                        $"{sourceLength}, " +
                        $"target has " +
                        $"{targetLength}."
                });
        }
    }

    private static void ValidatePrecisionScale(
    TableDefinition table,
    ColumnDefinition sourceColumn,
    ColumnDefinition targetColumn,
    List<ValidationIssue> issues)
    {
        var sourceType =
            NormalizeType(sourceColumn.PostgreSqlDataType);

        if (sourceType != "numeric")
        {
            return;
        }

        if (sourceColumn.Precision !=
            targetColumn.Precision)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Precision",
                    Message =
                        $"Expected precision = " +
                        $"{sourceColumn.Precision}, " +
                        $"target has " +
                        $"{targetColumn.Precision}."
                });
        }

        if (sourceColumn.Scale !=
            targetColumn.Scale)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceColumn.Name,
                    ValidationType = "Scale",
                    Message =
                        $"Expected scale = " +
                        $"{sourceColumn.Scale}, " +
                        $"target has " +
                        $"{targetColumn.Scale}."
                });
        }
    }

    private async Task ValidatePrimaryKeyAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourcePrimaryKey = table.PrimaryKey;

        var targetPrimaryKey =
            await _targetReader.GetPrimaryKeyAsync(
                table,
                cancellationToken);

        // Source has no PK and target has no PK.
        if (sourcePrimaryKey is null &&
            targetPrimaryKey is null)
        {
            return;
        }

        // Source has PK but target does not.
        if (sourcePrimaryKey is not null &&
            targetPrimaryKey is null)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourcePrimaryKey.Name,
                    ValidationType = "Primary Key",
                    Message =
                        "Primary key exists in source " +
                        "but is missing in target."
                });

            return;
        }

        // Source has no PK but target does.
        if (sourcePrimaryKey is null &&
            targetPrimaryKey is not null)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = targetPrimaryKey.Name,
                    ValidationType = "Primary Key",
                    Message =
                        "Primary key exists in target " +
                        "but is missing in source."
                });

            return;
        }

        // Both exist.
        if (sourcePrimaryKey is null ||
            targetPrimaryKey is null)
        {
            return;
        }

        ValidatePrimaryKeyName(
            table,
            sourcePrimaryKey,
            targetPrimaryKey,
            issues);

        ValidatePrimaryKeyColumns(
            table,
            sourcePrimaryKey,
            targetPrimaryKey,
            issues);
    }

    private static void ValidatePrimaryKeyName(
    TableDefinition table,
    PrimaryKeyDefinition sourcePrimaryKey,
    PrimaryKeyDefinition targetPrimaryKey,
    List<ValidationIssue> issues)
    {
        if (!string.Equals(
            sourcePrimaryKey.Name,
            targetPrimaryKey.Name,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourcePrimaryKey.Name,
                    ValidationType = "Primary Key",
                    Message =
                        $"Expected primary key " +
                        $"'{sourcePrimaryKey.Name}', " +
                        $"target has " +
                        $"'{targetPrimaryKey.Name}'."
                });
        }
    }

    private static void ValidatePrimaryKeyColumns(
    TableDefinition table,
    PrimaryKeyDefinition sourcePrimaryKey,
    PrimaryKeyDefinition targetPrimaryKey,
    List<ValidationIssue> issues)
    {
        if (sourcePrimaryKey.Columns.Count !=
            targetPrimaryKey.Columns.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourcePrimaryKey.Name,
                    ValidationType = "Primary Key Columns",
                    Message =
                        $"Expected " +
                        $"{sourcePrimaryKey.Columns.Count} " +
                        $"column(s), target has " +
                        $"{targetPrimaryKey.Columns.Count}."
                });

            return;
        }

        for (var i = 0;
             i < sourcePrimaryKey.Columns.Count;
             i++)
        {
            var sourceColumn =
                sourcePrimaryKey.Columns[i];

            var targetColumn =
                targetPrimaryKey.Columns[i];

            if (!string.Equals(
                sourceColumn,
                targetColumn,
                StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourcePrimaryKey.Name,
                        ValidationType = "Primary Key Columns",
                        Message =
                            $"Expected column " +
                            $"'{sourceColumn}' at position {i + 1}, " +
                            $"target has " +
                            $"'{targetColumn}'."
                    });
            }
        }
    }

    private async Task ValidateUniqueConstraintsAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourceConstraints =
            table.UniqueConstraints
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

        var targetConstraints =
            await _targetReader.GetUniqueConstraintsAsync(
                table,
                cancellationToken);

        var targetConstraintMap =
            targetConstraints.ToDictionary(
                c => c.Name,
                StringComparer.OrdinalIgnoreCase);

        // Compare constraint count.
        if (sourceConstraints.Count !=
            targetConstraints.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = table.TableName,
                    ValidationType = "Unique Constraint Count",
                    Message =
                        $"Expected " +
                        $"{sourceConstraints.Count} unique constraint(s), " +
                        $"target has " +
                        $"{targetConstraints.Count}."
                });
        }

        foreach (var sourceConstraint in sourceConstraints)
        {
            if (!targetConstraintMap.TryGetValue(
                sourceConstraint.Name,
                out var targetConstraint))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceConstraint.Name,
                        ValidationType = "Unique Constraint",
                        Message =
                            "Unique constraint exists in source " +
                            "but is missing in target."
                    });

                continue;
            }

            ValidateUniqueConstraintColumns(
                table,
                sourceConstraint,
                targetConstraint,
                issues);
        }

        // Detect constraints that exist only in target.
        var sourceConstraintNames =
            new HashSet<string>(
                sourceConstraints.Select(c => c.Name),
                StringComparer.OrdinalIgnoreCase);

        foreach (var targetConstraint in targetConstraints)
        {
            if (!sourceConstraintNames.Contains(
                targetConstraint.Name))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = targetConstraint.Name,
                        ValidationType = "Unique Constraint",
                        Message =
                            "Unique constraint exists in target " +
                            "but is missing in source."
                    });
            }
        }
    }

    private static void ValidateUniqueConstraintColumns(
    TableDefinition table,
    UniqueConstraintDefinition sourceConstraint,
    UniqueConstraintDefinition targetConstraint,
    List<ValidationIssue> issues)
    {
        if (sourceConstraint.Columns.Count !=
            targetConstraint.Columns.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceConstraint.Name,
                    ValidationType = "Unique Constraint Columns",
                    Message =
                        $"Expected " +
                        $"{sourceConstraint.Columns.Count} column(s), " +
                        $"target has " +
                        $"{targetConstraint.Columns.Count}."
                });

            return;
        }

        for (var i = 0;
             i < sourceConstraint.Columns.Count;
             i++)
        {
            var sourceColumn =
                sourceConstraint.Columns[i];

            var targetColumn =
                targetConstraint.Columns[i];

            if (!string.Equals(
                sourceColumn,
                targetColumn,
                StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceConstraint.Name,
                        ValidationType = "Unique Constraint Columns",
                        Message =
                            $"Expected column " +
                            $"'{sourceColumn}' at position {i + 1}, " +
                            $"target has " +
                            $"'{targetColumn}'."
                    });
            }
        }
    }

    private async Task ValidateForeignKeysAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourceForeignKeys =
            table.ForeignKeys
                .OrderBy(
                    fk => fk.Name,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();

        var targetForeignKeys =
            await _targetReader.GetForeignKeysAsync(
                table,
                cancellationToken);

        var targetForeignKeyMap =
            targetForeignKeys.ToDictionary(
                fk => fk.Name,
                StringComparer.OrdinalIgnoreCase);

        if (sourceForeignKeys.Count !=
            targetForeignKeys.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = table.TableName,
                    ValidationType = "Foreign Key Count",
                    Message =
                        $"Expected " +
                        $"{sourceForeignKeys.Count} foreign key(s), " +
                        $"target has " +
                        $"{targetForeignKeys.Count}."
                });
        }

        foreach (var sourceForeignKey in sourceForeignKeys)
        {
            if (!targetForeignKeyMap.TryGetValue(
                sourceForeignKey.Name,
                out var targetForeignKey))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceForeignKey.Name,
                        ValidationType = "Foreign Key",
                        Message =
                            "Foreign key exists in source " +
                            "but is missing in target."
                    });

                continue;
            }

            ValidateForeignKey(
                table,
                sourceForeignKey,
                targetForeignKey,
                issues);
        }

        var sourceForeignKeyNames =
            new HashSet<string>(
                sourceForeignKeys.Select(fk => fk.Name),
                StringComparer.OrdinalIgnoreCase);

        foreach (var targetForeignKey in targetForeignKeys)
        {
            if (!sourceForeignKeyNames.Contains(
                targetForeignKey.Name))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = targetForeignKey.Name,
                        ValidationType = "Foreign Key",
                        Message =
                            "Foreign key exists in target " +
                            "but is missing in source."
                    });
            }
        }
    }

    private static void ValidateForeignKey(
    TableDefinition table,
    ForeignKeyDefinition sourceForeignKey,
    ForeignKeyDefinition targetForeignKey,
    List<ValidationIssue> issues)
    {
        ValidateForeignKeyColumns(
            table,
            sourceForeignKey,
            targetForeignKey,
            issues);

        ValidateReferencedTable(
            table,
            sourceForeignKey,
            targetForeignKey,
            issues);

        ValidateReferencedColumns(
            table,
            sourceForeignKey,
            targetForeignKey,
            issues);

        ValidateReferentialActions(
            table,
            sourceForeignKey,
            targetForeignKey,
            issues);
    }

    private static void ValidateForeignKeyColumns(
    TableDefinition table,
    ForeignKeyDefinition sourceForeignKey,
    ForeignKeyDefinition targetForeignKey,
    List<ValidationIssue> issues)
    {
        if (sourceForeignKey.Columns.Count !=
            targetForeignKey.Columns.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Columns",
                    Message =
                        $"Expected " +
                        $"{sourceForeignKey.Columns.Count} " +
                        $"source column(s), target has " +
                        $"{targetForeignKey.Columns.Count}."
                });

            return;
        }

        for (var i = 0;
             i < sourceForeignKey.Columns.Count;
             i++)
        {
            if (!string.Equals(
                sourceForeignKey.Columns[i],
                targetForeignKey.Columns[i],
                StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceForeignKey.Name,
                        ValidationType = "Foreign Key Columns",
                        Message =
                            $"Expected source column " +
                            $"'{sourceForeignKey.Columns[i]}' " +
                            $"at position {i + 1}, target has " +
                            $"'{targetForeignKey.Columns[i]}'."
                    });
            }
        }
    }

    private static void ValidateReferencedTable(
    TableDefinition table,
    ForeignKeyDefinition sourceForeignKey,
    ForeignKeyDefinition targetForeignKey,
    List<ValidationIssue> issues)
    {
        if (!string.Equals(
            sourceForeignKey.ReferencedSchema,
            targetForeignKey.ReferencedSchema,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Reference",
                    Message =
                        $"Expected referenced schema " +
                        $"'{sourceForeignKey.ReferencedSchema}', " +
                        $"target has " +
                        $"'{targetForeignKey.ReferencedSchema}'."
                });
        }

        if (!string.Equals(
            sourceForeignKey.ReferencedTable,
            targetForeignKey.ReferencedTable,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Reference",
                    Message =
                        $"Expected referenced table " +
                        $"'{sourceForeignKey.ReferencedTable}', " +
                        $"target has " +
                        $"'{targetForeignKey.ReferencedTable}'."
                });
        }
    }

    private static void ValidateReferencedColumns(
    TableDefinition table,
    ForeignKeyDefinition sourceForeignKey,
    ForeignKeyDefinition targetForeignKey,
    List<ValidationIssue> issues)
    {
        if (sourceForeignKey.ReferencedColumns.Count !=
            targetForeignKey.ReferencedColumns.Count)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Reference Columns",
                    Message =
                        $"Expected " +
                        $"{sourceForeignKey.ReferencedColumns.Count} " +
                        $"referenced column(s), target has " +
                        $"{targetForeignKey.ReferencedColumns.Count}."
                });

            return;
        }

        for (var i = 0;
             i < sourceForeignKey.ReferencedColumns.Count;
             i++)
        {
            if (!string.Equals(
                sourceForeignKey.ReferencedColumns[i],
                targetForeignKey.ReferencedColumns[i],
                StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceForeignKey.Name,
                        ValidationType =
                            "Foreign Key Reference Columns",
                        Message =
                            $"Expected referenced column " +
                            $"'{sourceForeignKey.ReferencedColumns[i]}' " +
                            $"at position {i + 1}, target has " +
                            $"'{targetForeignKey.ReferencedColumns[i]}'."
                    });
            }
        }
    }

    private static void ValidateReferentialActions(
    TableDefinition table,
    ForeignKeyDefinition sourceForeignKey,
    ForeignKeyDefinition targetForeignKey,
    List<ValidationIssue> issues)
    {
        var sourceDelete =
            NormalizeReferentialAction(
                sourceForeignKey.DeleteAction);

        var targetDelete =
            NormalizeReferentialAction(
                targetForeignKey.DeleteAction);

        if (!string.Equals(
            sourceDelete,
            targetDelete,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Delete Action",
                    Message =
                        $"Expected delete action " +
                        $"'{sourceDelete}', target has " +
                        $"'{targetDelete}'."
                });
        }

        var sourceUpdate =
            NormalizeReferentialAction(
                sourceForeignKey.UpdateAction);

        var targetUpdate =
            NormalizeReferentialAction(
                targetForeignKey.UpdateAction);

        if (!string.Equals(
            sourceUpdate,
            targetUpdate,
            StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceForeignKey.Name,
                    ValidationType = "Foreign Key Update Action",
                    Message =
                        $"Expected update action " +
                        $"'{sourceUpdate}', target has " +
                        $"'{targetUpdate}'."
                });
        }
    }

    private async Task ValidateIndexesAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourceIndexes =
            table.Indexes
                .Where(index =>
                    !index.IsPrimaryKey &&
                    !index.IsUniqueConstraint)
                .ToList();

        var targetIndexes =
            (await _targetReader.GetIndexesAsync(
                table,
                cancellationToken))
            .Where(index =>
                !index.IsPrimaryKey &&
                !index.IsUniqueConstraint)
            .ToList();

        var sourceByName =
            sourceIndexes.ToDictionary(
                index => index.Name,
                StringComparer.OrdinalIgnoreCase);

        var targetByName =
            targetIndexes.ToDictionary(
                index => index.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceIndex in sourceIndexes)
        {
            if (!targetByName.TryGetValue(
                    sourceIndex.Name,
                    out var targetIndex))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceIndex.Name,
                        ValidationType = "Index",
                        Message =
                            $"Index '{sourceIndex.Name}' exists in SQL Server but not in PostgreSQL."
                    });

                continue;
            }

            ValidateIndex(
                table,
                sourceIndex,
                targetIndex,
                issues);
        }

        foreach (var targetIndex in targetIndexes)
        {
            if (!sourceByName.ContainsKey(
                    targetIndex.Name))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = targetIndex.Name,
                        ValidationType = "Index",
                        Message =
                            $"Index '{targetIndex.Name}' exists in PostgreSQL but not in SQL Server."
                    });
            }
        }
    }

    private static void ValidateIndex(
    TableDefinition table,
    IndexDefinition sourceIndex,
    IndexDefinition targetIndex,
    List<ValidationIssue> issues)
    {
        if (sourceIndex.IsUnique != targetIndex.IsUnique)
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceIndex.Name,
                    ValidationType = "Index",
                    Message =
                        $"Index '{sourceIndex.Name}' uniqueness mismatch. " +
                        $"Expected '{sourceIndex.IsUnique}', " +
                        $"target has '{targetIndex.IsUnique}'."
                });
        }

        ValidateIndexColumns(
            table,
            sourceIndex,
            targetIndex,
            issues);

        ValidateIncludedColumns(
            table,
            sourceIndex,
            targetIndex,
            issues);

        ValidateFilterDefinition(
            table,
            sourceIndex,
            targetIndex,
            issues);
    }

    private static void ValidateIndexColumns(
     TableDefinition table,
     IndexDefinition sourceIndex,
     IndexDefinition targetIndex,
     List<ValidationIssue> issues)
    {
        if (!sourceIndex.Columns.SequenceEqual(
                targetIndex.Columns,
                StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceIndex.Name,
                    ValidationType = "Index",
                    Message =
                        $"Index '{sourceIndex.Name}' column mismatch. " +
                        $"Expected [{string.Join(", ", sourceIndex.Columns)}], " +
                        $"target has [{string.Join(", ", targetIndex.Columns)}]."
                });
        }
    }

    private static void ValidateIncludedColumns(
    TableDefinition table,
    IndexDefinition sourceIndex,
    IndexDefinition targetIndex,
    List<ValidationIssue> issues)
    {
        if (!sourceIndex.IncludedColumns.SequenceEqual(
                targetIndex.IncludedColumns,
                StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceIndex.Name,
                    ValidationType = "Index",
                    Message =
                        $"Index '{sourceIndex.Name}' included-column mismatch. " +
                        $"Expected [{string.Join(", ", sourceIndex.IncludedColumns)}], " +
                        $"target has [{string.Join(", ", targetIndex.IncludedColumns)}]."
                });
        }
    }

    private static void ValidateFilterDefinition(
     TableDefinition table,
     IndexDefinition sourceIndex,
     IndexDefinition targetIndex,
     List<ValidationIssue> issues)
    {
        var sourceFilter =
            NormalizeFilter(
                sourceIndex.FilterDefinition);

        var targetFilter =
            NormalizeFilter(
                targetIndex.FilterDefinition);

        if (!string.Equals(
                sourceFilter,
                targetFilter,
                StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(
                new ValidationIssue
                {
                    TableName = table.FullName,
                    ObjectName = sourceIndex.Name,
                    ValidationType = "Index",
                    Message =
                        $"Index '{sourceIndex.Name}' filter mismatch. " +
                        $"Expected '{sourceIndex.FilterDefinition}', " +
                        $"target has '{targetIndex.FilterDefinition}'."
                });
        }
    }

    private async Task ValidateDefaultConstraintsAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourceDefaults =
            table.Columns
                .Where(c =>
                    !string.IsNullOrWhiteSpace(
                        c.DefaultValue))
                .ToDictionary(
                    c => c.Name,
                    StringComparer.OrdinalIgnoreCase);

        var targetDefaults =
            await _targetReader
                .GetDefaultConstraintsAsync(
                    table,
                    cancellationToken);

        var targetByColumn =
            targetDefaults.ToDictionary(
                d => d.ColumnName,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceColumn in sourceDefaults)
        {
            if (!targetByColumn.TryGetValue(
                    sourceColumn.Key,
                    out var targetDefault))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceColumn.Key,
                        ValidationType = "Default",
                        Message =
                            $"Column '{sourceColumn.Key}' has a default in SQL Server " +
                            $"but no default in PostgreSQL."
                    });

                continue;
            }

            var sourceDefault =
                NormalizeSqlServerDefault(
                    sourceColumn.Value.DefaultValue);

            var postgresDefault =
                NormalizePostgreSqlDefault(
                    targetDefault.Definition);

            if (!DefaultsAreEquivalent(
                    sourceDefault,
                    postgresDefault,
                    sourceColumn.Value))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceColumn.Key,
                        ValidationType = "Default",
                        Message =
                            $"Default mismatch for column '{sourceColumn.Key}'. " +
                            $"Expected '{sourceColumn.Value.DefaultValue}', " +
                            $"target has '{targetDefault.Definition}'."
                    });
            }
        }

        foreach (var targetDefault in targetDefaults)
        {
            if (!sourceDefaults.ContainsKey(
                    targetDefault.ColumnName))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = targetDefault.ColumnName,
                        ValidationType = "Default",
                        Message =
                            $"Column '{targetDefault.ColumnName}' has a default in PostgreSQL " +
                            $"but no default in SQL Server."
                    });
            }
        }
    }

    private async Task ValidateCheckConstraintsAsync(
    TableDefinition table,
    List<ValidationIssue> issues,
    CancellationToken cancellationToken)
    {
        var sourceConstraints =
            table.CheckConstraints
                .ToDictionary(
                    c => c.Name,
                    StringComparer.OrdinalIgnoreCase);

        var targetConstraints =
            await _targetReader
                .GetCheckConstraintsAsync(
                    table,
                    cancellationToken);

        var targetByName =
            targetConstraints.ToDictionary(
                c => c.Name,
                StringComparer.OrdinalIgnoreCase);

        foreach (var sourceConstraint in sourceConstraints.Values)
        {
            if (!targetByName.TryGetValue(
                    sourceConstraint.Name,
                    out var targetConstraint))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceConstraint.Name,
                        ValidationType = "Check Constraint",
                        Message =
                            $"Check constraint '{sourceConstraint.Name}' " +
                            $"exists in SQL Server but not in PostgreSQL."
                    });

                continue;
            }

            var sourceDefinition =
                NormalizeConstraintDefinition(
                    sourceConstraint.Definition);

            var targetDefinition =
                NormalizeConstraintDefinition(
                    targetConstraint.Definition);

            if (!string.Equals(
                    sourceDefinition,
                    targetDefinition,
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = sourceConstraint.Name,
                        ValidationType = "Check Constraint",
                        Message =
                            $"Check constraint '{sourceConstraint.Name}' definition mismatch. " +
                            $"Expected '{sourceConstraint.Definition}', " +
                            $"target has '{targetConstraint.Definition}'."
                    });
            }
        }

        foreach (var targetConstraint in targetConstraints)
        {
            if (!sourceConstraints.ContainsKey(
                    targetConstraint.Name))
            {
                issues.Add(
                    new ValidationIssue
                    {
                        TableName = table.FullName,
                        ObjectName = targetConstraint.Name,
                        ValidationType = "Check Constraint",
                        Message =
                            $"Check constraint '{targetConstraint.Name}' " +
                            $"exists in PostgreSQL but not in SQL Server."
                    });
            }
        }
    }

    private static string NormalizeReferentialAction(
     string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return "NO ACTION";
        }

        return action
            .Trim()
            .ToUpperInvariant() switch
        {
            "NO ACTION" => "NO ACTION",
            "NO_ACTION" => "NO ACTION",
            "NOACTION" => "NO ACTION",

            "RESTRICT" => "RESTRICT",

            "CASCADE" => "CASCADE",

            "SET NULL" => "SET NULL",
            "SET_NULL" => "SET NULL",
            "SETNULL" => "SET NULL",

            "SET DEFAULT" => "SET DEFAULT",
            "SET_DEFAULT" => "SET DEFAULT",
            "SETDEFAULT" => "SET DEFAULT",

            _ => action
                .Trim()
                .ToUpperInvariant()
        };
    }

    private static string? NormalizeFilter(
     string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return null;
        }

        return filter
            .Trim()
            .Trim('(', ')')
            .Replace("[", "")
            .Replace("]", "")
            .Replace("\"", "")
            .ToUpperInvariant();
    }

    private static string NormalizeSqlServerDefault(
    string defaultValue)
    {
        var value = defaultValue.Trim();

        // Remove SQL Server's nested default parentheses.
        while (value.Length >= 2 &&
               value.StartsWith("(") &&
               value.EndsWith(")"))
        {
            value = value[1..^1].Trim();
        }

        return value.ToUpperInvariant();
    }

    private static string NormalizePostgreSqlDefault(
    string defaultValue)
    {
        var value = defaultValue.Trim();

        // PostgreSQL information_schema may return:
        // 'Open'::character varying
        // 'Active'::character varying
        //
        // Remove the PostgreSQL character-varying cast
        // so that the semantic value can be compared.

        value = Regex.Replace(
            value,
            @"::\s*(CHARACTER\s+VARYING|VARCHAR)",
            string.Empty,
            RegexOptions.IgnoreCase);

        value = Regex.Replace(
            value,
            @"::\s*TEXT",
            string.Empty,
            RegexOptions.IgnoreCase);

        return value
            .Trim()
            .ToUpperInvariant();
    }

    private static bool DefaultsAreEquivalent(
     string sourceDefault,
     string targetDefault,
     ColumnDefinition column)
    {
        if (string.Equals(
                sourceDefault,
                targetDefault,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return sourceDefault switch
        {
            "GETDATE()" =>
                targetDefault == "CURRENT_TIMESTAMP",

            "SYSDATETIME()" =>
                targetDefault == "CURRENT_TIMESTAMP",

            "SYSUTCDATETIME()" =>
                targetDefault == "CURRENT_TIMESTAMP",

            "NEWID()" =>
                targetDefault == "GEN_RANDOM_UUID()",

            "1" when IsBooleanColumn(column) =>
                targetDefault is "TRUE" or "TRUE::BOOLEAN",

            "0" when IsBooleanColumn(column) =>
                targetDefault is "FALSE" or "FALSE::BOOLEAN",

            _ => false
        };
    }

    private static string NormalizeConstraintDefinition(
    string definition)
    {
        return definition
            .Trim()
            .Trim('(', ')')
            .Replace("[", "")
            .Replace("]", "")
            .Replace("\"", "")
            .Replace(" ", "")
            .ToUpperInvariant();
    }

    private static (string Type, int? Length) ParsePostgreSqlType(
        string dataType)
    {
        var normalized =
            dataType
                .Trim()
                .ToLowerInvariant();

        var openParen =
            normalized.IndexOf('(');

        if (openParen < 0)
        {
            return (
                NormalizeType(normalized),
                null);
        }

        var closeParen =
            normalized.IndexOf(
                ')',
                openParen);

        if (closeParen < 0)
        {
            return (
                NormalizeType(normalized),
                null);
        }

        var baseType =
            normalized[..openParen]
                .Trim();

        var lengthText =
            normalized[
                (openParen + 1)..closeParen];

        if (int.TryParse(
            lengthText,
            out var length))
        {
            return (
                NormalizeType(baseType),
                length);
        }

        return (
            NormalizeType(baseType),
            null);
    }

    private static bool IsBooleanColumn(
    ColumnDefinition column)
    {
        return string.Equals(
            column.SqlServerDataType,
            "bit",
            StringComparison.OrdinalIgnoreCase);
    }
}