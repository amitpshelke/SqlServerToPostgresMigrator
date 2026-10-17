namespace MigrationTool.Core.Interfaces;

public interface ITypeMapper
{
    string MapType(
        string sqlServerType,
        int? maxLength,
        byte? precision,
        byte? scale);
}