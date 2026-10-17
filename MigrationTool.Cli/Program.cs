using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MigrationTool.Core.Configuration;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;
using MigrationTool.Core.Services;
using MigrationTool.Mapping;
using MigrationTool.PostgreSql;
using MigrationTool.SqlServer;

var configuration =
    new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();

var options = configuration.Get<MigrationOptions>() ?? throw new InvalidOperationException("Migration configuration could not be loaded.");

if (string.IsNullOrWhiteSpace(options.Source.ConnectionString))
{
    throw new InvalidOperationException("Source connection string is missing.");
}

var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });

    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddSingleton(options);
services.AddSingleton<ITypeMapper, SqlServerTypeMapper>();
services.AddSingleton<IDatabaseMetadataReader>(_ => new SqlServerMetadataReader(options.Source.ConnectionString));
services.AddSingleton<ISchemaGenerator, PostgreSqlSchemaGenerator>();
services.AddSingleton<ISchemaReader>(_ => new SqlServerSchemaReader(options.Source.ConnectionString));
services.AddSingleton<ISchemaWriter>(_ => new PostgreSqlSchemaWriter(options.Target.ConnectionString));
services.AddSingleton<IMigrationService, MigrationService>();
services.AddSingleton<IDataReader>(_ => new SqlServerDataReader(options.Source.ConnectionString));
services.AddSingleton<IDataWriter>(serviceProvider =>
{
    var converter =
        serviceProvider.GetRequiredService<IDataValueConverter>();

    return new PostgreSqlDataWriter(
        options.Target.ConnectionString,
        converter);
});
services.AddSingleton<IDataMigrationService, DataMigrationService>();
services.AddSingleton<IDataValueConverter, SqlServerValueConverter>();
services.AddSingleton<IIdentitySynchronizer>(
    serviceProvider =>
    {
        var logger =
            serviceProvider
                .GetRequiredService<
                    ILogger<PostgreSqlIdentitySynchronizer>>();

        return new PostgreSqlIdentitySynchronizer(
            options.Target.ConnectionString,
            logger);
    });
services.AddSingleton<ITableDependencySorter, TableDependencySorter>();
services.AddSingleton<ITargetDataReader>(_ => new PostgreSqlDataReader(options.Target.ConnectionString));
services.AddSingleton<IDataValidator, DataValidator>();

using var serviceProvider = services.BuildServiceProvider();
var migrationService = serviceProvider.GetRequiredService<IMigrationService>();
var dataMigrationService = serviceProvider.GetRequiredService<IDataMigrationService>();
var dataValidator = serviceProvider.GetRequiredService<IDataValidator>();

await ExecuteCommandAsync(args, migrationService, dataMigrationService, dataValidator);

static async Task ExecuteCommandAsync(string[] args, IMigrationService migrationService, IDataMigrationService dataMigrationService, IDataValidator dataValidator)
{
    if (args.Length == 0)
    {
        PrintHelp();
        return;
    }

    var command = args[0].ToLowerInvariant();
    using var cancellationTokenSource = new CancellationTokenSource();

    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cancellationTokenSource.Cancel();

        Console.WriteLine("Cancellation requested...");
    };

    switch (command)
    {
        case "list-tables":
            await ListTablesAsync(migrationService, cancellationTokenSource.Token);
            break;

        case "inspect":
            await InspectAsync(migrationService, cancellationTokenSource.Token);
            break;

        case "generate-schema":
            var outputDirectory = GetOption(args, "--output") ?? "generated-schema";
            await migrationService.GenerateSchemaAsync(outputDirectory, cancellationTokenSource.Token);
            break;

        case "create-postgresql-schema":
            await migrationService.CreatePostgreSqlSchemaAsync(cancellationTokenSource.Token);
            break;

        case "create-postgresql-constraints":
            await migrationService.CreatePostgreSqlConstraintsAsync(cancellationTokenSource.Token);
            break;

        case "migrate-data":
            await MigrateDataAsync(args, migrationService, dataMigrationService, cancellationTokenSource.Token);
            break;

        case "validate":
            await ValidateAsync(args, migrationService, dataValidator, cancellationTokenSource.Token);
            break;

        case "help":
        case "--help":
        case "-h":
            PrintHelp();
            break;

        default:
            Console.WriteLine($"Unknown command: {command}");
            Console.WriteLine();
            PrintHelp();
            break;
    }
}

