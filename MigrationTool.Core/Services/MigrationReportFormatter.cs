using System.Text;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public static class MigrationReportFormatter
{
    public static string Format(MigrationReport report)
    {
        var builder = new StringBuilder();

        builder.AppendLine("============================================================");
        builder.AppendLine("SQL SERVER → POSTGRESQL MIGRATION REPORT");
        builder.AppendLine("============================================================");
        builder.AppendLine();

        builder.AppendLine(
            $"Started       : {report.StartedAt:dd-MMM-yyyy HH:mm:ss}");

        builder.AppendLine(
            $"Completed     : {report.CompletedAt:dd-MMM-yyyy HH:mm:ss}");

        builder.AppendLine(
            $"Duration      : {FormatDuration(report.Duration)}");

        builder.AppendLine();

        builder.AppendLine("SUMMARY");
        builder.AppendLine("------------------------------------------------------------");

        builder.AppendLine(
            $"Planned       : {report.PlannedTables}");

        builder.AppendLine(
            $"Attempted     : {report.TotalTables}");

        builder.AppendLine(
            $"Succeeded     : {report.SuccessfulTables}");

        builder.AppendLine(
            $"Failed        : {report.FailedTables}");

        builder.AppendLine(
            $"Cancelled     : {report.CancelledTables}");

        builder.AppendLine(
            $"Not Started   : {report.NotStartedTables}");

        builder.AppendLine(
            $"Rows Migrated : {report.TotalRowsMigrated}");

        builder.AppendLine();

        builder.AppendLine("TABLE RESULTS");
        builder.AppendLine("------------------------------------------------------------");

        if (report.Tables.Count == 0)
        {
            builder.AppendLine("No tables were migrated.");
        }
        else
        {
            for (var index = 0; index < report.Tables.Count; index++)
            {
                AppendTableResult(
                    builder,
                    report.Tables[index],
                    index + 1);
            }
        }

        builder.AppendLine("------------------------------------------------------------");
        builder.AppendLine("FINAL RESULT");
        builder.AppendLine("------------------------------------------------------------");

        builder.AppendLine(
            $"Result        : {(report.IsSuccess ? "SUCCESS" : "FAILED")}");

        builder.AppendLine(
            $"Successful    : {report.SuccessfulTables}");

        builder.AppendLine(
            $"Failed        : {report.FailedTables}");

        builder.AppendLine(
            $"Cancelled     : {report.CancelledTables}");

        builder.AppendLine(
            $"Not Started   : {report.NotStartedTables}");

        builder.AppendLine(
            $"Rows Migrated : {report.TotalRowsMigrated}");

        builder.AppendLine();
        builder.AppendLine("============================================================");

        return builder.ToString();
    }

    private static void AppendTableResult(
        StringBuilder builder,
        MigrationTableResult result,
        int number)
    {
        builder.AppendLine();

        builder.AppendLine(
            $"{number}. {result.TableName}");

        builder.AppendLine(
            $"   Status        : {result.Status}");

        builder.AppendLine(
            $"   Rows Migrated : {result.RowsMigrated}");

        if (result.TotalRows.HasValue)
        {
            builder.AppendLine(
                $"   Total Rows    : {result.TotalRows.Value}");
        }

        builder.AppendLine(
            $"   Duration      : {FormatDuration(result.Duration)}");

        if (!string.IsNullOrWhiteSpace(result.ErrorCode))
        {
            builder.AppendLine(
                $"   Error Code    : {result.ErrorCode}");
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            builder.AppendLine(
                $"   Error         : {result.ErrorMessage}");
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return duration.ToString(@"hh\:mm\:ss");
        }

        if (duration.TotalMinutes >= 1)
        {
            return duration.ToString(@"mm\:ss");
        }

        return $"{duration.TotalSeconds:F2}s";
    }
}