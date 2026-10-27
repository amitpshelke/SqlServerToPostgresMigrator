using MigrationTool.Core.Models;
using MigrationTool.Core.Services;

namespace MigrationTool.Core.Tests;

public class JsonMigrationStateStoreTests : IDisposable
{
    private readonly string _testDirectory;

    public JsonMigrationStateStoreTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MigrationToolTests_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_testDirectory);
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateStateFile()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var state = CreateMigrationState();

        await store.SaveAsync(state);

        var filePath = Path.Combine(
            _testDirectory,
            $"{state.MigrationId}.json");

        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnSavedState()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var state = CreateMigrationState();

        await store.SaveAsync(state);

        var loadedState =
            await store.LoadAsync(state.MigrationId);

        Assert.NotNull(loadedState);

        Assert.Equal(
            state.MigrationId,
            loadedState!.MigrationId);

        Assert.Equal(
            state.PlannedTables,
            loadedState.PlannedTables);

        Assert.Equal(
            state.Status,
            loadedState.Status);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNull_WhenMigrationDoesNotExist()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var result =
            await store.LoadAsync("migration-does-not-exist");

        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateTableAsync_ShouldAddNewTable()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var state = CreateMigrationState();

        await store.SaveAsync(state);

        var table = new MigrationStateTable
        {
            TableName = "dbo.tbl_Candidate",
            Status = MigrationTableStatus.Success,
            RowsMigrated = 100,
            TotalRows = 100,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        await store.UpdateTableAsync(
            state.MigrationId,
            table);

        var loadedState =
            await store.LoadAsync(state.MigrationId);

        Assert.NotNull(loadedState);

        Assert.Single(loadedState!.Tables);

        Assert.Equal(
            "dbo.tbl_Candidate",
            loadedState.Tables[0].TableName);

        Assert.Equal(
            MigrationTableStatus.Success,
            loadedState.Tables[0].Status);

        Assert.Equal(
            100,
            loadedState.Tables[0].RowsMigrated);
    }

    [Fact]
    public async Task UpdateTableAsync_ShouldReplaceExistingTable()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var state = CreateMigrationState();

        state.Tables.Add(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Candidate",
                Status = MigrationTableStatus.Failed,
                RowsMigrated = 0,
                ErrorCode = "23505",
                ErrorMessage = "Duplicate key"
            });

        await store.SaveAsync(state);

        var updatedTable = new MigrationStateTable
        {
            TableName = "dbo.tbl_Candidate",
            Status = MigrationTableStatus.Success,
            RowsMigrated = 100,
            TotalRows = 100,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        await store.UpdateTableAsync(
            state.MigrationId,
            updatedTable);

        var loadedState =
            await store.LoadAsync(state.MigrationId);

        Assert.NotNull(loadedState);

        Assert.Single(loadedState!.Tables);

        var table = loadedState.Tables[0];

        Assert.Equal(
            MigrationTableStatus.Success,
            table.Status);

        Assert.Equal(
            100,
            table.RowsMigrated);

        Assert.Null(table.ErrorCode);

        Assert.Null(table.ErrorMessage);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistReadableJson()
    {
        var store = new JsonMigrationStateStore(
            _testDirectory);

        var state = CreateMigrationState();

        state.Tables.Add(
            new MigrationStateTable
            {
                TableName = "dbo.tbl_Job",
                Status = MigrationTableStatus.Success,
                RowsMigrated = 25,
                TotalRows = 25
            });

        await store.SaveAsync(state);

        var filePath = Path.Combine(
            _testDirectory,
            $"{state.MigrationId}.json");

        var json =
            await File.ReadAllTextAsync(filePath);

        Assert.Contains(
            "\"migrationId\"",
            json,
            StringComparison.OrdinalIgnoreCase);

        Assert.Contains(
            "dbo.tbl_Job",
            json);

        Assert.Contains(
            "Success",
            json);
    }

    private static MigrationState CreateMigrationState()
    {
        return new MigrationState
        {
            MigrationId =
                $"migration-test-{Guid.NewGuid():N}",

            StartedAt = DateTime.UtcNow,

            Status =
                MigrationStateStatus.Running,

            PlannedTables = 3
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(
                _testDirectory,
                recursive: true);
        }
    }
}