static async Task ListTablesAsync(IMigrationService migrationService, CancellationToken cancellationToken)
{
    var tables = await migrationService.InspectAsync(cancellationToken);

    Console.WriteLine();
    Console.WriteLine("Tables");
    Console.WriteLine("==============================");

    foreach (var table in tables)
    {
        Console.WriteLine($"{table.SchemaName}.{table.TableName}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total tables: {tables.Count}");
}

static async Task InspectAsync(IMigrationService migrationService, CancellationToken cancellationToken)
{
    var tables = await migrationService.InspectAsync(cancellationToken);
    Console.WriteLine();

    foreach (var table in tables)
    {
        Console.WriteLine($"TABLE: {table.SchemaName}.{table.TableName}");

        Console.WriteLine("----------------------------------------");

        foreach (var column in table.Columns)
        {
            Console.WriteLine(
                $"  {column.Name,-30}" +
                $"{column.SqlServerDataType,-20}" +
                $"{(column.IsNullable ? "NULL" : "NOT NULL"),-10}" +
                $"{(column.IsIdentity ? "IDENTITY" : "")}");
        }

        Console.WriteLine();
    }
}

static async Task MigrateSingleTableAsync(string[] args, IMigrationService migrationService, IDataMigrationService dataMigrationService, CancellationToken cancellationToken)
{
    var tableName = GetOption(args, "--table");

    if (string.IsNullOrWhiteSpace(tableName))
    {
        Console.WriteLine("Please specify a table using --table.");
        Console.WriteLine();
        Console.WriteLine("Example:");
        Console.WriteLine("  migrate-data --table dbo.tbl_Candidate");
        return;
    }

    var tables = await migrationService.InspectAsync(cancellationToken);
    var table = tables.FirstOrDefault(t => t.FullName.Equals(tableName, StringComparison.OrdinalIgnoreCase));
    if (table is null)
    {
        Console.WriteLine($"Table '{tableName}' was not found.");
        return;
    }

    Console.WriteLine();
    Console.WriteLine($"Starting data migration: {table.FullName}");
    Console.WriteLine("----------------------------------------");

    var progress =
    new Progress<MigrationProgress>(
        p =>
        {
            if (p.Percentage.HasValue)
            {
                Console.WriteLine($"Progress: {p.RowsMigrated:N0} / " +
                                  $"{p.TotalRows:N0} " +
                                  $"({p.Percentage.Value:F1}%)");
            }
            else
            {
                Console.WriteLine($"Rows migrated: {p.RowsMigrated:N0}");
            }
        });

    var rowCount =
    await dataMigrationService.MigrateTableAsync(table, progress, cancellationToken);

    Console.WriteLine();
    Console.WriteLine($"Migration completed: {table.FullName}");
    Console.WriteLine($"Rows migrated: {rowCount}");
    Console.WriteLine();
}

static async Task MigrateDataAsync(string[] args, IMigrationService migrationService, IDataMigrationService dataMigrationService, CancellationToken cancellationToken)
{
    if (args.Length < 2)
    {
        Console.WriteLine("Usage: migrate-data --table <schema.table>");
        Console.WriteLine("   or: migrate-data --all");
        return;
    }

    var option = args[1].ToLowerInvariant();

    if (option == "--table")
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Please specify a table.");
            Console.WriteLine("Example: migrate-data --table dbo.tbl_Role");
            return;
        }

        await MigrateSingleTableAsync(args, migrationService, dataMigrationService, cancellationToken);
        return;
    }

    if (option == "--all")
    {
        await MigrateAllTablesAsync(migrationService, dataMigrationService, cancellationToken);
        return;
    }

    Console.WriteLine($"Unknown option: {args[1]}");
    Console.WriteLine("Usage: migrate-data --table <schema.table>");
    Console.WriteLine("   or: migrate-data --all");
}

