namespace MigrationTool.Core.Models;

public sealed class ValidationReport
{
    public List<ValidationResult> Results { get; init; } = [];

    public List<ValidationIssue> Issues { get; init; } = [];

    public int TablesValidated =>
        Results.Count;

    public int Passed =>
        Results.Count(r => r.IsValid);

    public int Failed =>
        Results.Count(r => !r.IsValid);

    public bool IsValid =>
        Failed == 0 &&
        Issues.Count == 0;
}