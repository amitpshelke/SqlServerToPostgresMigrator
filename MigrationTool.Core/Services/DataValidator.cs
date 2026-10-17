using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

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

    private static bool ArePostgreSqlTypesEquivalent(
        ColumnDefinition source,
        ColumnDefinition target)
    {
        return string.Equals(
            NormalizeType(source.PostgreSqlDataType),
            NormalizeType(target.PostgreSqlDataType),
            StringComparison.OrdinalIgnoreCase);
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

    private static (string Type, int? Length)
    ParsePostgreSqlType(
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
}