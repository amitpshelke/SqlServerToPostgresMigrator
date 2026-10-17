namespace MigrationTool.Core.Configuration;

public sealed class MigrationSettings
{
    public bool CreateSchema { get; set; } = true;

    public bool MigrateData { get; set; }

    public bool MigrateIndexes { get; set; }

    public bool MigrateConstraints { get; set; }

    public bool ValidateData { get; set; }

    public int BatchSize { get; set; } = 50_000;
}