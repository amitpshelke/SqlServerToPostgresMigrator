namespace MigrationTool.Core.Configuration;

public sealed class MigrationOptions
{
    public DatabaseOptions Source { get; set; } = new();

    public DatabaseOptions Target { get; set; } = new();

    public MigrationSettings Migration { get; set; } = new();
}