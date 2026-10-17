namespace MigrationTool.Core.Models;

public sealed class MigrationProgress
{
    public string TableName { get; init; } = string.Empty;

    public long RowsMigrated { get; init; }

    public long? TotalRows { get; init; }

    public double? Percentage
    {
        get
        {
            if (!TotalRows.HasValue || TotalRows.Value == 0)
            {
                return null;
            }

            return RowsMigrated * 100.0 / TotalRows.Value;
        }
    }
}