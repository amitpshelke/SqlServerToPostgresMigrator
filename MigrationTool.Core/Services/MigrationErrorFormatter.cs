using Npgsql;

namespace MigrationTool.Core.Services;

public static class MigrationErrorFormatter
{
    public static string Format(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            return FormatPostgresException(postgresException);
        }

        return exception.Message;
    }

    public static string? GetErrorCode(Exception exception)
    {
        if (exception is PostgresException postgresException)
        {
            return postgresException.SqlState;
        }

        return null;
    }

    private static string FormatPostgresException(
        PostgresException exception)
    {
        return exception.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation =>
                $"Duplicate key value violates unique constraint \"{exception.ConstraintName}\".",

            PostgresErrorCodes.ForeignKeyViolation =>
                $"Foreign key constraint \"{exception.ConstraintName}\" was violated.",

            PostgresErrorCodes.NotNullViolation =>
                $"Required column \"{exception.ColumnName}\" cannot contain NULL.",

            PostgresErrorCodes.CheckViolation =>
                $"Check constraint \"{exception.ConstraintName}\" was violated.",

            PostgresErrorCodes.UndefinedTable =>
                $"Target table does not exist: \"{exception.TableName}\".",

            PostgresErrorCodes.UndefinedColumn =>
                $"Target column does not exist: \"{exception.ColumnName}\".",

            PostgresErrorCodes.SyntaxError =>
                "PostgreSQL SQL syntax error.",

            PostgresErrorCodes.StringDataRightTruncation =>
                "A value exceeds the maximum length allowed by the target column.",

            PostgresErrorCodes.InvalidTextRepresentation =>
                "A value could not be converted to the required PostgreSQL data type.",

            _ =>
                exception.Message
        };
    }
}