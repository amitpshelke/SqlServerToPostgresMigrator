using MigrationTool.Core.Interfaces;

namespace MigrationTool.Mapping;

public sealed class SqlServerTypeMapper : ITypeMapper
{
    public string MapType(
        string sqlServerType,
        int? maxLength,
        byte? precision,
        byte? scale)
    {
        var type = sqlServerType.ToLowerInvariant();

        return type switch
        {
            "bigint" => "BIGINT",
            "int" => "INTEGER",
            "smallint" => "SMALLINT",
            "tinyint" => "SMALLINT",

            "bit" => "BOOLEAN",

            "decimal" or "numeric" =>
                $"NUMERIC({precision ?? 18},{scale ?? 0})",

            "money" => "NUMERIC(19,4)",
            "smallmoney" => "NUMERIC(10,4)",

            "float" => "DOUBLE PRECISION",
            "real" => "REAL",

            "date" => "DATE",
            "datetime" => "TIMESTAMP",
            "datetime2" => "TIMESTAMP",
            "smalldatetime" => "TIMESTAMP",

            "datetimeoffset" =>
                "TIMESTAMP WITH TIME ZONE",

            "time" => "TIME",

            "uniqueidentifier" => "UUID",

            "binary" or
            "varbinary" or
            "image" => "BYTEA",
            "timestamp" => "BYTEA",

            "xml" => "XML",

            "char" =>
                $"CHAR({NormalizeLength(maxLength)})",

            "varchar" =>
                MapStringType("VARCHAR", maxLength),

            "nchar" =>
                $"CHAR({NormalizeUnicodeLength(maxLength)})",

            "nvarchar" =>
                MapStringType("VARCHAR", NormalizeUnicodeLength(maxLength)),

            "text" or
            "ntext" => "TEXT",

            "sql_variant" => "TEXT",

            _ => throw new NotSupportedException(
                $"SQL Server data type '{sqlServerType}' is not currently supported.")
        };
    }

    private static string MapStringType(
        string postgresType,
        int? length)
    {
        if (!length.HasValue || length.Value < 0)
        {
            return "TEXT";
        }

        return $"{postgresType}({length.Value})";
    }

    private static int NormalizeUnicodeLength(int? maxLength)
    {
        if (!maxLength.HasValue)
        {
            return -1;
        }

        if (maxLength.Value == -1)
        {
            return -1;
        }

        return maxLength.Value / 2;
    }

    private static int NormalizeLength(int? maxLength)
    {
        if (!maxLength.HasValue || maxLength.Value < 0)
        {
            return 1;
        }

        return maxLength.Value;
    }
}