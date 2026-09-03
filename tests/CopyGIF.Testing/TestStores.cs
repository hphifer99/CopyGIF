using CopyGIF.Core.Contracts;
using CopyGIF.Core.Models;
using CopyGIF.Core.Settings;

namespace CopyGIF.Testing;

public sealed class FakeSettingsStore :
    ISettingsStore
{
    private readonly List<AppSettings>
        _savedSettings = [];

    public AppSettings Value { get; set; } =
        new();

    public int LoadCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<AppSettings>>? LoadHandler
    { get; set; }

    public Func<
        AppSettings,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public IReadOnlyList<AppSettings> SavedSettings =>
        _savedSettings.ToArray();

    public Task<AppSettings> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return LoadHandler is null
            ? Task.FromResult(
                Value)
            : LoadHandler(
                cancellationToken);
    }

    public async Task SaveAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            settings);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    settings,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = settings;

        _savedSettings.Add(
            settings);
    }
}

public sealed class FakeLibraryStore :
    ILibraryStore
{
    private readonly List<LibrarySnapshot>
        _savedSnapshots = [];

    public LibrarySnapshot Value { get; set; } =
        new();

    public int LoadCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<LibrarySnapshot>>? LoadHandler
    { get; set; }

    public Func<
        LibrarySnapshot,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public IReadOnlyList<LibrarySnapshot>
        SavedSnapshots =>
            _savedSnapshots.ToArray();

    public Task<LibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return LoadHandler is null
            ? Task.FromResult(
                Value)
            : LoadHandler(
                cancellationToken);
    }

    public async Task SaveAsync(
        LibrarySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    snapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = snapshot;

        _savedSnapshots.Add(
            snapshot);
    }
}

public sealed class FakeSearchHistoryStore :
    ISearchHistoryStore
{
    private readonly List<SearchHistorySnapshot>
        _savedSnapshots = [];

    public SearchHistorySnapshot Value { get; set; } =
        new();

    public int LoadCallCount { get; private set; }

    public int ClearCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<SearchHistorySnapshot>>? LoadHandler
    { get; set; }

    public Func<
        SearchHistorySnapshot,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public Func<
        CancellationToken,
        Task>? ClearHandler
    { get; set; }

    public IReadOnlyList<SearchHistorySnapshot>
        SavedSnapshots =>
            _savedSnapshots.ToArray();

    public Task<SearchHistorySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return LoadHandler is null
            ? Task.FromResult(
                Value)
            : LoadHandler(
                cancellationToken);
    }

    public async Task SaveAsync(
        SearchHistorySnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            snapshot);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    snapshot,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = snapshot;

        _savedSnapshots.Add(
            snapshot);
    }

    public async Task ClearAsync(
        CancellationToken cancellationToken = default)
    {
        ClearCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        if (ClearHandler is not null)
        {
            await ClearHandler(
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = new SearchHistorySnapshot();
    }
}

public sealed class FakeUpdateStateStore :
    IUpdateStateStore
{
    private readonly List<UpdateState>
        _savedStates = [];

    public UpdateState Value { get; set; } =
        new();

    public int LoadCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<UpdateState>>? LoadHandler
    { get; set; }

    public Func<
        UpdateState,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public IReadOnlyList<UpdateState> SavedStates =>
        _savedStates.ToArray();

    public Task<UpdateState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return LoadHandler is null
            ? Task.FromResult(
                Value)
            : LoadHandler(
                cancellationToken);
    }

    public async Task SaveAsync(
        UpdateState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = state;

        _savedStates.Add(
            state);
    }
}

public sealed class FakeMigrationStateStore :
    IMigrationStateStore
{
    private readonly List<MigrationState>
        _savedStates = [];

    public MigrationState Value { get; set; } =
        new();

    public int LoadCallCount { get; private set; }

    public Func<
        CancellationToken,
        Task<MigrationState>>? LoadHandler
    { get; set; }

    public Func<
        MigrationState,
        CancellationToken,
        Task>? SaveHandler
    { get; set; }

    public IReadOnlyList<MigrationState> SavedStates =>
        _savedStates.ToArray();

    public Task<MigrationState> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        LoadCallCount++;

        cancellationToken.ThrowIfCancellationRequested();

        return LoadHandler is null
            ? Task.FromResult(
                Value)
            : LoadHandler(
                cancellationToken);
    }

    public async Task SaveAsync(
        MigrationState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            state);

        cancellationToken.ThrowIfCancellationRequested();

        if (SaveHandler is not null)
        {
            await SaveHandler(
                    state,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        Value = state;

        _savedStates.Add(
            state);
    }
}
