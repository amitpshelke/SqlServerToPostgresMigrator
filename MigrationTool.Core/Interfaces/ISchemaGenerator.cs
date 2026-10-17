using MigrationTool.Core.Models;

namespace MigrationTool.Core.Interfaces;

public interface ISchemaGenerator
{
    string GenerateCreateTableScript(TableDefinition table);
}