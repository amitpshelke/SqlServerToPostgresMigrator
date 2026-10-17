namespace MigrationTool.Core.Interfaces;

public interface IDataValueConverter
{
    object? Convert(
        object? value,
        string sqlServerDataType,
        string postgreSqlDataType);
}