using System.Text.Json;
using System.Text.Json.Serialization;
using MigrationTool.Core.Interfaces;
using MigrationTool.Core.Models;

namespace MigrationTool.Core.Services;

public sealed class JsonMigrationStateStore : IMigrationStateStore
{
    private readonly string _baseDirectory;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    public JsonMigrationStateStore(string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException(
                "Migration state directory cannot be empty.",
                nameof(baseDirectory));
        }

        _baseDirectory = baseDirectory;
    }

    public async Task SaveAsync(
        MigrationState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        Directory.CreateDirectory(_baseDirectory);

        var filePath = GetFilePath(state.MigrationId);
        var temporaryFilePath = $"{filePath}.{Guid.NewGuid():N}.tmp";

        var json = JsonSerializer.Serialize(
            state,
            _jsonOptions);

        await File.WriteAllTextAsync(
            temporaryFilePath,
            json,
            cancellationToken);

        ReplaceFile(
            temporaryFilePath,
            filePath);
    }

    public async Task<MigrationState?> LoadAsync(
        string migrationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(migrationId))
        {
            throw new ArgumentException(
                "Migration ID cannot be empty.",
                nameof(migrationId));
        }

        var filePath = GetFilePath(migrationId);

        if (!File.Exists(filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(
            filePath,
            cancellationToken);

        return JsonSerializer.Deserialize<MigrationState>(
            json,
            _jsonOptions);
    }

    public async Task UpdateTableAsync(
        string migrationId,
        MigrationStateTable table,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(table);

        var state = await LoadAsync(
            migrationId,
            cancellationToken);

        if (state is null)
        {
            throw new InvalidOperationException(
                $"Migration state not found: {migrationId}");
        }

        var existingTable = state.Tables.FirstOrDefault(
            t => string.Equals(
                t.TableName,
                table.TableName,
                StringComparison.OrdinalIgnoreCase));

        if (existingTable is null)
        {
            state.Tables.Add(table);
        }
        else
        {
            var index = state.Tables.IndexOf(existingTable);

            state.Tables[index] = table;
        }

        await SaveAsync(
            state,
            cancellationToken);
    }

    private string GetFilePath(string migrationId)
    {
        var safeMigrationId = SanitizeMigrationId(migrationId);

        return Path.Combine(
            _baseDirectory,
            $"{safeMigrationId}.json");
    }

    private static string SanitizeMigrationId(
        string migrationId)
    {
        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        var characters = migrationId
            .Select(character =>
                invalidCharacters.Contains(character)
                    ? '_'
                    : character)
            .ToArray();

        return new string(characters);
    }

    private static void ReplaceFile(
        string temporaryFilePath,
        string targetFilePath)
    {
        try
        {
            if (File.Exists(targetFilePath))
            {
                File.Replace(
                    temporaryFilePath,
                    targetFilePath,
                    null);
            }
            else
            {
                File.Move(
                    temporaryFilePath,
                    targetFilePath);
            }
        }
        catch
        {
            if (File.Exists(temporaryFilePath))
            {
                File.Delete(temporaryFilePath);
            }

            throw;
        }
    }
}