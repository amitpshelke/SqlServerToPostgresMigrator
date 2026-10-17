using MigrationTool.Mapping;

namespace MigrationTool.Mapping.Tests;

public class SqlServerTypeMapperTests
{
    private readonly SqlServerTypeMapper _mapper = new();

    [Theory]
    [InlineData("int", "INTEGER")]
    [InlineData("bigint", "BIGINT")]
    [InlineData("smallint", "SMALLINT")]
    [InlineData("tinyint", "SMALLINT")]
    [InlineData("bit", "BOOLEAN")]
    [InlineData("datetime", "TIMESTAMP")]
    [InlineData("datetime2", "TIMESTAMP")]
    [InlineData("uniqueidentifier", "UUID")]
    [InlineData("timestamp", "BYTEA")]
    [InlineData("text", "TEXT")]
    [InlineData("ntext", "TEXT")]
    [InlineData("xml", "XML")]
    public void MapType_ShouldReturnExpectedPostgreSqlType(
        string sqlServerType,
        string expected)
    {
        var result =
            _mapper.MapType(
                sqlServerType,
                null,
                null,
                null);

        Assert.Equal(expected, result);
    }
}