static async Task MigrateAllTablesAsync(IMigrationService migrationService, IDataMigrationService dataMigrationService, CancellationToken cancellationToken)
{
    var tables = await migrationService.InspectAsync();

    if (tables.Count == 0)
    {
        Console.WriteLine("No tables found.");
        return;
    }

    var orderedTables = dataMigrationService.GetMigrationOrder(tables);

    Console.WriteLine();
    Console.WriteLine("Migration plan");
    Console.WriteLine("==============");

    for (var i = 0; i < orderedTables.Count; i++)
    {
        Console.WriteLine($"{i + 1,2}. {orderedTables[i].FullName}");
    }

    Console.WriteLine();
    Console.WriteLine($"Total tables: {tables.Count}");

    Console.WriteLine();
    Console.WriteLine("Starting data migration...");

    Console.WriteLine(new string('-', 40));

    var progress = new Progress<MigrationProgress>(p =>
                                                    {
                                                        if (p.Percentage.HasValue)
                                                        {
                                                            Console.WriteLine($"{p.TableName}: {p.RowsMigrated:N0} / {p.TotalRows:N0} ({p.Percentage.Value:F1}%)");
                                                        }
                                                        else
                                                        {
                                                            Console.WriteLine($"{p.TableName}: {p.RowsMigrated:N0}");
                                                        }
                                                    });

    await dataMigrationService.MigrateAllTablesAsync(tables, progress, cancellationToken);
    Console.WriteLine();
    Console.WriteLine("All table migrations completed.");
}

static async Task ValidateAsync(string[] args, IMigrationService migrationService, IDataValidator dataValidator, CancellationToken cancellationToken)
{
    if (args.Length < 2)
    {
        Console.WriteLine(
            "Usage: validate --row-count");

        Console.WriteLine(
            "       validate --schema");

        return;
    }

    var tables =
        await migrationService.InspectAsync();

    if (tables.Count == 0)
    {
        Console.WriteLine(
            "No tables found.");

        return;
    }

    var validationType =
        args[1].ToLowerInvariant();

    ValidationReport report;

    switch (validationType)
    {
        case "--row-count":

            Console.WriteLine();
            Console.WriteLine(
                "Row count validation");

            Console.WriteLine(
                "====================");

            report =
                await dataValidator.ValidateRowCountsAsync(
                    tables,
                    cancellationToken);

            PrintValidationReport(report);

            break;

        case "--schema":

            Console.WriteLine();
            Console.WriteLine(
                "Schema validation");

            Console.WriteLine(
                "=================");

            report =
                await dataValidator.ValidateSchemaAsync(
                    tables,
                    cancellationToken);

            PrintValidationReport(report);

            break;

        default:

            Console.WriteLine(
                "Unknown validation option.");

            Console.WriteLine(
                "Usage: validate --row-count");

            Console.WriteLine(
                "       validate --schema");

            break;
    }
}

static string? GetOption(string[] args, string optionName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(optionName, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }
    return null;
}

static void PrintHelp()
{
    Console.WriteLine("""
    
    SQL Server → PostgreSQL Migration Tool
    =======================================

    Commands:

      list-tables
          Lists all user tables from SQL Server.

      inspect
          Displays tables and columns.

      generate-schema
          Generates PostgreSQL CREATE TABLE scripts.

      generate-schema --output <directory>
          Specifies the schema output directory.

      create-postgresql-schema
          Creates PostgreSQL schemas and tables.

      create-postgresql-constraints
          Creates PostgreSQL constraints and indexes.

      migrate-data --table <schema.table>
          Migrates data for a specific table.

      validate --row-count
          Row count validation
      help
          Displays this help.

    Examples:

      MigrationTool list-tables

      MigrationTool inspect

      MigrationTool generate-schema

      MigrationTool create-postgresql-schema

      MigrationTool create-postgresql-constraints

      MigrationTool migrate-data --table dbo.tbl_Candidate

    """);
}

static void PrintValidationReport(ValidationReport report)
{
    foreach (var result in report.Results)
    {
        var status =
            result.IsValid
                ? "PASS"
                : "FAIL";

        Console.WriteLine(
            $"{status,-5} " +
            $"{result.TableName,-45} " +
            result.Message);
    }

    foreach (var issue in report.Issues)
    {
        Console.WriteLine(
            $"      {issue.TableName} " +
            $"{issue.ObjectName}: " +
            $"{issue.Message}");
    }

    Console.WriteLine();

    Console.WriteLine(
        $"Tables validated: {report.TablesValidated}");

    Console.WriteLine(
        $"Passed: {report.Passed}");

    Console.WriteLine(
        $"Failed: {report.Failed}");

    Console.WriteLine();

    Console.WriteLine(
        report.IsValid
            ? "Validation PASSED."
            : "Validation FAILED.");
}