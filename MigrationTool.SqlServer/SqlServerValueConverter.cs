using MigrationTool.Core.Interfaces;

namespace MigrationTool.SqlServer;

public sealed class SqlServerValueConverter : IDataValueConverter
{
    public object? Convert(
        object? value,
        string sqlServerDataType,
        string postgreSqlDataType)
    {
        if (value is null || value == DBNull.Value)
        {
            return null;
        }

        var sourceType =
            sqlServerDataType.Trim().ToLowerInvariant();

        return sourceType switch
        {
            "bit" =>
                ConvertBit(value),

            "uniqueidentifier" =>
                ConvertGuid(value),

            "datetimeoffset" =>
                ConvertDateTimeOffset(value),

            "binary" or
            "varbinary" or
            "image" or
            "timestamp" or
            "rowversion" =>
                ConvertBinary(value),

            _ => value
        };
    }

    private static bool ConvertBit(object value)
    {
        return value switch
        {
            bool boolValue => boolValue,

            byte byteValue =>
                byteValue != 0,

            short shortValue =>
                shortValue != 0,

            int intValue =>
                intValue != 0,

            long longValue =>
                longValue != 0,

            _ => System.Convert.ToBoolean(value)
        };
    }

    private static Guid ConvertGuid(object value)
    {
        return value switch
        {
            Guid guid => guid,

            string stringValue =>
                Guid.Parse(stringValue),

            _ => System.Convert.ToString(value) is { } text
                ? Guid.Parse(text)
                : throw new InvalidOperationException(
                    $"Unable to convert value '{value}' to Guid.")
        };
    }

    private static DateTimeOffset ConvertDateTimeOffset(
        object value)
    {
        return value switch
        {
            DateTimeOffset dateTimeOffset =>
                dateTimeOffset,

            DateTime dateTime =>
                new DateTimeOffset(dateTime),

            _ => DateTimeOffset.Parse(
                value.ToString()!)
        };
    }

    private static byte[] ConvertBinary(object value)
    {
        return value switch
        {
            byte[] bytes => bytes,

            _ => throw new InvalidOperationException(
                $"Expected binary value but received " +
                $"'{value.GetType().FullName}'.")
        };
    }
}