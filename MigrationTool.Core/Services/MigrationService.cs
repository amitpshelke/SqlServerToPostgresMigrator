using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using Microsoft.Extensions.Logging;

namespace MigrationTool.Core.Services;

public sealed class MigrationService : IMigrationService
{
    private readonly IDatabaseMetadataReader _metadataReader;
    private readonly ISchemaGenerator _schemaGenerator;
    private readonly ILogger<MigrationService> _logger;
    private readonly ISchemaReader _schemaReader;
    private readonly ISchemaWriter _schemaWriter;
    private readonly ITypeMapper _typeMapper;

    public MigrationService(
    IDatabaseMetadataReader metadataReader,
    ISchemaReader schemaReader,
    ISchemaGenerator schemaGenerator,
    ISchemaWriter schemaWriter,
    ITypeMapper typeMapper,
    ILogger<MigrationService> logger)
    {
        _metadataReader = metadataReader;
        _schemaReader = schemaReader;
        _schemaGenerator = schemaGenerator;
        _schemaWriter = schemaWriter;
        _typeMapper = typeMapper;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TableDefinition>> InspectAsync(
    CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Reading SQL Server database metadata...");

        var tables =
            await _metadataReader.GetTablesAsync(cancellationToken);

        _logger.LogInformation(
            "Found {TableCount} tables.",
            tables.Count);

        _logger.LogInformation(
            "Reading SQL Server schema objects...");

        var schema =
            await _schemaReader.ReadSchemaAsync(
                tables,
                cancellationToken);

        foreach (var table in schema)
        {
            foreach (var column in table.Columns)
            {
                column.PostgreSqlDataType = _typeMapper.MapType(
                    column.SqlServerDataType,
                    column.MaxLength,
                    column.Precision,
                    column.Scale);
            }
        }

        _logger.LogInformation(
            "SQL Server schema analysis completed.");

        var primaryKeyCount =
            schema.Count(x => x.PrimaryKey is not null);

        var foreignKeyCount =
            schema.Sum(x => x.ForeignKeys.Count);

        var uniqueConstraintCount =
            schema.Sum(x => x.UniqueConstraints.Count);

        var checkConstraintCount =
            schema.Sum(x => x.CheckConstraints.Count);

        var indexCount =
            schema.Sum(x => x.Indexes.Count);

        _logger.LogInformation(
            "Schema objects: {PrimaryKeys} PKs, {ForeignKeys} FKs, " +
            "{UniqueConstraints} unique constraints, " +
            "{CheckConstraints} check constraints, {Indexes} indexes.",
            primaryKeyCount,
            foreignKeyCount,
            uniqueConstraintCount,
            checkConstraintCount,
            indexCount);

        return schema;
    }

    public async Task<int> GenerateSchemaAsync(string outputDirectory, CancellationToken cancellationToken = default)
    {
        var tables = await InspectAsync(cancellationToken);
        Directory.CreateDirectory(outputDirectory);
        int schemaFileCount = 0;

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var script = _schemaGenerator.GenerateCreateTableScript(table);
            var fileName = $"{table.SchemaName}_{table.TableName}.sql";
            var path = Path.Combine(outputDirectory, fileName);

            await File.WriteAllTextAsync(path, script, cancellationToken);

            schemaFileCount++;
        }
        return schemaFileCount;
    }

    public async Task CreatePostgreSqlSchemaAsync(CancellationToken cancellationToken = default)
    {
        var tables = await InspectAsync(cancellationToken);

        _logger.LogInformation("Creating PostgreSQL schema for {TableCount} tables...", tables.Count);

        await _schemaWriter.CreateSchemaAsync(tables, cancellationToken);

        _logger.LogInformation("PostgreSQL schema creation completed successfully.");
    }

    public async Task CreatePostgreSqlConstraintsAsync(
    CancellationToken cancellationToken = default)
    {
        var tables = await InspectAsync(cancellationToken);

        _logger.LogInformation(
            "Creating PostgreSQL constraints and indexes...");

        await _schemaWriter.CreateConstraintsAndIndexesAsync(
            tables,
            cancellationToken);

        _logger.LogInformation(
            "PostgreSQL constraints and indexes created successfully.");
    }
}